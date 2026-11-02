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

// CF-C3-2 / CF-C3-3 pinning tests for LinearZeroRateInterpolator.
//
// KEY DOCUMENTED DIVERGENCE (INodalCurveInterpolator contract, CF-C3-2):
// Before the first node, LinearZeroRateInterpolator extrapolates by holding the
// first-node zero rate z₀ = -ln(P₀)/t₀ constant, returning exp(-z₀·t).
// All four sibling implementations (FlatForward, LogLinear, MonotoneCubic, MonotoneConvex)
// return points[0].Value (flat-DF) instead.  The divergence is intentional and is
// documented on INodalCurveInterpolator.Interpolate and on this class's own remarks.
// This test file pins both the divergence and the full boundary-condition coverage.

using Boutquin.Curves.Abstractions.Curves;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Interpolation.Tests;

public sealed class LinearZeroRateInterpolatorTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly LinearZeroRateInterpolator s_interpolator = new();
    private static readonly IYearFractionCalculator s_dayCount = new Act365DayCount();

    // Standard nodes identical to the sibling test fixtures for cross-interpolator comparisons.
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
    public void Name_Returns_LinearZeroRateInterpolator()
    {
        s_interpolator.Name.Should().Be(nameof(LinearZeroRateInterpolator));
    }

    // ── At valuation date ─────────────────────────────────────────────────────

    [Fact]
    public void Interpolate_AtValuationDate_Returns_One()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, s_valuationDate, points, s_dayCount);
        df.Should().Be(1d);
    }

    // ── Before-first-node: flat-zero-rate extrapolation (the documented divergence) ──
    //
    // LinearZeroRateInterpolator holds z₀ = -ln(P₀)/t₀ constant for t < t₀ and
    // returns exp(-z₀·t).  All siblings return points[0].Value instead.
    // Probe (critic-C3 PROBE 2): divergence = 36.89 bps for target 5 days before 1M node.

    [Fact]
    public void Interpolate_BeforeFirstNode_Returns_FlatZeroRateDF_NotFlatDF()
    {
        // Node: 1M → DF=0.9956.  Target: 5 days before the 1M node.
        var points = new CurvePoint[] { new(s_valuationDate.AddMonths(1), 0.9956) };
        var targetDate = s_valuationDate.AddDays(5);  // 5 days before the 1M node

        double t0 = s_dayCount.YearFraction(s_valuationDate, points[0].Date);
        double t = s_dayCount.YearFraction(s_valuationDate, targetDate);
        double z0 = -Math.Log(points[0].Value) / t0;
        double expectedFlatZeroRateDF = Math.Exp(-z0 * t);

        double actual = s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);

        // Must match flat-zero-rate formula to machine precision.
        actual.Should().BeApproximately(expectedFlatZeroRateDF, 1e-12,
            "LinearZeroRate holds the first-node zero rate constant before the first node");

        // Must NOT equal the first-node DF (which siblings return) — probe proved 36.89 bps gap.
        actual.Should().NotBeApproximately(points[0].Value, 1e-4,
            "LinearZeroRate diverges from siblings (flat-DF) by ~37 bps; if this fails the behavior has been aligned");
    }

    /// <summary>
    /// Documents and pins the before-first-node sibling divergence.
    /// For the probe scenario (target 5 days before 1M node with first-node DF=0.9956):
    ///   LinearZeroRate returns exp(-z₀·t) ≈ 0.99928901
    ///   Siblings return 0.99560000 (flat-DF)
    ///   Divergence ≈ 36.9 bps
    /// </summary>
    [Fact]
    public void Interpolate_BeforeFirstNode_DivergesFromFlatDF_Siblings_By_ApproximatelyProbeValue()
    {
        // Reproduce the critic-C3 PROBE 2 scenario exactly.
        // First node at 2026-05-10 (≈ 31 days from 2026-04-09), DF=0.9956.
        // Target: 2026-04-14 (5 days before first node).
        var firstNodeDate = new DateOnly(2026, 5, 10);   // s_valuationDate + 31 days ≈ AddMonths(1)
        var targetDate = new DateOnly(2026, 4, 14);

        var points = new CurvePoint[] { new(firstNodeDate, 0.9956) };

        double actual = s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);

        // The probe reported 0.99928901 for LinearZeroRate.
        actual.Should().BeApproximately(0.99928901, 1e-5,
            "matches critic-C3 PROBE 2 reported value for LinearZeroRate");

        // Divergence from sibling flat-DF (0.9956) must be ~36.9 bps.
        double divergence = Math.Abs(actual - points[0].Value);
        divergence.Should().BeGreaterThan(0.003,
            "divergence from sibling flat-DF rule must be at least 30 bps (probe: 36.89 bps)");
    }

    // ── After-last-node ───────────────────────────────────────────────────────

    [Fact]
    public void Interpolate_AfterLastNode_Returns_LastNodeValue()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, s_valuationDate.AddYears(35), points, s_dayCount);
        df.Should().Be(points[^1].Value);
    }

    [Fact]
    public void Interpolate_AtLastNodeDate_Returns_LastNodeValue()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, points[^1].Date, points, s_dayCount);
        df.Should().Be(points[^1].Value,
            "right-edge shortcut: when targetDate >= last node, return last node DF directly");
    }

    // ── At-node round-trip ────────────────────────────────────────────────────

    [Fact]
    public void Interpolate_AtFirstNodeDate_Returns_FirstNodeValue()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, points[0].Date, points, s_dayCount);
        // Linear interpolation at an exact node must round-trip to within machine precision.
        df.Should().BeApproximately(points[0].Value, 1e-12);
    }

    [Fact]
    public void Interpolate_AtInteriorNodeDate_Returns_NodeValue()
    {
        var points = CreateStandardNodes();
        // Node index 3 is the 1Y node (0.9479).
        double df = s_interpolator.Interpolate(s_valuationDate, points[3].Date, points, s_dayCount);
        df.Should().BeApproximately(points[3].Value, 1e-12,
            "interpolating at a pillar date must round-trip to that pillar's DF");
    }

    // ── Interior interpolation ────────────────────────────────────────────────

    [Fact]
    public void Interpolate_Interior_Returns_PositiveDF_InRange()
    {
        var points = CreateStandardNodes();
        var midDate = s_valuationDate.AddMonths(18);
        double df = s_interpolator.Interpolate(s_valuationDate, midDate, points, s_dayCount);
        df.Should().BeGreaterThan(0d);
        df.Should().BeLessThan(1d);
    }

    // ── Zero-rate interpolation algebraic invariant ───────────────────────────
    //
    // For target between nodes i and i+1:
    //   z(t) = zᵢ + (zᵢ₊₁ − zᵢ) · (t − tᵢ) / (tᵢ₊₁ − tᵢ)
    //   P(t) = exp(−z(t)·t)

    [Fact]
    public void Interpolate_BetweenTwoNodes_Satisfies_LinearZeroRateFormula()
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
        double z0 = -Math.Log(p0) / t0;
        double z1 = -Math.Log(p1) / t1;
        double z = z0 + (z1 - z0) * (t - t0) / (t1 - t0);
        double expected = Math.Exp(-z * t);

        double actual = s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);

        actual.Should().BeApproximately(expected, 1e-12,
            "LinearZeroRate interior must satisfy linear-zero-rate formula to machine precision");
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
    public void Factory_Creates_LinearZeroRateInterpolator()
    {
        var interpolator = InterpolatorFactory.Create(InterpolatorKind.LinearZeroRate);
        interpolator.Should().BeOfType<LinearZeroRateInterpolator>();
    }

    private sealed class Act365DayCount : IYearFractionCalculator
    {
        public string Code => "ACT/365F";
        public double YearFraction(DateOnly startDate, DateOnly endDate) =>
            (endDate.DayNumber - startDate.DayNumber) / 365.0;
    }
}
