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
using Boutquin.Numerics.LinearAlgebra;
using FluentAssertions;

namespace Boutquin.Curves.Risk.Tests;

/// <summary>
/// Tests for <see cref="PcaCurveShock"/> and <see cref="PrincipalComponent"/> enum.
/// </summary>
public sealed class PcaCurveShockTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 1, 2);

    /// <summary>
    /// Creates a PcaResult via PrincipalComponentAnalysis&lt;decimal&gt;.Decompose from a diagonal
    /// rank-one covariance matrix with equal variance and perfect correlation across tenors.
    /// Its leading component has equal loadings and a one-basis-point shift at one sigma.
    /// </summary>
    private static PcaResult BuildFlatLevelPcaResult(int n)
    {
        // Covariance = 11ᵀ: eigenvalue n, unit eigenvector 1/sqrt(n), hence sqrt(lambda)*v = 1.
        var cov = new decimal[n, n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                cov[i, j] = 1m;
            }
        }
#pragma warning disable BNU0001 // PrincipalComponentAnalysis legacy facade — intentional for PcaResult (non-generic) contract test
        return PrincipalComponentAnalysis.Decompose(cov);
#pragma warning restore BNU0001
    }

    private static IReadOnlyList<Tenor> BuildTenorGrid(int n)
    {
        // Build a simple grid: 1Y, 2Y, 3Y, ... nY
        var tenors = new List<Tenor>();
        for (int i = 1; i <= n; i++)
        {
            tenors.Add(new Tenor($"{i}Y"));
        }
        return tenors;
    }

    /// <summary>
    /// AC-C5: PcaCurveShock with Level component applied to a flat discount curve
    /// produces the same one-basis-point zero-rate shift at every tenor grid point.
    /// </summary>
    [Fact]
    public void Apply_LevelComponent_ProducesParallelShift()
    {
        const int n = 4;
        var factorModel = BuildFlatLevelPcaResult(n);
        var tenorGrid = BuildTenorGrid(n);

        var shock = new PcaCurveShock(
            PrincipalComponent.Level,
            standardDeviations: 1.0m,
            factorModel,
            tenorGrid);

        var baseCurve = new FlatDiscountCurve(
            new CurveName("USD-Test"), s_valuationDate, Currency.USD, 0.03);

        var shockedCurve = shock.Apply(baseCurve);

        shockedCurve.Should().NotBeNull();
        shockedCurve.Should().BeAssignableTo<IDiscountCurve>();

        var shockedDiscount = (IDiscountCurve)shockedCurve;

        // Equal zero shifts imply tenor-dependent exponential DF ratios, not equal DF ratios.
        for (int i = 1; i <= n; i++)
        {
            DateOnly date = s_valuationDate.AddYears(i);
            double t = baseCurve.DayCount.YearFraction(s_valuationDate, date);
            double shift = -Math.Log(shockedDiscount.DiscountFactor(date) / baseCurve.DiscountFactor(date)) / t;
            Math.Abs(shift).Should().BeApproximately(0.0001, 1e-12,
                "a rank-one unit-bp covariance gives one bp per tenor at one sigma");
        }
    }

    /// <summary>
    /// AC-C6: PcaCurveShock uses Boutquin.Numerics.LinearAlgebra.PcaResult directly —
    /// no adapter layer. This test verifies by using PcaResult as the constructor parameter type.
    /// </summary>
    [Fact]
    public void Constructor_AcceptsPcaResultDirectly_NoAdapterLayer()
    {
        const int n = 3;
        // AC-C6: Use PcaResult (non-generic, decimal version) directly from Numerics namespace
#pragma warning disable BNU0001 // PrincipalComponentAnalysis legacy facade — intentional for AC-C6 no-adapter contract
        PcaResult factorModel = PrincipalComponentAnalysis.Decompose(new decimal[n, n]
        {
            { 1.0m, 0.5m, 0.2m },
            { 0.5m, 1.0m, 0.5m },
            { 0.2m, 0.5m, 1.0m }
        });
#pragma warning restore BNU0001

        var tenorGrid = BuildTenorGrid(n);

        // Should construct without any adapter/wrapper
        var shock = new PcaCurveShock(
            PrincipalComponent.Level,
            standardDeviations: 1.0m,
            factorModel,
            tenorGrid);

        shock.Should().NotBeNull();
        shock.Component.Should().Be(PrincipalComponent.Level);
    }

    [Fact]
    public void Apply_NonDiscountCurve_ReturnsCurveUnchanged()
    {
        const int n = 4;
        var factorModel = BuildFlatLevelPcaResult(n);
        var tenorGrid = BuildTenorGrid(n);

        var shock = new PcaCurveShock(
            PrincipalComponent.Level,
            standardDeviations: 1.0m,
            factorModel,
            tenorGrid);

        // Use a non-discount curve (ICurve but not IDiscountCurve)
        var nonDiscountCurve = new NonDiscountTestCurve();

        var result = shock.Apply(nonDiscountCurve);

        result.Should().BeSameAs(nonDiscountCurve,
            "non-discount curves should be returned unchanged");
    }

    [Fact]
    public void PrincipalComponent_Enum_HasExpectedValues()
    {
        ((int)PrincipalComponent.Level).Should().Be(0);
        ((int)PrincipalComponent.Slope).Should().Be(1);
        ((int)PrincipalComponent.Curvature).Should().Be(2);
    }

    [Fact]
    public void Apply_SlopeComponent_ReturnsDiscountCurve()
    {
        const int n = 4;
        var factorModel = BuildFlatLevelPcaResult(n);
        var tenorGrid = BuildTenorGrid(n);

        var shock = new PcaCurveShock(
            PrincipalComponent.Slope,
            standardDeviations: 1.0m,
            factorModel,
            tenorGrid);

        var baseCurve = new FlatDiscountCurve(
            new CurveName("USD-Test"), s_valuationDate, Currency.USD, 0.03);

        var result = shock.Apply(baseCurve);

        result.Should().BeAssignableTo<IDiscountCurve>(
            "shocked curve must implement IDiscountCurve");
    }

    // ─── T-C7-4: component index must be bounded by the eigenvector column count ──

    [Fact]
    public void Constructor_ComponentBeyondEigenvectorColumns_ShouldThrowArgumentOutOfRangeException()
    {
        // A 2-tenor PCA model has eigenvectors with only 2 columns (indices 0, 1).
        // Requesting Curvature (component index 2) reads column 2 of a width-2 matrix.
        const int n = 2;
        var factorModel = BuildFlatLevelPcaResult(n);
        var tenorGrid = BuildTenorGrid(n);

        Action act = () => new PcaCurveShock(
            PrincipalComponent.Curvature,
            standardDeviations: 1.0m,
            factorModel,
            tenorGrid);

        act.Should().Throw<ArgumentOutOfRangeException>(
            because: "Curvature (column 2) is out of range for a 2-column eigenvector model and must fail loudly, not throw a raw IndexOutOfRangeException");
    }

    [Fact]
    public void Constructor_UndefinedComponentEnum_ShouldThrowArgumentOutOfRangeException()
    {
        const int n = 4;
        var factorModel = BuildFlatLevelPcaResult(n);
        var tenorGrid = BuildTenorGrid(n);

        Action act = () => new PcaCurveShock(
            (PrincipalComponent)999,
            standardDeviations: 1.0m,
            factorModel,
            tenorGrid);

        act.Should().Throw<ArgumentOutOfRangeException>(
            because: "an undefined PrincipalComponent value must be rejected with a clear exception");
    }

    /// <summary>
    /// A minimal ICurve that is NOT IDiscountCurve, for testing the pass-through path.
    /// </summary>
    private sealed class NonDiscountTestCurve : ICurve
    {
        public CurveName Name => new("NonDiscount");
        public DateOnly ValuationDate => new(2026, 1, 2);
        public Currency Currency => Currency.USD;
        public Boutquin.MarketData.DayCount.IYearFractionCalculator DayCount =>
            Boutquin.MarketData.DayCount.Actual365Fixed.Instance;
        public double ValueAt(DateOnly date) => 0.0;
    }
}
