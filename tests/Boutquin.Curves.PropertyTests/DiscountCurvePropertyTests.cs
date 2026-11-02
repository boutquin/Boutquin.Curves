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
using Boutquin.Curves.Interpolation;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.PropertyTests;

/// <summary>
/// Property-based tests for discount curve invariants.
/// </summary>
public sealed class DiscountCurvePropertyTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private const Currency Usd = Currency.USD;

    [Property]
    public void FlatDiscountCurve_ShouldStayPositive(double rate, int days)
    {
        double finiteRate = double.IsFinite(rate) ? rate : 0d;
        double boundedRate = Math.Min(Math.Abs(finiteRate), 0.25d);
        int boundedDays = Math.Abs(days % 3650);

        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), s_valuationDate, Usd, boundedRate);
        double df = curve.DiscountFactor(s_valuationDate.AddDays(boundedDays));

        df.Should().BeGreaterThan(0d);
        df.Should().BeLessThanOrEqualTo(1d);
    }

    [Property]
    public void FlatDiscountCurve_ShouldRejectNonFiniteRates(bool useNaN)
    {
        double invalidRate = useNaN ? double.NaN : double.PositiveInfinity;

        Action act = () =>
        {
            _ = new FlatDiscountCurve(
                new CurveName("USD-Disc"),
                s_valuationDate,
                Usd,
                invalidRate);
        };

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithParameterName("continuouslyCompoundedZeroRate");
    }

    [Property]
    public void FlatDiscountCurve_s_valuationDate_DF_Is_One(double rate)
    {
        double finiteRate = double.IsFinite(rate) ? rate : 0d;
        double boundedRate = Math.Min(Math.Abs(finiteRate), 0.25d);

        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), s_valuationDate, Usd, boundedRate);

        curve.DiscountFactor(s_valuationDate).Should().Be(1d);
    }

    [Property]
    public void FlatDiscountCurve_ForwardDF_Is_Positive(double rate, int days1, int days2)
    {
        double finiteRate = double.IsFinite(rate) ? rate : 0d;
        double boundedRate = Math.Min(Math.Abs(finiteRate), 0.25d);
        int d1 = Math.Abs(days1 % 3650) + 1;
        int d2 = d1 + Math.Abs(days2 % 3650) + 1;

        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), s_valuationDate, Usd, boundedRate);
        double df1 = curve.DiscountFactor(s_valuationDate.AddDays(d1));
        double df2 = curve.DiscountFactor(s_valuationDate.AddDays(d2));

        // Forward DF from d1 to d2 = DF(d2) / DF(d1)
        double forwardDf = df2 / df1;
        forwardDf.Should().BeGreaterThan(0d, "forward discount factor should be positive");
        forwardDf.Should().BeLessThanOrEqualTo(1d, "forward discount factor should be at most 1");
    }

    [Property]
    public void ZeroSpreadedCurve_ZeroSpread_MatchesUnderlying(double rate, int days)
    {
        double finiteRate = double.IsFinite(rate) ? rate : 0d;
        double boundedRate = Math.Min(Math.Abs(finiteRate), 0.25d);
        int boundedDays = Math.Abs(days % 3650) + 1;

        var baseCurve = new FlatDiscountCurve(new CurveName("Base"), s_valuationDate, Usd, boundedRate);
        var spreaded = new ZeroSpreadedDiscountCurve(new CurveName("Spread"), baseCurve, 0d);
        var date = s_valuationDate.AddDays(boundedDays);

        spreaded.DiscountFactor(date).Should().BeApproximately(baseCurve.DiscountFactor(date), 1e-12);
    }

    [Property]
    public void MultiplicativeSpreadCurve_UnitSpread_MatchesUnderlying(double rate, int days)
    {
        double finiteRate = double.IsFinite(rate) ? rate : 0d;
        double boundedRate = Math.Min(Math.Abs(finiteRate), 0.25d);
        int boundedDays = Math.Abs(days % 3650) + 1;

        var baseCurve = new FlatDiscountCurve(new CurveName("Base"), s_valuationDate, Usd, boundedRate);
        var spreaded = new MultiplicativeSpreadDiscountCurve(new CurveName("Mult"), baseCurve, 1.0);
        var date = s_valuationDate.AddDays(boundedDays);

        spreaded.DiscountFactor(date).Should().BeApproximately(baseCurve.DiscountFactor(date), 1e-12);
    }

    [Property]
    public void InterpolatedDiscountCurve_AllDates_Produce_Positive_DF(int days)
    {
        int boundedDays = Math.Abs(days % 7300) + 1;
        var points = new[]
        {
            new Boutquin.Curves.Abstractions.Curves.CurvePoint(s_valuationDate.AddYears(1), 0.9512),
            new Boutquin.Curves.Abstractions.Curves.CurvePoint(s_valuationDate.AddYears(5), 0.7788),
            new Boutquin.Curves.Abstractions.Curves.CurvePoint(s_valuationDate.AddYears(10), 0.6065),
        };
        var curve = new InterpolatedDiscountCurve(
            new CurveName("Test"), s_valuationDate, Usd, points);

        double df = curve.DiscountFactor(s_valuationDate.AddDays(boundedDays));

        df.Should().BeGreaterThan(0d, "discount factor must always be positive");
    }

    [Property]
    public void FlatDiscountCurve_ZeroRate_RoundTrips(double rate, int days)
    {
        double finiteRate = double.IsFinite(rate) ? rate : 0d;
        double boundedRate = Math.Clamp(Math.Abs(finiteRate), 0.001, 0.25);
        int boundedDays = Math.Abs(days % 3650) + 30; // at least 30 days for stable numerics

        var curve = new FlatDiscountCurve(new CurveName("Test"), s_valuationDate, Usd, boundedRate);
        var date = s_valuationDate.AddDays(boundedDays);

        double zero = curve.ZeroRate(date, CompoundingConvention.Continuous);
        zero.Should().BeApproximately(boundedRate, 1e-8, "continuous zero rate should recover the input rate");
    }
    [Property]
    public void Rolling_PreservesConditionalDiscountSurface(double rate, int rollDays, int horizonDays, int interpolator)
    {
        double finiteRate = double.IsFinite(rate) ? Math.Clamp(rate, -0.1, 0.1) : 0.03;
        InterpolatorKind kind = (InterpolatorKind)Math.Abs(interpolator % 5);
        // Monotone-convex's Numerics kernel requires non-decreasing cumulative returns.
        if (kind == InterpolatorKind.MonotoneConvex)
        {
            finiteRate = Math.Abs(finiteRate);
        }

        int step = 200 + Math.Abs(rollDays % 400);
        DateOnly rolledDate = s_valuationDate.AddDays(step);
        DateOnly target = rolledDate.AddDays(1 + Math.Abs(horizonDays % 1200));
        CurvePoint[] points = [new(s_valuationDate.AddDays(365), Math.Exp(-finiteRate)),
            new(s_valuationDate.AddDays(730), Math.Exp(-2.2 * finiteRate)),
            new(s_valuationDate.AddDays(1460), Math.Exp(-5 * finiteRate))];
        InterpolatedDiscountCurve original = new(new CurveName("Roll property"), s_valuationDate, Usd, points,
            new InterpolationSettings(kind, "FlatZero", "FlatForward"), Actual360.Instance);
        IDiscountCurve rolled = original.WithValuationDate(rolledDate);
        rolled.DiscountFactor(target).Should().BeApproximately(
            original.DiscountFactor(target) / original.DiscountFactor(rolledDate), 1e-12);
    }

}
