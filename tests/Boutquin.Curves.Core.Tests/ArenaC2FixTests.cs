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
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Core.Discounting;
using Boutquin.Curves.Core.Forwards;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

/// <summary>
/// TDD tests for code-arena run 2026-06-13-2323 round-1 cluster C2 findings.
/// </summary>
public sealed class ArenaC2FixTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 11);
    private const Currency Usd = Currency.USD;
    private static readonly DateOnly s_date1Y = new(2027, 4, 11);

    // Helper: minimal two-pillar interpolated curve
    private static InterpolatedDiscountCurve MakeInterpolatedCurve(DateOnly valuationDate)
    {
        return new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            valuationDate,
            Usd,
            new[]
            {
                new CurvePoint(valuationDate.AddYears(1), 0.9512),
                new CurvePoint(valuationDate.AddYears(5), 0.7788),
            });
    }

    // ─────────────────────────────────────────────────────────────────
    // CF-C2-1: InstantaneousForwardRate throws at date == valuationDate
    // Correct behavior: returns a finite (non-throwing) forward estimate
    // at the front boundary (one-sided right difference).
    // Predicted failure: ArgumentOutOfRangeException thrown before the
    // assertion, because leftDate = valuationDate - 1 day, which makes
    // YearFraction(valDate, leftDate) throw "endDate must be on or after startDate".
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void InstantaneousForwardRate_AtValuationDate_ShouldReturnFiniteValue()
    {
        InterpolatedDiscountCurve curve = MakeInterpolatedCurve(s_valuationDate);

        // Must not throw and must return a finite positive value.
        Action act = () => _ = curve.InstantaneousForwardRate(s_valuationDate);

        act.Should().NotThrow();
        double fwd = curve.InstantaneousForwardRate(s_valuationDate);
        double.IsFinite(fwd).Should().BeTrue("forward rate at valuation date must be finite, not NaN or Inf");
    }

    [Fact]
    public void InstantaneousForwardRate_OneDayAfterValuationDate_ShouldReturnFiniteValue()
    {
        InterpolatedDiscountCurve curve = MakeInterpolatedCurve(s_valuationDate);
        DateOnly oneDayLater = s_valuationDate.AddDays(1);

        // Must not throw and must return a finite value.
        Action act = () => _ = curve.InstantaneousForwardRate(oneDayLater);

        act.Should().NotThrow();
        double fwd = curve.InstantaneousForwardRate(oneDayLater);
        double.IsFinite(fwd).Should().BeTrue("forward rate 1 day after valuation date must be finite");
    }

    // ─────────────────────────────────────────────────────────────────
    // CF-C2-2: CurveGroupBuilder.Build() passes mutable dict by reference.
    // Correct behavior: Build() returns a snapshot; builder mutation after
    // Build() does NOT change the already-returned group.
    // Predicted failure: group.GetCurve(newRef) succeeds (or group.AsDictionary()
    // has Count=2) after we Add() a second curve to the builder, proving
    // the live mutable dict is shared.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CurveGroupBuilder_MutationAfterBuild_ShouldNotAffectReturnedGroup()
    {
        var builder = new CurveGroupBuilder(new CurveGroupName("Test"), s_valuationDate);
        var ref1 = new CurveReference(CurveRole.Discount, Usd);
        var curve1 = new FlatDiscountCurve(new CurveName("USD-Disc"), s_valuationDate, Usd, 0.05);
        builder.Add(ref1, curve1);

        // Capture the snapshot before mutation.
        CurveGroup group = builder.Build();

        // Now mutate the builder — should have no effect on the already-built group.
        var ref2 = new CurveReference(CurveRole.Forward, Usd, new BenchmarkName("SOFR"));
        var curve2 = new FlatDiscountCurve(new CurveName("USD-SOFR"), s_valuationDate, Usd, 0.04);
        builder.Add(ref2, curve2);

        // The built group must still contain only 1 curve — ref2 was added AFTER Build().
        group.AsDictionary().Should().HaveCount(1, "builder mutation after Build() must not retroactively change the group");
        group.AsDictionary().Should().ContainKey(ref1);
        group.AsDictionary().Should().NotContainKey(ref2);
    }

    // ─────────────────────────────────────────────────────────────────
    // CF-C2-3: JumpAdjustedDiscountCurve ctor: no null guard on `jumps`.
    // Correct behavior: ArgumentNullException at construction time.
    // Predicted failure: NullReferenceException thrown deep in LINQ
    // rather than ArgumentNullException at the ctor parameter site.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void JumpAdjustedDiscountCurve_NullJumps_ShouldThrowArgumentNullException()
    {
        IDiscountCurve inner = new FlatDiscountCurve(new CurveName("USD"), s_valuationDate, Usd, 0.05);

        // null for jumps → must produce ArgumentNullException (not NRE buried in LINQ).
        Action act = () => _ = new JumpAdjustedDiscountCurve(inner, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("jumps");
    }

    [Fact]
    public void JumpAdjustedDiscountCurve_NullInnerCurve_ShouldThrowArgumentNullException()
    {
        Action act = () => _ = new JumpAdjustedDiscountCurve(null!, Array.Empty<CurvePoint>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("innerCurve");
    }

    // ─────────────────────────────────────────────────────────────────
    // CF-C2-4: JumpAdjustedDiscountCurve ctor: no jump.Value > 0 check.
    // Correct behavior: ArgumentOutOfRangeException when any jump factor ≤ 0.
    // Predicted failure: no exception at construction, allowing a zero/negative
    // factor to silently produce a non-positive (arbitrage-violating) DF later.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void JumpAdjustedDiscountCurve_ZeroJumpFactor_ShouldThrowArgumentOutOfRangeException()
    {
        IDiscountCurve inner = new FlatDiscountCurve(new CurveName("USD"), s_valuationDate, Usd, 0.05);
        CurvePoint[] jumps = [new CurvePoint(s_date1Y, 0d)]; // factor = 0 → DF collapses to 0

        Action act = () => _ = new JumpAdjustedDiscountCurve(inner, jumps);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("jumps");
    }

    [Fact]
    public void JumpAdjustedDiscountCurve_NegativeJumpFactor_ShouldThrowArgumentOutOfRangeException()
    {
        IDiscountCurve inner = new FlatDiscountCurve(new CurveName("USD"), s_valuationDate, Usd, 0.05);
        CurvePoint[] jumps = [new CurvePoint(s_date1Y, -0.5d)]; // negative factor → negative DF

        Action act = () => _ = new JumpAdjustedDiscountCurve(inner, jumps);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("jumps");
    }

    [Fact]
    public void JumpAdjustedDiscountCurve_PositiveJumpFactor_ShouldConstruct()
    {
        IDiscountCurve inner = new FlatDiscountCurve(new CurveName("USD"), s_valuationDate, Usd, 0.05);
        CurvePoint[] jumps = [new CurvePoint(s_date1Y, 0.9975d)]; // valid

        Action act = () => _ = new JumpAdjustedDiscountCurve(inner, jumps);

        act.Should().NotThrow();
    }
}

/// <summary>
/// TDD tests for code-arena run 2026-06-13-2323 round-2 cluster C2 findings.
/// Findings covered:
///   F-L1-C2-01 = F-L2-C2-04  FlatDiscountCurve.DiscountFactor past-date throw
///   F-L1-C2-02                FlatDiscountCurve.ZeroRate past-date throw (and all siblings)
///   F-L2-C2-01                ZeroSpreadedDiscountCurve ctor null underlying
///   F-L2-C2-02                MultiplicativeSpreadDiscountCurve ctor null underlying
///   F-L2-C2-03                ForwardCurveFromDiscountCurves ctor null discountCurve / projectionCurve
/// </summary>
public sealed class ArenaC2R2FixTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 15);
    private const Currency Usd = Currency.USD;
    private static readonly DateOnly s_pastDate = new(2026, 4, 10);      // 5 days before valuation
    private static readonly CurveName s_curveName = new("USD-Test");
    private static readonly BenchmarkName s_benchmark = new("SOFR");

    private static FlatDiscountCurve MakeFlatCurve() =>
        new(s_curveName, s_valuationDate, Usd, 0.04);

    // ─────────────────────────────────────────────────────────────────
    // F-L1-C2-01 / F-L2-C2-04 (SAME DEFECT — fixed once):
    // FlatDiscountCurve.DiscountFactor throws for date < ValuationDate
    // Correct behavior: return 1d (matching all sibling implementations).
    // Predicted RED: ArgumentOutOfRangeException from YearFraction.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FlatDiscountCurve_DiscountFactor_PastDate_ShouldReturnOne()
    {
        // Predicted RED: ArgumentOutOfRangeException —
        //   "endDate (2026-04-10) must be on or after startDate (2026-04-15)"
        var curve = MakeFlatCurve();

        double result = curve.DiscountFactor(s_pastDate);

        result.Should().Be(1d, "DiscountFactor for any date <= ValuationDate must equal 1, matching all sibling discount curves");
    }

    [Fact]
    public void FlatDiscountCurve_DiscountFactor_AtValuationDate_ShouldReturnOne()
    {
        // Verify boundary: date == ValuationDate → already returns 1 (YearFraction = 0 → exp(0) = 1).
        // This is a mutation-check anchor: if the guard is removed, date < ValuationDate throws.
        var curve = MakeFlatCurve();

        double result = curve.DiscountFactor(s_valuationDate);

        result.Should().Be(1d, "DiscountFactor at ValuationDate must equal 1 exactly");
    }

    // ─────────────────────────────────────────────────────────────────
    // F-L1-C2-02: FlatDiscountCurve.ZeroRate throws for date < ValuationDate
    // Correct behavior: return 0d (matching the t=0 convention in CurveMath
    // and the at-valuation boundary that already works for date == ValuationDate).
    // Predicted RED: ArgumentOutOfRangeException from YearFraction at line 122.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FlatDiscountCurve_ZeroRate_PastDate_ShouldReturnZero()
    {
        // Predicted RED: ArgumentOutOfRangeException from DayCount.YearFraction
        //   before even reaching CurveMath.ZeroRateFromDiscountFactor.
        var curve = MakeFlatCurve();

        double result = curve.ZeroRate(s_pastDate, CompoundingConvention.Continuous);

        result.Should().Be(0d, "ZeroRate for date <= ValuationDate must return 0, consistent with zero time-horizon convention");
    }

    [Fact]
    public void FlatDiscountCurve_ZeroRate_AtValuationDate_ShouldReturnZero()
    {
        // Verify the at-boundary case already works and must keep working after the fix.
        var curve = MakeFlatCurve();

        double result = curve.ZeroRate(s_valuationDate, CompoundingConvention.Continuous);

        result.Should().Be(0d, "ZeroRate at ValuationDate must equal 0 (t=0 convention)");
    }

    // ─────────────────────────────────────────────────────────────────
    // F-L2-C2-01: ZeroSpreadedDiscountCurve ctor stores null underlying.
    // Correct behavior: ArgumentNullException at ctor, not deferred NRE.
    // Predicted RED: no exception at construction — NRE only fires later.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ZeroSpreadedDiscountCurve_NullUnderlying_ShouldThrowArgumentNullException()
    {
        // Predicted RED: no exception thrown at all (null stored silently),
        //   so .Throw<ArgumentNullException>() assertion fails.
        Action act = () => _ = new ZeroSpreadedDiscountCurve(s_curveName, null!, 0.005);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("underlying");
    }

    // ─────────────────────────────────────────────────────────────────
    // F-L2-C2-02: MultiplicativeSpreadDiscountCurve ctor stores null underlying.
    // Correct behavior: ArgumentNullException at ctor, not deferred NRE.
    // Predicted RED: no exception at construction.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MultiplicativeSpreadDiscountCurve_NullUnderlying_ShouldThrowArgumentNullException()
    {
        // Predicted RED: no exception thrown at all (null stored silently).
        Action act = () => _ = new MultiplicativeSpreadDiscountCurve(s_curveName, null!, 0.999);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("underlying");
    }

    // ─────────────────────────────────────────────────────────────────
    // F-L2-C2-03: ForwardCurveFromDiscountCurves ctor stores both params without null checks.
    // Correct behavior: ArgumentNullException at ctor for both parameters.
    // Predicted RED: no exception at construction for either null.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ForwardCurveFromDiscountCurves_NullDiscountCurve_ShouldThrowArgumentNullException()
    {
        // Predicted RED: no exception at construction (NRE only fires later in WithValuationDate).
        IDiscountCurve projection = MakeFlatCurve();

        Action act = () => _ = new ForwardCurveFromDiscountCurves(s_curveName, s_benchmark, null!, projection);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("discountCurve");
    }

    [Fact]
    public void ForwardCurveFromDiscountCurves_NullProjectionCurve_ShouldThrowArgumentNullException()
    {
        // Predicted RED: no exception at construction (NRE deferred to first ValuationDate access).
        IDiscountCurve discount = MakeFlatCurve();

        Action act = () => _ = new ForwardCurveFromDiscountCurves(s_curveName, s_benchmark, discount, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("projectionCurve");
    }
}

