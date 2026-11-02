// Copyright (c) 2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.Curves.Conventions;
using Boutquin.MarketData.Abstractions.Calendars;
using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Bootstrap.Helpers;

/// <summary>
/// Solves fixed-float swap bootstrap nodes from quoted par rates using the closed-form par-annuity relation.
/// </summary>
/// <remarks>
/// <para>
/// The terminal pillar discount factor is solved from the swap par condition
/// <c>R * A_c = floatPV</c>, where the fixed annuity <c>A_c = sum_j tauK_j * Pc(S_j)</c> is discounted on
/// the in-progress discount curve's locked pillars and the unknown terminal factor enters the final coupon.
/// </para>
/// <para>
/// In the self-discounting case (no <see cref="ResolvedNode.ProjectionCurves"/>) the floating leg
/// telescopes to <c>1 - Pc(S_N)</c>, giving
/// <c>Pc(S_N) = (1 - R * A_prior) / (1 + R * tauK_N)</c>.
/// In the multi-curve case the floating-leg forwards <c>Fx_i</c> are read from a separately calibrated
/// projection curve, and the terminal factor solves the linear relation
/// <c>Pc(T_N) = (floatPV_prior - R * A_prior) / (tauK_N * (R - Fx(T_N)))</c>.
/// </para>
/// <para>
/// When the par rate <c>R</c> equals the terminal forward <c>Fx(T_N)</c> (or closely approaches it),
/// the multi-curve denominator <c>tauK_N * (R - Fx_N)</c> cancels to near zero, causing catastrophic
/// numerical loss. The self-discounting formula is used as a stable fallback whenever the denominator
/// is below 1e-15 or the resulting discount factor falls outside the plausible range <c>(0, 2]</c>.
/// </para>
/// <para>
/// <b>Valuation-date convention:</b> <see cref="PillarDate"/> adds the node tenor directly to
/// <c>valuationDate</c> using <see cref="TenorParser.AddTenor"/>. This assumes a spot-starting
/// swap (effective date = valuation date, no spot lag). Implementations requiring a spot lag
/// should override <see cref="PillarDate"/> accordingly.
/// </para>
/// This replaces the prior single-exponential collapse <c>exp(-rate * totalTau)</c>, which ignored both
/// the locked intermediate discount pillars and the projection forwards.
/// <para>
/// If no target discount curve has been established, the helper returns a self-discounting seed.
/// An externally projected single-period par quote does not identify a discount factor: both legs
/// contain the same factor, which cancels. This helper is not a simultaneous multi-curve solver;
/// callers must establish discount pillars first and inspect the returned repricing diagnostics.
/// </para>
/// </remarks>
public sealed class FixedFloatSwapInstrumentHelper : AbstractInstrumentHelper
{
    private readonly ResolvedNode _node;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedFloatSwapInstrumentHelper"/> type.
    /// </summary>
    /// <param name="node">Resolved node carrying rate and metadata.</param>
    /// <param name="quoteValue">Observed market quote for the swap instrument.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public FixedFloatSwapInstrumentHelper(ResolvedNode node, double quoteValue)
        : base(RequireNode(node).Label, node.TargetCurve, quoteValue, node.InstrumentType)
    {
        _node = node;
    }

    /// <summary>Resolves the instrument pillar by applying its tenor to the valuation date.</summary>
    /// <param name="valuationDate">Date from which the node tenor is measured.</param>
    /// <param name="referenceData">Reference-data provider retained for the helper contract.</param>
    /// <returns>The unadjusted contractual pillar date.</returns>
    /// <exception cref="NotSupportedException">The node tenor format is unsupported.</exception>
    public override DateOnly PillarDate(DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        return TenorParser.AddTenor(valuationDate, _node.Tenor);
    }

