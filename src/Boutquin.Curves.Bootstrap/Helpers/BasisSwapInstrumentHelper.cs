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
/// Solves basis-swap bootstrap nodes and reprices them from the calibrated discount curve.
/// </summary>
/// <remarks>
/// <para>
/// The terminal pillar discount factor is solved from the Ametrano-Bianchetti section 4.3.6 two-projection-curve
/// par relation. A tenor basis swap exchanges two floating legs projected off distinct forward curves
/// <c>Fa</c> and <c>Fb</c>, both discounted on the collateral curve <c>Pc</c>; the quoted basis spread
/// <c>s</c> makes the legs par:
/// <c>s = (sum_i (Fb(T_i) - Fa(T_i)) * tau_i * Pc(T_i)) / (sum_i tau_i * Pc(T_i))</c>.
/// Writing <c>d_i = Fb(T_i) - Fa(T_i)</c> and splitting the locked pillars (<c>i &lt; N</c>) from the
/// unknown terminal factor gives the linear closed form
/// <c>Pc(T_N) = (basisPvPrior - s * A_prior) / (tau_N * (s - d_N))</c>, where
/// <c>A_prior = sum_{i&lt;N} tau_i * Pc(T_i)</c> and
/// <c>basisPvPrior = sum_{i&lt;N} d_i * tau_i * Pc(T_i)</c>. The forwards <c>Fa</c>/<c>Fb</c> are read from
/// <see cref="ResolvedNode.ProjectionCurves"/> index 0 and 1 respectively, and the discount pillars from
/// the in-progress curve group. This is structurally identical to the multi-curve
/// <see cref="FixedFloatSwapInstrumentHelper"/> solve with <c>R -&gt; s</c> and <c>Fx_i -&gt; d_i</c>.
/// </para>
/// <para>
/// When no projection curves are supplied (<see cref="ResolvedNode.ProjectionCurves"/> null or empty) the
/// helper keeps the self-discounting form in which the basis spread acts as the rate on a fixed annuity
/// against a telescoping float leg, giving
/// <c>Pc(S_N) = (1 - s * A_prior) / (1 + s * tauK_N)</c>. A basis swap configured with exactly ONE
/// projection curve is a misconfiguration (a basis swap needs both the <c>Fa</c> and <c>Fb</c> legs) and
/// throws <see cref="InvalidOperationException"/> rather than silently treating the single curve as one leg.
/// </para>
/// <para>
/// When the basis spread <c>s</c> equals the terminal differential <c>d_N</c> (or closely approaches it),
/// the denominator <c>tau_N * (s - d_N)</c> cancels to near zero, causing catastrophic numerical loss.
/// The self-discounting formula is used as a stable fallback whenever the denominator is below 1e-15 or
/// the resulting discount factor falls outside the plausible range <c>(0, 2]</c>.
/// </para>
/// <para>
/// This replaces the prior single-exponential collapse <c>exp(-s * tau)</c>, which ignored both the
/// locked discount pillars and the projection forwards entirely.
/// </para>
/// </remarks>
public sealed class BasisSwapInstrumentHelper : AbstractInstrumentHelper
{
    private readonly ResolvedNode _node;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasisSwapInstrumentHelper"/> type.
    /// </summary>
    /// <param name="node">Resolved node carrying rate and metadata.</param>
    /// <param name="quoteValue">Observed market quote for the basis-swap instrument.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public BasisSwapInstrumentHelper(ResolvedNode node, double quoteValue)
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

        // Resolve the two projection legs (Fa, Fb) up front: a basis swap requires either zero (self-
        // discounting) or exactly two projection curves. Exactly one is a misconfiguration that fails loud.
        (ICurve? faCurve, ICurve? fbCurve) = TryGetProjectionCurves(partialCurveGroup);

        if (!partialCurveGroup.TryGetCurve(TargetCurve, out ICurve? targetCurve) || targetCurve is not IDiscountCurve discountCurve)
        {
            double tauSingle = Math.Max(1e-12, periods.Sum(p => p.YearFraction));
            return 1.0 / (1.0 + QuoteValue * tauSingle);
        }

        double annuityPrior = 0.0;
        double basisPvPrior = 0.0;
        for (int i = 0; i < periods.Count - 1; i++)
        {
            double pc = discountCurve.DiscountFactor(periods[i].PeriodEndDate);
            annuityPrior += periods[i].YearFraction * pc;
            if (faCurve != null && fbCurve != null)
            {
                double d = GetDifferential(faCurve, fbCurve, periods[i].PeriodStartDate, periods[i].PeriodEndDate, periods[i].YearFraction);
                basisPvPrior += d * periods[i].YearFraction * pc;
            }
        }

        SchedulePeriod lastPeriod = periods[^1];
        double tauN = Math.Max(1e-12, lastPeriod.YearFraction);

        if (faCurve == null || fbCurve == null)
        {
            // Self-discounting: the basis spread acts as the rate on a fixed annuity formed from the
            // locked Pc pillars, against a telescoping float leg.
            return (1.0 - QuoteValue * annuityPrior) / (1.0 + QuoteValue * tauN);
        }

        // Multi-curve §4.3.6: terminal differential d_N = Fb(T_N) - Fa(T_N) from the two projection curves.
        double dN = GetDifferential(faCurve, fbCurve, lastPeriod.PeriodStartDate, lastPeriod.PeriodEndDate, tauN);
        double numerator = basisPvPrior - QuoteValue * annuityPrior;
        double denominator = tauN * (QuoteValue - dN);

