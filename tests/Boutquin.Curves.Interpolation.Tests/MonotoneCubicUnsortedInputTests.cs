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

// CF-C3-3 pinning tests for MonotoneCubicInterpolator unsorted-input guard.
//
// The INodalCurveInterpolator contract requires "nodes ordered by increasing date".
// The MonotoneCubicInterpolator wrapper previously passed the unsorted year-fraction
// array directly to the Numerics kernel, which has no sort-order validation for the
// monotone-cubic case. This produced a silent 257-bps wrong result (critic-C3 PROBE 3)
// instead of the ArgumentException thrown by all four sibling kernels.
//
// Fix: add a sort-order guard in MonotoneCubicInterpolator.Interpolate before delegating.

using Boutquin.Curves.Abstractions.Curves;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Interpolation.Tests;

public sealed class MonotoneCubicUnsortedInputTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly MonotoneCubicInterpolator s_interpolator = new();
    private static readonly IYearFractionCalculator s_dayCount = new Act365DayCount();

    /// <summary>
    /// Reversed nodes (1Y node before 1M node) must throw ArgumentException, NOT silently
    /// return a wrong discount factor. Critic-C3 PROBE 3 showed 257 bps divergence.
    /// </summary>
    [Fact]
    public void Interpolate_UnsortedNodes_Throws_ArgumentException()
    {
        // Reversed: 1Y node listed before 1M node — violates "nodes ordered by increasing date".
        var points = new CurvePoint[]
        {
            new(s_valuationDate.AddYears(1),   0.9479), // ← should be after 1M
            new(s_valuationDate.AddMonths(1),  0.9956)  // ← should be before 1Y
        };

        var targetDate = s_valuationDate.AddMonths(6);

        Action act = () => s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*points*");
    }

    /// <summary>
    /// A partially-sorted list (third node before second) must also throw.
    /// </summary>
    [Fact]
    public void Interpolate_PartiallyUnsortedNodes_Throws_ArgumentException()
    {
        var points = new CurvePoint[]
        {
            new(s_valuationDate.AddMonths(1),  0.9956),
            new(s_valuationDate.AddYears(1),   0.9479), // out of order: 1Y before 6M
            new(s_valuationDate.AddMonths(6),  0.9737)
        };

        var targetDate = s_valuationDate.AddMonths(3);

        Action act = () => s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*points*");
    }

    private sealed class Act365DayCount : IYearFractionCalculator
    {
        public string Code => "ACT/365F";
        public double YearFraction(DateOnly startDate, DateOnly endDate) =>
            (endDate.DayNumber - startDate.DayNumber) / 365.0;
    }
}
