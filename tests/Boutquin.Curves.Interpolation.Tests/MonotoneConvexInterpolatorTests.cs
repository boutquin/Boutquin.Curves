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

public sealed class MonotoneConvexInterpolatorTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly MonotoneConvexInterpolator s_interpolator = new();
    private static readonly IYearFractionCalculator s_dayCount = new Act365DayCount();

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

    [Fact]
    public void Name_Returns_MonotoneConvexInterpolator()
    {
        s_interpolator.Name.Should().Be(nameof(MonotoneConvexInterpolator));
    }

    [Fact]
    public void Interpolate_AtValuationDate_Returns_One()
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
        df.Should().BeApproximately(points[3].Value, 1e-10);
    }

    [Fact]
    public void Interpolate_BetweenNodes_Returns_PositiveDiscountFactor_InRange()
    {
        var points = CreateStandardNodes();
        var midDate = s_valuationDate.AddMonths(18);
        double df = s_interpolator.Interpolate(s_valuationDate, midDate, points, s_dayCount);
        df.Should().BeGreaterThan(0d);
        df.Should().BeLessThan(1d);
    }

    [Fact]
    public void Interpolate_MonotonicallyDecreasing_DiscountFactors()
    {
        var points = CreateStandardNodes();
        double prev = 1d;
        for (int months = 1; months <= 120; months++)
        {
            double df = s_interpolator.Interpolate(s_valuationDate, s_valuationDate.AddMonths(months), points, s_dayCount);
            df.Should().BeLessThanOrEqualTo(prev,
                $"DF at month {months} ({df:F8}) must not exceed previous ({prev:F8})");
            prev = df;
        }
    }

    [Fact]
    public void Interpolate_AllValuesPositive_AcrossFullRange()
    {
        var points = CreateStandardNodes();
        for (int days = 1; days <= 365 * 30; days += 30)
        {
            double df = s_interpolator.Interpolate(s_valuationDate, s_valuationDate.AddDays(days), points, s_dayCount);
            df.Should().BeGreaterThan(0d,
                $"DF at day {days} should remain strictly positive");
        }
    }

    // The defining property of the Hagan-West monotone-convex method:
    // instantaneous forward rates f(t) = -d(ln P)/dt must be non-negative
    // everywhere for a normal upward- or flat-rate environment.
    // We approximate f(t) ≈ [ln P(t) - ln P(t+ε)] / ε with ε = 1 day.
    [Fact]
    public void Interpolate_InstantaneousForwardRates_NonNegative_BetweenAllNodes()
    {
        var points = CreateStandardNodes();
        double epsilon = 1.0 / 365.0; // 1-day approximation

        for (int months = 1; months <= 359; months++)
        {
            var date = s_valuationDate.AddMonths(months);
            var dateNext = date.AddDays(1);

            double df = s_interpolator.Interpolate(s_valuationDate, date, points, s_dayCount);
            double dfNext = s_interpolator.Interpolate(s_valuationDate, dateNext, points, s_dayCount);

            // f ≈ (ln P(t) - ln P(t+ε)) / ε
            double forwardRate = (Math.Log(df) - Math.Log(dfNext)) / epsilon;
            forwardRate.Should().BeGreaterThanOrEqualTo(-1e-8, // tolerance for numerical noise
                $"Forward rate at month {months} should be non-negative, got {forwardRate:F8}");
        }
    }

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

    [Fact]
    public void Factory_Creates_MonotoneConvexInterpolator()
    {
        var interpolator = InterpolatorFactory.Create(InterpolatorKind.MonotoneConvex);
        interpolator.Should().BeOfType<MonotoneConvexInterpolator>();
    }

    [Fact]
    public void Interpolate_ComparativeTest_AllFiveInterpolators_ProducePositiveDFs()
    {
        var points = CreateStandardNodes();
        INodalCurveInterpolator[] interpolators =
        [
            new LogLinearDiscountFactorInterpolator(),
            new LinearZeroRateInterpolator(),
            new FlatForwardInterpolator(),
            new MonotoneCubicInterpolator(),
            new MonotoneConvexInterpolator()
        ];

        var midDate = s_valuationDate.AddYears(3);

        foreach (var interp in interpolators)
        {
            double df = interp.Interpolate(s_valuationDate, midDate, points, s_dayCount);
            df.Should().BeGreaterThan(0d).And.BeLessThan(1d,
                $"{interp.Name} should produce valid DF at 3Y");
        }
    }

    private sealed class Act365DayCount : IYearFractionCalculator
    {
        public string Code => "ACT/365F";
        public double YearFraction(DateOnly startDate, DateOnly endDate) =>
            (endDate.DayNumber - startDate.DayNumber) / 365.0;
    }
}
