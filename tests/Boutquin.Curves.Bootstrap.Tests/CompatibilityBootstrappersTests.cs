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

using Boutquin.Curves.Bootstrap.Inputs;
using Boutquin.Curves.Bootstrap.Instruments;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

public sealed class CompatibilityBootstrappersTests
{
    [Fact]
    public void OisBootstrapper_ShouldUseTerminalOisSwapRate_WhenOisQuotesExist()
    {
        OisBootstrapInput input = new(
            "USD-OIS",
            new DateOnly(2026, 4, 10),
            new[] { new DepositQuote(1, 0.031) },
            new[] { new OisSwapQuote(12, 0.041), new OisSwapQuote(24, 0.049) },
            Actual365Fixed.Instance,
            Currency.USD);

        OisDiscountCurveBootstrapper bootstrapper = new();

        var curve = bootstrapper.Bootstrap(input);

        curve.Name.Value.Should().Be("USD-OIS");
        curve.Currency.Should().Be(Currency.USD);
        curve.InstantaneousForwardRate(input.AnchorDate).Should().BeApproximately(0.049, 1e-12);
    }

    [Fact]
    public void OisBootstrapper_ShouldUseTerminalDepositRate_WhenNoOisQuotesExist()
    {
        OisBootstrapInput input = new(
            "USD-OIS",
            new DateOnly(2026, 4, 10),
            new[] { new DepositQuote(1, 0.028), new DepositQuote(3, 0.032) },
            Array.Empty<OisSwapQuote>(),
            Actual365Fixed.Instance);

        OisDiscountCurveBootstrapper bootstrapper = new();

        var curve = bootstrapper.Bootstrap(input);

        curve.Currency.Should().Be(Currency.USD);
        curve.InstantaneousForwardRate(input.AnchorDate).Should().BeApproximately(0.032, 1e-12);
    }

    [Fact]
    public void SwapBootstrapper_ShouldUseTerminalSwapRate()
    {
        SwapBootstrapInput input = new(
            "USD-LIBOR-3M",
            new DateOnly(2026, 4, 10),
            new[] { new InterestRateSwapQuote(12, 0.038), new InterestRateSwapQuote(60, 0.044) },
            Actual365Fixed.Instance,
            Currency.USD);

        SwapProjectionCurveBootstrapper bootstrapper = new();

        var curve = bootstrapper.Bootstrap(input);

        curve.Name.Value.Should().Be("USD-LIBOR-3M");
        curve.Currency.Should().Be(Currency.USD);
        curve.InstantaneousForwardRate(input.AnchorDate).Should().BeApproximately(0.044, 1e-12);
    }

    [Fact]
    public void OisBootstrapper_SelectsLastChronologicalQuote_NotMaxRate_AndPinsFlatCurveMath()
    {
        // Decreasing rate sequence: last (0.030) != max (0.052). A max-rate heuristic would pick
        // 0.052; the documented last-chronological-quote heuristic must pick 0.030. The earlier
        // echo tests used increasing sequences where last == max and could not discriminate.
        DateOnly anchor = new(2026, 4, 10);
        OisBootstrapInput input = new(
            "USD-OIS",
            anchor,
            new[] { new DepositQuote(1, 0.031) },
            new[] { new OisSwapQuote(12, 0.052), new OisSwapQuote(24, 0.030) },
            Actual365Fixed.Instance,
            Currency.USD);

        var curve = new OisDiscountCurveBootstrapper().Bootstrap(input);

        // Last quote, not max.
        curve.InstantaneousForwardRate(anchor).Should().BeApproximately(0.030, 1e-12);

        // Flat-curve DF math pinned at a future date: P(t) = exp(-terminalRate * t), ACT/365.
        DateOnly future = anchor.AddYears(2);
        double t = Actual365Fixed.Instance.YearFraction(anchor, future);
        curve.DiscountFactor(future).Should().BeApproximately(Math.Exp(-0.030 * t), 1e-12);
    }

    [Fact]
    public void SwapBootstrapper_SelectsLastChronologicalQuote_NotMaxRate_AndPinsFlatCurveMath()
    {
        // Decreasing sequence: last (0.036) != max (0.050).
        DateOnly anchor = new(2026, 4, 10);
        SwapBootstrapInput input = new(
            "USD-LIBOR-3M",
            anchor,
            new[] { new InterestRateSwapQuote(12, 0.050), new InterestRateSwapQuote(60, 0.036) },
            Actual365Fixed.Instance,
            Currency.USD);

        var curve = new SwapProjectionCurveBootstrapper().Bootstrap(input);

        curve.InstantaneousForwardRate(anchor).Should().BeApproximately(0.036, 1e-12);

        DateOnly future = anchor.AddYears(3);
        double t = Actual365Fixed.Instance.YearFraction(anchor, future);
        curve.DiscountFactor(future).Should().BeApproximately(Math.Exp(-0.036 * t), 1e-12);
    }

    [Fact]
    public void SwapBootstrapper_ShouldThrow_WhenNoSwapQuotesExist()
    {
        SwapBootstrapInput input = new(
            "USD-LIBOR-3M",
            new DateOnly(2026, 4, 10),
            Array.Empty<InterestRateSwapQuote>(),
            Actual365Fixed.Instance);

        SwapProjectionCurveBootstrapper bootstrapper = new();

        Action action = () => bootstrapper.Bootstrap(input);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*At least one swap quote is required.*");
    }
}
