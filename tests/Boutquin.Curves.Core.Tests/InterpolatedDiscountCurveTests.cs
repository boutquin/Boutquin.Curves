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
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

public sealed class InterpolatedDiscountCurveTests
{
    [Fact]
    public void DiscountFactor_ShouldDecreaseAcrossCurve()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        InterpolatedDiscountCurve curve = new(
            new CurveName("USD-Disc"),
            valuationDate,
            Currency.USD,
            new[]
            {
                new CurvePoint(valuationDate.AddMonths(6), 0.98d),
                new CurvePoint(valuationDate.AddYears(2), 0.92d)
            });

        double sixMonths = curve.DiscountFactor(valuationDate.AddMonths(6));
        double twoYears = curve.DiscountFactor(valuationDate.AddYears(2));

        sixMonths.Should().BeGreaterThan(twoYears);
        curve.ZeroRate(valuationDate.AddYears(2), CompoundingConvention.Continuous).Should().BePositive();
    }

    [Fact]
    public void DiscountFactor_ShouldRespectLinearZeroRateInterpolationSelection()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        InterpolatedDiscountCurve curve = new(
            new CurveName("USD-Disc"),
            valuationDate,
            Currency.USD,
            new[]
            {
                new CurvePoint(valuationDate.AddYears(1), 0.98d),
                new CurvePoint(valuationDate.AddYears(2), 0.92d)
            },
            new InterpolationSettings(InterpolatorKind.LinearZeroRate, "FlatZero", "FlatForward"));

        double targetDf = curve.DiscountFactor(valuationDate.AddMonths(18));

        double t = curve.DayCount.YearFraction(valuationDate, valuationDate.AddMonths(18));
        double t0 = curve.DayCount.YearFraction(valuationDate, valuationDate.AddYears(1));
        double t1 = curve.DayCount.YearFraction(valuationDate, valuationDate.AddYears(2));
        double z0 = -Math.Log(0.98d) / t0;
        double z1 = -Math.Log(0.92d) / t1;
        double z = z0 + ((z1 - z0) * ((t - t0) / (t1 - t0)));
        double expected = Math.Exp(-z * t);

        targetDf.Should().BeApproximately(expected, 1e-12d);
    }

    [Fact]
    public void DiscountFactor_ShouldRespectFlatForwardRightExtrapolationSelection()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        InterpolatedDiscountCurve curve = new(
            new CurveName("USD-Disc"),
            valuationDate,
            Currency.USD,
            new[]
            {
                new CurvePoint(valuationDate.AddYears(1), 0.97d),
                new CurvePoint(valuationDate.AddYears(2), 0.91d)
            },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        double targetDf = curve.DiscountFactor(valuationDate.AddYears(3));

        DateOnly oneYear = valuationDate.AddYears(1);
        DateOnly twoYears = valuationDate.AddYears(2);
        DateOnly threeYears = valuationDate.AddYears(3);
        double t1 = curve.DayCount.YearFraction(valuationDate, oneYear);
        double t2 = curve.DayCount.YearFraction(valuationDate, twoYears);
        double t3 = curve.DayCount.YearFraction(valuationDate, threeYears);
        double forward = -Math.Log(0.91d / 0.97d) / (t2 - t1);
        double expected = 0.91d * Math.Exp(-forward * (t3 - t2));

        targetDf.Should().BeApproximately(expected, 1e-12d);
    }

    // ── F-05: Unrecognised extrapolator mode caught at construction ────────────

    [Fact]
    public void Constructor_ThrowsArgumentException_ForUnrecognisedLeftExtrapolatorMode()
    {
        // Before fix: unrecognised mode deferred to first evaluation call.
        // After fix: ArgumentException at construction time.
        DateOnly valuationDate = new(2026, 4, 9);
        Action act = () => _ = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            valuationDate,
            Currency.USD,
            new[] { new CurvePoint(valuationDate.AddYears(1), 0.96d) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatFoo", "FlatForward"));

        act.Should().Throw<ArgumentException>().WithMessage("*FlatFoo*");
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_ForUnrecognisedRightExtrapolatorMode()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        Action act = () => _ = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            valuationDate,
            Currency.USD,
            new[] { new CurvePoint(valuationDate.AddYears(1), 0.96d) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatBar"));

        act.Should().Throw<ArgumentException>().WithMessage("*FlatBar*");
    }

    // ── F-11: Consistent exception types for invalid constructor arguments ─────

    [Fact]
    public void Constructor_ThrowsArgumentException_ForEmptyPoints()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        Action act = () => _ = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            valuationDate,
            Currency.USD,
            Array.Empty<CurvePoint>());

        act.Should().Throw<ArgumentException>().WithParameterName("points");
    }

    [Fact]
    public void Constructor_ThrowsExactlyArgumentException_ForNonPositiveDiscountFactor()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        Action act = () => _ = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            valuationDate,
            Currency.USD,
            new[] { new CurvePoint(valuationDate.AddYears(1), 0d) });

        // F-11: standardised to ArgumentException (not ArgumentOutOfRangeException).
        // Uses ThrowExactly so the test is red until the source change lands.
        act.Should().ThrowExactly<ArgumentException>().WithParameterName("points");
    }

    // ── F-35: Single-point curve correctness ──────────────────────────────────

    [Fact]
    public void DiscountFactor_SingleNode_ReturnsNodeValueAtPillar()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        DateOnly pillar = valuationDate.AddYears(1);
        const double df = 0.96d;

        InterpolatedDiscountCurve curve = new(
            new CurveName("USD-Disc"), valuationDate, Currency.USD,
            new[] { new CurvePoint(pillar, df) });

        curve.DiscountFactor(pillar).Should().BeApproximately(df, 1e-12d);
    }

    [Fact]
    public void DiscountFactor_SingleNode_ReturnsOneAtValuationDate()
    {
        DateOnly valuationDate = new(2026, 4, 9);

        InterpolatedDiscountCurve curve = new(
            new CurveName("USD-Disc"), valuationDate, Currency.USD,
            new[] { new CurvePoint(valuationDate.AddYears(1), 0.96d) });

        curve.DiscountFactor(valuationDate).Should().Be(1d);
    }

    [Fact]
    public void DiscountFactor_SingleNode_ExtrapolatesLeftWithFlatZero()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        DateOnly pillar = valuationDate.AddYears(1);
        const double df = 0.96d;

        InterpolatedDiscountCurve curve = new(
            new CurveName("USD-Disc"), valuationDate, Currency.USD,
            new[] { new CurvePoint(pillar, df) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        // Left extrapolation target: 6 months before the pillar (between valuation date and pillar)
        DateOnly target = valuationDate.AddMonths(6);
        double result = curve.DiscountFactor(target);
        // With flat-zero left extrapolation the result uses the pillar zero rate
        result.Should().BeGreaterThan(0d).And.BeLessThanOrEqualTo(1d);
    }

    [Fact]
    public void DiscountFactor_SingleNode_ExtrapolatesRightWithFlatForward()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        DateOnly pillar = valuationDate.AddYears(1);

        InterpolatedDiscountCurve curve = new(
            new CurveName("USD-Disc"), valuationDate, Currency.USD,
            new[] { new CurvePoint(pillar, 0.96d) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        double df2Y = curve.DiscountFactor(valuationDate.AddYears(2));
        // Right flat-forward: single node → falls back to flat-zero, discount factor continues to decline
        df2Y.Should().BeGreaterThan(0d).And.BeLessThan(0.96d);
    }
}