        // The closed form is numerically unstable when s ≈ d_N (near-zero denominator and near-zero
        // numerator due to cancellation). Fall back to the self-discounting par-annuity whenever the
        // formula yields an implausible discount factor: a valid DF must lie in (0, 2] for any realistic
        // rate environment, so anything outside that range indicates numerical failure.
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

        (ICurve? faCurve, ICurve? fbCurve) = TryGetProjectionCurves(curveGroup);

        double annuity = 0.0;
        for (int i = 0; i < periods.Count - 1; i++)
        {
            annuity += periods[i].YearFraction * discountCurve.DiscountFactor(periods[i].PeriodEndDate);
        }
        // The terminal period's DF must use the calibrated pillar date, NOT the BD-adjusted schedule
        // end date. When the raw tenor date falls on a non-business day the two dates diverge by 1-3
        // calendar days and the DF lookup would return an extrapolated (rather than exact) value,
        // breaking the algebraic identity  s = basisPvTotal / annuity_total  by ~2e-7 per day of drift.
        double pcTerminal = discountCurve.DiscountFactor(pillar);
        annuity += periods[^1].YearFraction * pcTerminal;

        if (annuity < 1e-15)
        {
            return QuoteValue;
        }

        if (faCurve == null || fbCurve == null)
        {
            // Self-discounting: the floating leg telescopes to 1 - Pc(T_N).
            return (1.0 - pcTerminal) / annuity;
        }

        // Multi-curve §4.3.6: invert the SAME par relation SolveNodeValue uses —
        //   s = (sum_i d_i * tau_i * Pc(T_i)) / (sum_i tau_i * Pc(T_i)), d_i = Fb(T_i) - Fa(T_i).
        // The terminal period uses the calibrated pillar DF, mirroring SolveNodeValue, so a multi-curve
        // node round-trips its repricing diagnostic.
        double basisPvTotal = 0.0;
        for (int i = 0; i < periods.Count - 1; i++)
        {
            double pc = discountCurve.DiscountFactor(periods[i].PeriodEndDate);
            double d = GetDifferential(faCurve, fbCurve, periods[i].PeriodStartDate, periods[i].PeriodEndDate, periods[i].YearFraction);
            basisPvTotal += d * periods[i].YearFraction * pc;
        }

        SchedulePeriod terminalPeriod = periods[^1];
        double dN = GetDifferential(faCurve, fbCurve, terminalPeriod.PeriodStartDate, terminalPeriod.PeriodEndDate, terminalPeriod.YearFraction);
        basisPvTotal += dN * terminalPeriod.YearFraction * pcTerminal;

        return basisPvTotal / annuity;
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

    /// <summary>
    /// Resolves the two projection curves (<c>Fa</c>, <c>Fb</c>) backing a tenor basis swap. Returns
    /// <c>(null, null)</c> for the self-discounting case (no projection curves); throws for the
    /// misconfigured single-curve case; otherwise returns both legs (index 0 = Fa, index 1 = Fb).
    /// </summary>
    private (ICurve? Fa, ICurve? Fb) TryGetProjectionCurves(ICurveGroup curveGroup)
    {
        IReadOnlyList<CurveReference>? projRefs = _node.ProjectionCurves;
        if (projRefs == null || projRefs.Count == 0)
        {
            return (null, null);
        }

        if (projRefs.Count == 1)
        {
            // A basis swap is parameterised by TWO projected legs (Fa and Fb). A single projection
            // curve cannot describe a basis swap — fail loud rather than silently mis-pricing it as a
            // single-leg instrument (which would produce a plausible-but-wrong discount factor).
            throw new InvalidOperationException(
                $"Basis-swap node '{_node.Label}' was configured with exactly one projection curve, but a " +
                $"basis swap requires TWO projection legs (Fa and Fb). Supply both projection curves, or " +
                $"none to use the self-discounting form.");
        }

        ICurve faCurve = ResolveProjectionCurve(curveGroup, projRefs[0]);
        ICurve fbCurve = ResolveProjectionCurve(curveGroup, projRefs[1]);
        return (faCurve, fbCurve);
    }

    private ICurve ResolveProjectionCurve(ICurveGroup curveGroup, CurveReference projRef)
    {
        if (!curveGroup.TryGetCurve(projRef, out ICurve? pCurve))
        {
            // A node explicitly configured for multi-curve mode whose named projection curve is absent
            // must fail loudly — silently degrading would produce a plausible-but-wrong discount factor
            // with no diagnostic. (Pre-calibration validation normally catches this, but that guard is
            // bypassed when SkipValidation=true.)
            throw new InvalidOperationException(
                $"Projection curve for reference {projRef} (required by basis-swap node '{_node.Label}') " +
                $"was not found in the curve group. Ensure both projection curves are calibrated before this node.");
        }

        if (pCurve is not IDiscountCurve && pCurve is not IForwardCurve)
        {
            throw new InvalidOperationException(
                $"Projection curve for reference {projRef} must implement IDiscountCurve or IForwardCurve. " +
                $"Actual type: {pCurve?.GetType().Name ?? "null"}.");
        }

        return pCurve;
    }

    /// <summary>
    /// The per-period basis differential <c>d = Fb - Fa</c>, where each forward is read from the
    /// matching projection curve via <see cref="GetForwardRate"/>.
    /// </summary>
    private static double GetDifferential(ICurve faCurve, ICurve fbCurve, DateOnly start, DateOnly end, double tau)
    {
        double fa = GetForwardRate(faCurve, start, end, tau);
        double fb = GetForwardRate(fbCurve, start, end, tau);
        return fb - fa;
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
