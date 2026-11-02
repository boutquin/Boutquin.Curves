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

using Boutquin.Curves.Bootstrap.ConvexityAdjustments;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

/// <summary>
/// Tests for <see cref="BondYieldConvexityAdjustment"/> (Hull eq 30.1).
/// </summary>
public sealed class BondYieldConvexityAdjustmentTests
{
    // 5-year annual coupon bond functions G(y), G'(y), G''(y)
    // Face = 1.0, coupon rate c.
    private static Func<double, double> MakeG(double coupon) =>
        y =>
        {
            double sum = 0.0;
            for (int i = 1; i <= 5; i++)
            {
                sum += coupon / Math.Pow(1.0 + y, i);
            }
            sum += 1.0 / Math.Pow(1.0 + y, 5);
            return sum;
        };

    private static Func<double, double> MakeGPrime(double coupon) =>
        y =>
        {
            double sum = 0.0;
            for (int i = 1; i <= 5; i++)
            {
                sum -= coupon * i / Math.Pow(1.0 + y, i + 1);
            }
            sum -= 5.0 / Math.Pow(1.0 + y, 6);
            return sum;
        };

    private static Func<double, double> MakeGDoublePrime(double coupon) =>
        y =>
        {
            double sum = 0.0;
            for (int i = 1; i <= 5; i++)
            {
                sum += coupon * i * (i + 1) / Math.Pow(1.0 + y, i + 2);
            }
            sum += 5.0 * 6.0 / Math.Pow(1.0 + y, 7);
            return sum;
        };

    /// <summary>
    /// AC-C1: BondYieldConvexityAdjustment agrees with Hull eq 30.1 to 1e-6.
    /// Uses 5-year 6% annual coupon bond, y_F=0.05, σ=0.01, T=1.
    /// Computes expected value analytically from the formula and verifies the implementation.
    /// </summary>
    [Fact]
    public void Adjustment_HullEq30Point1_AgreesWith1E6()
    {
        const double coupon = 0.06;
        const double yF = 0.05;
        const double sigma = 0.01;
        const double T = 1.0;

        var G = MakeG(coupon);
        var GPrime = MakeGPrime(coupon);
        var GDoublePrime = MakeGDoublePrime(coupon);

        var sut = new BondYieldConvexityAdjustment(
            forwardYield: yF,
            forwardYieldVolatility: sigma,
            timeToObservation: T,
            bondPrice: G,
            bondPriceFirst: GPrime,
            bondPriceSecond: GDoublePrime);

        // Expected from Hull eq 30.1: CA = -0.5 * (G''(y_F) / G'(y_F)) * sigma^2 * T
        double expected = -0.5 * (GDoublePrime(yF) / GPrime(yF)) * sigma * sigma * T;

        double actual = sut.Adjustment(0.0, 5.0);

        actual.Should().BeApproximately(expected, 1e-6,
            "BondYieldConvexityAdjustment must reproduce Hull eq 30.1 to 1e-6");

        // CA should be small positive (convexity makes futures-implied rate higher than forward)
        actual.Should().BePositive("bond yield convexity adjustment is positive for finite volatility");
    }

