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
using Boutquin.Curves.Bootstrap;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Recipes.Testing;
using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.MarketData.Conventions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Curves.Recipes.Tests;

/// <summary>
/// Golden integration tests that exercise the full pipeline:
/// FixtureData -> CurveBuilder -> PiecewiseBootstrapCalibrator -> calibrated discount curves.
/// </summary>
public sealed class GoldenCurveTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);

    // Tenor-to-date lookup for standard tenors relative to the valuation date.
    private static DateOnly TenorDate(string tenor) => tenor switch
    {
        "1Y" => s_valuationDate.AddYears(1),
        "2Y" => s_valuationDate.AddYears(2),
        "3Y" => s_valuationDate.AddYears(3),
        "5Y" => s_valuationDate.AddYears(5),
        "7Y" => s_valuationDate.AddYears(7),
        "10Y" => s_valuationDate.AddYears(10),
        "30Y" => s_valuationDate.AddYears(30),
        _ => throw new ArgumentException($"Unsupported tenor: {tenor}")
    };

    /// <summary>
    /// Calibrates a discount curve from a recipe using the full fixture pipeline.
    /// </summary>
    private static async Task<IDiscountCurve> CalibrateDiscountCurveAsync(
        CurveGroupRecipe recipe,
        CurrencyCode currency,
        BenchmarkName benchmark)
    {
        var pipeline = FixtureData.CreatePipeline();

        var calendars = HolidayCalendarFactory.SupportedCodes.Select(HolidayCalendarFactory.Create).ToArray();
        var benchmarks = new[]
        {
            new RateBenchmark(new BenchmarkName("SOFR"), CurrencyCode.USD, BenchmarkKind.OvernightRiskFree, null, 0, 1, "USNY", "ACT/360", true),
            new RateBenchmark(new BenchmarkName("CORRA"), CurrencyCode.CAD, BenchmarkKind.OvernightRiskFree, null, 0, 1, "CATO", "ACT/365F", true),
            new RateBenchmark(new BenchmarkName("SONIA"), CurrencyCode.GBP, BenchmarkKind.OvernightRiskFree, null, 0, 0, "GBLO", "ACT/365F", true),
            new RateBenchmark(new BenchmarkName("ESTR"), CurrencyCode.EUR, BenchmarkKind.OvernightRiskFree, null, 0, 1, "TARGET", "ACT/360", true),
        };
        var registry = InstrumentConventionRegistry.CreateDefault();
        var conventions = new[]
        {
            registry.GetRequired("USD-SOFR-OIS"),
            registry.GetRequired("CAD-CORRA-OIS"),
            registry.GetRequired("GBP-SONIA-OIS"),
            registry.GetRequired("EUR-ESTR-OIS"),
            registry.GetRequired("USD-FIXED-6M-30-360"),
            registry.GetRequired("GBP-FIXED-1Y-ACT-365F"),
            registry.GetRequired("EUR-FIXED-1Y-ACT-360"),
        };
        var referenceData = new SimpleReferenceDataProvider(calendars, benchmarks, conventions);

        var calibrator = new PiecewiseBootstrapCalibrator();
        var logger = NullLoggerFactory.Instance.CreateLogger<CurveBuilder>();
        var builder = new CurveBuilder(pipeline, calibrator, referenceData, logger);

        CurveSnapshot snapshot = await builder.BuildAsync(recipe, s_valuationDate);

        var curveRef = new CurveReference(CurveRole.Discount, currency, benchmark);
        ICurve curve = snapshot.CurveGroup.GetCurve(curveRef);

        return curve as IDiscountCurve
            ?? throw new InvalidCastException(
                $"Curve for {currency}/{benchmark} does not implement IDiscountCurve.");
    }

    // -------------------------------------------------------------------------
    //  USD SOFR
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UsdSofr_DiscountFactor_AtValuation_Is_One()
    {
        // Arrange
        var curve = await CalibrateDiscountCurveAsync(
            StandardCurveRecipes.UsdSofrDiscount(),
            CurrencyCode.USD,
            new BenchmarkName("SOFR"));

        // Act
        double df = curve.DiscountFactor(s_valuationDate);

        // Assert
        df.Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public async Task UsdSofr_DiscountFactors_Are_Positive_And_Decreasing()
    {
        // Arrange
        var curve = await CalibrateDiscountCurveAsync(
            StandardCurveRecipes.UsdSofrDiscount(),
            CurrencyCode.USD,
            new BenchmarkName("SOFR"));
        string[] tenors = ["1Y", "2Y", "3Y", "5Y", "7Y", "10Y", "30Y"];

        // Act
        double[] dfs = tenors.Select(t => curve.DiscountFactor(TenorDate(t))).ToArray();

        // Assert
        for (int i = 0; i < dfs.Length; i++)
        {
            dfs[i].Should().BePositive($"DF at {tenors[i]} should be positive");
            dfs[i].Should().BeLessThan(1.0, $"DF at {tenors[i]} should be less than 1");
        }

        for (int i = 1; i < dfs.Length; i++)
        {
            dfs[i].Should().BeLessThan(dfs[i - 1],
                $"DF at {tenors[i]} should be less than DF at {tenors[i - 1]}");
        }
    }

    [Fact]
    public async Task UsdSofr_ZeroRates_Are_Positive()
    {
        // Arrange
        var curve = await CalibrateDiscountCurveAsync(
            StandardCurveRecipes.UsdSofrDiscount(),
            CurrencyCode.USD,
            new BenchmarkName("SOFR"));
        string[] tenors = ["1Y", "2Y", "3Y", "5Y", "7Y", "10Y", "30Y"];

        // Act & Assert
        foreach (string tenor in tenors)
        {
            double zero = curve.ZeroRate(TenorDate(tenor), CompoundingConvention.Continuous);
            zero.Should().BeGreaterThan(0.0, $"zero rate at {tenor} should be positive");
            zero.Should().BeLessThan(0.20, $"zero rate at {tenor} should be below 20%");
        }
    }

    // -------------------------------------------------------------------------
    //  CAD CORRA
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CadCorra_DiscountFactor_AtValuation_Is_One()
    {
        // Arrange
        var curve = await CalibrateDiscountCurveAsync(
            StandardCurveRecipes.CadCorraDiscount(),
            CurrencyCode.CAD,
            new BenchmarkName("CORRA"));

        // Act
        double df = curve.DiscountFactor(s_valuationDate);

        // Assert
        df.Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public async Task CadCorra_DiscountFactors_Are_Positive_And_Decreasing()
    {
        // Arrange
        var curve = await CalibrateDiscountCurveAsync(
            StandardCurveRecipes.CadCorraDiscount(),
            CurrencyCode.CAD,
            new BenchmarkName("CORRA"));
        string[] tenors = ["1Y", "2Y", "3Y", "5Y", "7Y", "10Y", "30Y"];

        // Act
        double[] dfs = tenors.Select(t => curve.DiscountFactor(TenorDate(t))).ToArray();

        // Assert
        for (int i = 0; i < dfs.Length; i++)
        {
            dfs[i].Should().BePositive($"DF at {tenors[i]} should be positive");
            dfs[i].Should().BeLessThan(1.0, $"DF at {tenors[i]} should be less than 1");
        }

        for (int i = 1; i < dfs.Length; i++)
        {
            dfs[i].Should().BeLessThan(dfs[i - 1],
                $"DF at {tenors[i]} should be less than DF at {tenors[i - 1]}");
        }
    }

    // -------------------------------------------------------------------------
    //  GBP SONIA
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GbpSonia_DiscountFactor_AtValuation_Is_One()
    {
        // Arrange
        var curve = await CalibrateDiscountCurveAsync(
            StandardCurveRecipes.GbpSoniaDiscount(),
            CurrencyCode.GBP,
            new BenchmarkName("SONIA"));

        // Act
        double df = curve.DiscountFactor(s_valuationDate);

        // Assert
        df.Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public async Task GbpSonia_DiscountFactors_Are_Positive_And_Decreasing()
    {
        // Arrange
        var curve = await CalibrateDiscountCurveAsync(
            StandardCurveRecipes.GbpSoniaDiscount(),
            CurrencyCode.GBP,
            new BenchmarkName("SONIA"));
        string[] tenors = ["1Y", "2Y", "3Y", "5Y", "7Y", "10Y", "30Y"];

        // Act
        double[] dfs = tenors.Select(t => curve.DiscountFactor(TenorDate(t))).ToArray();

        // Assert
        for (int i = 0; i < dfs.Length; i++)
        {
            dfs[i].Should().BePositive($"DF at {tenors[i]} should be positive");
            dfs[i].Should().BeLessThan(1.0, $"DF at {tenors[i]} should be less than 1");
        }

        for (int i = 1; i < dfs.Length; i++)
        {
            dfs[i].Should().BeLessThan(dfs[i - 1],
                $"DF at {tenors[i]} should be less than DF at {tenors[i - 1]}");
        }
    }

    // -------------------------------------------------------------------------
    //  EUR ESTR
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EurEstr_DiscountFactor_AtValuation_Is_One()
    {
        // Arrange
        var curve = await CalibrateDiscountCurveAsync(
            StandardCurveRecipes.EurEstrDiscount(),
            CurrencyCode.EUR,
            new BenchmarkName("ESTR"));

        // Act
        double df = curve.DiscountFactor(s_valuationDate);

        // Assert
        df.Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public async Task EurEstr_DiscountFactors_Are_Positive_And_Decreasing()
    {
        // Arrange
        var curve = await CalibrateDiscountCurveAsync(
            StandardCurveRecipes.EurEstrDiscount(),
            CurrencyCode.EUR,
            new BenchmarkName("ESTR"));
        string[] tenors = ["1Y", "2Y", "3Y", "5Y", "7Y", "10Y", "30Y"];

        // Act
        double[] dfs = tenors.Select(t => curve.DiscountFactor(TenorDate(t))).ToArray();

        // Assert
        for (int i = 0; i < dfs.Length; i++)
        {
            dfs[i].Should().BePositive($"DF at {tenors[i]} should be positive");
            dfs[i].Should().BeLessThan(1.0, $"DF at {tenors[i]} should be less than 1");
        }

        for (int i = 1; i < dfs.Length; i++)
        {
            dfs[i].Should().BeLessThan(dfs[i - 1],
                $"DF at {tenors[i]} should be less than DF at {tenors[i - 1]}");
        }
    }

    // -------------------------------------------------------------------------
    //  Cross-currency forward consistency
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("USD", "SOFR")]
    [InlineData("CAD", "CORRA")]
    [InlineData("GBP", "SONIA")]
    [InlineData("EUR", "ESTR")]
    public async Task AllCurrencies_ForwardConsistency_DF1Y_Times_DF2Y_Over_DF1Y_Positive(
        string currencyCode, string benchmarkCode)
    {
        // Arrange
        var currency = Enum.Parse<CurrencyCode>(currencyCode, ignoreCase: true);
        var benchmark = new BenchmarkName(benchmarkCode);
        CurveGroupRecipe recipe = (currencyCode, benchmarkCode) switch
        {
            ("USD", "SOFR") => StandardCurveRecipes.UsdSofrDiscount(),
            ("CAD", "CORRA") => StandardCurveRecipes.CadCorraDiscount(),
            ("GBP", "SONIA") => StandardCurveRecipes.GbpSoniaDiscount(),
            ("EUR", "ESTR") => StandardCurveRecipes.EurEstrDiscount(),
            _ => throw new ArgumentException($"Unknown pair: {currencyCode}/{benchmarkCode}")
        };

        var curve = await CalibrateDiscountCurveAsync(recipe, currency, benchmark);

        // Act
        double df1Y = curve.DiscountFactor(TenorDate("1Y"));
        double df2Y = curve.DiscountFactor(TenorDate("2Y"));
        double forwardDf = df2Y / df1Y;

        // Assert — forward DF from 1Y to 2Y should be in (0, 1)
        forwardDf.Should().BeGreaterThan(0.0, $"{currencyCode} forward DF should be positive");
        forwardDf.Should().BeLessThan(1.0, $"{currencyCode} forward DF should be less than 1");
    }

    // -------------------------------------------------------------------------
    //  DI resolution
    // -------------------------------------------------------------------------

    [Fact]
    public void CurveBuilder_Resolves_From_DI()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddAnalytics();
        services.AddSingleton<IDataPipeline>(_ => FixtureData.CreatePipeline());
        services.AddLogging();

        using var provider = services.BuildServiceProvider();

        // Act
        var builder = provider.GetRequiredService<CurveBuilder>();

        // Assert
        builder.Should().NotBeNull();
    }
}
