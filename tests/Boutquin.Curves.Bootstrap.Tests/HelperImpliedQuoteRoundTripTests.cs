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
using Boutquin.Curves.Core.Discounting;
using Boutquin.Curves.Interpolation;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.Calendars;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

/// <summary>
/// Pins the solve-side / reprice-side date consistency of the instrument helpers'
/// <c>ImpliedQuote</c> repricing inverse against their <c>SolveNodeValue</c> par solve:
/// for any node calibrated by <c>SolveNodeValue</c>, <c>ImpliedQuote</c> on the completed curve
/// MUST recover the input par rate to floating-point precision (the algebraic round-trip identity),
/// even when the terminal tenor lands on a non-business day (OIS, AT-01) and when the node is
/// calibrated in multi-curve mode against a separate projection curve (FixedFloat, AT-02).
/// </summary>
public sealed class HelperImpliedQuoteRoundTripTests
{
    // 2026-04-10 is a Friday; AddTenor(+1Y) = 2027-04-10 which is a Saturday, so the raw pillar
    // date lands off a business day and Following adjustment shifts the schedule end to Monday.
    private static readonly DateOnly s_fridayValuation = new(2026, 4, 10);

    // AT-01: OIS ImpliedQuote round-trips the par rate when the terminal pillar lands off a
    // business day. SolveNodeValue places the terminal DF at the raw pillar; ImpliedQuote must
    // form the numerator and the terminal annuity term from the SAME (pillar) date — otherwise the
    // identity R = (1 - Pc(T_N)) / annuity drifts by ~2e-7 per day of BD adjustment.
    [Fact]
    public void OisHelper_ImpliedQuote_RoundTripsParRate_WhenTerminalPillarLandsOffBusinessDay()
    {
        const string conventionCode = "OIS-TEST-SA-FOLLOWING";
        const double parRate = 0.045;

        OisConvention oisConvention = new(
            conventionCode,
            "USNY",
            "ACT/360",
            PaymentFrequency.SemiAnnual,
            BusinessDayAdjustment.Following,
            SettlementLagBusinessDays: 0,
            PaymentLagBusinessDays: 0,
            ObservationShiftConvention.None);

        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);
        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new IInstrumentConvention[] { oisConvention });

        ResolvedNode node = new("OIS-1Y", new Tenor("1Y"), "Ois", conventionCode, discountRef, (decimal)parRate);
        var helper = new OisInstrumentHelper(node, parRate);

        // Build the schedule to discover the intermediate (6M) coupon date so we can lock its pillar
        // before the terminal solve, exercising the annuityPrior path with a real prior pillar.
        DateOnly pillar = helper.PillarDate(s_fridayValuation, referenceData);
        pillar.DayOfWeek.Should().Be(DayOfWeek.Saturday, "the test requires the raw pillar to land off a business day");

        IYearFractionCalculator dayCount = DayCountResolver.Resolve(oisConvention.DayCountCode);
        IBusinessCalendar calendar = HolidayCalendarFactory.Create(oisConvention.CalendarCode);
        var schedule = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            s_fridayValuation, pillar, oisConvention.PaymentFrequency, calendar,
            oisConvention.BusinessDayAdjustment, dayCount, oisConvention.PaymentLagBusinessDays);

        // Sanity: the terminal schedule end date diverges from the raw pillar (BD adjustment fired).
        schedule[^1].PeriodEndDate.Should().NotBe(pillar);

        // Lock the intermediate 6M pillar on the partial discount curve at its BD-adjusted coupon date.
        double pc6m = Math.Exp(-0.05 * dayCount.YearFraction(s_fridayValuation, schedule[0].PeriodEndDate));
        IDiscountCurve partialCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_fridayValuation,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pc6m) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));
        ICurveGroup partialGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_fridayValuation)
            .Add(discountRef, partialCurve)
            .Build();

        // Solve the terminal pillar DF (placed at the raw pillar by SolveNodeValue).
        double terminalDf = helper.SolveNodeValue(partialGroup, s_fridayValuation, referenceData);

        // Build the completed curve carrying both the locked 6M pillar and the solved terminal pillar.
        IDiscountCurve fullCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_fridayValuation,
            Currency.USD,
            new[]
            {
                new CurvePoint(schedule[0].PeriodEndDate, pc6m),
                new CurvePoint(pillar, terminalDf),
            },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));
        ICurveGroup fullGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_fridayValuation)
            .Add(discountRef, fullCurve)
            .Build();

        double implied = helper.ImpliedQuote(fullGroup, s_fridayValuation, referenceData);

        implied.Should().BeApproximately(parRate, 1e-10,
            because: "the OIS repricing inverse must round-trip the par rate the solve consumed, " +
                     "using the calibrated pillar date for the terminal annuity term and numerator alike");
    }

    // F-L3-R2-01 (characterization): for a SINGLE-PERIOD multi-curve FixedFloat node the discount
    // factor cancels out of the par condition (R * tauN * Pc = fxN * tauN * Pc => R = fxN), so the
    // ONLY self-consistent par rate is fxN — the terminal forward read from the projection curve.
    // When fed that consistent par rate the round-trip identity holds regardless of which DF
    // SolveNodeValue returns for the (algebraically undetermined) discount factor. This test pins
    // that consistency and proves ImpliedQuote correctly reads the terminal projection forward in
    // the single-period branch (load-bearing: mutation-checked). See round-2 fix report — the
    // "round-trip break" in the original finding was an artifact of feeding R != fxN, which is never
    // a consistent single-period multi-curve par rate, so there is no fix to apply on SolveNodeValue.
    [Fact]
    public void FixedFloatHelper_RoundTripsParRate_ForSinglePeriodMultiCurveNode()
    {
        // A 3M tenor under a 6M (semi-annual) payment convention produces a single stub period.
        const string conventionCode = "USD-FIXED-6M-30-360";
        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);
        CurveReference projectionRef = new(CurveRole.Forward, Currency.USD);

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired(conventionCode) });

        DateOnly valuation = new(2026, 4, 9);

        SwapLegConvention convention = (SwapLegConvention)referenceData.GetConvention(conventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);
        IBusinessCalendar calendar = HolidayCalendarFactory.Create(convention.CalendarCode);

        var helper0 = new FixedFloatSwapInstrumentHelper(
            new ResolvedNode("STUB", new Tenor("3M"), "FixedFloatSwap", conventionCode, discountRef, 0m, new[] { projectionRef }),
            0d);
        DateOnly pillar = helper0.PillarDate(valuation, referenceData);

        var schedule = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            valuation, pillar, convention.PaymentFrequency, calendar,
            convention.BusinessDayAdjustment, dayCount, convention.PaymentLagBusinessDays, convention.EndOfMonth);
        schedule.Count.Should().Be(1, "the test requires a single stub period to exercise periods.Count == 1");

        // Distinct discount and projection levels.
        const double pcTerminal = 0.98765; // discount factor at the 3M pillar
        const double ppTerminal = 0.99012; // projection factor at the 3M pillar (distinct)

        IDiscountCurve projectionCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Proj"), valuation, Currency.USD,
            new[] { new CurvePoint(pillar, ppTerminal) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        // The single-period par rate is the terminal forward read from the projection curve.
        double tauN = schedule[0].YearFraction;
        double fxN = (1.0 / ppTerminal - 1.0) / tauN;
        double parRate = fxN;

        // Partial discount curve carrying the (locked) terminal pillar so the solve sees a discount curve.
        IDiscountCurve partialDiscount = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"), valuation, Currency.USD,
            new[] { new CurvePoint(pillar, pcTerminal) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        ResolvedNode node = new(
            "SWAP-3M-MC", new Tenor("3M"), "FixedFloatSwap", conventionCode, discountRef,
            (decimal)parRate, new[] { projectionRef });
        var helper = new FixedFloatSwapInstrumentHelper(node, parRate);

        ICurveGroup solveGroup = new CurveGroupBuilder(new CurveGroupName("g"), valuation)
            .Add(discountRef, partialDiscount)
            .Add(projectionRef, projectionCurve)
            .Build();

        double terminalDf = helper.SolveNodeValue(solveGroup, valuation, referenceData);

        IDiscountCurve fullDiscount = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"), valuation, Currency.USD,
            new[] { new CurvePoint(pillar, terminalDf) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));
        ICurveGroup fullGroup = new CurveGroupBuilder(new CurveGroupName("g"), valuation)
            .Add(discountRef, fullDiscount)
            .Add(projectionRef, projectionCurve)
            .Build();

        double implied = helper.ImpliedQuote(fullGroup, valuation, referenceData);

        implied.Should().BeApproximately(parRate, 1e-10,
            because: "a single-period multi-curve FixedFloat node must round-trip its par rate");
    }

    // AT-02: a FixedFloat node calibrated in multi-curve mode (projection curve present) must
    // reprice via ImpliedQuote back to its par rate. SolveNodeValue reads the projection forwards;
    // ImpliedQuote must invert the SAME multi-curve par relation, not the self-discounting one.
    [Fact]
    public void FixedFloatHelper_ImpliedQuote_RoundTripsParRate_ForMultiCurveNode()
    {
        const string conventionCode = "USD-FIXED-6M-30-360";
        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);
        CurveReference projectionRef = new(CurveRole.Forward, Currency.USD);

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired(conventionCode) });

        DateOnly valuation = new(2026, 4, 9);
        DateOnly pillar1Y = valuation.AddYears(1);

        // Distinct discount and projection levels so the multi-curve relation is not degenerate.
        const double pc6m = 0.97531;   // ~ exp(-0.05 * 0.5)
        const double pc1y = 0.96079;   // ~ exp(-0.04 * 1.0)
        const double pp6m = 0.98522;   // ~ exp(-0.03 * 0.5)
        const double pp1y = 0.95600;   // distinct projection level at 1Y

        SwapLegConvention convention = (SwapLegConvention)referenceData.GetConvention(conventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);
        IBusinessCalendar calendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        var schedule = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            valuation, pillar1Y, convention.PaymentFrequency, calendar,
            convention.BusinessDayAdjustment, dayCount, convention.PaymentLagBusinessDays, convention.EndOfMonth);

        double tauK1 = schedule[0].YearFraction;
        double tauK2 = schedule[1].YearFraction;

        double fx1 = (1.0 / pp6m - 1.0) / tauK1;
        double fx2 = (pp6m / pp1y - 1.0) / tauK2;

        double floatPv = fx1 * tauK1 * pc6m + fx2 * tauK2 * pc1y;
        double fixedAnnuity = tauK1 * pc6m + tauK2 * pc1y;
        double parRate = floatPv / fixedAnnuity;

        IDiscountCurve discountCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            valuation,
            Currency.USD,
            new[]
            {
                new CurvePoint(schedule[0].PeriodEndDate, pc6m),
                new CurvePoint(pillar1Y, pc1y),
            },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        IDiscountCurve projectionCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Proj"),
            valuation,
            Currency.USD,
            new[]
            {
                new CurvePoint(schedule[0].PeriodEndDate, pp6m),
                new CurvePoint(pillar1Y, pp1y),
            },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        ICurveGroup fullGroup = new CurveGroupBuilder(new CurveGroupName("g"), valuation)
            .Add(discountRef, discountCurve)
            .Add(projectionRef, projectionCurve)
            .Build();

        ResolvedNode node = new(
            "SWAP-1Y-MC",
            new Tenor("1Y"),
            "FixedFloatSwap",
            conventionCode,
            discountRef,
            (decimal)parRate,
            new[] { projectionRef });

        var helper = new FixedFloatSwapInstrumentHelper(node, parRate);
        double implied = helper.ImpliedQuote(fullGroup, valuation, referenceData);

        implied.Should().BeApproximately(parRate, 1e-10,
            because: "a multi-curve FixedFloat node must reprice via the multi-curve par relation " +
                     "(reading the projection forwards), not the self-discounting inverse");
    }
}
