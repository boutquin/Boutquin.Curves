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
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using Boutquin.Numerics.LinearAlgebra;
using FluentAssertions;

namespace Boutquin.Curves.Risk.Tests;

public sealed class RcNumericalOracleTests
{
    private static readonly DateOnly s_valuation = new(2026, 1, 2);

    // Exact eigensystem of covariance [[9,12],[12,16]] in bp²: lambda=25, v=(0.6,0.8).
    private static PcaResult Model(decimal variance = 25m) => new()
    {
        Eigenvalues = [variance, 0m],
        Eigenvectors = new decimal[,] { { 0.6m, -0.8m }, { 0.8m, 0.6m } },
        ExplainedVarianceRatio = [1m, 0m],
        CumulativeExplainedVariance = [1m, 1m],
        Mean = []
    };

    private static IDiscountCurve Shock(PcaResult model, params Tenor[] grid) =>
        (IDiscountCurve)new PcaCurveShock(PrincipalComponent.Level, 2m, model, grid)
            .Apply(new FlatDiscountCurve(new CurveName("PCA oracle"), s_valuation, Currency.USD, 0.03));

    [Fact]
    public void PcaShock_UsesCovarianceStandardDeviation()
    {
        IDiscountCurve curve = Shock(Model(), new Tenor("1Y"), new Tenor("3Y"));
        // Two sigmas: 2 * sqrt(25) * 0.6 = 6 bp at one year.
        curve.ZeroRate(s_valuation.AddDays(365), CompoundingConvention.Continuous)
            .Should().BeApproximately(0.0306, 1e-13);
    }

    [Fact]
    public void StandardizedPcaShock_RestoresPerVariableBasisPointUnits()
    {
        PcaResult model = Model() with { StandardDeviations = [2m, 3m] };
        IDiscountCurve curve = Shock(model, new Tenor("1Y"), new Tenor("3Y"));
        curve.ZeroRate(s_valuation.AddDays(365), CompoundingConvention.Continuous)
            .Should().BeApproximately(0.0312, 1e-13);
    }

