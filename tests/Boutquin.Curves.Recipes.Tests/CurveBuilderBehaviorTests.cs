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
using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.Diagnostics;
using Boutquin.MarketData.Abstractions.Provenance;
using Boutquin.MarketData.Abstractions.Records;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Abstractions.Requests;
using Boutquin.MarketData.Abstractions.Results;
using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.MarketData.Conventions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MdReferenceData = Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Recipes.Tests;

/// <summary>
/// Tests for CurveBuilder fetch-dispatch and error-path behaviour:
/// AT-12 (FuturesSettlement branch), AT-13 (FETCH_FAILED and NODE_MISSING paths),
/// AT-14 (multi-curve DF influence assertion), AT-17 (programming errors not masked as FETCH_FAILED).
/// </summary>
public sealed class CurveBuilderBehaviorTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly CurveReference s_usdSofrDisc =
        new(CurveRole.Discount, Currency.USD, new BenchmarkName("SOFR"));
    private static readonly InterpolationSettings s_interpolation =
        new(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward");

    // -------------------------------------------------------------------------
    // AT-12 — FuturesSettlementRequest branch executes end-to-end
    // -------------------------------------------------------------------------

    /// <summary>
    /// AT-12: BuildAsync must dispatch a FuturesSettlementRequest (not an OvernightFixingRequest or
    /// YieldCurveQuoteRequest) when a recipe node produces one. We verify this by giving the pipeline
    /// a FuturesSettlement envelope keyed to the futures product code "SR3" and asserting that the
    /// snapshot calibrates successfully (proof that FetchRecordsAsync's FuturesSettlementRequest arm ran).
    /// </summary>
    [Fact]
    public async Task BuildAsync_WithFuturesSettlementNode_DispatchesFuturesSettlementRequest()
    {
        // Arrange — pipeline has a FuturesSettlement envelope keyed to "SR3" (the product code).
        // Settlement price 95.70 → implied rate = (100 - 95.70) / 100 = 0.0430.
        var futuresEnvelope = new DataEnvelope<IReadOnlyList<FuturesSettlement>>(
            new[]
            {
                new FuturesSettlement(
                    s_valuationDate,
                    new MdReferenceData.FuturesProductCode("SR3"),
                    new MdReferenceData.ContractMonth("2026-06"),
                    95.70m,
                    null),
                new FuturesSettlement(
                    s_valuationDate,
                    new MdReferenceData.FuturesProductCode("SR3"),
                    new MdReferenceData.ContractMonth("2026-09"),
                    95.50m,
                    null),
                new FuturesSettlement(
                    s_valuationDate,
                    new MdReferenceData.FuturesProductCode("SR3"),
                    new MdReferenceData.ContractMonth("2026-12"),
                    95.30m,
                    null),
            },
            new DataCoverage(3, 3, 0, 1.0m),
            Array.Empty<DataIssue>(),
            new[]
            {
                new DataProvenance(new ProviderCode("CME"), "SR3", LicenseType.Free, RetrievalMode.Cache,
                    FreshnessClass.Live, DateTimeOffset.UtcNow, null),
            });

        // The pipeline must NOT have a "SOFR" key (no overnight fixing node in this recipe).
        var pipeline = new FakeDataPipeline()
            .Register("SR3", futuresEnvelope);

        var builder = CreateBuilder(pipeline, "USD-SR3");

        // Recipe: three futures nodes only (no overnight fixing node).
        var nodes = new ICurveNodeSpec[]
        {
            new FuturesSettlementNode("SR3-Jun26", new Tenor("3M"), "SR3", "2026-06", "USD-SR3", s_usdSofrDisc),
            new FuturesSettlementNode("SR3-Sep26", new Tenor("6M"), "SR3", "2026-09", "USD-SR3", s_usdSofrDisc),
            new FuturesSettlementNode("SR3-Dec26", new Tenor("9M"), "SR3", "2026-12", "USD-SR3", s_usdSofrDisc),
        };
        var curve = new CurveRecipe("USD-SOFR-FUT", s_usdSofrDisc, CurveValueType.DiscountFactor,
            "ACT/360", s_interpolation, nodes);
        var recipe = new CurveGroupRecipe("USD-SOFR-FUT", new[] { curve });

        // Act
        CurveSnapshot snapshot = await builder.BuildAsync(recipe, s_valuationDate);

        // Assert — the snapshot must be non-null, no FETCH_FAILED issues, and the discount curve
        // must be present: proves the FuturesSettlementRequest branch ran in FetchRecordsAsync.
        snapshot.Should().NotBeNull();
        snapshot.DataIssues.Should().NotContain(i => i.Code == new IssueCode("FETCH_FAILED"),
            because: "the FuturesSettlement branch must have successfully dispatched and returned records");
        snapshot.CurveGroup.TryGetCurve(s_usdSofrDisc, out _).Should()
            .BeTrue(because: "the futures branch must have resolved rates and produced a calibrated curve");
    }

    // -------------------------------------------------------------------------
    // AT-13 — FETCH_FAILED and NODE_MISSING diagnostic paths
    // -------------------------------------------------------------------------

    /// <summary>
    /// AT-13a: When the pipeline throws for a dataset key, BuildAsync must record a FETCH_FAILED
    /// issue with severity Error. The test uses a recipe whose only node requests an unregistered
    /// key — FakeDataPipeline throws InvalidOperationException for unknown keys.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenPipelineThrows_RecordsFetchFailedError()
    {
        // Arrange — pipeline has no registration for "MISSING-KEY".
        // FakeDataPipeline.ExecuteAsync throws InvalidOperationException for unknown keys.
        // Add "CME-USD-SOFR-OIS" so the curve has at least one node and can calibrate.
        var oisEnvelope = new DataEnvelope<IReadOnlyList<YieldCurveQuote>>(
            new[]
            {
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1M", 0.0480m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "3M", 0.0460m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "6M", 0.0440m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1Y", 0.0420m),
            },
            new DataCoverage(4, 4, 0, 1.0m),
            Array.Empty<DataIssue>(),
            new[]
            {
                new DataProvenance(new ProviderCode("TestSource"), "CME-USD-SOFR-OIS", LicenseType.Free,
                    RetrievalMode.Cache, FreshnessClass.Live, DateTimeOffset.UtcNow, null),
            });

        // Only register "CME-USD-SOFR-OIS" — "MISSING-KEY" is intentionally absent.
        var pipeline = new FakeDataPipeline()
            .Register("CME-USD-SOFR-OIS", oisEnvelope);

        var builder = CreateBuilder(pipeline, "USD-SOFR-OIS");

        // Recipe: one node targeting the missing key, plus OIS nodes that succeed.
        const string oisConv = "USD-SOFR-OIS";
        var nodes = new ICurveNodeSpec[]
        {
            new YieldCurveNode("MISSING", new Tenor("ON"), "Ois", "MISSING-KEY", "1M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("1M", new Tenor("1M"), "Ois", "CME-USD-SOFR-OIS", "1M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("3M", new Tenor("3M"), "Ois", "CME-USD-SOFR-OIS", "3M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("6M", new Tenor("6M"), "Ois", "CME-USD-SOFR-OIS", "6M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("1Y", new Tenor("1Y"), "Ois", "CME-USD-SOFR-OIS", "1Y", oisConv, s_usdSofrDisc),
        };
        var curve = new CurveRecipe("USD-SOFR-DISC", s_usdSofrDisc, CurveValueType.DiscountFactor,
            "ACT/360", s_interpolation, nodes);
        var recipe = new CurveGroupRecipe("USD-SOFR", new[] { curve });

        // Act
        CurveSnapshot snapshot = await builder.BuildAsync(recipe, s_valuationDate);

        // Assert — FETCH_FAILED must appear with Error severity.
        var fetchFailed = snapshot.DataIssues.Where(i => i.Code == new IssueCode("FETCH_FAILED")).ToList();
        fetchFailed.Should().NotBeEmpty(
            because: "a pipeline exception on 'MISSING-KEY' must produce a FETCH_FAILED issue");
        fetchFailed.Should().OnlyContain(i => i.Severity == IssueSeverity.Error,
            because: "FETCH_FAILED is always Error severity");
    }

    /// <summary>
    /// AT-13b: When the pipeline succeeds but returns null records (captured from the exception path),
    /// each node in that group must produce a NODE_MISSING issue with Warning severity.
    ///
    /// We simulate this by creating a pipeline that throws for the target key —
    /// the exception sets records=null, triggering NODE_MISSING for each node in the group.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenPipelineThrows_RecordsNodeMissingForEachNodeInGroup()
    {
        // Arrange — pipeline knows only "CME-USD-SOFR-OIS"; the "MISSING-KEY" group throws.
        var oisEnvelope = new DataEnvelope<IReadOnlyList<YieldCurveQuote>>(
            new[]
            {
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1M", 0.0480m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "3M", 0.0460m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "6M", 0.0440m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1Y", 0.0420m),
            },
            new DataCoverage(4, 4, 0, 1.0m),
            Array.Empty<DataIssue>(),
            new[]
            {
                new DataProvenance(new ProviderCode("TestSource"), "CME-USD-SOFR-OIS", LicenseType.Free,
                    RetrievalMode.Cache, FreshnessClass.Live, DateTimeOffset.UtcNow, null),
            });

        var pipeline = new FakeDataPipeline()
            .Register("CME-USD-SOFR-OIS", oisEnvelope);

        var builder = CreateBuilder(pipeline, "USD-SOFR-OIS");

        // Two nodes share the same failing key ("MISSING-KEY") so both should get NODE_MISSING.
        const string oisConv = "USD-SOFR-OIS";
        var nodes = new ICurveNodeSpec[]
        {
            new YieldCurveNode("M1", new Tenor("3M"), "Ois", "MISSING-KEY", "3M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("M2", new Tenor("6M"), "Ois", "MISSING-KEY", "6M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("1M", new Tenor("1M"), "Ois", "CME-USD-SOFR-OIS", "1M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("3M", new Tenor("3M"), "Ois", "CME-USD-SOFR-OIS", "3M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("6M", new Tenor("6M"), "Ois", "CME-USD-SOFR-OIS", "6M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("1Y", new Tenor("1Y"), "Ois", "CME-USD-SOFR-OIS", "1Y", oisConv, s_usdSofrDisc),
        };
        var curve = new CurveRecipe("USD-SOFR-DISC", s_usdSofrDisc, CurveValueType.DiscountFactor,
            "ACT/360", s_interpolation, nodes);
        var recipe = new CurveGroupRecipe("USD-SOFR", new[] { curve });

        // Act
        CurveSnapshot snapshot = await builder.BuildAsync(recipe, s_valuationDate);

        // Assert — two NODE_MISSING issues (one per failing node, since records=null after exception).
        var nodeMissing = snapshot.DataIssues.Where(i => i.Code == new IssueCode("NODE_MISSING")).ToList();
        nodeMissing.Should().HaveCount(2,
            because: "two nodes from the failing 'MISSING-KEY' group must each produce a NODE_MISSING issue");
        nodeMissing.Should().OnlyContain(i => i.Severity == IssueSeverity.Warning,
            because: "NODE_MISSING is always Warning severity");
    }

    // -------------------------------------------------------------------------
    // AT-14 — multi-curve projection-curve DF influence
    // -------------------------------------------------------------------------

    /// <summary>
    /// AT-14: The UsdSofrMultiCurve recipe's discount curve uses swap nodes whose ProjectionCurves
    /// reference a separate SOFR forward curve. The test proves the projection curve actually
    /// influences the calibrated discount factors by comparing against a single-curve baseline.
    ///
    /// Justification for modifying the existing test: the original test in MultiCurveRecipeTests.cs
    /// (UsdSofrMultiCurve_CalibratesEndToEndWithProjectionForwardsEnteringSwapPillarSolve) asserts
    /// only that both curves are present in the snapshot, which passes even if the projection curve
    /// is silently ignored. This test adds a numeric assertion proving the projection curve changed
    /// the calibrated discount factors versus the single-curve baseline.
    /// </summary>
    [Fact]
    public async Task UsdSofrMultiCurve_DiscountFactors_DifferFromSingleCurveBaseline()
    {
        // Arrange — full fixture pipeline (both single-curve UsdSofrDiscount and multi-curve recipes).
        var pipeline = FixtureData.CreatePipeline();
        var calendars = HolidayCalendarFactory.SupportedCodes.Select(HolidayCalendarFactory.Create).ToArray();
        var benchmarks = new[]
        {
            new RateBenchmark(new BenchmarkName("SOFR"), Currency.USD, BenchmarkKind.OvernightRiskFree,
                null, 0, 1, "USNY", "ACT/360", true),
        };
        var registry = InstrumentConventionRegistry.CreateDefault();
        var conventions = new[]
        {
            registry.GetRequired("USD-SOFR-OIS"),
            registry.GetRequired("USD-FIXED-6M-30-360"),
        };
        var referenceData = new SimpleReferenceDataProvider(calendars, benchmarks, conventions);
        var calibrator = new PiecewiseBootstrapCalibrator();
        var logger = NullLoggerFactory.Instance.CreateLogger<CurveBuilder>();
        var builder = new CurveBuilder(pipeline, calibrator, referenceData, logger);

        var discountRef = new CurveReference(CurveRole.Discount, Currency.USD, new BenchmarkName("SOFR"));

        // Single-curve baseline: SOFR discount curve only (no separate projection curve).
        CurveSnapshot singleCurveSnapshot = await builder.BuildAsync(
            StandardCurveRecipes.UsdSofrDiscount(), s_valuationDate);
        singleCurveSnapshot.CurveGroup.TryGetCurve(discountRef, out var singleCurve).Should().BeTrue();
        var singleDisc = (IDiscountCurve)singleCurve!;

        // Multi-curve: re-create pipeline (FakeDataPipeline is stateless, can reuse).
        CurveSnapshot multiCurveSnapshot = await builder.BuildAsync(
            StandardCurveRecipes.UsdSofrMultiCurve(), s_valuationDate);
        multiCurveSnapshot.CurveGroup.TryGetCurve(discountRef, out var multiCurve).Should()
            .BeTrue(because: "the multi-curve discount curve must be present");
        var multiDisc = (IDiscountCurve)multiCurve!;

        // Assert — at least one pillar discount factor differs between single and multi-curve.
        // If the projection curve had no effect, both DFs would be identical.
        DateOnly[] pillars =
        [
            s_valuationDate.AddYears(2),
            s_valuationDate.AddYears(5),
            s_valuationDate.AddYears(10),
        ];

        bool anyDiffers = pillars.Any(d =>
            Math.Abs(singleDisc.DiscountFactor(d) - multiDisc.DiscountFactor(d)) > 1e-6);

        anyDiffers.Should().BeTrue(
            because: "the projection curve's SOFR forwards must change at least one calibrated " +
                     "discount factor compared with the single-curve UST-par-rate baseline");
    }

    // -------------------------------------------------------------------------
    // AT-17 — programming errors (NotSupportedException) must not be masked as FETCH_FAILED
    // -------------------------------------------------------------------------

    /// <summary>
    /// AT-17: CurveBuilder.FetchRecordsAsync throws NotSupportedException for unrecognised request
    /// types. The broad catch in BuildAsync currently swallows this programming error as FETCH_FAILED.
    /// After the fix, NotSupportedException must propagate out of BuildAsync rather than being
    /// silently recorded as a FETCH_FAILED data issue.
    ///
    /// We produce an unsupported request type by injecting a custom ICurveNodeSpec whose
    /// CreateRequest returns an unknown IDataRequest implementation. The recipe also has OIS
    /// nodes that succeed, so without the fix BuildAsync returns a snapshot with FETCH_FAILED —
    /// which this test rejects.
    /// </summary>
    [Fact]
    public async Task BuildAsync_WhenDispatchThrowsNotSupportedException_PropagatesRatherThanRecordingFetchFailed()
    {
        // Arrange — OIS nodes succeed; the unsupported node fires NotSupportedException in dispatch.
        var oisEnvelope = new DataEnvelope<IReadOnlyList<YieldCurveQuote>>(
            new[]
            {
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1M", 0.0480m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "3M", 0.0460m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "6M", 0.0440m),
                new YieldCurveQuote(new DateOnly(2026, 4, 9), "1Y", 0.0420m),
            },
            new DataCoverage(4, 4, 0, 1.0m),
            Array.Empty<DataIssue>(),
            new[]
            {
                new DataProvenance(new ProviderCode("TestSource"), "CME-USD-SOFR-OIS", LicenseType.Free,
                    RetrievalMode.Cache, FreshnessClass.Live, DateTimeOffset.UtcNow, null),
            });

        var pipeline = new FakeDataPipeline()
            .Register("CME-USD-SOFR-OIS", oisEnvelope);

        var builder = CreateBuilder(pipeline, "USD-SOFR-OIS");

        // Recipe: one unsupported node (fires NotSupportedException) PLUS OIS nodes that succeed.
        // Without the fix: BuildAsync returns a snapshot with FETCH_FAILED for the unsupported node.
        // With the fix: BuildAsync propagates NotSupportedException (a programming error).
        const string oisConv = "USD-SOFR-OIS";
        var nodes = new ICurveNodeSpec[]
        {
            new UnsupportedRequestNode("UNSUPPORTED", s_usdSofrDisc),   // triggers NotSupportedException
            new YieldCurveNode("1M", new Tenor("1M"), "Ois", "CME-USD-SOFR-OIS", "1M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("3M", new Tenor("3M"), "Ois", "CME-USD-SOFR-OIS", "3M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("6M", new Tenor("6M"), "Ois", "CME-USD-SOFR-OIS", "6M", oisConv, s_usdSofrDisc),
            new YieldCurveNode("1Y", new Tenor("1Y"), "Ois", "CME-USD-SOFR-OIS", "1Y", oisConv, s_usdSofrDisc),
        };
        var curve = new CurveRecipe("USD-SOFR-DISC", s_usdSofrDisc, CurveValueType.DiscountFactor,
            "ACT/360", s_interpolation, nodes);
        var recipe = new CurveGroupRecipe("USD-SOFR", new[] { curve });

        // Act & Assert — after the fix, NotSupportedException propagates.
        // The pre-fix behaviour (snapshot returned with FETCH_FAILED) must not occur.
        Func<Task> act = async () => await builder.BuildAsync(recipe, s_valuationDate);

        await act.Should().ThrowAsync<NotSupportedException>(
            because: "NotSupportedException is a programming error — it must propagate rather than " +
                     "being silently recorded as a FETCH_FAILED data issue");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static CurveBuilder CreateBuilder(FakeDataPipeline pipeline, string conventionCode)
    {
        var calendars = HolidayCalendarFactory.SupportedCodes.Select(HolidayCalendarFactory.Create).ToArray();
        var benchmarks = new[]
        {
            new RateBenchmark(new BenchmarkName("SOFR"), Currency.USD,
                BenchmarkKind.OvernightRiskFree, null, 0, 1, "USNY", "ACT/360", true),
        };
        var registry = InstrumentConventionRegistry.CreateDefault();
        var conventions = new[] { registry.GetRequired(conventionCode) };
        var referenceData = new SimpleReferenceDataProvider(calendars, benchmarks, conventions);
        var calibrator = new PiecewiseBootstrapCalibrator();
        var logger = NullLoggerFactory.Instance.CreateLogger<CurveBuilder>();
        return new CurveBuilder(pipeline, calibrator, referenceData, logger);
    }

    /// <summary>
    /// A node that produces a custom IDataRequest implementation not handled by
    /// CurveBuilder.FetchRecordsAsync's switch expression, triggering NotSupportedException.
    /// </summary>
    private sealed class UnsupportedRequestNode : ICurveNodeSpec
    {
        private readonly CurveReference _target;

        public UnsupportedRequestNode(string label, CurveReference target)
        {
            Label = label;
            _target = target;
        }

        public string Label { get; }
        public Tenor Tenor => new("ON");
        public string InstrumentType => "Ois";
        public string ConventionCode => "USD-SOFR-OIS";
        public CurveReference TargetCurve => _target;

        public IDataRequest CreateRequest(DateOnly valuationDate) =>
            new UnknownRequest("UNKNOWN-KEY", valuationDate);

        public decimal? ExtractRate(IReadOnlyList<object> records, DateOnly valuationDate) => null;
    }

    /// <summary>
    /// A custom IDataRequest that is not handled by any arm of FetchRecordsAsync.
    /// </summary>
    private sealed record UnknownRequest(string DatasetKey, DateOnly AsOfDate) : IDataRequest
    {
        public DateRange Range => new(AsOfDate, AsOfDate);
        public DataFrequency Frequency => DataFrequency.Daily;
    }
}
