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
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

/// <summary>
/// Tests for <see cref="ZeroSpreadedDiscountCurve"/> and <see cref="MultiplicativeSpreadDiscountCurve"/>.
/// </summary>
public sealed class SpreadedDiscountCurveTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 11);
    private const CurrencyCode Usd = CurrencyCode.USD;
    private static readonly DateOnly s_date1Y = new(2027, 4, 11);
    private static readonly DateOnly s_date5Y = new(2031, 4, 11);

    private static FlatDiscountCurve CreateBaseCurve(double rate = 0.05)
    {
        return new FlatDiscountCurve(new CurveName("USD-Base"), s_valuationDate, Usd, rate);
    }

    // --- ZeroSpreadedDiscountCurve ---

    [Fact]
    public void ZeroSpreaded_Ats_valuationDate_Returns_One()
    {
        var spreaded = new ZeroSpreadedDiscountCurve(new CurveName("USD-Spread"), CreateBaseCurve(), 0.0050);

        spreaded.DiscountFactor(s_valuationDate).Should().Be(1d);
    }

    [Fact]
    public void ZeroSpreaded_WithZeroSpread_MatchesUnderlying()
    {
        var baseCurve = CreateBaseCurve();
        var spreaded = new ZeroSpreadedDiscountCurve(new CurveName("USD-Spread"), baseCurve, 0.0);

        spreaded.DiscountFactor(s_date1Y).Should().BeApproximately(baseCurve.DiscountFactor(s_date1Y), 1e-12);
        spreaded.DiscountFactor(s_date5Y).Should().BeApproximately(baseCurve.DiscountFactor(s_date5Y), 1e-12);
    }

    [Fact]
    public void ZeroSpreaded_PositiveSpread_ProducesSmallerDiscountFactors()
    {
        var baseCurve = CreateBaseCurve();
        var spreaded = new ZeroSpreadedDiscountCurve(new CurveName("USD-Spread"), baseCurve, 0.0050);

        spreaded.DiscountFactor(s_date1Y).Should().BeLessThan(baseCurve.DiscountFactor(s_date1Y));
        spreaded.DiscountFactor(s_date5Y).Should().BeLessThan(baseCurve.DiscountFactor(s_date5Y));
    }

    [Fact]
    public void ZeroSpreaded_NegativeSpread_ProducesLargerDiscountFactors()
    {
        var baseCurve = CreateBaseCurve();
        var spreaded = new ZeroSpreadedDiscountCurve(new CurveName("USD-Spread"), baseCurve, -0.0050);

        spreaded.DiscountFactor(s_date1Y).Should().BeGreaterThan(baseCurve.DiscountFactor(s_date1Y));
    }

    [Fact]
    public void ZeroSpreaded_ZeroRate_IncludesSpread()
    {
        var baseCurve = CreateBaseCurve(0.04);
        var spreaded = new ZeroSpreadedDiscountCurve(new CurveName("USD-Spread"), baseCurve, 0.01);

        double spreadedZero = spreaded.ZeroRate(s_date1Y, CompoundingConvention.Continuous);
        double baseZero = baseCurve.ZeroRate(s_date1Y, CompoundingConvention.Continuous);

        spreadedZero.Should().BeApproximately(baseZero + 0.01, 1e-6);
    }

    [Fact]
    public void ZeroSpreaded_ForwardRateIncludesSpread()
    {
        var baseCurve = CreateBaseCurve(0.04);
        var spreaded = new ZeroSpreadedDiscountCurve(new CurveName("USD-Spread"), baseCurve, 0.01);

        double spreadedForward = spreaded.InstantaneousForwardRate(s_date1Y);
        double baseForward = baseCurve.InstantaneousForwardRate(s_date1Y);

        spreadedForward.Should().BeApproximately(baseForward + 0.01, 1e-6);
    }

    [Fact]
    public void ZeroSpreaded_ExposesUnderlyingProperties()
    {
        var baseCurve = CreateBaseCurve();
        var spreaded = new ZeroSpreadedDiscountCurve(new CurveName("USD-Spread"), baseCurve, 0.01);

        spreaded.ValuationDate.Should().Be(s_valuationDate);
        spreaded.Currency.Should().Be(Usd);
        spreaded.DayCount.Should().BeSameAs(baseCurve.DayCount);
    }

    // --- MultiplicativeSpreadDiscountCurve ---

    [Fact]
    public void Multiplicative_Ats_valuationDate_Returns_One()
    {
        var spreaded = new MultiplicativeSpreadDiscountCurve(new CurveName("USD-Mult"), CreateBaseCurve(), 0.999);

        spreaded.DiscountFactor(s_valuationDate).Should().Be(1d);
    }

    [Fact]
    public void Multiplicative_WithUnitSpread_MatchesUnderlying()
    {
        var baseCurve = CreateBaseCurve();
        var spreaded = new MultiplicativeSpreadDiscountCurve(new CurveName("USD-Mult"), baseCurve, 1.0);

        spreaded.DiscountFactor(s_date1Y).Should().BeApproximately(baseCurve.DiscountFactor(s_date1Y), 1e-12);
        spreaded.DiscountFactor(s_date5Y).Should().BeApproximately(baseCurve.DiscountFactor(s_date5Y), 1e-12);
    }

    [Fact]
    public void Multiplicative_SpreadBelowOne_ProducesSmallerDiscountFactors()
    {
        var baseCurve = CreateBaseCurve();
        var spreaded = new MultiplicativeSpreadDiscountCurve(new CurveName("USD-Mult"), baseCurve, 0.99);

        spreaded.DiscountFactor(s_date1Y).Should().BeLessThan(baseCurve.DiscountFactor(s_date1Y));
    }

    [Fact]
    public void Multiplicative_SpreadAboveOne_ProducesLargerDiscountFactors()
    {
        var baseCurve = CreateBaseCurve();
        var spreaded = new MultiplicativeSpreadDiscountCurve(new CurveName("USD-Mult"), baseCurve, 1.01);

        spreaded.DiscountFactor(s_date1Y).Should().BeGreaterThan(baseCurve.DiscountFactor(s_date1Y));
    }

    [Fact]
    public void Multiplicative_RejectsNonPositiveSpread()
    {
        var act = () => new MultiplicativeSpreadDiscountCurve(new CurveName("USD-Mult"), CreateBaseCurve(), 0d);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("spreadFactor");
    }

    [Fact]
    public void Multiplicative_RejectsNegativeSpread()
    {
        var act = () => new MultiplicativeSpreadDiscountCurve(new CurveName("USD-Mult"), CreateBaseCurve(), -0.5);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("spreadFactor");
    }

    [Fact]
    public void Multiplicative_ExposesUnderlyingProperties()
    {
        var baseCurve = CreateBaseCurve();
        var spreaded = new MultiplicativeSpreadDiscountCurve(new CurveName("USD-Mult"), baseCurve, 0.999);

        spreaded.ValuationDate.Should().Be(s_valuationDate);
        spreaded.Currency.Should().Be(Usd);
        spreaded.DayCount.Should().BeSameAs(baseCurve.DayCount);
    }

    [Fact]
    public void BothSpreads_ImplementIDiscountCurve()
    {
        IDiscountCurve zero = new ZeroSpreadedDiscountCurve(new CurveName("Z"), CreateBaseCurve(), 0.01);
        IDiscountCurve mult = new MultiplicativeSpreadDiscountCurve(new CurveName("M"), CreateBaseCurve(), 0.999);

        zero.DiscountFactor(s_date1Y).Should().BeGreaterThan(0d);
        mult.DiscountFactor(s_date1Y).Should().BeGreaterThan(0d);
    }
}