    /// <summary>Estimates the terminal discount factor from this instrument's quote and available prior curve state.</summary>
    /// <param name="partialCurveGroup">Previously calibrated curves and pillars.</param>
    /// <param name="valuationDate">Calibration valuation date.</param>
    /// <param name="referenceData">Instrument conventions used to resolve accruals and schedules.</param>
    /// <returns>The discount factor implied by this helper's pricing relation; coupon estimates may require refinement by the calibrator.</returns>
    public override double SolveNodeValue(ICurveGroup partialCurveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        DateOnly pillar = PillarDate(valuationDate, referenceData);
        IReadOnlyList<SchedulePeriod> periods = GenerateSchedule(valuationDate, pillar, referenceData);

        if (periods.Count == 0)
        {
            throw new InvalidOperationException(
                $"Schedule for node '{_node.Label}' with tenor '{_node.Tenor}' produced no periods. " +
                $"Verify the tenor is consistent with the '{_node.ConventionCode}' payment frequency.");
        }

        ICurve? projectionCurve = TryGetProjectionCurve(partialCurveGroup);

        // No locked discount pillars yet (single-node edge case): fall back to the single-period
        // par-annuity simple-interest form Pc(T_1) = 1 / (1 + R * tau).
        if (!partialCurveGroup.TryGetCurve(TargetCurve, out ICurve? targetCurve) || targetCurve is not IDiscountCurve discountCurve)
        {
            double tauSingle = Math.Max(1e-12, periods.Sum(p => p.YearFraction));
            return 1.0 / (1.0 + QuoteValue * tauSingle);
        }

        double annuityPrior = 0.0;
        double floatPvPrior = 0.0;
        for (int i = 0; i < periods.Count - 1; i++)
        {
            double pc = discountCurve.DiscountFactor(periods[i].PeriodEndDate);
            annuityPrior += periods[i].YearFraction * pc;
            if (projectionCurve != null)
            {
                double fx = GetForwardRate(projectionCurve, periods[i].PeriodStartDate, periods[i].PeriodEndDate, periods[i].YearFraction);
                floatPvPrior += fx * periods[i].YearFraction * pc;
            }
        }

        SchedulePeriod lastPeriod = periods[^1];
        double tauN = Math.Max(1e-12, lastPeriod.YearFraction);

        if (projectionCurve == null)
        {
            // Self-discounting: float leg telescopes to 1 - Pc(T_N).
            return (1.0 - QuoteValue * annuityPrior) / (1.0 + QuoteValue * tauN);
        }

        // Multi-curve: terminal Fx forward comes from the (already calibrated) projection curve.
        double fxN = GetForwardRate(projectionCurve, lastPeriod.PeriodStartDate, lastPeriod.PeriodEndDate, tauN);
        double numerator = floatPvPrior - QuoteValue * annuityPrior;
        double denominator = tauN * (QuoteValue - fxN);

        // The par-annuity formula is numerically unstable when R ≈ Fx_N (near-zero denominator
        // and near-zero numerator due to cancellation). Use the self-discounting par-annuity as
        // a stable fallback whenever the formula yields an implausible discount factor: a valid
        // DF must lie in (0, 2] for any realistic rate environment, so anything outside that
        // range — including negative values from inconsistent inputs or values > 2 from
        // catastrophic cancellation — indicates numerical failure.
        if (Math.Abs(denominator) < 1e-15)
        {
            return (1.0 - QuoteValue * annuityPrior) / (1.0 + QuoteValue * tauN);
        }

        double result = numerator / denominator;
        if (!(result > 0d && result <= 2d))
        {
            return (1.0 - QuoteValue * annuityPrior) / (1.0 + QuoteValue * tauN);
        }

        return result;
    }

