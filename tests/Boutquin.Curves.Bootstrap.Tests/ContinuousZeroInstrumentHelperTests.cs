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

using System.Reflection;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Bootstrap.Helpers;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Core;
using Boutquin.Curves.Core.Discounting;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Bootstrap.Tests;

public sealed class ContinuousZeroInstrumentHelperTests
{
    private static readonly DateOnly s_valuationDate = new(2024, 1, 12);
    private static readonly CurveReference s_reference = new(CurveRole.Discount, Currency.CAD);
    private static readonly SimpleReferenceDataProvider s_referenceData = new([], [], []);
    private static readonly Type s_helperType = typeof(PiecewiseBootstrapCalibrator).Assembly.GetType(
        "Boutquin.Curves.Bootstrap.Helpers.ContinuousZeroInstrumentHelper", throwOnError: true)!;

    // Independent 80-digit Decimal exponential values, rounded once to binary64.
    [Theory]
    [InlineData("3M", 0.04, 0.99004983374916805357390597718003655777)]
    [InlineData("6M", 0.04, 0.98019867330675530222081410422530886630)]
    [InlineData("1Y", 0.04, 0.96078943915232320943921069132324588603)]
    [InlineData("10Y", 0.04, 0.67032004603563930074443292514782607194)]
    [InlineData("30Y", 0.04, 0.30119421191220209664497760708322245997)]
    [InlineData("3M", -0.01, 1.00250312760579508497462200741078540401)]
    [InlineData("6M", -0.01, 1.00501252085940106338356624112406858073)]
    [InlineData("1Y", -0.01, 1.01005016708416805754216545690286003381)]
    [InlineData("10Y", -0.01, 1.10517091807564762481170782649024666822)]
    [InlineData("30Y", -0.01, 1.34985880757600310398374431332800733038)]
    [InlineData("3M", 0, 1)]
    [InlineData("6M", 0, 1)]
    [InlineData("1Y", 0, 1)]
    [InlineData("10Y", 0, 1)]
    [InlineData("30Y", 0, 1)]
    public void SolveAndReprice_UseIndependentNominalMaturityDiscountFactors(string tenor, double quote, double expected)
    {
        AbstractInstrumentHelper helper = CreateHelper(Node(tenor), quote);
        DateOnly pillar = helper.PillarDate(s_valuationDate, s_referenceData);

        double actual = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, s_referenceData);