    /// <summary>
    /// AC-C2: Cross-check BondYieldConvexityAdjustment against HullWhiteConvexityAdjustment
    /// within 1e-4 on a reference problem where both are valid approximations.
    /// For short-maturity (T=0.25 year, 3M deposit) with low vol, both approaches agree
    /// to within 1 bp.
    /// </summary>
    [Fact]
    public void Adjustment_CrossCheckWithHullWhite_Within1E4()
    {
        // Short maturity problem: 2Y bond, 3M observation (T=0.25)
        // Use a 2-year annual coupon bond
        const double coupon = 0.05;
        const double yF = 0.05;
        const double sigma = 0.01;
        const double T = 0.25;

        Func<double, double> G2Y = y =>
        {
            double s = 0.0;
            for (int i = 1; i <= 2; i++)
            {
                s += coupon / Math.Pow(1 + y, i);
            }
            s += 1.0 / Math.Pow(1 + y, 2);
            return s;
        };

        Func<double, double> G2YPrime = y =>
        {
            double s = 0.0;
            for (int i = 1; i <= 2; i++)
            {
                s -= coupon * i / Math.Pow(1 + y, i + 1);
            }
            s -= 2.0 / Math.Pow(1 + y, 3);
            return s;
        };

        Func<double, double> G2YDoublePrime = y =>
        {
            double s = 0.0;
            for (int i = 1; i <= 2; i++)
            {
                s += coupon * i * (i + 1) / Math.Pow(1 + y, i + 2);
            }
            s += 2.0 * 3.0 / Math.Pow(1 + y, 4);
            return s;
        };

        var bondAdj = new BondYieldConvexityAdjustment(
            forwardYield: yF,
            forwardYieldVolatility: sigma,
            timeToObservation: T,
            bondPrice: G2Y,
            bondPriceFirst: G2YPrime,
            bondPriceSecond: G2YDoublePrime);

        // HullWhite on the same parameters: use a=0.1, sigma matching the forward yield vol
        // For a simple deposit (T1=T, T2=T+0.25), CA_HW = 0.5*sigma^2*B(T1,T2)*(1-exp(-2aT1))/(2a)
        var hwAdj = new HullWhiteConvexityAdjustment(meanReversion: 0.1, volatility: sigma);

        double bondCA = bondAdj.Adjustment(T, T + 0.25);
        double hwCA = hwAdj.Adjustment(T, T + 0.25);

        // Both should be small positive numbers, and should agree within 1e-4 (1 bp)
        bondCA.Should().BePositive("convexity adjustment is positive");
        hwCA.Should().BePositive("HW convexity adjustment is positive");
        Math.Abs(bondCA - hwCA).Should().BeLessThanOrEqualTo(1e-4,
            "Bond yield and HullWhite adjustments should agree within 1e-4 for short maturities and low vol");
    }

    [Fact]
    public void Constructor_AcceptsAllParameters()
    {
        var sut = new BondYieldConvexityAdjustment(
            forwardYield: 0.05,
            forwardYieldVolatility: 0.01,
            timeToObservation: 1.0,
            bondPrice: y => 1.0,
            bondPriceFirst: y => -1.0,
            bondPriceSecond: y => 2.0);

        // Should not throw and produce a finite result
        double result = sut.Adjustment(0.0, 1.0);
        double.IsFinite(result).Should().BeTrue("result should be a finite number");
    }

    [Fact]
    public void Adjustment_DegenerateGPrimeZero_ThrowsRatherThanProducingNonFinite()
    {
        // T-C1-2: a degenerate user-supplied G'(y_F) == 0 makes G''/G' non-finite (NaN when G''
        // is also 0, ±Infinity otherwise). The NaN adjustment would escape the downstream
        // InterpolatedDiscountCurve positivity guard (NaN <= 0d is false) and poison the curve.
        // The adjustment must fail loud at the source rather than return a non-finite value.
        var sut = new BondYieldConvexityAdjustment(
            forwardYield: 0.05,
            forwardYieldVolatility: 0.01,
            timeToObservation: 1.0,
            bondPrice: y => 1.0,
            bondPriceFirst: y => 0.0,   // degenerate: G'(y_F) = 0
            bondPriceSecond: y => 0.0); // G''(y_F) = 0 → 0/0 = NaN

        Action act = () => sut.Adjustment(0.0, 5.0);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-finite*");
    }

    [Fact]
    public void Adjustment_ZeroVolatility_ReturnsZero()
    {
        // With sigma=0, CA = 0 regardless of bond shape
        var sut = new BondYieldConvexityAdjustment(
            forwardYield: 0.05,
            forwardYieldVolatility: 0.0,
            timeToObservation: 1.0,
            bondPrice: MakeG(0.06),
            bondPriceFirst: MakeGPrime(0.06),
            bondPriceSecond: MakeGDoublePrime(0.06));

        sut.Adjustment(0.0, 5.0).Should().Be(0.0,
            "zero volatility implies zero convexity adjustment");
    }

    [Fact]
    public void Adjustment_ZeroTimeToObservation_ReturnsZero()
    {
        // With T=0, CA = 0
        var sut = new BondYieldConvexityAdjustment(
            forwardYield: 0.05,
            forwardYieldVolatility: 0.01,
            timeToObservation: 0.0,
            bondPrice: MakeG(0.06),
            bondPriceFirst: MakeGPrime(0.06),
            bondPriceSecond: MakeGDoublePrime(0.06));

        sut.Adjustment(0.0, 5.0).Should().Be(0.0,
            "zero time to observation implies zero convexity adjustment");
    }
}