    [Fact]
    public void ParallelShock_WithExpressionCannotBypassFiniteInvariant()
    {
        ParallelZeroRateShock valid = new("base", 1d);
        Action act = () => _ = valid with { ShiftInBasisPoints = double.NaN };
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ZeroCouponRisk_AgreesWithClosedForm()
    {
        FlatDiscountCurve curve = new(new CurveName("zero coupon"), s_valuation, Currency.USD, 0.03);
        CashFlow[] flows = [new(s_valuation.AddDays(1825), 100m, Currency.USD)];
        curve.MacaulayDuration(flows, s_valuation).Should().BeApproximately(5d, 1e-13);
        curve.Convexity(flows, s_valuation).Should().BeApproximately(25d, 1e-12);
        // Independent exponential repricing of a 5Y 100-unit payment under +1bp.
        double expected = 100d * (Math.Exp(-0.0301d * 5d) - Math.Exp(-0.03d * 5d));
        ((double)curve.Dv01(flows, s_valuation)).Should().BeApproximately(expected, 1e-12);
    }

    [Fact]
    public void PcaShock_ZeroVarianceProducesNoShock()
    {
        IDiscountCurve curve = Shock(Model(0m), new Tenor("1Y"), new Tenor("3Y"));
        curve.ZeroRate(s_valuation.AddDays(365), CompoundingConvention.Continuous)
            .Should().BeApproximately(0.03, 1e-13);
    }

    [Fact]
    public void PcaForward_EqualsDerivativeOfItsOwnLogDiscountFactor()
    {
        IDiscountCurve curve = Shock(Model(), new Tenor("1Y"), new Tenor("3Y"));
        DateOnly date = s_valuation.AddDays(730);
        double derivative = -Math.Log(curve.DiscountFactor(date.AddDays(1)) /
            curve.DiscountFactor(date.AddDays(-1))) / (2d / 365d);
        curve.InstantaneousForwardRate(date).Should().BeApproximately(derivative, 1e-11);
        // At t=2, shift=7bp and t*shift'=2bp, so the forward shift is 9bp.
        curve.InstantaneousForwardRate(date).Should().BeApproximately(0.0309, 1e-13);
    }

    [Theory]
    [InlineData("3Y", "1Y")]
    [InlineData("1Y", "12M")]
    [InlineData("1Y", "bad")]
    [InlineData("0Y", "3Y")]
    [InlineData("-1Y", "3Y")]
    public void PcaShock_RejectsInvalidOrUnorderedTenorGrid(string first, string second)
    {
        Action act = () => Shock(Model(), new Tenor(first), new Tenor(second));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PcaShock_RejectsNegativeVariance()
    {
        Action act = () => Shock(Model(-1m), new Tenor("1Y"), new Tenor("3Y"));
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ParallelShock_RejectsNonFiniteShift(double shift)
    {
        Action act = () => new ParallelZeroRateShock("invalid", shift)
            .Apply(new FlatDiscountCurve(new CurveName("base"), s_valuation, Currency.USD, 0.03));
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0d, 1d)]
    [InlineData(-1d, 1d)]
    [InlineData(double.NaN, 1d)]
    [InlineData(1d, double.PositiveInfinity)]
    public void BucketedShock_RejectsInvalidPillars(double maturity, double shift)
    {
        Action act = () => new BucketedZeroRateShock("invalid", [new(maturity, shift)]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BucketedShock_RejectsDuplicateMaturities()
    {
        Action act = () => new BucketedZeroRateShock("duplicate", [new(1d, 1d), new(1d, 2d)]);
        act.Should().Throw<ArgumentException>();
    }
    [Fact]
    public void RiskReport_PublicSensitivityViewCannotMutateTheSnapshot()
    {
        RiskReport report = new("report", "scenario", [new BucketedSensitivity("1Y", 0.1)]);
        Action act = () => ((IList<BucketedSensitivity>)report.Sensitivities)[0] = new("changed", 99d);
        act.Should().Throw<NotSupportedException>();
        report.Sensitivities[0].Delta.Should().Be(0.1);
    }

    [Fact]
    public void ModifiedDuration_UsesLongestMaturityAndDoesNotDependOnCashFlowEnumeration()
    {
        InterpolatedDiscountCurve curve = new(new CurveName("nonflat"), s_valuation, Currency.USD,
            [new(s_valuation.AddDays(365), 0.99), new(s_valuation.AddDays(1825), 0.80)]);
        CashFlow[] ordered = [new(s_valuation.AddDays(365), 5m, Currency.USD), new(s_valuation.AddDays(1825), 105m, Currency.USD)];
        double expected = curve.MacaulayDuration(ordered, s_valuation) / Math.Exp(-Math.Log(0.8) / 5d / 2d);
        curve.ModifiedDuration(ordered, s_valuation, 2).Should().BeApproximately(expected, 1e-12);
        curve.ModifiedDuration(ordered.Reverse().ToArray(), s_valuation, 2).Should().BeApproximately(expected, 1e-12);
    }

    [Fact]
    public void EmptyBucketProfile_IsIdentityAndLinearProfileMatchesAffineZeroShift()
    {
        FlatDiscountCurve curve = new(new CurveName("bucket oracle"), s_valuation, Currency.USD, 0.03);
        new BucketedZeroRateShock("identity", []).Apply(curve).Should().BeSameAs(curve);
        IDiscountCurve shifted = (IDiscountCurve)new BucketedZeroRateShock("affine", [new(1d, 10d), new(3d, 30d)]).Apply(curve);
        foreach ((int days, double shift) in new[] { (180, 0.001), (365, 0.001), (730, 0.002), (1095, 0.003), (1460, 0.003) })
        {
            shifted.DiscountFactor(s_valuation.AddDays(days)).Should().BeApproximately(Math.Exp(-(0.03 + shift) * days / 365d), 1e-12);
        }
    }

}
