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
/// Solves OIS bootstrap nodes from quoted par rates using the closed-form par-annuity relation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Always self-discounting.</b> An OIS swap's floating leg accrues overnight fixings compounded
/// on the same curve being calibrated, so there is no separate projection curve. Unlike
/// <see cref="FixedFloatSwapInstrumentHelper"/>, which supports multi-curve mode via
/// <see cref="ResolvedNode.ProjectionCurves"/>, this helper ignores any projection curve
/// references and always reads forwards from the target discount curve.
/// </para>
/// <para>
/// The terminal pillar discount factor is solved from the OIS par condition <c>R * A_c = 1 - Pc(T_N)</c>,
/// where the OIS-discounted annuity <c>A_c = sum_j tauK_j * Pc(S_j)</c> uses the in-progress curve's
/// locked pillars and the unknown terminal factor enters the final coupon:
/// <c>Pc(T_N) = (1 - R * A_prior) / (1 + R * tauK_N)</c>.
/// For a single-period OIS (first pillar, no prior locked nodes) this degenerates to the simple-interest
/// form <c>Pc(T_1) = 1 / (1 + R * tau)</c>. Repricing inverts via <c>R = (1 - Pc(T_N)) / A_c</c>.
/// </para>
/// This replaces the prior single-exponential collapse <c>exp(-rate * tau)</c>, which ignored the
/// locked intermediate discount pillars.
/// </remarks>
public sealed class OisInstrumentHelper : AbstractInstrumentHelper
{
    private readonly ResolvedNode _node;

    /// <summary>
    /// Initializes a new instance of the <see cref="OisInstrumentHelper"/> type.
    /// </summary>
    /// <param name="node">Resolved node carrying rate and metadata.</param>
    /// <param name="quoteValue">Observed market quote for the OIS instrument.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public OisInstrumentHelper(ResolvedNode node, double quoteValue)
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

        if (!partialCurveGroup.TryGetCurve(TargetCurve, out ICurve? targetCurve) || targetCurve is not IDiscountCurve discountCurve)
        {
            double tauSingle = Math.Max(1e-12, periods.Sum(p => p.YearFraction));
            return 1.0 / (1.0 + QuoteValue * tauSingle);
        }

        double annuityPrior = 0.0;
        for (int i = 0; i < periods.Count - 1; i++)
        {
            annuityPrior += periods[i].YearFraction * discountCurve.DiscountFactor(periods[i].PeriodEndDate);
        }

        double tauN = Math.Max(1e-12, periods[^1].YearFraction);
        return (1.0 - QuoteValue * annuityPrior) / (1.0 + QuoteValue * tauN);
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

        double annuity = 0.0;
        for (int i = 0; i < periods.Count - 1; i++)
        {
            annuity += periods[i].YearFraction * discountCurve.DiscountFactor(periods[i].PeriodEndDate);
        }
        // Same pillar-date consistency fix as FixedFloatSwapInstrumentHelper/BasisSwapInstrumentHelper:
        // the terminal period's DF must use the calibrated pillar date rather than the BD-adjusted
        // schedule end date, so the algebraic identity  R = (1 - Pc(T_N)) / annuity_total  holds to
        // floating-point precision. When the raw tenor date falls on a non-business day the two dates
        // diverge by 1-3 calendar days and the mismatch breaks the round-trip by ~2e-7 per day of drift.
        double pcTerminal = discountCurve.DiscountFactor(pillar);
        annuity += periods[^1].YearFraction * pcTerminal;

        if (annuity < 1e-15)
        {
            return QuoteValue;
        }

        return (1.0 - pcTerminal) / annuity;
    }

    private IReadOnlyList<SchedulePeriod> GenerateSchedule(DateOnly valuationDate, DateOnly pillar, IReferenceDataProvider referenceData)
    {
        OisConvention convention = (OisConvention)referenceData.GetConvention(_node.ConventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);
        IBusinessCalendar calendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        return ScheduleGenerator.Generate(
            valuationDate,
            pillar,
            convention.PaymentFrequency,
            calendar,
            convention.BusinessDayAdjustment,
            dayCount,
            convention.PaymentLagBusinessDays);
    }
}
