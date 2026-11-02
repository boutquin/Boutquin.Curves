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
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.Calendars;
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
        Currency currency,
        BenchmarkName benchmark)
    {
        var pipeline = FixtureData.CreatePipeline();

        var calendars = HolidayCalendarFactory.SupportedCodes.Select(HolidayCalendarFactory.Create).ToArray();
        var benchmarks = new[]
        {
            new RateBenchmark(new BenchmarkName("SOFR"), Currency.USD, BenchmarkKind.OvernightRiskFree, null, 0, 1, "USNY", "ACT/360", true),
            new RateBenchmark(new BenchmarkName("CORRA"), Currency.CAD, BenchmarkKind.OvernightRiskFree, null, 0, 1, "CATO", "ACT/365F", true),
            new RateBenchmark(new BenchmarkName("SONIA"), Currency.GBP, BenchmarkKind.OvernightRiskFree, null, 0, 0, "GBLO", "ACT/365F", true),
            new RateBenchmark(new BenchmarkName("ESTR"), Currency.EUR, BenchmarkKind.OvernightRiskFree, null, 0, 1, "TARGET", "ACT/360", true),
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
        snapshot.Diagnostics.Repricing.Should().NotBeEmpty();
        snapshot.Diagnostics.Repricing.Should().AllSatisfy(item => item.AbsoluteError.Should().BeLessThan(1e-10));

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
            Currency.USD,
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
            Currency.USD,
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
            Currency.USD,
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
            Currency.CAD,
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
            Currency.CAD,
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
            Currency.GBP,
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
            Currency.GBP,
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
            Currency.EUR,
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
            Currency.EUR,
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
        var currency = Enum.Parse<Currency>(currencyCode, ignoreCase: true);
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
    //  Numeric golden pins — oracle-verified discount factors / zero rates
    //
    //  These guard the calibrator's numeric output against silent drift (CF-C5-5 /
    //  F-L4-5). The prior golden tests asserted only structural invariants (T=0
    //  identity, positivity, monotonicity) which pass even with a ~10% interpolation
    //  or day-count error. The pins below are tight (1e-9) so any numeric regression
    //  in the bootstrap, interpolation, or day-count path surfaces immediately.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Golden zero-rate pins for the SONIA OIS/swap discount curve, re-pinned at M-3. Before the
    /// par-annuity bootstrap, an OIS quoting a zero rate directly reproduced that rate exactly under
    /// the <c>exp(-rate*tau)</c> collapse. The par-annuity solve discounts a multi-period annuity, so
    /// the continuously-compounded zero rate at each pillar is the (deterministic) calibrated output
    /// rather than the input par quote. These tight (1e-9) pins guard the bootstrap / interpolation /
    /// day-count numeric path against silent drift.
    /// </summary>
    [Theory]
    [InlineData("1Y", 0.033918218203)]
    [InlineData("2Y", 0.032442583653)]
    [InlineData("5Y", 0.032268263240166)]
    [InlineData("10Y", 0.033535860615717)]
    [InlineData("30Y", 0.034646850490273)]
    public async Task GbpSonia_ZeroRate_MatchesGoldenPin(string tenor, double expectedZeroRate)
    {
        // Arrange
        var curve = await CalibrateDiscountCurveAsync(
            StandardCurveRecipes.GbpSoniaDiscount(),
            Currency.GBP,
            new BenchmarkName("SONIA"));

        // Act
        double zero = curve.ZeroRate(TenorDate(tenor), CompoundingConvention.Continuous);

        // Assert — calibrated zero rate matches the oracle-verified golden pin.
        zero.Should().BeApproximately(expectedZeroRate, 1e-9);
    }

    /// <summary>
    /// Drift-guard golden pins: calibrated discount factors and continuously-compounded
    /// zero rates at representative tenors across all four standard currency curves, pinned
    /// to oracle values to 1e-9. Any silent change in the bootstrap / interpolation / day-count
    /// numeric path moves these and fails the test deliberately.
    /// </summary>
    [Theory]
    // Coupon-curve pins use an independent 80-digit Decimal par-equation solve with bisection
    // and explicit log-linear discount-factor arithmetic over the actual coupon schedules.
    // Sparse coupons participate in the trial solve. Curve zeros use their declared day-count axes;
    // CAD government points use the Bank of Canada nominal-tenor continuous-zero formula.
    // USD SOFR
    [InlineData("USD", "SOFR", "1Y", 0.959155942770000, 0.041130352649727)]
    [InlineData("USD", "SOFR", "2Y", 0.933770056103558, 0.033746953381697)]
    [InlineData("USD", "SOFR", "10Y", 0.657483726443046, 0.041325128858925)]
    [InlineData("USD", "SOFR", "30Y", 0.259590612812320, 0.044306789907084)]
    // CAD CORRA
    [InlineData("CAD", "CORRA", "1Y", 0.971222200600746, 0.029200000000000)]
    [InlineData("CAD", "CORRA", "10Y", 0.708220353467800, 0.034471667122913)]
    [InlineData("CAD", "CORRA", "30Y", 0.344727854767220, 0.035474082861836)]
    // GBP SONIA
    [InlineData("GBP", "SONIA", "1Y", 0.966650555824, 0.033918218203)]
    [InlineData("GBP", "SONIA", "10Y", 0.714884531762821, 0.033535860615717)]
    [InlineData("GBP", "SONIA", "30Y", 0.353396402888315, 0.034646850490273)]
    // EUR ESTR
    [InlineData("EUR", "ESTR", "1Y", 0.972586566959000, 0.027415422821843)]
    [InlineData("EUR", "ESTR", "10Y", 0.758655295954354, 0.027220036599705)]
    [InlineData("EUR", "ESTR", "30Y", 0.422585418660877, 0.028298131427147)]
    public async Task AllCurrencies_GoldenDiscountFactorAndZeroRate_ArePinned(
        string currencyCode, string benchmarkCode, string tenor, double expectedDf, double expectedZero)
    {
        // Arrange
        var currency = Enum.Parse<Currency>(currencyCode, ignoreCase: true);
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
        double df = curve.DiscountFactor(TenorDate(tenor));
        double zero = curve.ZeroRate(TenorDate(tenor), CompoundingConvention.Continuous);

        // Assert — pinned to oracle values; tight tolerance guards against silent drift.
        df.Should().BeApproximately(expectedDf, 1e-9,
            $"{currencyCode} DF at {tenor} should match the golden value");
        zero.Should().BeApproximately(expectedZero, 1e-9,
            $"{currencyCode} zero rate at {tenor} should match the golden value");
    }

    // -------------------------------------------------------------------------
    //  CHF SARON
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ChfSaron_DiscountFactor_AtValuation_Is_One()
    {
        // Arrange
        var curve = await CalibrateChfJpyDiscountCurveAsync(
            StandardCurveRecipes.ChfSaronDiscount(),
            Currency.CHF,
            new BenchmarkName("SARON"));

        // Act
        double df = curve.DiscountFactor(s_valuationDate);

        // Assert
        df.Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public async Task ChfSaron_DiscountFactors_Are_Positive_And_Decreasing()
    {
        // Arrange
        var curve = await CalibrateChfJpyDiscountCurveAsync(
            StandardCurveRecipes.ChfSaronDiscount(),
            Currency.CHF,
            new BenchmarkName("SARON"));
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
    //  JPY TONA
    // -------------------------------------------------------------------------

    [Fact]
    public async Task JpyTona_DiscountFactor_AtValuation_Is_One()
    {
        // Arrange
        var curve = await CalibrateChfJpyDiscountCurveAsync(
            StandardCurveRecipes.JpyTonaDiscount(),
            Currency.JPY,
            new BenchmarkName("TONA"));

        // Act
        double df = curve.DiscountFactor(s_valuationDate);

        // Assert
        df.Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public async Task JpyTona_DiscountFactors_Are_Positive_And_Decreasing()
    {
        // Arrange
        var curve = await CalibrateChfJpyDiscountCurveAsync(
            StandardCurveRecipes.JpyTonaDiscount(),
            Currency.JPY,
            new BenchmarkName("TONA"));
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

    /// <summary>
    /// Calibrates a discount curve for CHF SARON or JPY TONA using the full fixture pipeline.
    /// Extends the reference data provider with CHF and JPY benchmarks and conventions.
    /// </summary>
    private static async Task<IDiscountCurve> CalibrateChfJpyDiscountCurveAsync(
        CurveGroupRecipe recipe,
        Currency currency,
        BenchmarkName benchmark)
    {
        var pipeline = FixtureData.CreatePipeline();

        var calendars = HolidayCalendarFactory.SupportedCodes.Select(HolidayCalendarFactory.Create).ToArray();
        var benchmarks = new[]
        {
            new RateBenchmark(new BenchmarkName("SOFR"), Currency.USD, BenchmarkKind.OvernightRiskFree, null, 0, 1, "USNY", "ACT/360", true),
            new RateBenchmark(new BenchmarkName("CORRA"), Currency.CAD, BenchmarkKind.OvernightRiskFree, null, 0, 1, "CATO", "ACT/365F", true),
            new RateBenchmark(new BenchmarkName("SONIA"), Currency.GBP, BenchmarkKind.OvernightRiskFree, null, 0, 0, "GBLO", "ACT/365F", true),
            new RateBenchmark(new BenchmarkName("ESTR"), Currency.EUR, BenchmarkKind.OvernightRiskFree, null, 0, 1, "TARGET", "ACT/360", true),
            new RateBenchmark(new BenchmarkName("SARON"), Currency.CHF, BenchmarkKind.OvernightRiskFree, null, 0, 0, "CHZU", "ACT/360", true),
            new RateBenchmark(new BenchmarkName("TONA"), Currency.JPY, BenchmarkKind.OvernightRiskFree, null, 0, 1, "JPTO", "ACT/365F", true),
        };
        var registry = InstrumentConventionRegistry.CreateDefault();
        var conventions = new IInstrumentConvention[]
        {
            registry.GetRequired("USD-SOFR-OIS"),
            registry.GetRequired("CAD-CORRA-OIS"),
            registry.GetRequired("GBP-SONIA-OIS"),
            registry.GetRequired("EUR-ESTR-OIS"),
            registry.GetRequired("USD-FIXED-6M-30-360"),
            registry.GetRequired("GBP-FIXED-1Y-ACT-365F"),
            registry.GetRequired("EUR-FIXED-1Y-ACT-360"),
            // CHF SARON and JPY TONA conventions not yet in CreateDefault() — constructed inline.
            new OisConvention("CHF-SARON-OIS", "CHZU", "ACT/360", PaymentFrequency.Annual, BusinessDayAdjustment.ModifiedFollowing, 2, 2, ObservationShiftConvention.Shifted),
            new OisConvention("JPY-TONA-OIS", "JPTO", "ACT/365F", PaymentFrequency.Annual, BusinessDayAdjustment.ModifiedFollowing, 2, 2, ObservationShiftConvention.Shifted),
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
