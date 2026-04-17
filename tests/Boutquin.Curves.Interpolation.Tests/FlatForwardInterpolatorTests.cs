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

public sealed class FlatForwardInterpolatorTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly FlatForwardInterpolator s_interpolator = new();
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
    public void Name_Returns_FlatForwardInterpolator()
    {
        s_interpolator.Name.Should().Be(nameof(FlatForwardInterpolator));
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
        double df = s_interpolator.Interpolate(s_valuationDate, points[4].Date, points, s_dayCount);
        df.Should().BeApproximately(points[4].Value, 1e-12);
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
                $"DF at month {months} ({df:F8}) must not exceed DF at month {months - 1} ({prev:F8})");
            prev = df;
        }
    }

    // Flat-forward property: between two nodes (t0, P0) and (t1, P1), the
    // interpolated DF satisfies P(t) = P0 * exp(-f*(t-t0)) where f = -(ln P1 - ln P0)/(t1-t0).
    // This is equivalent to log-linear interpolation but stated in forward-rate terms.
    [Fact]
    public void Interpolate_BetweenTwoNodes_Satisfies_FlatForwardFormula()
    {
        var t0 = s_valuationDate.AddYears(1);
        var t1 = s_valuationDate.AddYears(2);
        double p0 = 0.9479;
        double p1 = 0.8979;

        var points = new CurvePoint[] { new(t0, p0), new(t1, p1) };
        var midDate = s_valuationDate.AddMonths(18);

        double tFrac0 = s_dayCount.YearFraction(s_valuationDate, t0);
        double tFrac1 = s_dayCount.YearFraction(s_valuationDate, t1);
        double tFracMid = s_dayCount.YearFraction(s_valuationDate, midDate);

        double f = (Math.Log(p0) - Math.Log(p1)) / (tFrac1 - tFrac0);
        double expected = p0 * Math.Exp(-f * (tFracMid - tFrac0));

        double actual = s_interpolator.Interpolate(s_valuationDate, midDate, points, s_dayCount);

        actual.Should().BeApproximately(expected, 1e-12);
    }

    // Flat-forward and log-linear produce identical results since both implement
    // piecewise-constant instantaneous forward rates in continuous compounding.
    [Fact]
    public void Interpolate_Matches_LogLinear_OnSameNodes()
    {
        var points = CreateStandardNodes();
        var logLinear = new LogLinearDiscountFactorInterpolator();

        for (int months = 1; months <= 360; months++)
        {
            var date = s_valuationDate.AddMonths(months);
            double dfFF = s_interpolator.Interpolate(s_valuationDate, date, points, s_dayCount);
            double dfLL = logLinear.Interpolate(s_valuationDate, date, points, s_dayCount);
            dfFF.Should().BeApproximately(dfLL, 1e-12,
                $"FlatForward and LogLinear should agree at month {months}");
        }
    }

    [Fact]
    public void Factory_Creates_FlatForwardInterpolator()
    {
        var interpolator = InterpolatorFactory.Create(InterpolatorKind.FlatForward);
        interpolator.Should().BeOfType<FlatForwardInterpolator>();
    }

    private sealed class Act365DayCount : IYearFractionCalculator
    {
        public string Code => "ACT/365F";
        public double YearFraction(DateOnly startDate, DateOnly endDate) =>
            (endDate.DayNumber - startDate.DayNumber) / 365.0;
    }
}