    /// <summary>Reprices this instrument using the supplied curve group.</summary>
    /// <param name="curveGroup">Curves used to compute the implied quote.</param>
    /// <param name="valuationDate">Calibration valuation date.</param>
    /// <param name="referenceData">Instrument conventions used for accruals and schedules.</param>
    /// <returns>The implied quote, or the original quote when the target discount curve is unavailable.</returns>
    public override double ImpliedQuote(ICurveGroup curveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        DateOnly pillar = PillarDate(valuationDate, referenceData);
        IReadOnlyList<SchedulePeriod> periods = GenerateSchedule(valuationDate, pillar, referenceData);

        if (!curveGroup.TryGetCurve(TargetCurve, out ICurve? targetCurve) || targetCurve is not IDiscountCurve discountCurve)
        {
            return QuoteValue;
        }

        ICurve? projectionCurve = TryGetProjectionCurve(curveGroup);

        double annuity = 0.0;
        for (int i = 0; i < periods.Count - 1; i++)
        {
            annuity += periods[i].YearFraction * discountCurve.DiscountFactor(periods[i].PeriodEndDate);
        }
        // Same pillar-date consistency fix as BasisSwapInstrumentHelper: the terminal period's DF
        // must use the calibrated pillar date rather than the BD-adjusted schedule end date, so the
        // algebraic identity  R = floatPV / annuity_total  holds to floating-point precision.
        double pcTerminal = discountCurve.DiscountFactor(pillar);
        annuity += periods[^1].YearFraction * pcTerminal;

        if (annuity < 1e-15)
        {
            return QuoteValue;
        }

        if (projectionCurve == null)
        {
            // Self-discounting: the floating leg telescopes to 1 - Pc(T_N).
            return (1.0 - pcTerminal) / annuity;
        }

        // Multi-curve: invert the SAME par relation SolveNodeValue uses — the floating-leg PV is
        // Sigma_i Fx_i * tauL_i * Pc(T_i) with forwards read from the projection curve, so the par
        // rate is R = floatPV / fixedAnnuity. This must mirror SolveNodeValue (which reads the
        // projection forwards) or a multi-curve node never round-trips its repricing diagnostic.
        double floatPv = 0.0;
        for (int i = 0; i < periods.Count - 1; i++)
        {
            double pc = discountCurve.DiscountFactor(periods[i].PeriodEndDate);
            double fx = GetForwardRate(projectionCurve, periods[i].PeriodStartDate, periods[i].PeriodEndDate, periods[i].YearFraction);
            floatPv += fx * periods[i].YearFraction * pc;
        }

        SchedulePeriod terminalPeriod = periods[^1];
        double fxN = GetForwardRate(projectionCurve, terminalPeriod.PeriodStartDate, terminalPeriod.PeriodEndDate, terminalPeriod.YearFraction);
        floatPv += fxN * terminalPeriod.YearFraction * pcTerminal;

        return floatPv / annuity;
    }

    private IReadOnlyList<SchedulePeriod> GenerateSchedule(DateOnly valuationDate, DateOnly pillar, IReferenceDataProvider referenceData)
    {
        SwapLegConvention convention = (SwapLegConvention)referenceData.GetConvention(_node.ConventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);
        IBusinessCalendar calendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        return ScheduleGenerator.Generate(
            valuationDate,
            pillar,
            convention.PaymentFrequency,
            calendar,
            convention.BusinessDayAdjustment,
            dayCount,
            convention.PaymentLagBusinessDays,
            convention.EndOfMonth);
    }

    private ICurve? TryGetProjectionCurve(ICurveGroup partialCurveGroup)
    {
        if (_node.ProjectionCurves == null || _node.ProjectionCurves.Count == 0)
        {
            return null;
        }

        CurveReference projRef = _node.ProjectionCurves[0];

        // A node naming its own target curve as its projection reference is "equivalent to
        // self-discounting" (CurveGroupDefinitionValidator certifies this as valid, no error/warning).
        // Honour that contract here: collapse the self-reference to the self-discounting path
        // (return null) instead of hitting the AT-08 absent-curve throw — the target curve is not
        // yet published when this node is the first solved (empty partial group). The fail-loud
        // guard below still fires for a genuinely-absent *different* projection curve.
        if (projRef == TargetCurve)
        {
            return null;
        }

        if (!partialCurveGroup.TryGetCurve(projRef, out ICurve? pCurve))
        {
            // A node explicitly configured for multi-curve mode whose named projection curve is
            // absent must fail loudly — silently degrading to self-discounting would produce a
            // plausible-but-wrong discount factor with no diagnostic. This mirrors the fail-loud
            // wrong-type guard immediately below. (Pre-calibration validation normally catches this
            // via PROJECTION_CURVE_UNRESOLVED, but that guard is bypassed when SkipValidation=true.)
            throw new InvalidOperationException(
                $"Projection curve for reference {projRef} (required by multi-curve node '{_node.Label}') " +
                $"was not found in the curve group. Ensure the projection curve is calibrated before this node.");
        }

        if (pCurve is not IDiscountCurve && pCurve is not IForwardCurve)
        {
            throw new InvalidOperationException(
                $"Projection curve for reference {projRef} must implement IDiscountCurve or IForwardCurve. " +
                $"Actual type: {pCurve?.GetType().Name ?? "null"}.");
        }

        return pCurve;
    }

    private static double GetForwardRate(ICurve projCurve, DateOnly start, DateOnly end, double tau)
    {
        if (projCurve is IForwardCurve fwdCurve)
        {
            return fwdCurve.ForwardRate(start, end);
        }

        IDiscountCurve discCurve = (IDiscountCurve)projCurve;
        double pStart = discCurve.DiscountFactor(start);
        double pEnd = discCurve.DiscountFactor(end);
        return (pStart / pEnd - 1.0) / Math.Max(tau, 1e-12);
    }
}
