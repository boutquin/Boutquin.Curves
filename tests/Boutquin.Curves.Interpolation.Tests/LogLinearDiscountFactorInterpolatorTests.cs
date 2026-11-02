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

// CF-C3-3 test coverage for LogLinearDiscountFactorInterpolator (F-L4-01).
//
// Previously only incidental coverage existed: a comparative multi-interpolator test
// at an interior date and a cross-validation against FlatForward. These tests add
// dedicated boundary-condition, algebraic-invariant, at-node round-trip, two-node,
// and before/after-node coverage.

using Boutquin.Curves.Abstractions.Curves;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Interpolation.Tests;

public sealed class LogLinearDiscountFactorInterpolatorTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly LogLinearDiscountFactorInterpolator s_interpolator = new();
    private static readonly IYearFractionCalculator s_dayCount = new Act365DayCount();

    // Standard nodes identical to the sibling test fixtures.
    private static IReadOnlyList<CurvePoint> CreateStandardNodes() =>
        new CurvePoint[]
        {
            new(s_valuationDate.AddMonths(1),  0.9956),
            new(s_valuationDate.AddMonths(3),  0.9868),
            new(s_valuationDate.AddMonths(6),  0.9737),
            new(s_valuationDate.AddYears(1),   0.9479),
            new(s_valuationDate.AddYears(2),   0.8979),
            new(s_valuationDate.AddYears(5),   0.7788),
            new(s_valuationDate.AddYears(10),  0.6065),
            new(s_valuationDate.AddYears(30),  0.2231)
        };

    // ── Name ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Name_Returns_LogLinearDiscountFactorInterpolator()
    {
        s_interpolator.Name.Should().Be(nameof(LogLinearDiscountFactorInterpolator));
    }

    // ── At valuation date ─────────────────────────────────────────────────────

    [Fact]
    public void Interpolate_AtValuationDate_Returns_One()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, s_valuationDate, points, s_dayCount);
        df.Should().Be(1d);
    }

    // ── Before-first-node (flat-DF extrapolation) ─────────────────────────────
    //
    // LogLinearDiscountFactor delegates to the Numerics LogLinearInterpolator, which returns
    // the first node value for targets before the first xs. This is the "flat-DF" rule shared
    // by all four non-LinearZeroRate siblings.

    [Fact]
    public void Interpolate_BeforeFirstNode_Returns_FirstNodeValue()
    {
        var points = CreateStandardNodes();
        var targetDate = s_valuationDate.AddDays(5);  // 5 days before 1M node
        double df = s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);
        df.Should().Be(points[0].Value,
            "LogLinear uses flat-DF extrapolation before the first node (returns points[0].Value)");
    }

    [Fact]
    public void Interpolate_JustBeforeFirstNode_Returns_FirstNodeValue_Exactly()
    {
        var points = new CurvePoint[] { new(s_valuationDate.AddMonths(1), 0.9956) };
        var targetDate = s_valuationDate.AddDays(1);
        double df = s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);
        df.Should().Be(points[0].Value);
    }

    // ── After-last-node (flat-DF extrapolation) ───────────────────────────────

    [Fact]
    public void Interpolate_AfterLastNode_Returns_LastNodeValue()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, s_valuationDate.AddYears(35), points, s_dayCount);
        df.Should().Be(points[^1].Value);
    }

    // ── At-node round-trip ────────────────────────────────────────────────────

    [Fact]
    public void Interpolate_AtFirstNodeDate_Returns_FirstNodeValue()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, points[0].Date, points, s_dayCount);
        df.Should().BeApproximately(points[0].Value, 1e-12);
    }

    [Fact]
    public void Interpolate_AtInteriorNodeDate_Returns_NodeValue()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, points[4].Date, points, s_dayCount);
        df.Should().BeApproximately(points[4].Value, 1e-12);
    }

    // ── Interior: log-linear algebraic invariant ──────────────────────────────
    //
    // For t in [t₀, t₁]: P(t) = P₀^(1−w) · P₁^w  where w = (t−t₀)/(t₁−t₀).
    // Equivalently: ln P(t) = (1−w)·ln P₀ + w·ln P₁.

    [Fact]
    public void Interpolate_BetweenTwoNodes_Satisfies_LogLinearFormula()
    {
        var t0Date = s_valuationDate.AddYears(1);
        var t1Date = s_valuationDate.AddYears(2);
        double p0 = 0.9479;
        double p1 = 0.8979;

        var points = new CurvePoint[] { new(t0Date, p0), new(t1Date, p1) };
        var targetDate = s_valuationDate.AddMonths(18);

        double t0 = s_dayCount.YearFraction(s_valuationDate, t0Date);
        double t1 = s_dayCount.YearFraction(s_valuationDate, t1Date);
        double t = s_dayCount.YearFraction(s_valuationDate, targetDate);
        double w = (t - t0) / (t1 - t0);
        double expected = Math.Pow(p0, 1d - w) * Math.Pow(p1, w);

        double actual = s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);

        actual.Should().BeApproximately(expected, 1e-12,
            "log-linear interpolation must satisfy the geometric-weighting formula to machine precision");
    }

    // ── Interior: positive discount factor ────────────────────────────────────

    [Fact]
    public void Interpolate_Interior_Returns_PositiveDF_InRange()
    {
        var points = CreateStandardNodes();
        var midDate = s_valuationDate.AddMonths(18);
        double df = s_interpolator.Interpolate(s_valuationDate, midDate, points, s_dayCount);
        df.Should().BeGreaterThan(0d);
        df.Should().BeLessThan(1d);
    }

    // ── Two-node case ─────────────────────────────────────────────────────────

    [Fact]
    public void Interpolate_TwoNodes_ProducesReasonableResult()
    {
        var points = new CurvePoint[]
        {
            new(s_valuationDate.AddMonths(1), 0.9956),
            new(s_valuationDate.AddYears(1),  0.9479)
        };

        var midDate = s_valuationDate.AddMonths(6);
        double df = s_interpolator.Interpolate(s_valuationDate, midDate, points, s_dayCount);
        df.Should().BeGreaterThan(points[1].Value);
        df.Should().BeLessThan(points[0].Value);
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    [Fact]
    public void Factory_Creates_LogLinearDiscountFactorInterpolator()
    {
        var interpolator = InterpolatorFactory.Create(InterpolatorKind.LogLinearDiscountFactor);
        interpolator.Should().BeOfType<LogLinearDiscountFactorInterpolator>();
    }

    private sealed class Act365DayCount : IYearFractionCalculator
    {
        public string Code => "ACT/365F";
        public double YearFraction(DateOnly startDate, DateOnly endDate) =>
            (endDate.DayNumber - startDate.DayNumber) / 365.0;
    }
}