/// <summary>
/// TDD tests for code-arena run 2026-06-13-2323 round-4 cluster C2 findings.
/// Findings covered:
///   F-CENSUS-A-01  InterpolatedDiscountCurve.ZeroRate past-date throws instead of returning 0
///   F-CENSUS-A-02  MultiplicativeSpreadDiscountCurve.ZeroRate past-date throws instead of returning 0
///   F-CENSUS-A-03  ZeroSpreadedDiscountCurve.ZeroRate past-date throws instead of returning 0
/// Family convention (established by R2-fixed FlatDiscountCurve): ZeroRate at/before ValuationDate → 0d.
/// Root cause: DayCount.YearFraction(ValuationDate, pastDate) throws ArgumentOutOfRangeException when
///   pastDate &lt; ValuationDate, before CurveMath.ZeroRateFromDiscountFactor is reached.
/// Fix shape: same front-boundary guard as FlatDiscountCurve.ZeroRate — add
///   `if (date &lt;= ValuationDate) return 0d;` before the YearFraction call.
/// </summary>
public sealed class ArenaC2R4FixTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 6, 14);
    private static readonly DateOnly s_pastDate = new(2026, 6, 4);   // 10 days before — same as critic probe
    private const Currency Usd = Currency.USD;
    private static readonly CurveName s_curveName = new("USD-Test");

    private static FlatDiscountCurve MakeFlatBase() =>
        new(s_curveName, s_valuationDate, Usd, 0.05);

    private static InterpolatedDiscountCurve MakeInterpolated() =>
        new(
            s_curveName,
            s_valuationDate,
            Usd,
            new[]
            {
                new CurvePoint(s_valuationDate.AddYears(1), 0.9512),
                new CurvePoint(s_valuationDate.AddYears(5), 0.7788),
            });

    // ─────────────────────────────────────────────────────────────────
    // F-CENSUS-A-01: InterpolatedDiscountCurve.ZeroRate past-date
    // Correct behavior: return 0d (matching FlatDiscountCurve.ZeroRate family convention).
    // Predicted RED: ArgumentOutOfRangeException —
    //   "endDate (2026-06-04) must be on or after startDate (2026-06-14)"
    //   thrown by DayCount.YearFraction(ValuationDate, pastDate) at line 227.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void InterpolatedDiscountCurve_ZeroRate_PastDate_ShouldReturnZero()
    {
        // PREDICTED RED before fix:
        //   System.ArgumentOutOfRangeException: endDate (2026-06-04) must be on or after startDate (2026-06-14).
        //   Thrown from YearFraction(ValuationDate=2026-06-14, date=2026-06-04) at ZeroRate L227.
        InterpolatedDiscountCurve curve = MakeInterpolated();

        double result = curve.ZeroRate(s_pastDate, CompoundingConvention.Continuous);

        result.Should().Be(0d,
            "ZeroRate for date <= ValuationDate must return 0 (t=0 family convention), matching FlatDiscountCurve.ZeroRate");
    }

    [Fact]
    public void InterpolatedDiscountCurve_ZeroRate_AtValuationDate_ShouldReturnZero()
    {
        // Mutation-check anchor: date == ValuationDate must remain 0 after the fix.
        // This case already passes (YearFraction returns 0 → CurveMath returns 0).
        InterpolatedDiscountCurve curve = MakeInterpolated();

        double result = curve.ZeroRate(s_valuationDate, CompoundingConvention.Continuous);

        result.Should().Be(0d, "ZeroRate at ValuationDate must equal 0 (t=0 convention)");
    }

    [Fact]
    public void InterpolatedDiscountCurve_ZeroRate_FutureDate_ShouldRemainUnchanged()
    {
        // Regression anchor: future-date behavior must not change after adding the guard.
        InterpolatedDiscountCurve curve = MakeInterpolated();
        DateOnly futureDate = s_valuationDate.AddYears(1);

        double result = curve.ZeroRate(futureDate, CompoundingConvention.Continuous);

        result.Should().BeGreaterThan(0d, "ZeroRate for a future date on a positive-rate curve must be positive");
        double.IsFinite(result).Should().BeTrue("ZeroRate for a future date must be finite");
    }

    // ─────────────────────────────────────────────────────────────────
    // F-CENSUS-A-02: MultiplicativeSpreadDiscountCurve.ZeroRate past-date
    // Correct behavior: return 0d.
    // Predicted RED: ArgumentOutOfRangeException from YearFraction at line 97.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MultiplicativeSpreadDiscountCurve_ZeroRate_PastDate_ShouldReturnZero()
    {
        // PREDICTED RED before fix:
        //   System.ArgumentOutOfRangeException: endDate (2026-06-04) must be on or after startDate (2026-06-14).
        //   Thrown from YearFraction at MultiplicativeSpreadDiscountCurve.ZeroRate L97.
        var curve = new MultiplicativeSpreadDiscountCurve(s_curveName, MakeFlatBase(), spreadFactor: 0.999);

        double result = curve.ZeroRate(s_pastDate, CompoundingConvention.Continuous);

        result.Should().Be(0d,
            "ZeroRate for date <= ValuationDate must return 0 (family convention), matching same-class DiscountFactor guard");
    }

    [Fact]
    public void MultiplicativeSpreadDiscountCurve_ZeroRate_AtValuationDate_ShouldReturnZero()
    {
        var curve = new MultiplicativeSpreadDiscountCurve(s_curveName, MakeFlatBase(), spreadFactor: 0.999);

        double result = curve.ZeroRate(s_valuationDate, CompoundingConvention.Continuous);

        result.Should().Be(0d, "ZeroRate at ValuationDate must equal 0 (t=0 convention)");
    }

    [Fact]
    public void MultiplicativeSpreadDiscountCurve_ZeroRate_FutureDate_ShouldRemainUnchanged()
    {
        // Regression anchor: future behavior must not change.
        var curve = new MultiplicativeSpreadDiscountCurve(s_curveName, MakeFlatBase(), spreadFactor: 0.999);
        DateOnly futureDate = s_valuationDate.AddYears(1);

        double result = curve.ZeroRate(futureDate, CompoundingConvention.Continuous);

        result.Should().BeGreaterThan(0d, "ZeroRate for future date must be positive");
        double.IsFinite(result).Should().BeTrue("ZeroRate for future date must be finite");
    }

    // ─────────────────────────────────────────────────────────────────
    // F-CENSUS-A-03: ZeroSpreadedDiscountCurve.ZeroRate past-date
    // Correct behavior: return 0d.
    // Predicted RED: ArgumentOutOfRangeException from YearFraction at line 89.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ZeroSpreadedDiscountCurve_ZeroRate_PastDate_ShouldReturnZero()
    {
        // PREDICTED RED before fix:
        //   System.ArgumentOutOfRangeException: endDate (2026-06-04) must be on or after startDate (2026-06-14).
        //   Thrown from YearFraction at ZeroSpreadedDiscountCurve.ZeroRate L89.
        var curve = new ZeroSpreadedDiscountCurve(s_curveName, MakeFlatBase(), spread: 0.005);

        double result = curve.ZeroRate(s_pastDate, CompoundingConvention.Continuous);

        result.Should().Be(0d,
            "ZeroRate for date <= ValuationDate must return 0 (family convention), matching same-class DiscountFactor guard");
    }

    [Fact]
    public void ZeroSpreadedDiscountCurve_ZeroRate_AtValuationDate_ShouldReturnZero()
    {
        var curve = new ZeroSpreadedDiscountCurve(s_curveName, MakeFlatBase(), spread: 0.005);

        double result = curve.ZeroRate(s_valuationDate, CompoundingConvention.Continuous);

        result.Should().Be(0d, "ZeroRate at ValuationDate must equal 0 (t=0 convention)");
    }

    [Fact]
    public void ZeroSpreadedDiscountCurve_ZeroRate_FutureDate_ShouldRemainUnchanged()
    {
        // Regression anchor: future behavior must not change.
        var curve = new ZeroSpreadedDiscountCurve(s_curveName, MakeFlatBase(), spread: 0.005);
        DateOnly futureDate = s_valuationDate.AddYears(1);

        double result = curve.ZeroRate(futureDate, CompoundingConvention.Continuous);

        result.Should().BeGreaterThan(0d, "ZeroRate for future date must be positive");
        double.IsFinite(result).Should().BeTrue("ZeroRate for future date must be finite");
    }
}

