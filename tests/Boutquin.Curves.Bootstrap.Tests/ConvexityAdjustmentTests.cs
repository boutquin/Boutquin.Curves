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
/// Tests for convexity adjustment implementations.
/// </summary>
public sealed class ConvexityAdjustmentTests
{
    // --- ConstantConvexityAdjustment ---

    [Fact]
    public void Constant_Returns_FixedValue()
    {
        var ca = new ConstantConvexityAdjustment(0.0003);

        ca.Adjustment(0.25, 0.50).Should().Be(0.0003);
        ca.Adjustment(2.0, 2.25).Should().Be(0.0003);
        ca.Adjustment(5.0, 5.25).Should().Be(0.0003);
    }

    [Fact]
    public void Constant_Zero_Returns_Zero()
    {
        var ca = new ConstantConvexityAdjustment(0d);

        ca.Adjustment(1.0, 1.25).Should().Be(0d);
    }

    // --- HullWhiteConvexityAdjustment ---

    [Fact]
    public void HullWhite_RejectsNegativeMeanReversion()
    {
        var act = () => new HullWhiteConvexityAdjustment(-0.01, 0.01);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("meanReversion");
    }

    [Fact]
    public void HullWhite_RejectsNonPositiveVolatility()
    {
        var act = () => new HullWhiteConvexityAdjustment(0.03, 0d);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("volatility");
    }

    [Fact]
    public void HullWhite_ZeroExpiry_Returns_Zero()
    {
        var ca = new HullWhiteConvexityAdjustment(0.03, 0.01);

        ca.Adjustment(0d, 0.25).Should().Be(0d);
    }

    [Fact]
    public void HullWhite_ExpiryAfterMaturity_Returns_Zero()
    {
        var ca = new HullWhiteConvexityAdjustment(0.03, 0.01);

        ca.Adjustment(1.0, 0.5).Should().Be(0d);
    }

    [Fact]
    public void HullWhite_PositiveAdjustment_GrowsWithMaturity()
    {
        var ca = new HullWhiteConvexityAdjustment(0.03, 0.01);

        double adj1Y = ca.Adjustment(1.0, 1.25);
        double adj3Y = ca.Adjustment(3.0, 3.25);
        double adj5Y = ca.Adjustment(5.0, 5.25);

        adj1Y.Should().BeGreaterThan(0d);
        adj3Y.Should().BeGreaterThan(adj1Y);
        adj5Y.Should().BeGreaterThan(adj3Y);
    }

    [Fact]
    public void HullWhite_ShortDated_SmallAdjustment()
    {
        // For 3M contract 3M from now, adjustment should be very small
        var ca = new HullWhiteConvexityAdjustment(0.03, 0.01);

        double adj = ca.Adjustment(0.25, 0.50);

        adj.Should().BeLessThan(0.0001); // Less than 1 bp
        adj.Should().BeGreaterThan(0d);
    }

    [Fact]
    public void HullWhite_LongDated_MaterialAdjustment()
    {
        // For 3M contract 5Y from now with higher vol, adjustment should be material
        var ca = new HullWhiteConvexityAdjustment(0.03, 0.015);

        double adj = ca.Adjustment(5.0, 5.25);

        adj.Should().BeGreaterThan(0.0001); // Greater than 1 bp
    }

    [Fact]
    public void HullWhite_ZeroMeanReversion_UsesHoLeeLimitFormula()
    {
        // Ho-Lee limit: CA = 0.5 * sigma^2 * T1 * (T2 - T1)
        double sigma = 0.01;
        double t1 = 2.0;
        double t2 = 2.25;
        double expectedHoLee = 0.5 * sigma * sigma * t1 * (t2 - t1);

        var ca = new HullWhiteConvexityAdjustment(0d, sigma);
        double adj = ca.Adjustment(t1, t2);

        adj.Should().BeApproximately(expectedHoLee, 1e-12);
    }

    [Fact]
    public void HullWhite_SmallMeanReversion_ConvergesToHoLee()
    {
        double sigma = 0.01;
        double t1 = 2.0;
        double t2 = 2.25;
        double hoLee = 0.5 * sigma * sigma * t1 * (t2 - t1);

        // Very small mean reversion should give similar result to Ho-Lee
        var ca = new HullWhiteConvexityAdjustment(1e-10, sigma);
        double adj = ca.Adjustment(t1, t2);

        adj.Should().BeApproximately(hoLee, 1e-8);
    }
}
