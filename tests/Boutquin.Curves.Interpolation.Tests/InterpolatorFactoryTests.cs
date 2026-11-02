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
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Interpolation.Tests;

public sealed class InterpolatorFactoryTests
{
    [Fact]
    public void Create_ShouldReturnInterpolator()
    {
        var interpolator = InterpolatorFactory.Create(InterpolatorKind.LogLinearDiscountFactor);

        interpolator.Should().NotBeNull();
    }

    [Theory]
    [InlineData(InterpolatorKind.LogLinearDiscountFactor, typeof(LogLinearDiscountFactorInterpolator))]
    [InlineData(InterpolatorKind.LinearZeroRate, typeof(LinearZeroRateInterpolator))]
    [InlineData(InterpolatorKind.FlatForward, typeof(FlatForwardInterpolator))]
    [InlineData(InterpolatorKind.MonotoneCubic, typeof(MonotoneCubicInterpolator))]
    [InlineData(InterpolatorKind.MonotoneConvex, typeof(MonotoneConvexInterpolator))]
    public void Create_AllDefinedKinds_SelectTheirConcreteImplementation(InterpolatorKind kind, Type expected)
    {
        InterpolatorFactory.Create(kind).GetType().Should().Be(expected);
    }

    /// <summary>
    /// B-4 (round-1 F-L4-02/04): the factory's <c>NotSupportedException</c> default arm — reached
    /// when <see cref="InterpolatorKind"/> carries a value outside the five mapped cases — had no
    /// test. Pins both the exception type and that the message names the offending value, matching
    /// the documented <c>&lt;exception&gt;</c> contract on <see cref="InterpolatorFactory.Create"/>.
    /// </summary>
    [Fact]
    public void Create_UnmappedInterpolatorKind_ThrowsNotSupportedException()
    {
        const InterpolatorKind unmapped = (InterpolatorKind)999;

        Action act = () => InterpolatorFactory.Create(unmapped);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*999*");
    }

    // B-4 (round-1 F-L4-02/04, V-C4): the monotone-decreasing-discount-factor law for
    // LogLinearDiscountFactor and LinearZeroRate had no dedicated coverage — every existing test
    // checks individual boundary/interior values, not the curve-wide monotonicity invariant a
    // production discount curve must satisfy (DF strictly decreases as date increases, since the
    // underlying zero/forward rates are positive). A regression that broke monotonicity between two
    // interpolated (non-node) dates would not be caught by any existing test.

    [Theory]
    [InlineData(InterpolatorKind.LogLinearDiscountFactor)]
    [InlineData(InterpolatorKind.LinearZeroRate)]
    public void Interpolate_DenseSampledDates_DiscountFactorsAreMonotoneDecreasing(InterpolatorKind kind)
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurvePoint[] points =
        [
            new(valuationDate.AddMonths(1), 0.9956),
            new(valuationDate.AddMonths(3), 0.9868),
            new(valuationDate.AddMonths(6), 0.9737),
            new(valuationDate.AddYears(1), 0.9479),
            new(valuationDate.AddYears(2), 0.8979),
            new(valuationDate.AddYears(5), 0.7788),
            new(valuationDate.AddYears(10), 0.6065),
            new(valuationDate.AddYears(30), 0.2231)
        ];

        var interpolator = InterpolatorFactory.Create(kind);
        var dayCount = new Act365DayCount();

        // Sample only the INTERIOR region (first node date through last node date). Before the
        // first node both interpolators extrapolate flat (constant DF / constant zero rate), so
        // strict monotonicity does not — and should not — hold there; that flat-extrapolation
        // behavior is already pinned by each interpolator's own boundary tests. The law under
        // test here is the interior (and node-to-node) monotone-decreasing-DF invariant.
        int firstNodeDay = points[0].Date.DayNumber - valuationDate.DayNumber;
        int lastNodeDay = points[^1].Date.DayNumber - valuationDate.DayNumber;

        double previousDf = interpolator.Interpolate(valuationDate, points[0].Date, points, dayCount);
        for (int day = firstNodeDay + 7; day <= lastNodeDay; day += 7)
        {
            DateOnly targetDate = valuationDate.AddDays(day);
            double df = interpolator.Interpolate(valuationDate, targetDate, points, dayCount);

            df.Should().BeLessThan(previousDf,
                because: "{0} must produce strictly decreasing discount factors as the target date " +
                    "advances through the interior (day {1}); a non-decreasing step is an " +
                    "arbitrage-violating curve", kind, day);

            previousDf = df;
        }
    }

    private sealed class Act365DayCount : IYearFractionCalculator
    {
        public string Code => "ACT/365F";
        public double YearFraction(DateOnly startDate, DateOnly endDate) =>
            (endDate.DayNumber - startDate.DayNumber) / 365.0;
    }
}