/// <summary>
/// TDD tests for code-arena run 2026-06-13-2323 round-3 cluster C2 findings.
/// Findings covered:
///   F-L1-C2-R3-01  FlatForwardCurve.ValueAt past-date throws instead of returning 1
///   F-L2★-C2-01    CurveGroup.AsDictionary() leaks castable mutable Dictionary
/// </summary>
public sealed class ArenaC2R3FixTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 15);
    private static readonly DateOnly s_pastDate = new(2026, 4, 10);       // 5 days before valuation
    private const Currency Usd = Currency.USD;
    private static readonly CurveName s_curveName = new("USD-Test");
    private static readonly BenchmarkName s_benchmark = new("SOFR");

    private static FlatForwardCurve MakeFlatForwardCurve() =>
        new(s_curveName, s_valuationDate, Usd, s_benchmark, flatForwardRate: 0.04);

    private static FlatDiscountCurve MakeFlatDiscountCurve() =>
        new(s_curveName, s_valuationDate, Usd, 0.04);

    // ─────────────────────────────────────────────────────────────────
    // F-L1-C2-R3-01: FlatForwardCurve.ValueAt throws for date < ValuationDate
    // Correct behavior: return 1d (matching the sibling ForwardCurveFromDiscountCurves.ValueAt
    // and every IDiscountCurve.DiscountFactor implementation which short-circuits date <= ValuationDate → 1d).
    // Predicted RED: ArgumentOutOfRangeException —
    //   "endDate (2026-04-10) must be on or after startDate (2026-04-15). (Parameter 'endDate')"
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void FlatForwardCurve_ValueAt_PastDate_ShouldReturnOne()
    {
        // Predicted RED: ArgumentOutOfRangeException from DayCount.YearFraction(ValuationDate, pastDate)
        // because pastDate (2026-04-10) < ValuationDate (2026-04-15).
        var curve = MakeFlatForwardCurve();

        double result = curve.ValueAt(s_pastDate);

        result.Should().Be(1d,
            "ValueAt for any date <= ValuationDate must equal 1 (P(0)=1 convention), matching ForwardCurveFromDiscountCurves.ValueAt");
    }

    [Fact]
    public void FlatForwardCurve_ValueAt_AtValuationDate_ShouldReturnOne()
    {
        // Mutation-check anchor: if the past-date guard is removed, date < ValuationDate throws.
        // date == ValuationDate already works (YearFraction=0 → exp(0)=1), so this test remains
        // green either way — it's a regression anchor for the at-boundary case.
        var curve = MakeFlatForwardCurve();

        double result = curve.ValueAt(s_valuationDate);

        result.Should().Be(1d, "ValueAt at ValuationDate must equal 1 exactly");
    }

    [Fact]
    public void FlatForwardCurve_ValueAt_FutureDate_ShouldDecreaseExponentially()
    {
        // Regression anchor: future-date behavior must not change after adding the guard.
        var curve = MakeFlatForwardCurve();
        DateOnly futureDate = s_valuationDate.AddYears(1);

        double result = curve.ValueAt(futureDate);

        // exp(-0.04 * 1) ≈ 0.9608
        result.Should().BeApproximately(Math.Exp(-0.04 * 1.0), precision: 1e-10,
            "ValueAt for future dates must continue to return exp(-f*t)");
        result.Should().BeLessThan(1d, "ValueAt for future dates must be < 1 for positive rates");
    }

    // ─────────────────────────────────────────────────────────────────
    // F-L2★-C2-01: CurveGroup.AsDictionary() returns castable mutable Dictionary
    // Correct behavior: the returned IReadOnlyDictionary cannot be upcast and mutated
    //   to corrupt the group's internal state.
    // Predicted RED: the cast (Dictionary<CurveReference,ICurve>)group.AsDictionary() succeeds
    //   and after mutation group.AsDictionary().Count == 2, proving internal state is exposed.
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CurveGroup_AsDictionary_ShouldNotExposeInternalDictionaryViaCast()
    {
        // Predicted RED: the upcast to Dictionary<CurveReference, ICurve> succeeds and
        // after mutation group.AsDictionary().Count becomes 2 instead of 1,
        // proving the internal _curves dict is shared.
        var builder = new CurveGroupBuilder(new CurveGroupName("Test"), s_valuationDate);
        var ref1 = new CurveReference(CurveRole.Discount, Usd);
        var disc = MakeFlatDiscountCurve();
        builder.Add(ref1, disc);
        CurveGroup group = builder.Build();

        // Attempt to cast the returned IReadOnlyDictionary back to a mutable Dictionary.
        var returnedDict = group.AsDictionary();
        bool castSucceeded = returnedDict is Dictionary<CurveReference, ICurve>;

        // Either the cast must fail (concrete type is ReadOnlyDictionary or similar),
        // OR if the cast somehow succeeds, mutation must NOT affect the group.
        if (castSucceeded)
        {
            var mutableRef = (Dictionary<CurveReference, ICurve>)returnedDict;
            var ref2 = new CurveReference(CurveRole.Forward, Usd, s_benchmark);
            var curve2 = new FlatDiscountCurve(new CurveName("USD-SOFR"), s_valuationDate, Usd, 0.03);
            mutableRef[ref2] = curve2;

            // After mutation via the cast, the group must still show 1 curve — not 2.
            group.AsDictionary().Count.Should().Be(1,
                "mutating the upcast reference must not corrupt CurveGroup's internal state");
        }
        // If cast didn't succeed, the test passes trivially — immutability is already enforced.
    }

    [Fact]
    public void CurveGroup_AsDictionary_RuntimeType_ShouldBeImmutable()
    {
        // Stricter form: the concrete runtime type returned by AsDictionary() must NOT be
        // Dictionary<CurveReference,ICurve> (because any caller can upcast and mutate it).
        // This test goes RED directly on the current implementation and GREEN only after the fix.
        var builder = new CurveGroupBuilder(new CurveGroupName("Test"), s_valuationDate);
        var ref1 = new CurveReference(CurveRole.Discount, Usd);
        builder.Add(ref1, MakeFlatDiscountCurve());
        CurveGroup group = builder.Build();

        var returnedDict = group.AsDictionary();

        // The returned object must NOT be a mutable Dictionary<,> — any non-mutable wrapper
        // (ReadOnlyDictionary, FrozenDictionary, etc.) passes this assertion.
        returnedDict.Should().NotBeOfType<Dictionary<CurveReference, ICurve>>(
            "AsDictionary() must not expose the raw internal Dictionary — callers can upcast and mutate it");
    }
}