        Assert.InRange(Math.Abs(actual - expected), 0d, 3e-15);
        ICurveGroup group = new CurveGroupBuilder(new CurveGroupName("continuous-zero"), s_valuationDate)
            .Add(s_reference, new InterpolatedDiscountCurve(new CurveName("CAD-Zero"), s_valuationDate,
                Currency.CAD, [new CurvePoint(pillar, expected)]))
            .Build();
        Assert.InRange(Math.Abs(helper.ImpliedQuote(group, s_valuationDate, s_referenceData) - quote), 0d, 1e-14);
    }

    [Fact]
    public void ThreeMonthGrid_UsesOneQuarterYearAcrossLeapDay()
    {
        AbstractInstrumentHelper helper = CreateHelper(Node("3M"), 0.04);
        DateOnly pillar = helper.PillarDate(s_valuationDate, s_referenceData);
        Assert.Equal(new DateOnly(2024, 4, 12), pillar);
        Assert.Equal(91, pillar.DayNumber - s_valuationDate.DayNumber);

        double actual = helper.SolveNodeValue(EmptyGroup(), s_valuationDate, s_referenceData);
        Assert.InRange(Math.Abs(actual - 0.9900498337491681), 0d, 3e-15);
        Assert.True(Math.Abs(actual - Math.Exp(-0.04 * 91d / 365d)) > 2e-5,
            "A nominal 3M quote must not be changed into an actual-calendar day-count yield.");
        Assert.True(Math.Abs(actual - 1d / (1d + 0.04 * 0.25)) > 4e-5,
            "A published continuous zero yield must not use the deposit simple-interest formula.");
    }

    [Theory]
    [InlineData("0M")]
    [InlineData("0Y")]
    [InlineData("-3M")]
    [InlineData("-1Y")]
    public void Constructor_RejectsNonPositiveNominalMaturity(string tenor)
    {
        var error = Assert.Throws<TargetInvocationException>(() => CreateHelper(Node(tenor), 0.04));
        Assert.Equal("node", Assert.IsType<ArgumentOutOfRangeException>(error.InnerException).ParamName);
    }

    [Theory]
    [InlineData("1D")]
    [InlineData("1W")]
    [InlineData("ON")]
    [InlineData("O/N")]
    [InlineData("1.5Y")]
    [InlineData("3Q")]
    [InlineData("xY")]
    [InlineData("M")]
    [InlineData("999999999999Y")]
    public void Constructor_RejectsUnsupportedTenor(string tenor)
    {
        var error = Assert.Throws<TargetInvocationException>(() => CreateHelper(Node(tenor), 0.04));
        Assert.IsType<NotSupportedException>(error.InnerException);
    }

    [Fact]
    public void Constructor_RejectsDefaultTenor()
    {
        ResolvedNode node = Node("1Y") with { Tenor = default };
        var error = Assert.Throws<TargetInvocationException>(() => CreateHelper(node, 0.04));
        Assert.IsType<NotSupportedException>(error.InnerException);
    }

    [Fact]
    public void Constructor_RejectsNullNode()
    {
        var error = Assert.Throws<TargetInvocationException>(() => CreateHelper(null, 0.04));
        Assert.Equal("node", Assert.IsType<ArgumentNullException>(error.InnerException).ParamName);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_RejectsNonFiniteQuote(double quote)
    {
        var error = Assert.Throws<TargetInvocationException>(() => CreateHelper(Node("1Y"), quote));
        Assert.Equal("quoteValue", Assert.IsType<ArgumentOutOfRangeException>(error.InnerException).ParamName);
    }

    [Theory]
    [InlineData(1000d)]
    [InlineData(-1000d)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    public void Solve_RejectsExponentialUnderflowAndOverflow(double quote)
    {
        AbstractInstrumentHelper helper = CreateHelper(Node("30Y"), quote);
        Assert.Throws<InvalidOperationException>(() => helper.SolveNodeValue(EmptyGroup(), s_valuationDate, s_referenceData));
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Reprice_RejectsInvalidDiscountFactor(double factor)
    {
        AbstractInstrumentHelper helper = CreateHelper(Node("1Y"), 0.04);
        ICurveGroup group = new CurveGroupBuilder(new CurveGroupName("invalid-zero"), s_valuationDate)
            .Add(s_reference, new FixedDiscountCurve(factor)).Build();
        Assert.Throws<InvalidOperationException>(() => helper.ImpliedQuote(group, s_valuationDate, s_referenceData));
    }

    [Fact]
    public void Reprice_ReturnsQuoteOnlyWhenDiscountCurveIsUnavailable()
    {
        AbstractInstrumentHelper helper = CreateHelper(Node("1Y"), -0.01);
        Assert.Equal(-0.01, helper.ImpliedQuote(EmptyGroup(), s_valuationDate, s_referenceData));
        Assert.Throws<ArgumentNullException>(() => helper.ImpliedQuote(null!, s_valuationDate, s_referenceData));
    }

    [Fact]
    public void Reprice_ReadsCurveInsteadOfEchoingOriginalQuote()
    {
        AbstractInstrumentHelper helper = CreateHelper(Node("1Y"), 0.04);
        ICurveGroup group = new CurveGroupBuilder(new CurveGroupName("negative-zero"), s_valuationDate)
            .Add(s_reference, new InterpolatedDiscountCurve(new CurveName("CAD-Negative"), s_valuationDate,
                Currency.CAD, [new CurvePoint(s_valuationDate.AddYears(1), 1.01005016708416805754216545690286003381)]))
            .Build();

        Assert.InRange(Math.Abs(helper.ImpliedQuote(group, s_valuationDate, s_referenceData) + 0.01), 0d, 1e-14);
    }

    [Fact]
    public void Pillar_RejectsCalendarOverflow()
    {
        AbstractInstrumentHelper helper = CreateHelper(Node("30Y"), 0.04);
        Assert.Throws<ArgumentOutOfRangeException>(() => helper.PillarDate(new DateOnly(9990, 1, 1), s_referenceData));
    }

    private static ResolvedNode Node(string tenor) => new("CAD-ZERO", new Tenor(tenor), "ContinuousZero", "CAD-CORRA-OIS", s_reference, 0.04m);

    // The helper remains an implementation detail; exercise its public base contract without a friend assembly.
    private static AbstractInstrumentHelper CreateHelper(ResolvedNode? node, double quote) =>
        Assert.IsAssignableFrom<AbstractInstrumentHelper>(Activator.CreateInstance(s_helperType, [node, quote]));

    private static ICurveGroup EmptyGroup() => new CurveGroupBuilder(new CurveGroupName("empty-zero"), s_valuationDate).Build();

    private sealed class FixedDiscountCurve(double factor) : IDiscountCurve
    {
        public CurveName Name => new("Invalid-DF");
        public DateOnly ValuationDate => s_valuationDate;
        public Currency Currency => Currency.CAD;
        public IYearFractionCalculator DayCount => Actual365Fixed.Instance;
        public double ValueAt(DateOnly date) => factor;
        public double DiscountFactor(DateOnly date) => factor;
        public double ZeroRate(DateOnly date, CompoundingConvention compounding) => 0d;
        public double InstantaneousForwardRate(DateOnly date) => 0d;
        public IDiscountCurve WithValuationDate(DateOnly newValuationDate) => this;
    }
}
