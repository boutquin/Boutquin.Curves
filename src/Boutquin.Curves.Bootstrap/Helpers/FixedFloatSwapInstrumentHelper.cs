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
using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Bootstrap.Helpers;

/// <summary>
/// Solves fixed-float swap bootstrap nodes from quoted par rates.
/// </summary>
/// <remarks>
/// Uses <c>df = exp(-rate * totalTau)</c> where <c>totalTau</c> is the sum of accrual year fractions
/// across the generated fixed-leg schedule.
/// </remarks>
public sealed class FixedFloatSwapInstrumentHelper : AbstractInstrumentHelper
{
    private readonly ResolvedNode _node;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedFloatSwapInstrumentHelper"/> type.
    /// </summary>
    /// <param name="node">Resolved node carrying rate and metadata.</param>
    /// <param name="quoteValue">Observed market quote for the swap instrument.</param>
    public FixedFloatSwapInstrumentHelper(ResolvedNode node, double quoteValue)
        : base(node.Label, node.TargetCurve, quoteValue, node.InstrumentType)
    {
        _node = node;
    }

    /// <inheritdoc/>
    public override DateOnly PillarDate(DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        return TenorParser.AddTenor(valuationDate, _node.Tenor);
    }

    /// <inheritdoc/>
    public override double SolveNodeValue(ICurveGroup partialCurveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        DateOnly pillar = PillarDate(valuationDate, referenceData);
        double yearFraction = ComputeSwapAccrualYearFraction(valuationDate, pillar, referenceData);
        return Math.Exp(-QuoteValue * yearFraction);
    }

    /// <inheritdoc/>
    public override double ImpliedQuote(ICurveGroup curveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        DateOnly pillar = PillarDate(valuationDate, referenceData);
        double? discountFactor = TryGetDiscountFactor(curveGroup, pillar);
        if (!discountFactor.HasValue)
        {
            return QuoteValue;
        }

        double yearFraction = ComputeSwapAccrualYearFraction(valuationDate, pillar, referenceData);
        return -Math.Log(Math.Max(discountFactor.Value, 1e-15)) / yearFraction;
    }

    private double ComputeSwapAccrualYearFraction(DateOnly valuationDate, DateOnly pillar, IReferenceDataProvider referenceData)
    {
        SwapLegConvention convention = (SwapLegConvention)referenceData.GetConvention(_node.ConventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);

        var calendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        IReadOnlyList<SchedulePeriod> periods = ScheduleGenerator.Generate(
            valuationDate,
            pillar,
            convention.PaymentFrequency,
            calendar,
            convention.BusinessDayAdjustment,
            dayCount,
            convention.PaymentLagBusinessDays,
            convention.EndOfMonth);

        return Math.Max(1e-12, periods.Sum(p => p.YearFraction));
    }
}
