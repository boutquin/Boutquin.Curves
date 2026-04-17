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
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Interpolation;
using Boutquin.Curves.Recipes.Nodes;
using Boutquin.Curves.Recipes.Testing;
using Boutquin.MarketData.Abstractions.Diagnostics;
using Boutquin.MarketData.Abstractions.Provenance;
using Boutquin.MarketData.Abstractions.Records;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Abstractions.Results;
using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.MarketData.Conventions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Curves.Recipes.Tests;

/// <summary>
/// Tests that CurveBuilder correctly surfaces UNEXPECTED_GAP issues from adapters
/// and escalates RATE_MISSING severity when gap issues are present.
/// </summary>
public sealed class CurveBuilderGapTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly CurveReference s_usdSofrDisc = new(CurveRole.Discount, CurrencyCode.USD, new BenchmarkName("SOFR"));

    private static readonly InterpolationSettings s_interpolation =
        new(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward");

    /// <summary>
    /// When the pipeline returns UNEXPECTED_GAP issues and no rate can be extracted,
    /// the RATE_MISSING issue should be escalated to Error severity.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WithUnexpectedGap_EscalatesRateMissingSeverityToError()
    {
        // Arrange -- pipeline returns an UNEXPECTED_GAP issue and empty SOFR records
        var gapIssue = new DataIssue(IssueCode.UnexpectedGap, IssueSeverity.Warning,
            "Missing data for 2026-04-08 -- expected business day observation.");
        var sofrEnvelope = new DataEnvelope<IReadOnlyList<ScalarObservation>>(
            Array.Empty<ScalarObservation>(),
            new DataCoverage(1, 0, 1, 0.0m),
            new[] { gapIssue },
            new[]
            {
                new DataProvenance(new ProviderCode("TestSource"), "SOFR", LicenseType.Free, RetrievalMode.Cache,
                    FreshnessClass.Stale, DateTimeOffset.UtcNow, null)
            });

        // OIS quotes succeed so the curve has enough nodes to calibrate
        var oisEnvelope = new DataEnvelope<IReadOnlyList<YieldCurveQuote>>(
            new[]
            {
                new YieldCurveQuote("1M", 0.0425m),
                new YieldCurveQuote("3M", 0.0430m),
                new YieldCurveQuote("6M", 0.0432m),
                new YieldCurveQuote("1Y", 0.0435m),
            },
            new DataCoverage(4, 4, 0, 1.0m),
            Array.Empty<DataIssue>(),
            new[]
            {
                new DataProvenance(new ProviderCode("TestSource"), "CME-USD-SOFR-OIS", LicenseType.Free, RetrievalMode.Cache,
                    FreshnessClass.Live, DateTimeOffset.UtcNow, null)
            });

        var pipeline = new FakeDataPipeline()
            .Register("SOFR", sofrEnvelope)
            .Register("CME-USD-SOFR-OIS", oisEnvelope);

        var builder = CreateBuilder(pipeline);
        var recipe = CreateMixedNodeRecipe();

        // Act
        var snapshot = await builder.BuildAsync(recipe, s_valuationDate);

        // Assert -- UNEXPECTED_GAP should be preserved in issues
        snapshot.DataIssues.Should().Contain(i => i.Code == IssueCode.UnexpectedGap);

        // RATE_MISSING for the SOFR node should be escalated to Error
        var rateMissing = snapshot.DataIssues.FirstOrDefault(i =>
            i.Code == new IssueCode("RATE_MISSING") && i.Message.Contains("SOFR"));
        rateMissing.Should().NotBeNull();
        rateMissing!.Severity.Should().Be(IssueSeverity.Error);
    }

    /// <summary>
    /// When no UNEXPECTED_GAP issues are present and a node has no rate,
    /// RATE_MISSING should remain at Warning severity.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WithoutGap_RateMissingIsWarning()
    {
        // Arrange -- SOFR returns empty records but no gap issues
        var sofrEnvelope = new DataEnvelope<IReadOnlyList<ScalarObservation>>(
            Array.Empty<ScalarObservation>(),
            new DataCoverage(1, 0, 1, 0.0m),
            Array.Empty<DataIssue>(),
            new[]
            {
                new DataProvenance(new ProviderCode("TestSource"), "SOFR", LicenseType.Free, RetrievalMode.Cache,
                    FreshnessClass.Stale, DateTimeOffset.UtcNow, null)
            });

        var oisEnvelope = new DataEnvelope<IReadOnlyList<YieldCurveQuote>>(
            new[]
            {
                new YieldCurveQuote("1M", 0.0425m),
                new YieldCurveQuote("3M", 0.0430m),
                new YieldCurveQuote("6M", 0.0432m),
                new YieldCurveQuote("1Y", 0.0435m),
            },
            new DataCoverage(4, 4, 0, 1.0m),
            Array.Empty<DataIssue>(),
            new[]
            {
                new DataProvenance(new ProviderCode("TestSource"), "CME-USD-SOFR-OIS", LicenseType.Free, RetrievalMode.Cache,
                    FreshnessClass.Live, DateTimeOffset.UtcNow, null)
            });

        var pipeline = new FakeDataPipeline()
            .Register("SOFR", sofrEnvelope)
            .Register("CME-USD-SOFR-OIS", oisEnvelope);

        var builder = CreateBuilder(pipeline);
        var recipe = CreateMixedNodeRecipe();

        // Act
        var snapshot = await builder.BuildAsync(recipe, s_valuationDate);

        // Assert -- RATE_MISSING should be Warning (no gap issues)
        var rateMissing = snapshot.DataIssues.FirstOrDefault(i =>
            i.Code == new IssueCode("RATE_MISSING") && i.Message.Contains("SOFR"));
        rateMissing.Should().NotBeNull();
        rateMissing!.Severity.Should().Be(IssueSeverity.Warning);
    }

    private static CurveBuilder CreateBuilder(FakeDataPipeline pipeline)
    {
        var calendars = HolidayCalendarFactory.SupportedCodes.Select(HolidayCalendarFactory.Create).ToArray();
        var benchmarks = new[]
        {
            new RateBenchmark(new BenchmarkName("SOFR"), CurrencyCode.USD,
                BenchmarkKind.OvernightRiskFree, null, 0, 1, "USNY", "ACT/360", true),
        };
        var registry = InstrumentConventionRegistry.CreateDefault();
        var conventions = new[]
        {
            registry.GetRequired("USD-SOFR-OIS"),
        };
        var referenceData = new SimpleReferenceDataProvider(calendars, benchmarks, conventions);
        var calibrator = new PiecewiseBootstrapCalibrator();
        var logger = NullLoggerFactory.Instance.CreateLogger<CurveBuilder>();

        return new CurveBuilder(pipeline, calibrator, referenceData, logger);
    }

    /// <summary>
    /// Creates a recipe with a SOFR overnight node (may fail) plus OIS nodes (will succeed).
    /// Uses Ois instrument type with USD-SOFR-OIS convention to match real convention lookup.
    /// </summary>
    private static CurveGroupRecipe CreateMixedNodeRecipe()
    {
        const string oisConv = "USD-SOFR-OIS";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("SOFR O/N", "SOFR", oisConv, s_usdSofrDisc),
            new YieldCurveNode("1M", new Tenor("1M"), "Ois", "CME-USD-SOFR-OIS", "1M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("3M", new Tenor("3M"), "Ois", "CME-USD-SOFR-OIS", "3M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("6M", new Tenor("6M"), "Ois", "CME-USD-SOFR-OIS", "6M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("1Y", new Tenor("1Y"), "Ois", "CME-USD-SOFR-OIS", "1Y", oisConv, s_usdSofrDisc),
        };

        var curve = new CurveRecipe(
            "USD-SOFR-DISC",
            s_usdSofrDisc,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_interpolation,
            nodes);

        return new CurveGroupRecipe("USD-SOFR", new[] { curve });
    }
}
