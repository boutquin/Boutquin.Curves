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

using Boutquin.Curves.Abstractions;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Bootstrap.Helpers;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Core;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

/// <summary>
/// Pins that <see cref="BasisSwapInstrumentHelper"/> routes its year fraction through the resolved
/// convention's day-count (CF-C1-7) rather than a hard-coded ACT/365 divisor.
/// </summary>
public sealed class BasisSwapDayCountTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);

    [Fact]
    public void SolveNodeValue_UsesConventionDayCount_NotHardcoded365()
    {
        const string conventionCode = "EUR-FIXED-1Y-ACT-360"; // ACT/360, not ACT/365
        const double rate = 0.03;

        CurveReference curveReference = new(CurveRole.Discount, Currency.EUR);
        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("TARGET") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired(conventionCode) });

        ResolvedNode node = new("BASIS-2Y", new Tenor("2Y"), "BasisSwap", conventionCode, curveReference, (decimal)rate);
        var helper = new BasisSwapInstrumentHelper(node, rate);
        ICurveGroup emptyGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_valuationDate).Build();

        double df = helper.SolveNodeValue(emptyGroup, s_valuationDate, referenceData);

        // With no locked discount pillars, the helper takes the single-period par-annuity fallback
        // Pc = 1 / (1 + R * tau), where tau is the convention-driven accrual year fraction summed across
        // the generated schedule. This test pins that the year fraction routes through the resolved
        // convention's day-count (ACT/360), not a hard-coded ACT/365 divisor.
        DateOnly pillar = helper.PillarDate(s_valuationDate, referenceData);
        SwapLegConvention convention = (SwapLegConvention)referenceData.GetConvention(conventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);
        var calendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        var schedule = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            s_valuationDate, pillar, convention.PaymentFrequency, calendar,
            convention.BusinessDayAdjustment, dayCount, convention.PaymentLagBusinessDays, convention.EndOfMonth);
        double tau360 = schedule.Sum(p => p.YearFraction);
        double expectedDf = 1.0 / (1.0 + rate * tau360);

        // The helper must match the ACT/360 convention-driven discount factor...
        df.Should().BeApproximately(expectedDf, 1e-12);

        // ...and must NOT equal the hard-coded ACT/365 value (they differ for the same dates).
        double tau365 = (pillar.DayNumber - s_valuationDate.DayNumber) / 365d;
        double hardcoded365Df = 1.0 / (1.0 + rate * tau365);
        df.Should().NotBeApproximately(hardcoded365Df, 1e-9);
    }
}
