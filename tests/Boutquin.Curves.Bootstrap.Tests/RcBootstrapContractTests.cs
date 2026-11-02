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

using Boutquin.Curves.Abstractions;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Bootstrap.ConvexityAdjustments;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.Helpers;
using Boutquin.Curves.Bootstrap.Inputs;
using Boutquin.Curves.Bootstrap.Instruments;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Core;
using Boutquin.Curves.Core.Discounting;
using Boutquin.Curves.Core.Forwards;
using Boutquin.Curves.Interpolation;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

public sealed class RcBootstrapContractTests
{
    [Fact]
    public void CalibratedCurve_UsesRequestedDayCount()
    {
        DateOnly valuation = new(2026, 1, 2);
        CurveReference reference = new(CurveRole.Discount, Currency.USD);
        SimpleReferenceDataProvider data = new(
            [new WeekendOnlyCalendar("USNY")], BenchmarkCatalog.CreateDefault().All(),
            [InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS")]);
        CurveCalibrationSpec spec = new(new CurveName("ACT360"), reference, CurveValueType.DiscountFactor,
            "ACT/360", new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
            [new ResolvedNode("Deposit", new Tenor("1Y"), "Deposit", "USD-SOFR-OIS", reference, 0.05m)]);
        IDiscountCurve curve = (IDiscountCurve)new PiecewiseBootstrapCalibrator()
            .Calibrate(new CurveCalibrationInput(valuation, [spec], data)).CurveGroup.GetCurve(reference);
        curve.DayCount.YearFraction(valuation, valuation.AddDays(360)).Should().Be(1d);
        double terminalDf = 1d / (1d + 0.05d * 365d / 360d);
        curve.ZeroRate(valuation.AddDays(365), CompoundingConvention.Continuous)
            .Should().BeApproximately(-Math.Log(terminalDf) / (365d / 360d), 1e-13);
    }

    [Theory]
    [InlineData(double.NaN, 0.01)]
    [InlineData(double.PositiveInfinity, 0.01)]
    [InlineData(0.1, double.NaN)]
    [InlineData(0.1, double.PositiveInfinity)]
    public void HullWhite_RejectsNonFiniteParameters(double meanReversion, double volatility)
    {
        Action act = () => new HullWhiteConvexityAdjustment(meanReversion, volatility);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TenorWeeks_DoesNotWrapIntegerMultiplication()
    {
        // 613566757 * 7 wraps to 3 in unchecked Int32 arithmetic.
        Action act = () => TenorParser.AddTenor(new DateOnly(2026, 1, 2), new Tenor("613566757W"));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Calibrator_RejectsUnsupportedZeroRateStorageEvenWhenValidationBypassed(bool skipValidation)
    {
        DateOnly valuation = new(2026, 1, 2);
        CurveReference reference = new(CurveRole.Discount, Currency.USD);
        SimpleReferenceDataProvider data = new(
            [new WeekendOnlyCalendar("USNY")], BenchmarkCatalog.CreateDefault().All(),
            [InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS")]);
        CurveCalibrationSpec spec = new(new CurveName("Unsupported"), reference, CurveValueType.ZeroRate,
            "ACT/360", new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
            [new ResolvedNode("Deposit", new Tenor("1Y"), "Deposit", "USD-SOFR-OIS", reference, 0.05m)]);
        Action act = () => new PiecewiseBootstrapCalibrator().Calibrate(
            new CurveCalibrationInput(valuation, [spec], data, SkipValidation: skipValidation));
        act.Should().Throw<NotSupportedException>().WithMessage("*DiscountFactor*");
    }

    [Fact]
    public void CompatibilityOis_UsesLongestSwapMaturityRegardlessOfListOrder()
    {
        OisBootstrapInput input = new("OIS", new DateOnly(2026, 1, 31), [],
            [new OisSwapQuote(24, 0.04), new OisSwapQuote(6, 0.02)], Actual360.Instance);
        FlatDiscountCurve curve = (FlatDiscountCurve)new OisDiscountCurveBootstrapper().Bootstrap(input);
        curve.ContinuouslyCompoundedZeroRate.Should().Be(0.04);
    }

    [Fact]
    public void CompatibilityOis_UsesLongestDepositMaturityRegardlessOfListOrder()
    {
        OisBootstrapInput input = new("Deposits", new DateOnly(2026, 1, 31),
            [new DepositQuote(12, 0.04), new DepositQuote(1, 0.02)], [], Actual360.Instance);
        FlatDiscountCurve curve = (FlatDiscountCurve)new OisDiscountCurveBootstrapper().Bootstrap(input);
        curve.ContinuouslyCompoundedZeroRate.Should().Be(0.04);
    }

    [Fact]
    public void CompatibilityProjection_UsesLongestMaturityRegardlessOfListOrder()
    {
        SwapBootstrapInput input = new("Projection", new DateOnly(2026, 1, 31),
            [new InterestRateSwapQuote(24, 0.04), new InterestRateSwapQuote(6, 0.02)], Actual360.Instance);
        FlatDiscountCurve curve = (FlatDiscountCurve)new SwapProjectionCurveBootstrapper().Bootstrap(input);
        curve.ContinuouslyCompoundedZeroRate.Should().Be(0.04);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-0.01)]
    public void SingleDeposit_ZeroAndNegativeRatesMatchSimpleInterestOracle(double rate)
    {
        DateOnly valuation = new(2026, 1, 2);
        CurveReference reference = new(CurveRole.Discount, Currency.USD);
        SimpleReferenceDataProvider data = new([new WeekendOnlyCalendar("USNY")], BenchmarkCatalog.CreateDefault().All(),
            [InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS")]);
        CurveCalibrationSpec spec = new(new CurveName("boundary"), reference, CurveValueType.DiscountFactor,
            "ACT/360", new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
            [new ResolvedNode("deposit", new Tenor("1Y"), "Deposit", "USD-SOFR-OIS", reference, (decimal)rate)]);
        IDiscountCurve curve = (IDiscountCurve)new PiecewiseBootstrapCalibrator()
            .Calibrate(new CurveCalibrationInput(valuation, [spec], data)).CurveGroup.GetCurve(reference);
        curve.DiscountFactor(valuation.AddDays(365)).Should().BeApproximately(1d / (1d + rate * 365d / 360d), 1e-12);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ImpliedQuote_RejectsMissingDeclaredProjectionCurve(bool basis)
    {
        DateOnly valuation = new(2026, 1, 2);
        CurveReference reference = new(CurveRole.Discount, Currency.USD);
        CurveReference missing = new(CurveRole.Forward, Currency.USD, new BenchmarkName("missing"));
        const string code = "USD-FIXED-6M-30-360";
        SimpleReferenceDataProvider data = new([new WeekendOnlyCalendar("USNY")], BenchmarkCatalog.CreateDefault().All(),
            [InstrumentConventionRegistry.CreateDefault().GetRequired(code)]);
        ICurveGroup group = new CurveGroupBuilder(new CurveGroupName("missing"), valuation)
            .Add(reference, new FlatDiscountCurve(new CurveName("discount"), valuation, Currency.USD, 0.03)).Build();
        ResolvedNode node = new("missing", new Tenor("1Y"), basis ? "BasisSwap" : "FixedFloatSwap", code,
            reference, 0.05m, basis ? [missing, missing] : [missing]);
        AbstractInstrumentHelper helper = basis ? new BasisSwapInstrumentHelper(node, 0.05) : new FixedFloatSwapInstrumentHelper(node, 0.05);
        Action act = () => helper.ImpliedQuote(group, valuation, data);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Projection curve*not*found*");
    }

    [Fact]
    public void UnderdeterminedFirstMultiCurveNode_ExposesItsSeedResidualRatherThanAnExactFitClaim()
    {
        DateOnly valuation = new(2026, 1, 2);
        CurveReference reference = new(CurveRole.Discount, Currency.USD);
        BenchmarkName benchmark = new("projection");
        CurveReference projection = new(CurveRole.Forward, Currency.USD, benchmark);
        const string code = "USD-FIXED-6M-30-360";
        SimpleReferenceDataProvider data = new([new WeekendOnlyCalendar("USNY")], BenchmarkCatalog.CreateDefault().All(),
            [InstrumentConventionRegistry.CreateDefault().GetRequired(code)]);
        CurveGroupBuilder builder = new(new CurveGroupName("underdetermined"), valuation);
        builder.Add(projection, new FlatForwardCurve(new CurveName("projection"), valuation, Currency.USD, benchmark, 0.04));
        ResolvedNode node = new("seed", new Tenor("6M"), "FixedFloatSwap", code, reference, 0.05m, [projection]);
        FixedFloatSwapInstrumentHelper helper = new(node, 0.05);
        double seed = helper.SolveNodeValue(builder.Build(), valuation, data);
        DateOnly pillar = helper.PillarDate(valuation, data);
        builder.Add(reference, new InterpolatedDiscountCurve(new CurveName("seed"), valuation, Currency.USD, [new(pillar, seed)]));
        // Fixed PV = 0.05*tau*DF; floating PV = 0.04*tau*DF. No positive DF makes them equal.
        double implied = helper.ImpliedQuote(builder.Build(), valuation, data);
        implied.Should().BeApproximately(0.04, 1e-12);
        (implied - helper.QuoteValue).Should().BeApproximately(-0.01, 1e-12);
    }

}
