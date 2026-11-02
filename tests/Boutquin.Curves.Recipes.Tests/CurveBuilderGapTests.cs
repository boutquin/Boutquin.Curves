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
using Boutquin.Domain.Enumerations;
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
    private static readonly CurveReference s_usdSofrDisc = new(CurveRole.Discount, Currency.USD, new BenchmarkName("SOFR"));

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
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1M", 0.0425m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "3M", 0.0430m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "6M", 0.0432m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1Y", 0.0435m),
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
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1M", 0.0425m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "3M", 0.0430m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "6M", 0.0432m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1Y", 0.0435m),
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

    /// <summary>
    /// An UNEXPECTED_GAP in one curve of the group must not escalate a RATE_MISSING in a
    /// different, clean curve. The escalation accumulator must be scoped per curve/group,
    /// not to the whole build. (CF-C5-2 / F-L1-02 cross-group severity contamination.)
    /// </summary>
    [Fact]
    public async Task BuildAsync_GapInOneCurve_DoesNotEscalateRateMissingInCleanCurve()
    {
        // Arrange -- Curve A (USD/SOFR): SOFR overnight returns an UNEXPECTED_GAP + empty
        //            records (its RATE_MISSING is correctly Error). Its OIS quotes succeed
        //            so the curve calibrates.
        var gapIssue = new DataIssue(IssueCode.UnexpectedGap, IssueSeverity.Warning,
            "Missing data for 2026-04-08 -- expected business day observation.");
        var sofrGapEnvelope = new DataEnvelope<IReadOnlyList<ScalarObservation>>(
            Array.Empty<ScalarObservation>(),
            new DataCoverage(1, 0, 1, 0.0m),
            new[] { gapIssue },
            new[]
            {
                new DataProvenance(new ProviderCode("TestSource"), "SOFR", LicenseType.Free, RetrievalMode.Cache,
                    FreshnessClass.Stale, DateTimeOffset.UtcNow, null)
            });

        var usdOisEnvelope = new DataEnvelope<IReadOnlyList<YieldCurveQuote>>(
            new[]
            {
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1M", 0.0425m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "3M", 0.0430m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "6M", 0.0432m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1Y", 0.0435m),
            },
            new DataCoverage(4, 4, 0, 1.0m),
            Array.Empty<DataIssue>(),
            new[]
            {
                new DataProvenance(new ProviderCode("TestSource"), "CME-USD-SOFR-OIS", LicenseType.Free, RetrievalMode.Cache,
                    FreshnessClass.Live, DateTimeOffset.UtcNow, null)
            });

        // Curve B (CAD/CORRA): CORRA overnight returns EMPTY records but NO gap issue.
        //            Its RATE_MISSING should stay Warning. Its OIS quotes succeed so the
        //            curve calibrates (avoiding the "no curves resolved" throw).
        var corraCleanEnvelope = new DataEnvelope<IReadOnlyList<ScalarObservation>>(
            Array.Empty<ScalarObservation>(),
            new DataCoverage(1, 0, 1, 0.0m),
            Array.Empty<DataIssue>(),
            new[]
            {
                new DataProvenance(new ProviderCode("TestSource"), "CORRA", LicenseType.Free, RetrievalMode.Cache,
                    FreshnessClass.Live, DateTimeOffset.UtcNow, null)
            });

        var cadOisEnvelope = new DataEnvelope<IReadOnlyList<YieldCurveQuote>>(
            new[]
            {
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1M", 0.0250m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "3M", 0.0255m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "6M", 0.0258m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1Y", 0.0260m),
            },
            new DataCoverage(4, 4, 0, 1.0m),
            Array.Empty<DataIssue>(),
            new[]
            {
                new DataProvenance(new ProviderCode("TestSource"), "CME-CAD-CORRA-OIS", LicenseType.Free, RetrievalMode.Cache,
                    FreshnessClass.Live, DateTimeOffset.UtcNow, null)
            });

        var pipeline = new FakeDataPipeline()
            .Register("SOFR", sofrGapEnvelope)
            .Register("CME-USD-SOFR-OIS", usdOisEnvelope)
            .Register("CORRA", corraCleanEnvelope)
            .Register("CME-CAD-CORRA-OIS", cadOisEnvelope);

        var builder = CreateBuilder(pipeline);
        var recipe = CreateTwoCurveRecipe();

        // Act
        var snapshot = await builder.BuildAsync(recipe, s_valuationDate);

        // Assert -- Curve A's SOFR RATE_MISSING is correctly escalated to Error
        var sofrMissing = snapshot.DataIssues.FirstOrDefault(i =>
            i.Code == new IssueCode("RATE_MISSING") && i.Message.Contains("SOFR"));
        sofrMissing.Should().NotBeNull();
        sofrMissing!.Severity.Should().Be(IssueSeverity.Error);

        // Assert -- Curve B's CORRA RATE_MISSING must remain Warning (its own data was clean).
        // The buggy global accumulator escalates it to Error because of A's unrelated gap.
        var corraMissing = snapshot.DataIssues.FirstOrDefault(i =>
            i.Code == new IssueCode("RATE_MISSING") && i.Message.Contains("CORRA"));
        corraMissing.Should().NotBeNull();
        corraMissing!.Severity.Should().Be(IssueSeverity.Warning);
    }

    /// <summary>
    /// F-26: When every node in the recipe fails to resolve a rate (pipeline returns empty
    /// envelopes for all data keys), <see cref="CurveBuilder.BuildAsync"/> must throw
    /// <see cref="InvalidOperationException"/> with a message that identifies the group name.
    /// This guards against silent no-op builds that return a seemingly valid snapshot with
    /// no curves.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenNoNodesResolve_ThrowsInvalidOperationException()
    {
        // All pipeline responses are empty — every node extraction will fail,
        // leaving zero resolved nodes and zero calibrated curves.
        var emptyScalar = new DataEnvelope<IReadOnlyList<ScalarObservation>>(
            Array.Empty<ScalarObservation>(),
            new DataCoverage(0, 0, 0, 0.0m),
            Array.Empty<DataIssue>(),
            Array.Empty<DataProvenance>());

        var emptyQuotes = new DataEnvelope<IReadOnlyList<YieldCurveQuote>>(
            Array.Empty<YieldCurveQuote>(),
            new DataCoverage(0, 0, 0, 0.0m),
            Array.Empty<DataIssue>(),
            Array.Empty<DataProvenance>());

        var pipeline = new FakeDataPipeline()
            .Register("SOFR", emptyScalar)
            .Register("CME-USD-SOFR-OIS", emptyQuotes);

        var builder = CreateBuilder(pipeline);
        var recipe = CreateMixedNodeRecipe();

        Func<Task> act = () => builder.BuildAsync(recipe, s_valuationDate);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*No curves resolved*");
    }

    private static CurveBuilder CreateBuilder(FakeDataPipeline pipeline)
    {
        var calendars = HolidayCalendarFactory.SupportedCodes.Select(HolidayCalendarFactory.Create).ToArray();
        var benchmarks = new[]
        {
            new RateBenchmark(new BenchmarkName("SOFR"), Currency.USD,
                BenchmarkKind.OvernightRiskFree, null, 0, 1, "USNY", "ACT/360", true),
            new RateBenchmark(new BenchmarkName("CORRA"), Currency.CAD,
                BenchmarkKind.OvernightRiskFree, null, 0, 1, "CATO", "ACT/365F", true),
        };
        var registry = InstrumentConventionRegistry.CreateDefault();
        var conventions = new[]
        {
            registry.GetRequired("USD-SOFR-OIS"),
            registry.GetRequired("CAD-CORRA-OIS"),
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

    /// <summary>
    /// Creates a two-curve recipe: curve A (USD/SOFR) whose overnight node carries a gap,
    /// and curve B (CAD/CORRA) whose overnight node is clean. Both have succeeding OIS nodes
    /// so each calibrates. Used to prove the RATE_MISSING escalation is scoped per curve.
    /// </summary>
    private static CurveGroupRecipe CreateTwoCurveRecipe()
    {
        var usdRef = new CurveReference(CurveRole.Discount, Currency.USD, new BenchmarkName("SOFR"));
        var cadRef = new CurveReference(CurveRole.Discount, Currency.CAD, new BenchmarkName("CORRA"));

        var usdNodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("SOFR O/N", "SOFR", "USD-SOFR-OIS", usdRef),
            new YieldCurveNode("USD 1M", new Tenor("1M"), "Ois", "CME-USD-SOFR-OIS", "1M", "USD-SOFR-OIS", usdRef),
            new YieldCurveNode("USD 3M", new Tenor("3M"), "Ois", "CME-USD-SOFR-OIS", "3M", "USD-SOFR-OIS", usdRef),
            new YieldCurveNode("USD 6M", new Tenor("6M"), "Ois", "CME-USD-SOFR-OIS", "6M", "USD-SOFR-OIS", usdRef),
            new YieldCurveNode("USD 1Y", new Tenor("1Y"), "Ois", "CME-USD-SOFR-OIS", "1Y", "USD-SOFR-OIS", usdRef),
        };

        var cadNodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("CORRA O/N", "CORRA", "CAD-CORRA-OIS", cadRef),
            new YieldCurveNode("CAD 1M", new Tenor("1M"), "Ois", "CME-CAD-CORRA-OIS", "1M", "CAD-CORRA-OIS", cadRef),
            new YieldCurveNode("CAD 3M", new Tenor("3M"), "Ois", "CME-CAD-CORRA-OIS", "3M", "CAD-CORRA-OIS", cadRef),
            new YieldCurveNode("CAD 6M", new Tenor("6M"), "Ois", "CME-CAD-CORRA-OIS", "6M", "CAD-CORRA-OIS", cadRef),
            new YieldCurveNode("CAD 1Y", new Tenor("1Y"), "Ois", "CME-CAD-CORRA-OIS", "1Y", "CAD-CORRA-OIS", cadRef),
        };

        var usdCurve = new CurveRecipe(
            "USD-SOFR-DISC", usdRef, CurveValueType.DiscountFactor, "ACT/360", s_interpolation, usdNodes);
        var cadCurve = new CurveRecipe(
            "CAD-CORRA-DISC", cadRef, CurveValueType.DiscountFactor, "ACT/365F", s_interpolation, cadNodes);

        return new CurveGroupRecipe("MIXED", new[] { usdCurve, cadCurve });
    }
}
