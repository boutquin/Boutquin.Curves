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
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.Curves.Bootstrap.Helpers;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Core;
using Boutquin.Curves.Core.Discounting;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

/// <summary>
/// Unit tests for individual instrument helpers.
/// Covers F-01 (FixedFloat empty schedule guard), F-02 (OisFuture negative DF guard),
/// F-03 (FixedFloat silent projection-curve cast guard), and F-08 (helper unit tests).
/// </summary>
public sealed class InstrumentHelperTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly CurveReference s_discRef = new(CurveRole.Discount, Currency.USD);

    // ── Test doubles ──────────────────────────────────────────────────────────

    private sealed class ConstantDiscountCurve : IDiscountCurve
    {
        private readonly double _df;
        public ConstantDiscountCurve(double df) => _df = df;
        public CurveName Name => new("TEST");
        public DateOnly ValuationDate => s_valuationDate;
        public Currency Currency => Currency.USD;
        public IYearFractionCalculator DayCount => Actual365Fixed.Instance;
        public double ValueAt(DateOnly date) => _df;
        public double DiscountFactor(DateOnly date) => _df;
        public double ZeroRate(DateOnly date, CompoundingConvention compounding) => 0d;
        public double InstantaneousForwardRate(DateOnly date) => 0d;
        public IDiscountCurve WithValuationDate(DateOnly newValuationDate) => this;
    }

    private sealed class NonDiscountCurve : ICurve
    {
        public CurveName Name => new("NON-DISCOUNT");
        public DateOnly ValuationDate => s_valuationDate;
        public Currency Currency => Currency.USD;
        public IYearFractionCalculator DayCount => Actual365Fixed.Instance;
        public double ValueAt(DateOnly date) => 0.99d;
    }

    private static CurveGroup BuildGroupWith(CurveReference reference, ICurve curve)
    {
        return new CurveGroupBuilder(new CurveGroupName("TEST"), s_valuationDate)
            .Add(reference, curve)
            .Build();
    }

    private static CurveGroup EmptyGroup()
    {
        return new CurveGroupBuilder(new CurveGroupName("TEST"), s_valuationDate).Build();
    }

    private static InterpolatedDiscountCurve SimpleCurve(CurveReference reference)
    {
        return new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_valuationDate,
            reference.Currency,
            new[] { new CurvePoint(s_valuationDate.AddYears(2), 0.92d) });
    }

    private static SimpleReferenceDataProvider CreateReferenceData(params string[] conventionCodes)
    {
        IInstrumentConvention[] conventions = conventionCodes
            .Select(c => InstrumentConventionRegistry.CreateDefault().GetRequired(c))
            .ToArray();
        return new SimpleReferenceDataProvider(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            conventions);
    }

    // ── F-01: FixedFloat stub-schedule (single period) handling ───────────────

    [Fact]
    public void FixedFloat_SolveNodeValue_ReturnsValidDiscountFactor_ForStubSchedule()
    {
        // A 3M tenor with a 6M payment convention produces a single stub period
        // (the schedule generator clamps the first period end to the 3M pillar).
        // The helper must return a positive DF in (0, 1] rather than crashing.
        const string conventionCode = "USD-FIXED-6M-30-360";
        ResolvedNode node = new("SWAP-3M", new Tenor("3M"), "FixedFloatSwap", conventionCode, s_discRef, 0.05m);
        FixedFloatSwapInstrumentHelper helper = new(node, 0.05d);

        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);
        CurveGroup partialGroup = BuildGroupWith(s_discRef, SimpleCurve(s_discRef));

        double df = helper.SolveNodeValue(partialGroup, s_valuationDate, referenceData);

        df.Should().BeGreaterThan(0d).And.BeLessThanOrEqualTo(1d);
    }

    // ── F-02: OisFuture negative DF guard ────────────────────────────────────

    [Fact]
    public void OisFuture_ImpliedQuote_ThrowsWhenDiscountFactorIsNegative()
    {
        // A degenerate calibration can make the curve return a negative discount factor.
        // ImpliedQuote must not silently clamp it; it should surface the error explicitly.
        const string conventionCode = "USD-SR3";
        ResolvedNode node = new("FUT-1Y", new Tenor("1Y"), "OisFuture", conventionCode, s_discRef, 0.04m);
        OisFutureInstrumentHelper helper = new(node, 0.04d);

        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);
        CurveGroup group = BuildGroupWith(s_discRef, new ConstantDiscountCurve(-0.1d));

        Action act = () => helper.ImpliedQuote(group, s_valuationDate, referenceData);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-positive*");
    }

    // ── T-C1-1: Deposit / FRA ImpliedQuote non-positive DF guard ──────────────

    [Fact]
    public void Deposit_ImpliedQuote_ThrowsWhenDiscountFactorIsNegative()
    {
        // A diverged calibration can make the curve return a non-positive discount factor.
        // Deposit ImpliedQuote inverts 1/df; on df<0 it would silently return a finite-but-wrong
        // rate, and on df=0 it would return +Infinity — both poison the repricing diagnostic.
        // It must fail loud, matching the OisFuture sibling contract (F-02 above).
        const string conventionCode = "USD-SOFR-OIS";
        ResolvedNode node = new("DEP-2Y", new Tenor("2Y"), "Deposit", conventionCode, s_discRef, 0.05m);
        DepositInstrumentHelper helper = new(node, 0.05d);

        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);
        CurveGroup group = BuildGroupWith(s_discRef, new ConstantDiscountCurve(-0.1d));

        Action act = () => helper.ImpliedQuote(group, s_valuationDate, referenceData);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-positive*");
    }

    [Fact]
    public void Deposit_ImpliedQuote_ThrowsWhenDiscountFactorIsZero()
    {
        // df == 0 makes 1/df = +Infinity, which the diagnostic must not propagate silently.
        const string conventionCode = "USD-SOFR-OIS";
        ResolvedNode node = new("DEP-2Y", new Tenor("2Y"), "Deposit", conventionCode, s_discRef, 0.05m);
        DepositInstrumentHelper helper = new(node, 0.05d);

        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);
        CurveGroup group = BuildGroupWith(s_discRef, new ConstantDiscountCurve(0d));

        Action act = () => helper.ImpliedQuote(group, s_valuationDate, referenceData);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-positive*");
    }

    [Fact]
    public void Fra_ImpliedQuote_ThrowsWhenDiscountFactorIsNegative()
    {
        // FRA ImpliedQuote is the byte-identical sibling of Deposit's; same fail-loud contract.
        const string conventionCode = "USD-FRA";
        ResolvedNode node = new("FRA-2Y", new Tenor("2Y"), "Fra", conventionCode, s_discRef, 0.044m);
        FraInstrumentHelper helper = new(node, 0.044d);

        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);
        CurveGroup group = BuildGroupWith(s_discRef, new ConstantDiscountCurve(-0.1d));

        Action act = () => helper.ImpliedQuote(group, s_valuationDate, referenceData);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-positive*");
    }

    // ── D-4: reject negative year fraction ──────────────────────────────────────────────
    //
    // Instrument helpers previously floored year fraction with Math.Max(1e-12, tau), which would
    // mask a NEGATIVE tau (pillar before valuation date — a caller/convention bug, e.g. a negative
    // tenor resolving to a past date) by silently clamping it to the tiny positive floor. D-4 adds
    // AbstractInstrumentHelper.FloorYearFraction, which rejects a negative tau with
    // ArgumentOutOfRangeException before flooring, at the 4 call sites where a raw two-date
    // YearFraction(valuationDate, pillar) result is floored inline (Deposit x2, OisFuture, Fra).
    //
    // For the day-count conventions exercised below (ACT/360, ACT/365F — the two conventions
    // backing every in-repo OIS/FRA/future instrument), the underlying IYearFractionCalculator
    // (e.g. Actual360.YearFraction) ALREADY throws ArgumentOutOfRangeException(paramName:
    // "endDate") for endDate < startDate — verified by reading the calculator source. So
    // FloorYearFraction's own negative-check never actually fires through these three helpers
    // today; the day-count layer wins the race. It remains correct defense-in-depth: it is the
    // instrument-helper layer's OWN expression of the D-4 contract, independent of which
    // day-count implementation a future convention supplies (a convention backed by a
    // hypothetical non-validating calculator would still be caught here). These tests therefore
    // assert only the exception TYPE (matching AC-D2's "throws ArgumentOutOfRangeException"
    // literally) — not a specific paramName, since which layer catches the violation first is an
    // implementation detail neither this milestone nor AC-D2 pins.

    [Fact]
    public void Deposit_SolveNodeValue_ThrowsOnNegativeYearFraction()
    {
        const string conventionCode = "USD-SOFR-OIS";
        // A negative tenor resolves the pillar to a date BEFORE the valuation date — a genuine
        // negative-tau scenario reached through the real TenorParser.AddTenor path, not synthetic.
        ResolvedNode node = new("DEP-NEG", new Tenor("-5D"), "Deposit", conventionCode, s_discRef, 0.05m);
        DepositInstrumentHelper helper = new(node, 0.05d);

        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);
        CurveGroup group = EmptyGroup();

        Action act = () => helper.SolveNodeValue(group, s_valuationDate, referenceData);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Fra_SolveNodeValue_ThrowsOnNegativeYearFraction()
    {
        const string conventionCode = "USD-FRA";
        ResolvedNode node = new("FRA-NEG", new Tenor("-5D"), "Fra", conventionCode, s_discRef, 0.044m);
        FraInstrumentHelper helper = new(node, 0.044d);

        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);
        CurveGroup group = EmptyGroup();

        Action act = () => helper.SolveNodeValue(group, s_valuationDate, referenceData);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OisFuture_SolveNodeValue_ThrowsOnNegativeYearFraction()
    {
        const string conventionCode = "USD-SR3";
        ResolvedNode node = new("FUT-NEG", new Tenor("-5D"), "OisFuture", conventionCode, s_discRef, 0.04m);
        OisFutureInstrumentHelper helper = new(node, 0.04d);

        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);
        CurveGroup group = EmptyGroup();

        Action act = () => helper.SolveNodeValue(group, s_valuationDate, referenceData);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Deposit_SolveNodeValue_ZeroYearFraction_StillFloorsRatherThanThrows()
    {
        // The legitimate degenerate case (same-day tenor, tau == 0) must still floor to 1e-12,
        // not throw — only a NEGATIVE tau is rejected. "0D" resolves the pillar to the exact
        // valuation date, giving tau == 0 through the real day-count path.
        const string conventionCode = "USD-SOFR-OIS";
        ResolvedNode node = new("DEP-0D", new Tenor("0D"), "Deposit", conventionCode, s_discRef, 0.05m);
        DepositInstrumentHelper helper = new(node, 0.05d);

        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);
        CurveGroup group = EmptyGroup();

        Action act = () => helper.SolveNodeValue(group, s_valuationDate, referenceData);

        act.Should().NotThrow("a zero (same-day) year fraction is legitimate and must floor to 1e-12, not be rejected");
    }

    // ── F-03: FixedFloat silent projection-curve cast guard ───────────────────

    [Fact]
    public void FixedFloat_SolveNodeValue_ThrowsWhenProjectionCurveIsNotIDiscountCurve()
    {
        // When ProjectionCurves is configured but the resolved curve does not implement
        // IDiscountCurve, the helper must fail loudly rather than silently fall back
        // to self-discounting (which would misrepresent the multi-curve setup).
        const string conventionCode = "USD-FIXED-6M-30-360";
        CurveReference projRef = new(CurveRole.Forward, Currency.USD);
        ResolvedNode node = new(
            "SWAP-1Y", new Tenor("1Y"), "FixedFloatSwap", conventionCode, s_discRef, 0.05m,
            ProjectionCurves: [projRef]);
        FixedFloatSwapInstrumentHelper helper = new(node, 0.05d);

        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);

        // Discount curve is a real IDiscountCurve; projection curve is plain ICurve only.
        CurveGroup group = new CurveGroupBuilder(new CurveGroupName("TEST"), s_valuationDate)
            .Add(s_discRef, SimpleCurve(s_discRef))
            .Add(projRef, new NonDiscountCurve())
            .Build();

        Action act = () => helper.SolveNodeValue(group, s_valuationDate, referenceData);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IDiscountCurve*");
    }

    // ── AT-08: FixedFloat absent-projection-curve fail-loud guard ─────────────

    [Fact]
    public void FixedFloat_SolveNodeValue_ThrowsWhenDeclaredProjectionCurveIsAbsent()
    {
        // A node explicitly configured for multi-curve mode (ProjectionCurves non-empty) whose named
        // projection curve is NOT present in the partial curve group must fail loudly — not silently
        // fall back to self-discounting, which would produce a plausible-but-wrong discount factor.
        // This is the same fail-loud shape as the adjacent wrong-type guard (F-03 above); the absent
        // curve case was previously fail-silent.
        const string conventionCode = "USD-FIXED-6M-30-360";
        CurveReference projRef = new(CurveRole.Forward, Currency.USD);
        ResolvedNode node = new(
            "SWAP-1Y", new Tenor("1Y"), "FixedFloatSwap", conventionCode, s_discRef, 0.05m,
            ProjectionCurves: [projRef]);
        FixedFloatSwapInstrumentHelper helper = new(node, 0.05d);

        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);

        // The discount curve is present but the declared projection curve is intentionally omitted.
        CurveGroup group = BuildGroupWith(s_discRef, SimpleCurve(s_discRef));

        Action act = () => helper.SolveNodeValue(group, s_valuationDate, referenceData);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*projection curve*");
    }

    // ── F-L2-R2-01: self-referencing projection curve is treated as self-discounting ──

    [Fact]
    public void FixedFloat_SolveNodeValue_SelfReferencingProjection_DoesNotThrow_AndSelfDiscounts()
    {
        // The validator explicitly ALLOWS a node naming its own target curve as its projection
        // reference ("equivalent to self-discounting"). The runtime must honour that: a
        // self-reference must take the self-discounting path, not throw the AT-08 absent-curve
        // guard. This case is reachable on the first-solved node (empty partial group), where the
        // target curve is not yet published — exactly where the AT-08 throw fired.
        const string conventionCode = "USD-FIXED-6M-30-360";
        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);

        // Self-reference: ProjectionCurves names the node's own TargetCurve.
        ResolvedNode selfRefNode = new(
            "SWAP-1Y", new Tenor("1Y"), "FixedFloatSwap", conventionCode, s_discRef, 0.05m,
            ProjectionCurves: [s_discRef]);
        FixedFloatSwapInstrumentHelper selfRefHelper = new(selfRefNode, 0.05d);

        // Plain self-discounting (ProjectionCurves = null) — the reference behaviour to match.
        ResolvedNode selfDiscNode = new(
            "SWAP-1Y", new Tenor("1Y"), "FixedFloatSwap", conventionCode, s_discRef, 0.05m);
        FixedFloatSwapInstrumentHelper selfDiscHelper = new(selfDiscNode, 0.05d);

        // First-solved node: the partial group is EMPTY (the target curve is not yet published).
        // This is exactly the position where the AT-08 absent-projection throw fired for a
        // self-reference, despite the validator certifying it valid.
        double selfRefDf = selfRefHelper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);
        double selfDiscDf = selfDiscHelper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);

        selfRefDf.Should().BeApproximately(selfDiscDf, 1e-15);
    }

    // ── F-08: Unit tests for individual instrument helpers (happy paths) ───────

    [Fact]
    public void DepositHelper_SolveNodeValue_ReturnsPlausibleDiscountFactor()
    {
        const double rate = 0.05;
        ResolvedNode node = new("DEP-3M", new Tenor("3M"), "Deposit", "USD-SOFR-OIS", s_discRef, (decimal)rate);
        DepositInstrumentHelper helper = new(node, rate);
        IReferenceDataProvider referenceData = CreateReferenceData("USD-SOFR-OIS");

        double df = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);

        df.Should().BeGreaterThan(0d).And.BeLessThanOrEqualTo(1d);
    }

    [Fact]
    public void DepositHelper_ImpliedQuote_RoundTripsMarketRate()
    {
        const double rate = 0.05;
        ResolvedNode node = new("DEP-3M", new Tenor("3M"), "Deposit", "USD-SOFR-OIS", s_discRef, (decimal)rate);
        DepositInstrumentHelper helper = new(node, rate);
        IReferenceDataProvider referenceData = CreateReferenceData("USD-SOFR-OIS");

        double df = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);

        CurveGroup fullGroup = BuildGroupWith(s_discRef,
            new InterpolatedDiscountCurve(
                new CurveName("USD-Disc"), s_valuationDate, Currency.USD,
                new[] { new CurvePoint(helper.PillarDate(s_valuationDate, referenceData), df) }));

        double implied = helper.ImpliedQuote(fullGroup, s_valuationDate, referenceData);
        implied.Should().BeApproximately(rate, 1e-10);
    }

    [Fact]
    public void OisHelper_SolveNodeValue_ReturnsPlausibleDiscountFactor()
    {
        const double rate = 0.04;
        ResolvedNode node = new("OIS-1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", s_discRef, (decimal)rate);
        OisInstrumentHelper helper = new(node, rate);
        IReferenceDataProvider referenceData = CreateReferenceData("USD-SOFR-OIS");

        double df = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);

        df.Should().BeGreaterThan(0d).And.BeLessThanOrEqualTo(1d);
    }

    [Fact]
    public void OisHelper_ImpliedQuote_RoundTripsMarketRate()
    {
        const double rate = 0.04;
        ResolvedNode node = new("OIS-1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", s_discRef, (decimal)rate);
        OisInstrumentHelper helper = new(node, rate);
        IReferenceDataProvider referenceData = CreateReferenceData("USD-SOFR-OIS");

        double df = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);
        DateOnly pillar = helper.PillarDate(s_valuationDate, referenceData);

        CurveGroup fullGroup = BuildGroupWith(s_discRef,
            new InterpolatedDiscountCurve(
                new CurveName("USD-Disc"), s_valuationDate, Currency.USD,
                new[] { new CurvePoint(pillar, df) }));

        double implied = helper.ImpliedQuote(fullGroup, s_valuationDate, referenceData);
        implied.Should().BeApproximately(rate, 1e-10);
    }

    [Fact]
    public void OisFutureHelper_SolveNodeValue_ReturnsExponentialDiscountFactor()
    {
        const double rate = 0.043;
        ResolvedNode node = new("FUT-6M", new Tenor("6M"), "OisFuture", "USD-SR3", s_discRef, (decimal)rate);
        OisFutureInstrumentHelper helper = new(node, rate);
        IReferenceDataProvider referenceData = CreateReferenceData("USD-SR3");

        double df = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);

        // exp(-R*tau) for a positive rate must be in (0,1)
        df.Should().BeGreaterThan(0d).And.BeLessThan(1d);
    }

    [Fact]
    public void OisFutureHelper_ImpliedQuote_RoundTripsPositiveDiscountFactor()
    {
        const double rate = 0.043;
        ResolvedNode node = new("FUT-6M", new Tenor("6M"), "OisFuture", "USD-SR3", s_discRef, (decimal)rate);
        OisFutureInstrumentHelper helper = new(node, rate);
        IReferenceDataProvider referenceData = CreateReferenceData("USD-SR3");

        double df = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);
        DateOnly pillar = helper.PillarDate(s_valuationDate, referenceData);

        CurveGroup fullGroup = BuildGroupWith(s_discRef,
            new InterpolatedDiscountCurve(
                new CurveName("USD-Disc"), s_valuationDate, Currency.USD,
                new[] { new CurvePoint(pillar, df) }));

        double implied = helper.ImpliedQuote(fullGroup, s_valuationDate, referenceData);
        implied.Should().BeApproximately(rate, 1e-10);
    }

    [Fact]
    public void FraHelper_SolveNodeValue_ReturnsPlausibleDiscountFactor()
    {
        const double rate = 0.044;
        ResolvedNode node = new("FRA-9M", new Tenor("9M"), "Fra", "USD-FRA", s_discRef, (decimal)rate);
        FraInstrumentHelper helper = new(node, rate);
        IReferenceDataProvider referenceData = CreateReferenceData("USD-FRA");

        double df = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);

        df.Should().BeGreaterThan(0d).And.BeLessThanOrEqualTo(1d);
    }

    [Fact]
    public void FixedFloatHelper_SolveNodeValue_ReturnsSinglePeriodFallbackWhenNoPartialCurve()
    {
        // With no discount curve in the partial group, the helper uses the single-period
        // simple-interest par-annuity formula: Pc = 1 / (1 + R * tau).
        const double rate = 0.046;
        const string conventionCode = "USD-FIXED-6M-30-360";
        ResolvedNode node = new("SWAP-1Y", new Tenor("1Y"), "FixedFloatSwap", conventionCode, s_discRef, (decimal)rate);
        FixedFloatSwapInstrumentHelper helper = new(node, rate);
        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);

        double df = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);

        df.Should().BeGreaterThan(0d).And.BeLessThanOrEqualTo(1d);
    }

    [Fact]
    public void BasisSwapHelper_SolveNodeValue_ReturnsPlausibleDiscountFactor()
    {
        const double spread = 0.0025;
        const string conventionCode = "USD-FIXED-6M-30-360";
        ResolvedNode node = new("BASIS-1Y", new Tenor("1Y"), "BasisSwap", conventionCode, s_discRef, (decimal)spread);
        BasisSwapInstrumentHelper helper = new(node, spread);
        IReferenceDataProvider referenceData = CreateReferenceData(conventionCode);

        double df = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);

        df.Should().BeGreaterThan(0d).And.BeLessThanOrEqualTo(1d);
    }

    // ── F-22: Known-answer tests ───────────────────────────────────────────────

    [Fact]
    public void DepositHelper_SolveNodeValue_MatchesSimpleInterestFormula()
    {
        // For a 1Y deposit at 5% ACT/360, the known discount factor is 1/(1+0.05*tau_ACT360).
        const double rate = 0.05;
        ResolvedNode node = new("DEP-1Y", new Tenor("1Y"), "Deposit", "USD-SOFR-OIS", s_discRef, (decimal)rate);
        DepositInstrumentHelper helper = new(node, rate);
        IReferenceDataProvider referenceData = CreateReferenceData("USD-SOFR-OIS");

        double df = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);

        // Deposit uses simple interest: Pc = 1/(1+R*tau)
        // tau for 2026-04-09 to 2027-04-09 under ACT/360 ≈ 365/360
        double tau = Actual360.Instance.YearFraction(s_valuationDate, helper.PillarDate(s_valuationDate, referenceData));
        double expected = 1.0 / (1.0 + rate * tau);
        df.Should().BeApproximately(expected, 1e-12);
    }

    [Fact]
    public void OisFutureHelper_SolveNodeValue_MatchesContinuousCompoundingFormula()
    {
        // For an OIS future at 4.3%, the known DF is exp(-rate*tau).
        const double rate = 0.043;
        ResolvedNode node = new("FUT-6M", new Tenor("6M"), "OisFuture", "USD-SR3", s_discRef, (decimal)rate);
        OisFutureInstrumentHelper helper = new(node, rate);
        IReferenceDataProvider referenceData = CreateReferenceData("USD-SR3");

        double df = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, referenceData);

        DateOnly pillar = helper.PillarDate(s_valuationDate, referenceData);
        double tau = Actual360.Instance.YearFraction(s_valuationDate, pillar);
        double expected = Math.Exp(-rate * tau);
        df.Should().BeApproximately(expected, 1e-6);
    }
}
