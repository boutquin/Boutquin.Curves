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

// CF-C3-1 pinning tests: MonotoneConvexInterpolator must throw a clear ArgumentException
// (not ArgumentOutOfRangeException or an opaque kernel exception) when the node list is
// empty or when it contains a node whose date is at or before the valuation date.

using Boutquin.Curves.Abstractions.Curves;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Interpolation.Tests;

public sealed class MonotoneConvexInterpolatorDomainGuardTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly MonotoneConvexInterpolator s_interpolator = new();
    private static readonly IYearFractionCalculator s_dayCount = new Act365DayCount();

    /// <summary>
    /// Empty point list must throw ArgumentException with a domain-level message,
    /// NOT ArgumentOutOfRangeException from a raw array-bounds fault (CF-C3-1 / F-L2-01).
    /// </summary>
    [Fact]
    public void Interpolate_EmptyPoints_Throws_ArgumentException_WithDomainMessage()
    {
        var emptyPoints = Array.Empty<CurvePoint>();
        var targetDate = s_valuationDate.AddMonths(6);

        Action act = () => s_interpolator.Interpolate(s_valuationDate, targetDate, emptyPoints, s_dayCount);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*points*");
    }

    /// <summary>
    /// A node whose date equals the valuation date must throw ArgumentException
    /// with a message that identifies the offending node date — not the kernel-internal
    /// parameter 'xs' (CF-C3-1 / F-L1-01 probe 1b).
    /// </summary>
    [Fact]
    public void Interpolate_NodeAtValuationDate_Throws_ArgumentException_WithNodeDate()
    {
        // Node at valuation date: xs[0]=0, xs[1]=0 in the augmented array — strictly-ascending violation.
        var points = new CurvePoint[]
        {
            new(s_valuationDate, 0.9956) // Node date == valuation date
        };
        var targetDate = s_valuationDate.AddMonths(6);

        Action act = () => s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*node*")
            .Which.Message.Should().NotContain("xs must be strictly ascending",
                "the error must identify the offending node date, not the kernel-internal xs parameter");
    }

    /// <summary>
    /// A node whose date is strictly before the valuation date must throw ArgumentException
    /// with a domain-level message naming the offending node date (CF-C3-1 / F-L1-01 probe 1c).
    /// </summary>
    [Fact]
    public void Interpolate_NodeBeforeValuationDate_Throws_ArgumentException_WithNodeDate()
    {
        // Node 10 days before valuation date: xs[1] is negative — strictly-ascending violation.
        var points = new CurvePoint[]
        {
            new(s_valuationDate.AddDays(-10), 0.9956) // Node date < valuation date
        };
        var targetDate = s_valuationDate.AddMonths(6);

        Action act = () => s_interpolator.Interpolate(s_valuationDate, targetDate, points, s_dayCount);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*node*")
            .Which.Message.Should().NotContain("xs must be strictly ascending",
                "the error must identify the offending node date, not the kernel-internal xs parameter");
    }

    private sealed class Act365DayCount : IYearFractionCalculator
    {
        public string Code => "ACT/365F";
        public double YearFraction(DateOnly startDate, DateOnly endDate) =>
            (endDate.DayNumber - startDate.DayNumber) / 365.0;
    }
}
