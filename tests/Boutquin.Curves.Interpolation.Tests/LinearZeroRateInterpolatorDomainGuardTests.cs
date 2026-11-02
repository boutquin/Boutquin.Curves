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

// Domain-guard pinning tests for LinearZeroRateInterpolator (arena round-2, cluster C3):
//
//   F-L1-C3-01 (DEEP-BUG-06): a node coincident with (or before) the valuation date drove
//   xs[i]=0 → Math.Max(1e-12, 0) floor → z_i ≈ 1e10 → exp(-z·x) underflowed silently to
//   DF=0 across the ENTIRE interior. The fix rejects such a node with a clear domain
//   ArgumentException naming the offending node date, instead of masking the singularity.
//
//   F-L2-C3-R2-01 (NET-API-03): empty points dereferenced points[^1] and leaked an opaque
//   System.ArgumentOutOfRangeException (param 'index', no domain message). The fix adds an
//   empty-points guard matching the MonotoneConvex sibling style.
//
// Both must NOT regress the DOCUMENTED before-first-node divergence (LinearZeroRate
// intentionally returns exp(-z₀·t) before the first node) — pinned by the final test.

using Boutquin.Curves.Abstractions.Curves;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Interpolation.Tests;

public sealed class LinearZeroRateInterpolatorDomainGuardTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly LinearZeroRateInterpolator s_interpolator = new();
    private static readonly IYearFractionCalculator s_dayCount = new Act365DayCount();

    // ── F-L1-C3-01 (DEEP-BUG-06): node-at-valuation singularity ──────────────────
    //
    // Characterization of the defect: with a node coincident with the valuation date
    // carrying DF != 1, the interpolator must NOT silently return DF=0 across the interior.
    // Post-fix it rejects the coincident node with a clear domain ArgumentException.

    /// <summary>
    /// A node coincident with the valuation date (DF != 1) must throw a clear domain
    /// ArgumentException naming the offending node date — NOT silently return DF=0 across
    /// the interior, and NOT leak the kernel-internal 'xs must be strictly ascending' message.
    /// </summary>
    [Fact]
    public void Interpolate_NodeAtValuationDate_Throws_ArgumentException_WithNodeDate()
    {
        // Probe reproduction: points = [(val, 0.99), (val+1Y, 0.96)]; interior query underflowed to 0.
        var points = new CurvePoint[]
        {
            new(s_valuationDate, 0.99),              // node date == valuation date, DF != 1
            new(s_valuationDate.AddYears(1), 0.96)
        };
        var targetDate = s_valuationDate.AddMonths(6);

        Action act = () => s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*valuation date*")
            .Which.Message.Should().NotContain("xs must be strictly ascending",
                "the error must identify the offending node date, not the kernel-internal xs parameter");
    }

    /// <summary>
    /// A node strictly before the valuation date must also throw a clear domain
    /// ArgumentException (xs[i] negative → same floor-masking singularity class).
    /// </summary>
    [Fact]
    public void Interpolate_NodeBeforeValuationDate_Throws_ArgumentException_WithNodeDate()
    {
        var points = new CurvePoint[]
        {
            new(s_valuationDate.AddDays(-10), 0.99),
            new(s_valuationDate.AddYears(1), 0.96)
        };
        var targetDate = s_valuationDate.AddMonths(6);

        Action act = () => s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*valuation date*");
    }

    // ── F-L2-C3-R2-01 (NET-API-03): empty-points guard ──────────────────────────

    /// <summary>
    /// Empty point list must throw ArgumentException with a domain-level message naming
    /// 'points', NOT a bare System.ArgumentOutOfRangeException from the points[^1] deref —
    /// matching the MonotoneConvex/MonotoneCubic sibling contract.
    /// </summary>
    [Fact]
    public void Interpolate_EmptyPoints_Throws_ArgumentException_WithDomainMessage()
    {
        var emptyPoints = Array.Empty<CurvePoint>();
        var targetDate = s_valuationDate.AddMonths(6);

        Action act = () => s_interpolator.Interpolate(s_valuationDate, targetDate, emptyPoints, s_dayCount);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("node",
                "empty input must surface a clear domain message, not an opaque BCL index error");
    }

    [Fact]
    public void Interpolate_EmptyPoints_DoesNotThrow_ArgumentOutOfRangeException()
    {
        var emptyPoints = Array.Empty<CurvePoint>();
        var targetDate = s_valuationDate.AddMonths(6);

        Action act = () => s_interpolator.Interpolate(s_valuationDate, targetDate, emptyPoints, s_dayCount);

        // ArgumentOutOfRangeException derives from ArgumentException; assert the concrete
        // opaque BCL type is no longer what leaks out.
        act.Should().Throw<ArgumentException>()
            .And.Should().NotBeOfType<ArgumentOutOfRangeException>(
                "the points[^1] deref must be guarded before it can throw the opaque BCL index error");
    }

    // ── Regression guard: documented before-first-node divergence must SURVIVE ───
    //
    // Over-correction risk: a node-date guard that rejects nodes <= valuationDate must NOT
    // touch the legitimate before-first-node extrapolation rule. For a node strictly AFTER
    // the valuation date, querying BEFORE that node must still return exp(-z₀·t) (flat-zero-
    // rate), NOT the flat-DF points[0].Value the siblings return, and NOT a throw.

    [Fact]
    public void Interpolate_BeforeFirstNode_StillReturns_FlatZeroRateDF_NotThrow()
    {
        // Single node strictly after valuation date (valid input — must NOT be rejected).
        var points = new CurvePoint[] { new(s_valuationDate.AddMonths(1), 0.9956) };
        var targetDate = s_valuationDate.AddDays(5);  // before the first node

        double t0 = s_dayCount.YearFraction(s_valuationDate, points[0].Date);
        double t = s_dayCount.YearFraction(s_valuationDate, targetDate);
        double z0 = -Math.Log(points[0].Value) / t0;
        double expectedFlatZeroRateDF = Math.Exp(-z0 * t);

        double actual = s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);

        actual.Should().BeApproximately(expectedFlatZeroRateDF, 1e-12,
            "the node-date guard must not regress the documented flat-zero-rate before-first-node rule");
        actual.Should().NotBeApproximately(points[0].Value, 1e-4,
            "the documented sibling divergence (~37 bps) must survive the guard");
    }

    private sealed class Act365DayCount : IYearFractionCalculator
    {
        public string Code => "ACT/365F";
        public double YearFraction(DateOnly startDate, DateOnly endDate) =>
            (endDate.DayNumber - startDate.DayNumber) / 365.0;
    }
}
