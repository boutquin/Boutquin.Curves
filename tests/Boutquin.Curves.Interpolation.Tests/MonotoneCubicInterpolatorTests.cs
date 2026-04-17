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

public sealed class MonotoneCubicInterpolatorTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly MonotoneCubicInterpolator s_interpolator = new();
    private static readonly IYearFractionCalculator s_dayCount = new Act360DayCount();

    /// <summary>
    /// Standard downward-sloping discount factor nodes for testing.
    /// </summary>
    private static IReadOnlyList<CurvePoint> CreateStandardNodes()
    {
        return new CurvePoint[]
        {
            new(s_valuationDate.AddMonths(1), 0.9956),
            new(s_valuationDate.AddMonths(3), 0.9868),
            new(s_valuationDate.AddMonths(6), 0.9737),
            new(s_valuationDate.AddYears(1), 0.9479),
            new(s_valuationDate.AddYears(2), 0.8979),
            new(s_valuationDate.AddYears(5), 0.7788),
            new(s_valuationDate.AddYears(10), 0.6065),
            new(s_valuationDate.AddYears(30), 0.2231)
        };
    }

    [Fact]
    public void Name_ShouldReturn_MonotoneCubicInterpolator()
    {
        s_interpolator.Name.Should().Be(nameof(MonotoneCubicInterpolator));
    }

    [Fact]
    public void Interpolate_AtValuationDate_Returns_1()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, s_valuationDate, points, s_dayCount);
        df.Should().Be(1d);
    }

    [Fact]
    public void Interpolate_BeforeFirstNode_Returns_FirstNodeValue()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, s_valuationDate.AddDays(5), points, s_dayCount);
        df.Should().Be(points[0].Value);
    }

    [Fact]
    public void Interpolate_AfterLastNode_Returns_LastNodeValue()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, s_valuationDate.AddYears(35), points, s_dayCount);
        df.Should().Be(points[^1].Value);
    }

    [Fact]
    public void Interpolate_AtNodeDate_Returns_NodeValue()
    {
        var points = CreateStandardNodes();
        double df = s_interpolator.Interpolate(s_valuationDate, points[3].Date, points, s_dayCount);
        df.Should().BeApproximately(points[3].Value, 1e-12);
    }

    [Fact]
    public void Interpolate_BetweenNodes_Returns_Positive_DiscountFactor()
    {
        var points = CreateStandardNodes();
        var midDate = s_valuationDate.AddMonths(18);
        double df = s_interpolator.Interpolate(s_valuationDate, midDate, points, s_dayCount);
        df.Should().BeGreaterThan(0d);
        df.Should().BeLessThan(1d);
    }

    [Fact]
    public void Interpolate_PreservesMonotonicity_DiscountFactors_Decrease()
    {
        // Monotone cubic must produce monotonically decreasing DFs for a downward-sloping curve
        var points = CreateStandardNodes();
        double previousDf = 1d;

        for (int months = 1; months <= 120; months++)
        {
            var date = s_valuationDate.AddMonths(months);
            double df = s_interpolator.Interpolate(s_valuationDate, date, points, s_dayCount);
            df.Should().BeLessThanOrEqualTo(previousDf,
                $"DF at month {months} ({df:F8}) should not exceed DF at month {months - 1} ({previousDf:F8})");
            previousDf = df;
        }
    }

    [Fact]
    public void Interpolate_NoBelowZeroOscillation()
    {
        // Even with widely spaced nodes, no negative DFs
        var points = CreateStandardNodes();
        for (int days = 1; days <= 365 * 30; days += 30)
        {
            var date = s_valuationDate.AddDays(days);
            double df = s_interpolator.Interpolate(s_valuationDate, date, points, s_dayCount);
            df.Should().BeGreaterThan(0d,
                $"DF at day {days} should remain positive");
        }
    }

    [Fact]
    public void Interpolate_TwoNodes_Linear_Fallback()
    {
        // With only two nodes, cubic cannot be formed — should still produce reasonable results
        var points = new CurvePoint[]
        {
            new(s_valuationDate.AddMonths(1), 0.9956),
            new(s_valuationDate.AddYears(1), 0.9479)
        };

        var midDate = s_valuationDate.AddMonths(6);
        double df = s_interpolator.Interpolate(s_valuationDate, midDate, points, s_dayCount);
        df.Should().BeGreaterThan(points[1].Value);
        df.Should().BeLessThan(points[0].Value);
    }

    [Fact]
    public void Factory_Creates_MonotoneCubicInterpolator()
    {
        var interpolator = InterpolatorFactory.Create(InterpolatorKind.MonotoneCubic);
        interpolator.Should().BeOfType<MonotoneCubicInterpolator>();
    }

    [Fact]
    public void Interpolate_ComparativeTest_AllThreeInterpolators_SameNodes()
    {
        // All three interpolators should produce DFs between 0 and 1 for the same input
        var points = CreateStandardNodes();
        var logLinear = new LogLinearDiscountFactorInterpolator();
        var linearZero = new LinearZeroRateInterpolator();
        var monotoneCubic = new MonotoneCubicInterpolator();

        var midDate = s_valuationDate.AddYears(3);

        double dfLogLinear = logLinear.Interpolate(s_valuationDate, midDate, points, s_dayCount);
        double dfLinearZero = linearZero.Interpolate(s_valuationDate, midDate, points, s_dayCount);
        double dfMonotoneCubic = monotoneCubic.Interpolate(s_valuationDate, midDate, points, s_dayCount);

        dfLogLinear.Should().BeGreaterThan(0d).And.BeLessThan(1d);
        dfLinearZero.Should().BeGreaterThan(0d).And.BeLessThan(1d);
        dfMonotoneCubic.Should().BeGreaterThan(0d).And.BeLessThan(1d);

        // All should be in a reasonable neighborhood (within 50 bps of DF)
        Math.Abs(dfLogLinear - dfMonotoneCubic).Should().BeLessThan(0.01);
        Math.Abs(dfLinearZero - dfMonotoneCubic).Should().BeLessThan(0.01);
    }

    /// <summary>
    /// Minimal day-count implementation for interpolator tests.
    /// </summary>
    private sealed class Act360DayCount : IYearFractionCalculator
    {
        public string Code => "ACT/360";

        public double YearFraction(DateOnly startDate, DateOnly endDate)
        {
            return (endDate.DayNumber - startDate.DayNumber) / 360.0;
        }
    }
}
