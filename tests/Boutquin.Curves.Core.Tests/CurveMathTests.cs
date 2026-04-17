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

using Boutquin.Curves.Core.Internal;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

/// <summary>
/// Unit tests for <see cref="CurveMath"/> — the shared conversion layer between
/// discount factors and zero rates across compounding conventions.
/// </summary>
/// <remarks>
/// The contract under test differentiates two regimes that were previously
/// collapsed into a single silent guard:
/// <list type="bullet">
///   <item><b>t &lt; 0</b> — a genuine data error (target date precedes valuation).
///         Must throw so the upstream caller is forced to fix its time calculation.</item>
///   <item><b>t == 0</b> — a legitimate degenerate case (target equals valuation).
///         Must return the mathematical convention at the boundary: 0 for zero rate,
///         1 for discount factor.</item>
/// </list>
/// </remarks>
public sealed class CurveMathTests
{
    public static IEnumerable<object[]> AllCompoundingConventions() =>
        new[]
        {
            new object[] { CompoundingConvention.Continuous },
            new object[] { CompoundingConvention.Simple },
            new object[] { CompoundingConvention.Annual },
            new object[] { CompoundingConvention.SemiAnnual },
            new object[] { CompoundingConvention.Quarterly },
            new object[] { CompoundingConvention.Monthly }
        };

    [Theory]
    [MemberData(nameof(AllCompoundingConventions))]
    public void ZeroRateFromDiscountFactor_NegativeTime_Throws(CompoundingConvention compounding)
    {
        Action act = () => CurveMath.ZeroRateFromDiscountFactor(0.95d, -0.25d, compounding);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("timeInYears");
    }

    [Theory]
    [MemberData(nameof(AllCompoundingConventions))]
    public void ZeroRateFromDiscountFactor_ZeroTime_ReturnsZero(CompoundingConvention compounding)
    {
        // At valuation date, time fraction is zero. Zero-rate limit is undefined;
        // convention is zero. Any non-unit DF at t=0 is technically inconsistent
        // but we do not validate that here — the no-arbitrage guarantee is the
        // caller's responsibility.
        double rate = CurveMath.ZeroRateFromDiscountFactor(1d, 0d, compounding);

        rate.Should().Be(0d);
    }

    [Fact]
    public void ZeroRateFromDiscountFactor_NonPositiveDiscountFactor_StillThrowsOnDiscountFactor()
    {
        // Pre-existing contract: DF <= 0 throws. Regression guard that the new
        // negative-time guard does not swallow this error when both are invalid.
        Action act = () => CurveMath.ZeroRateFromDiscountFactor(0d, 1d, CompoundingConvention.Continuous);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("discountFactor");
    }

    [Fact]
    public void ZeroRateFromDiscountFactor_Continuous_PositiveTime_ReturnsCorrectRate()
    {
        // Regression: P = e^{-rt}  ⇒  r = -ln(P) / t.
        double rate = CurveMath.ZeroRateFromDiscountFactor(0.9d, 2d, CompoundingConvention.Continuous);

        rate.Should().BeApproximately(-Math.Log(0.9d) / 2d, 1e-15);
    }

    [Fact]
    public void ZeroRateFromDiscountFactor_Simple_PositiveTime_ReturnsCorrectRate()
    {
        // Regression: P = 1 / (1 + rt)  ⇒  r = (1/P - 1) / t.
        double rate = CurveMath.ZeroRateFromDiscountFactor(0.9d, 2d, CompoundingConvention.Simple);

        rate.Should().BeApproximately(((1d / 0.9d) - 1d) / 2d, 1e-15);
    }

    [Fact]
    public void DiscountFactorFromContinuousZero_NegativeTime_Throws()
    {
        Action act = () => CurveMath.DiscountFactorFromContinuousZero(0.05d, -0.25d);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("timeInYears");
    }

    [Fact]
    public void DiscountFactorFromContinuousZero_ZeroTime_ReturnsOne()
    {
        // P(0) = e^{-r·0} = 1 by definition, independent of the rate.
        double df = CurveMath.DiscountFactorFromContinuousZero(0.05d, 0d);

        df.Should().Be(1d);
    }

    [Fact]
    public void DiscountFactorFromContinuousZero_PositiveTime_MatchesClosedForm()
    {
        // Regression.
        double df = CurveMath.DiscountFactorFromContinuousZero(0.05d, 2d);

        df.Should().BeApproximately(Math.Exp(-0.05d * 2d), 1e-15);
    }

    [Fact]
    public void ZeroRateFromDiscountFactor_ContinuousRoundTrip_Holds()
    {
        // r = ZeroRate(P, t)  ⇒  DF(r, t) == P  (for continuous compounding).
        const double originalDf = 0.87d;
        const double t = 3.5d;

        double rate = CurveMath.ZeroRateFromDiscountFactor(originalDf, t, CompoundingConvention.Continuous);
        double recovered = CurveMath.DiscountFactorFromContinuousZero(rate, t);

        recovered.Should().BeApproximately(originalDf, 1e-15);
    }
}
