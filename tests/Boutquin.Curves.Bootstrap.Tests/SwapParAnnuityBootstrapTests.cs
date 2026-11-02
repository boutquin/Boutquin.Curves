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
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

/// <summary>
/// Pins the canonical multi-curve par-annuity swap pillar relation (CF-C1-1):
/// the terminal pillar discount factor MUST be solved from
/// R = (sum_i F_x * tauL * Pc(Ti)) / (sum_j tauK * Pc(Sj)), so the locked discount pillars Pc
/// and the projection forwards F_x enter the solve — NOT the single-exponential collapse
/// P = exp(-R * sum(tau)) that references none of them.
/// </summary>
public sealed class SwapParAnnuityBootstrapTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);

    // The par-annuity relation shipped (CalibrationSeam redesign — the solve loop publishes each
    // solved pillar into the partial curve group before the next node is solved). This is a green
    // regression guard: the NotBeApproximately(collapseDf, 1e-6) assertion is the load-bearing
    // discriminator that proves the helper solves from the par-annuity relation, not the
    // single-exponential collapse exp(-R*sum(tau)).
    [Fact]
    public void SolveNodeValue_FixedFloatSwap_SolvesFromParAnnuityRelation_NotSingleExponential()
    {
        const string conventionCode = "USD-FIXED-6M-30-360"; // semi-annual fixed leg -> two coupon periods over 1Y
        const double parRate = 0.046;

        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);
        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired(conventionCode) });

        // A partial discount curve carrying earlier-locked pillars Pc at the intermediate
        // fixed-leg payment dates (here the 6M coupon date) under a DISTINCT discount level
        // from the projection forwards, so a faithful par-annuity solve produces a terminal DF
        // that differs from the single-exponential collapse exp(-R * sum(tau)).
        DateOnly pillar1Y = s_valuationDate.AddYears(1);
        DateOnly coupon6M = s_valuationDate.AddMonths(6);

        // Build a partial group with a discount curve that already prices the 6M pillar.
        var points = new[]
        {
            new CurvePoint(coupon6M, Math.Exp(-0.05 * 0.5)),
        };
        IDiscountCurve discountCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_valuationDate,
            Currency.USD,
            points,
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        ICurveGroup partialGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_valuationDate)
            .Add(discountRef, discountCurve)
            .Build();

        ResolvedNode node = new("SWAP-1Y", new Tenor("1Y"), "FixedFloatSwap", conventionCode, discountRef, (decimal)parRate);
        var helper = new FixedFloatSwapInstrumentHelper(node, parRate);

        double terminalDf = helper.SolveNodeValue(partialGroup, s_valuationDate, referenceData);

        // The single-exponential collapse the reference forbids.
        SwapLegConvention convention = (SwapLegConvention)referenceData.GetConvention(conventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);
        var calendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        var schedule = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            s_valuationDate, pillar1Y, convention.PaymentFrequency, calendar,
            convention.BusinessDayAdjustment, dayCount, convention.PaymentLagBusinessDays, convention.EndOfMonth);
        double totalTau = schedule.Sum(p => p.YearFraction);
        double collapseDf = Math.Exp(-parRate * totalTau);

        // Canonical par-annuity terminal DF: with the fixed annuity formed from Pc on each coupon
        // date, and the floating leg = Pc(start) - Pc(end), the par relation
        //   R * A_c = Pc(start) - Pc(end)
        // gives, for a coupon schedule {S_1=6M, S_2=1Y} with locked Pc(6M):
        //   R*(tauK1*Pc(6M) + tauK2*Pc(1Y)) = Pc(0) - Pc(1Y)
        // Solve for Pc(1Y):
        double pc0 = 1.0;
        double pc6m = discountCurve.DiscountFactor(coupon6M);
        double tauK1 = schedule[0].YearFraction;
        double tauK2 = schedule[1].YearFraction;
        double expectedTerminalDf = (pc0 - parRate * tauK1 * pc6m) / (1.0 + parRate * tauK2);

        // A faithful solve must match the par-annuity terminal DF, and must NOT be the collapse.
        terminalDf.Should().BeApproximately(expectedTerminalDf, 1e-9);
        terminalDf.Should().NotBeApproximately(collapseDf, 1e-6);
    }

    // ---------------------------------------------------------------------------------------------
    // Basis-swap §4.3.6 two-projection-curve bootstrap (build-arena M-1).
    //
    // Canonical relation (Ametrano-Bianchetti §4.3.6): the quoted basis spread s is the additive
    // spread that makes the two projection (FRA) legs Fa and Fb par on the collateral discount curve
    // Pc:
    //   s = (sum_i (Fb(Ti) - Fa(Ti)) * tau_i * Pc(Ti)) / (sum_i tau_i * Pc(Ti))
    // Splitting locked (i<N) from the unknown terminal Pc(T_N), with d_i = Fb(Ti) - Fa(Ti):
    //   Pc_N = (basisPvPrior - s * A_prior) / (tau_N * (s - d_N))
    // where A_prior = sum_{i<N} tau_i*Pc(Ti) and basisPvPrior = sum_{i<N} d_i*tau_i*Pc(Ti).
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// AC-1: Two-projection-curve hand-computed oracle. Builds a basis node with two distinct,
    /// non-flat projection curves (Fa = ProjectionCurves[0], Fb = ProjectionCurves[1]) and a discount
    /// curve carrying a locked interior 6M pillar at a level distinct from both projection levels.
    /// The terminal Pc(1Y) is hand-computed from explicit literal forwards/discount pillars (NOT by
    /// calling the helper's own forward/inverse pair), s is fed as the quote, and the solved DF must
    /// equal the independently-computed Pc_N to 1e-9.
    /// </summary>
    [Fact]
    public void SolveNodeValue_BasisSwap_TwoProjectionCurves_RepricesToHandComputedDf()
    {
        const string conventionCode = "USD-FIXED-6M-30-360"; // semi-annual schedule -> {6M, 1Y}
        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);
        CurveReference projARef = new(CurveRole.Forward, Currency.USD, new BenchmarkName("Fa"));
        CurveReference projBRef = new(CurveRole.Forward, Currency.USD, new BenchmarkName("Fb"));

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired(conventionCode) });

        DateOnly pillar1Y = s_valuationDate.AddYears(1);

        // Locked discount-curve DFs at 6M and 1Y. The 1Y value is the ORACLE we expect the solve to
        // recover; only the 6M pillar is published into the partial group.
        const double pc6m = 0.97531;   // distinct discount level
        const double pc1y = 0.96079;   // the oracle terminal DF

        // Fa projection-curve DFs (curve a — e.g. 1M tenor): distinct level from discount and Fb.
        const double pa6m = 0.98800;
        const double pa1y = 0.97300;
        // Fb projection-curve DFs (curve b — e.g. 3M tenor): distinct again.
        const double pb6m = 0.98200;
        const double pb1y = 0.96500;

        SwapLegConvention convention = (SwapLegConvention)referenceData.GetConvention(conventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);
        var calendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        var schedule = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            s_valuationDate, pillar1Y, convention.PaymentFrequency, calendar,
            convention.BusinessDayAdjustment, dayCount, convention.PaymentLagBusinessDays, convention.EndOfMonth);

        double tau1 = schedule[0].YearFraction;  // 6M period tau
        double tau2 = schedule[1].YearFraction;  // 1Y period tau

        // Hand-computed forwards from the literal projection DFs (discrete (P(t1)/P(t2)-1)/tau form),
        // computed independently of the helper.
        double fa1 = (1.0 / pa6m - 1.0) / tau1;
        double fa2 = (pa6m / pa1y - 1.0) / tau2;
        double fb1 = (1.0 / pb6m - 1.0) / tau1;
        double fb2 = (pb6m / pb1y - 1.0) / tau2;
        double d1 = fb1 - fa1;
        double d2 = fb2 - fa2;

        // Hand-compute the par spread s = (sum d_i*tau_i*Pc_i) / (sum tau_i*Pc_i), using the ORACLE
        // pc1y. Feeding this s back to the helper (which only knows pc6m) must recover pc1y.
        double basisPvTotal = d1 * tau1 * pc6m + d2 * tau2 * pc1y;
        double annuityTotal = tau1 * pc6m + tau2 * pc1y;
        double s = basisPvTotal / annuityTotal;

        // Independent oracle for the terminal DF (does NOT call the helper):
        //   Pc_N = (basisPvPrior - s*A_prior) / (tau_N * (s - d_N))
        double aPrior = tau1 * pc6m;
        double basisPvPrior = d1 * tau1 * pc6m;
        double expectedPcN = (basisPvPrior - s * aPrior) / (tau2 * (s - d2));

        // Sanity: the oracle algebra must round-trip to pc1y.
        expectedPcN.Should().BeApproximately(pc1y, 1e-9);

        IDiscountCurve partialDiscountCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pc6m) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        IDiscountCurve projACurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Fa"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pa6m), new CurvePoint(schedule[1].PeriodEndDate, pa1y) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        IDiscountCurve projBCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Fb"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pb6m), new CurvePoint(schedule[1].PeriodEndDate, pb1y) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        ICurveGroup partialGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_valuationDate)
            .Add(discountRef, partialDiscountCurve)
            .Add(projARef, projACurve)
            .Add(projBRef, projBCurve)
            .Build();

        ResolvedNode node = new(
            "BASIS-1Y-MC",
            new Tenor("1Y"),
            "BasisSwap",
            conventionCode,
            discountRef,
            (decimal)s,
            new[] { projARef, projBRef });

        var helper = new BasisSwapInstrumentHelper(node, s);
        double terminalDf = helper.SolveNodeValue(partialGroup, s_valuationDate, referenceData);

        terminalDf.Should().BeApproximately(expectedPcN, 1e-9,
            because: "a faithful §4.3.6 two-projection solve must recover the terminal Pc(T_N) " +
                     "used to derive the basis spread from both projection curves");
    }

    /// <summary>
    /// AC-2: Projection-actually-bites. Solves the SAME basis node once WITH the two projection
    /// curves and once self-discounting (ProjectionCurves = null), otherwise identical inputs, and
    /// asserts the two DFs differ by &gt; 1e-6 — proving the §4.3.6 forwards numerically enter the
    /// solved DF (anti-hollow guard). Also asserts the multi-curve DF is NOT the flat collapse
    /// exp(-s*totalTau).
    /// </summary>
    [Fact]
    public void SolveNodeValue_BasisSwap_TwoProjectionCurves_DiffersFromSelfDiscountingAndCollapse()
    {
        const string conventionCode = "USD-FIXED-6M-30-360";
        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);
        CurveReference projARef = new(CurveRole.Forward, Currency.USD, new BenchmarkName("Fa"));
        CurveReference projBRef = new(CurveRole.Forward, Currency.USD, new BenchmarkName("Fb"));

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired(conventionCode) });

        DateOnly pillar1Y = s_valuationDate.AddYears(1);

        SwapLegConvention convention = (SwapLegConvention)referenceData.GetConvention(conventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);
        var calendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        var schedule = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            s_valuationDate, pillar1Y, convention.PaymentFrequency, calendar,
            convention.BusinessDayAdjustment, dayCount, convention.PaymentLagBusinessDays, convention.EndOfMonth);
        double tau1 = schedule[0].YearFraction;
        double tau2 = schedule[1].YearFraction;

        // Locked discount pillar at 6M and the oracle 1Y discount pillar.
        const double pc6m = 0.97531;
        const double pc1y = 0.96079;

        // Fa / Fb projection-curve DFs (distinct, non-flat).
        const double pa6m = 0.98800, pa1y = 0.97300;
        const double pb6m = 0.98200, pb1y = 0.96500;

        // Hand-computed differentials and the CONSISTENT par spread that prices this basis swap to par
        // on these two projection curves (so the multi-curve branch yields a valid in-range DF, not a
        // fallback). An arbitrary spread inconsistent with the curves would trip the (0,2] guard.
        double fa1 = (1.0 / pa6m - 1.0) / tau1, fa2 = (pa6m / pa1y - 1.0) / tau2;
        double fb1 = (1.0 / pb6m - 1.0) / tau1, fb2 = (pb6m / pb1y - 1.0) / tau2;
        double d1 = fb1 - fa1, d2 = fb2 - fa2;
        double basisPvTotal = d1 * tau1 * pc6m + d2 * tau2 * pc1y;
        double annuityTotal = tau1 * pc6m + tau2 * pc1y;
        double basisSpread = basisPvTotal / annuityTotal;

        double totalTau = tau1 + tau2;
        double collapseDf = Math.Exp(-basisSpread * totalTau);

        IDiscountCurve partialDiscountCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pc6m) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        IDiscountCurve projACurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Fa"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pa6m), new CurvePoint(schedule[1].PeriodEndDate, pa1y) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        IDiscountCurve projBCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Fb"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pb6m), new CurvePoint(schedule[1].PeriodEndDate, pb1y) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        ICurveGroup mcGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_valuationDate)
            .Add(discountRef, partialDiscountCurve)
            .Add(projARef, projACurve)
            .Add(projBRef, projBCurve)
            .Build();

        ICurveGroup selfGroup = new CurveGroupBuilder(new CurveGroupName("g2"), s_valuationDate)
            .Add(discountRef, partialDiscountCurve)
            .Build();

        ResolvedNode mcNode = new(
            "BASIS-1Y-MC", new Tenor("1Y"), "BasisSwap", conventionCode, discountRef,
            (decimal)basisSpread, new[] { projARef, projBRef });
        ResolvedNode selfNode = new(
            "BASIS-1Y-SELF", new Tenor("1Y"), "BasisSwap", conventionCode, discountRef,
            (decimal)basisSpread);

        double mcDf = new BasisSwapInstrumentHelper(mcNode, basisSpread)
            .SolveNodeValue(mcGroup, s_valuationDate, referenceData);
        double selfDf = new BasisSwapInstrumentHelper(selfNode, basisSpread)
            .SolveNodeValue(selfGroup, s_valuationDate, referenceData);

        // The multi-curve solve must recover the oracle 1Y pillar (the §4.3.6 forwards entered the solve).
        mcDf.Should().BeApproximately(pc1y, 1e-9,
            "the multi-curve solve must recover the oracle terminal pillar from the two projection curves");
        Math.Abs(mcDf - selfDf).Should().BeGreaterThan(1e-6,
            "the §4.3.6 projection forwards must numerically enter the solved DF (anti-hollow guard)");
        mcDf.Should().NotBeApproximately(collapseDf, 1e-6,
            "the multi-curve solve must not collapse to exp(-s*tau)");
    }

    /// <summary>
    /// AC-4: Mis-count fails loud. A BasisSwap node with exactly ONE projection curve must throw
    /// <see cref="InvalidOperationException"/> naming the node and mentioning two projection legs.
    /// Zero projection curves degrades to self-discounting (no throw).
    /// </summary>
    [Fact]
    public void SolveNodeValue_BasisSwap_OneProjectionCurve_ThrowsLoud_ZeroDoesNot()
    {
        const string conventionCode = "USD-FIXED-6M-30-360";
        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);
        CurveReference projARef = new(CurveRole.Forward, Currency.USD, new BenchmarkName("Fa"));

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired(conventionCode) });

        DateOnly pillar1Y = s_valuationDate.AddYears(1);
        const double basisSpread = 0.0025;

        SwapLegConvention convention = (SwapLegConvention)referenceData.GetConvention(conventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);
        var calendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        var schedule = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            s_valuationDate, pillar1Y, convention.PaymentFrequency, calendar,
            convention.BusinessDayAdjustment, dayCount, convention.PaymentLagBusinessDays, convention.EndOfMonth);

        IDiscountCurve partialDiscountCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, Math.Exp(-0.05 * 0.5)) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        IDiscountCurve projACurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Fa"),
            s_valuationDate,
            Currency.USD,
            new[]
            {
                new CurvePoint(schedule[0].PeriodEndDate, Math.Exp(-0.030 * 0.5)),
                new CurvePoint(schedule[1].PeriodEndDate, Math.Exp(-0.032 * 1.0)),
            },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        ICurveGroup oneGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_valuationDate)
            .Add(discountRef, partialDiscountCurve)
            .Add(projARef, projACurve)
            .Build();

        ResolvedNode oneNode = new(
            "BASIS-1Y-ONE", new Tenor("1Y"), "BasisSwap", conventionCode, discountRef,
            (decimal)basisSpread, new[] { projARef });

        Action act = () => new BasisSwapInstrumentHelper(oneNode, basisSpread)
            .SolveNodeValue(oneGroup, s_valuationDate, referenceData);

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("BASIS-1Y-ONE")
            .And.MatchRegex("(?i)two projection");

        // Zero projection curves must NOT throw — it degrades to self-discounting.
        ICurveGroup zeroGroup = new CurveGroupBuilder(new CurveGroupName("g2"), s_valuationDate)
            .Add(discountRef, partialDiscountCurve)
            .Build();
        ResolvedNode zeroNode = new(
            "BASIS-1Y-ZERO", new Tenor("1Y"), "BasisSwap", conventionCode, discountRef,
            (decimal)basisSpread);

        Action zeroAct = () => new BasisSwapInstrumentHelper(zeroNode, basisSpread)
            .SolveNodeValue(zeroGroup, s_valuationDate, referenceData);
        zeroAct.Should().NotThrow();
    }

    /// <summary>
    /// AC-5: Backward compat + multi-curve round-trip. A BasisSwap node with null ProjectionCurves
    /// yields the EXACT self-discounting DF (1 - s*A_prior)/(1 + s*tau_N) it does today. Separately,
    /// the multi-curve solve round-trips: solve -> reprice (ImpliedQuote) -> recover s within 1e-9.
    /// </summary>
    [Fact]
    public void SolveNodeValue_BasisSwap_BackwardCompat_AndMultiCurveRoundTrip()
    {
        const string conventionCode = "USD-FIXED-6M-30-360";
        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);
        CurveReference projARef = new(CurveRole.Forward, Currency.USD, new BenchmarkName("Fa"));
        CurveReference projBRef = new(CurveRole.Forward, Currency.USD, new BenchmarkName("Fb"));

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired(conventionCode) });

        DateOnly pillar1Y = s_valuationDate.AddYears(1);
        const double basisSpread = 0.0025;

        SwapLegConvention convention = (SwapLegConvention)referenceData.GetConvention(conventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);
        var calendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        var schedule = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            s_valuationDate, pillar1Y, convention.PaymentFrequency, calendar,
            convention.BusinessDayAdjustment, dayCount, convention.PaymentLagBusinessDays, convention.EndOfMonth);
        double tau1 = schedule[0].YearFraction;
        double tau2 = schedule[1].YearFraction;
        double pc6m = Math.Exp(-0.05 * 0.5);

        IDiscountCurve partialDiscountCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pc6m) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        ICurveGroup selfGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_valuationDate)
            .Add(discountRef, partialDiscountCurve)
            .Build();

        ResolvedNode selfNode = new(
            "BASIS-1Y-SELF", new Tenor("1Y"), "BasisSwap", conventionCode, discountRef,
            (decimal)basisSpread);
        double selfDf = new BasisSwapInstrumentHelper(selfNode, basisSpread)
            .SolveNodeValue(selfGroup, s_valuationDate, referenceData);

        // Exact self-discounting closed form (today's behaviour).
        double aPrior = tau1 * pc6m;
        double expectedSelfDf = (1.0 - basisSpread * aPrior) / (1.0 + basisSpread * tau2);
        selfDf.Should().BeApproximately(expectedSelfDf, 1e-12,
            "the zero-projection path must keep the exact self-discounting closed form");

        // Multi-curve round-trip: solve mcSpread -> Pc(1Y), publish that pillar, reprice, recover the
        // SAME spread. The fed spread must be CONSISTENT with the projection curves so the multi-curve
        // branch fires (an inconsistent spread would trip the (0,2] guard and fall back).
        const double pa6m = 0.98800, pa1y = 0.97300;
        const double pb6m = 0.98200, pb1y = 0.96500;
        IDiscountCurve projACurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Fa"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pa6m), new CurvePoint(schedule[1].PeriodEndDate, pa1y) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));
        IDiscountCurve projBCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Fb"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pb6m), new CurvePoint(schedule[1].PeriodEndDate, pb1y) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        // Consistent par spread on these curves with the locked pc6m and the oracle pillar pc1yOracle.
        const double pc1yOracle = 0.96079;
        double fa1 = (1.0 / pa6m - 1.0) / tau1, fa2 = (pa6m / pa1y - 1.0) / tau2;
        double fb1 = (1.0 / pb6m - 1.0) / tau1, fb2 = (pb6m / pb1y - 1.0) / tau2;
        double d1 = fb1 - fa1, d2 = fb2 - fa2;
        double mcSpread = (d1 * tau1 * pc6m + d2 * tau2 * pc1yOracle) / (tau1 * pc6m + tau2 * pc1yOracle);

        ICurveGroup mcPartial = new CurveGroupBuilder(new CurveGroupName("gmc"), s_valuationDate)
            .Add(discountRef, partialDiscountCurve)
            .Add(projARef, projACurve)
            .Add(projBRef, projBCurve)
            .Build();

        ResolvedNode mcNode = new(
            "BASIS-1Y-MC", new Tenor("1Y"), "BasisSwap", conventionCode, discountRef,
            (decimal)mcSpread, new[] { projARef, projBRef });
        var mcHelper = new BasisSwapInstrumentHelper(mcNode, mcSpread);
        double mcDf = mcHelper.SolveNodeValue(mcPartial, s_valuationDate, referenceData);

        // The multi-curve branch must have fired (recovered the oracle pillar), not fallen back.
        mcDf.Should().BeApproximately(pc1yOracle, 1e-9,
            "the consistent spread must drive the §4.3.6 multi-curve branch, not the fallback");

        // Publish the solved terminal pillar so ImpliedQuote can reprice the calibrated curve.
        DateOnly pillarDate = mcHelper.PillarDate(s_valuationDate, referenceData);
        IDiscountCurve calibratedDiscount = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pc6m), new CurvePoint(pillarDate, mcDf) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        ICurveGroup mcCalibrated = new CurveGroupBuilder(new CurveGroupName("gmc2"), s_valuationDate)
            .Add(discountRef, calibratedDiscount)
            .Add(projARef, projACurve)
            .Add(projBRef, projBCurve)
            .Build();

        double recoveredSpread = mcHelper.ImpliedQuote(mcCalibrated, s_valuationDate, referenceData);
        recoveredSpread.Should().BeApproximately(mcSpread, 1e-9,
            "the multi-curve ImpliedQuote must invert the same §4.3.6 relation SolveNodeValue uses");
    }

    /// <summary>
    /// AC-6: Day-count from convention. The year fractions used by the basis-swap solve come from the
    /// resolved SwapLegConvention via the helper's GenerateSchedule (no hard-coded basis). This pins
    /// the dependency by feeding two conventions with DIFFERENT day-count bases and asserting the
    /// self-discounting solved DF tracks the convention's schedule tau (so a hard-coded basis would
    /// produce identical DFs and fail).
    /// </summary>
    [Fact]
    public void SolveNodeValue_BasisSwap_YearFractionsComeFromConvention()
    {
        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);
        const double basisSpread = 0.01; // 100 bp — amplify the day-count sensitivity

        // 30/360 semi-annual convention.
        const string conv30360 = "USD-FIXED-6M-30-360";
        // A distinct ACT/360 convention to contrast the day-count basis.
        const string convAct360 = "USD-SOFR-OIS";

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[]
            {
                InstrumentConventionRegistry.CreateDefault().GetRequired(conv30360),
                InstrumentConventionRegistry.CreateDefault().GetRequired(convAct360),
            });

        DateOnly pillar1Y = s_valuationDate.AddYears(1);

        IDiscountCurve discount = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(s_valuationDate.AddMonths(6), Math.Exp(-0.05 * 0.5)) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        ICurveGroup group = new CurveGroupBuilder(new CurveGroupName("g"), s_valuationDate)
            .Add(discountRef, discount)
            .Build();

        // Independently compute the expected self-discounting DF for the 30/360 convention using its
        // OWN schedule tau — proving the helper reads the convention, not a hard-coded basis.
        SwapLegConvention conv = (SwapLegConvention)referenceData.GetConvention(conv30360);
        IYearFractionCalculator dc = DayCountResolver.Resolve(conv.DayCountCode);
        var cal = HolidayCalendarFactory.Create(conv.CalendarCode);
        var sched = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            s_valuationDate, pillar1Y, conv.PaymentFrequency, cal,
            conv.BusinessDayAdjustment, dc, conv.PaymentLagBusinessDays, conv.EndOfMonth);
        double tau1 = sched[0].YearFraction;
        double tau2 = sched[1].YearFraction;
        double pc6m = discount.DiscountFactor(sched[0].PeriodEndDate);
        double expected = (1.0 - basisSpread * tau1 * pc6m) / (1.0 + basisSpread * tau2);

        ResolvedNode node30360 = new(
            "BASIS-30360", new Tenor("1Y"), "BasisSwap", conv30360, discountRef, (decimal)basisSpread);
        double df30360 = new BasisSwapInstrumentHelper(node30360, basisSpread)
            .SolveNodeValue(group, s_valuationDate, referenceData);

        df30360.Should().BeApproximately(expected, 1e-12,
            "year fractions must come from the resolved SwapLegConvention schedule, not a hard-coded basis");
    }

    // The OIS par-annuity relation shipped (same CalibrationSeam redesign as the FixedFloat case).
    // This is a green regression guard: the NotBeApproximately(collapseDf, 1e-6) assertion is the
    // load-bearing discriminator that proves the terminal DF is solved from the OIS-discounted
    // annuity, not the single-period collapse exp(-R*tau).
    [Fact]
    public void SolveNodeValue_Ois_SolvesFromParAnnuityRelation_NotSingleExponential()
    {
        const string conventionCode = "USD-SOFR-OIS"; // annual OIS
        const double parRate = 0.045;

        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);
        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired(conventionCode) });

        // Two-year OIS so the annuity has a prior locked coupon at 1Y under a distinct discount level.
        DateOnly pillar2Y = s_valuationDate.AddYears(2);
        DateOnly coupon1Y = s_valuationDate.AddYears(1);

        var points = new[] { new CurvePoint(coupon1Y, Math.Exp(-0.05 * 1.0)) };
        IDiscountCurve discountCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_valuationDate,
            Currency.USD,
            points,
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        ICurveGroup partialGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_valuationDate)
            .Add(discountRef, discountCurve)
            .Build();

        ResolvedNode node = new("OIS-2Y", new Tenor("2Y"), "Ois", conventionCode, discountRef, (decimal)parRate);
        var helper = new OisInstrumentHelper(node, parRate);

        double terminalDf = helper.SolveNodeValue(partialGroup, s_valuationDate, referenceData);

        // OIS float PV = Pc(0) - Pc(end); annual annuity {1Y, 2Y} with locked Pc(1Y):
        //   R*(tauK1*Pc(1Y) + tauK2*Pc(2Y)) = 1 - Pc(2Y)
        // The accrual year fractions come from the actual generated schedule (USD-SOFR-OIS is ACT/360,
        // so a 1Y period is ~365/360, not exactly 1.0); the oracle must use the same tau values the
        // helper discounts on, not a rounded assumption.
        OisConvention convention = (OisConvention)referenceData.GetConvention(conventionCode);
        IYearFractionCalculator oisDayCount = DayCountResolver.Resolve(convention.DayCountCode);
        var oisCalendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        var oisSchedule = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            s_valuationDate, pillar2Y, convention.PaymentFrequency, oisCalendar,
            convention.BusinessDayAdjustment, oisDayCount, convention.PaymentLagBusinessDays);
        double tauK1 = oisSchedule[0].YearFraction;
        double tauK2 = oisSchedule[1].YearFraction;
        double pc1y = discountCurve.DiscountFactor(oisSchedule[0].PeriodEndDate);
        double expectedTerminalDf = (1.0 - parRate * tauK1 * pc1y) / (1.0 + parRate * tauK2);

        double tauWhole = oisDayCount.YearFraction(s_valuationDate, pillar2Y);
        double collapseDf = Math.Exp(-parRate * tauWhole);

        terminalDf.Should().BeApproximately(expectedTerminalDf, 1e-9);
        terminalDf.Should().NotBeApproximately(collapseDf, 1e-6);
    }

    /// <summary>
    /// AC-3: Multi-curve oracle — projection and discount curves carry distinct, non-flat levels.
    /// Hand-computes the par rate from both curves with explicit literal inputs, feeds that rate
    /// to the helper, and asserts the solved terminal DF equals the independently-computed Pc(1Y)
    /// to 1e-9. This pins correctness independently of self-repricing (global learning L-0096).
    /// </summary>
    [Fact]
    public void SolveNodeValue_FixedFloatSwap_MultiCurve_RepricesToIndependentlyComputedDf()
    {
        const string conventionCode = "USD-FIXED-6M-30-360";
        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);
        CurveReference projectionRef = new(CurveRole.Forward, Currency.USD);

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired(conventionCode) });

        // Known discount curve DFs at 6M and 1Y (the oracle terminal DF).
        DateOnly pillar1Y = s_valuationDate.AddYears(1);
        const double pc6m = 0.97531;   // ~ exp(-0.05 * 0.5)
        const double pc1y = 0.96079;   // ~ exp(-0.04 * 1.0) — the oracle terminal DF

        // Known projection curve DFs at 6M and 1Y (distinct from the discount curve).
        const double pp6m = 0.98522;   // ~ exp(-0.03 * 0.5)
        const double pp1y = 0.96079;   // ~ exp(-0.04 * 1.0)

        // Build the schedule to get the fixed-leg tau values.
        SwapLegConvention convention = (SwapLegConvention)referenceData.GetConvention(conventionCode);
        IYearFractionCalculator dayCount = DayCountResolver.Resolve(convention.DayCountCode);
        var calendar = HolidayCalendarFactory.Create(convention.CalendarCode);
        var schedule = Boutquin.Curves.Conventions.ScheduleGenerator.Generate(
            s_valuationDate, pillar1Y, convention.PaymentFrequency, calendar,
            convention.BusinessDayAdjustment, dayCount, convention.PaymentLagBusinessDays, convention.EndOfMonth);

        double tauK1 = schedule[0].YearFraction;  // 6M period tau
        double tauK2 = schedule[1].YearFraction;  // 1Y period tau

        // Projection forwards: Fx(6M) = (Pp(0)/Pp(6M) - 1)/tau1; Fx(1Y) = (Pp(6M)/Pp(1Y) - 1)/tau2.
        double fx1 = (1.0 / pp6m - 1.0) / tauK1;
        double fx2 = (pp6m / pp1y - 1.0) / tauK2;

        // Hand-compute the par rate: R = (sum Fx_i * tauL_i * Pc(T_i)) / (sum tauK_j * Pc(S_j)).
        double floatPv = fx1 * tauK1 * pc6m + fx2 * tauK2 * pc1y;
        double fixedAnnuity = tauK1 * pc6m + tauK2 * pc1y;
        double parRate = floatPv / fixedAnnuity;

        // Partial discount curve carrying only the locked 6M pillar (the 1Y pillar is what we solve for).
        IDiscountCurve partialDiscountCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pc6m) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        // Fully-calibrated projection curve carrying both pillars.
        IDiscountCurve projectionCurve = new InterpolatedDiscountCurve(
            new CurveName("USD-Proj"),
            s_valuationDate,
            Currency.USD,
            new[] { new CurvePoint(schedule[0].PeriodEndDate, pp6m), new CurvePoint(schedule[1].PeriodEndDate, pp1y) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        ICurveGroup partialGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_valuationDate)
            .Add(discountRef, partialDiscountCurve)
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
        double terminalDf = helper.SolveNodeValue(partialGroup, s_valuationDate, referenceData);

        // Oracle: the helper must recover the terminal DF used to derive the par rate from both curves.
        terminalDf.Should().BeApproximately(pc1y, 1e-9,
            because: "a multi-curve par-annuity solve must recover the terminal DF used to derive the par rate");
    }
}
