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

using System.Text;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Bootstrap;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Interpolation;
using Boutquin.Curves.Recipes;
using Boutquin.Curves.Recipes.Nodes;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.Keys;
using Boutquin.MarketData.Abstractions.Records;
using Boutquin.MarketData.Abstractions.Requests;
using Boutquin.MarketData.Adapter.BankOfCanada;
using Boutquin.MarketData.Cache.Policies;
using Boutquin.MarketData.Cache.TimeSeries;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DependencyInjection;
using Boutquin.MarketData.Transport.Http;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using BenchmarkName = Boutquin.MarketData.Abstractions.ReferenceData.BenchmarkName;
using RateBenchmark = Boutquin.MarketData.Abstractions.ReferenceData.RateBenchmark;

namespace Boutquin.Curves.IntegrationTests;

/// <summary>Executes the pinned provider, kernel and storage packages through the Curves entry point.</summary>
public sealed class QualifiedCorraPipelineTests
{
    private static readonly DateOnly s_date = new(2024, 1, 12);
    private static readonly CurveReference s_reference = new(CurveRole.Discount, Currency.CAD, new BenchmarkName("CORRA"));
    private const string Payload = """
        {"observations":[
          {"d":"2024-01-08","OTHER":{"v":"99"},"AVG.INTWO":{"v":"5.05"}},
          {"d":"2024-01-09","OTHER":{"v":"99"},"AVG.INTWO":{"v":"5.04"}},
          {"d":"2024-01-10","OTHER":{"v":"99"},"AVG.INTWO":{"v":"5.03"}},
          {"d":"2024-01-11","OTHER":{"v":"99"},"AVG.INTWO":{"v":"5.02"}},
          {"d":"2024-01-12","OTHER":{"v":"99"},"AVG.INTWO":{"v":"5.01"}}]}
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealProviderThroughPipelineAndBuilder_ReopensSnapshotsOrRejectsUnsupportedGapFill(bool timeSeries)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"curves-corra-{Guid.NewGuid():N}");
        var transport = new FixtureTransport(Payload);
        try
        {
            await using (ServiceProvider first = CreateHost(directory, transport, timeSeries))
            {
                if (timeSeries)
                {
                    Func<Task> unsupported = () => CreateBuilder(first).BuildAsync(CreateRecipe(), s_date);
                    await unsupported.Should().ThrowAsync<NotSupportedException>().WithMessage("*WithRange*");
                    transport.Requests.Should().BeEmpty("unsupported request shapes must fail before fetching");
                    return;
                }
                CurveSnapshot snapshot = await CreateBuilder(first).BuildAsync(CreateRecipe(), s_date);
                AssertIndependentDeposit(snapshot);
                transport.Requests.Should().ContainSingle();
                transport.Requests[0].AbsolutePath.Should().EndWith("/observations/group/CORRA_GRAPH/json");
                transport.Requests[0].Query.Should().Contain("start_date=2024-01-08").And.Contain("end_date=2024-01-12");
            }

            // A new service provider creates a new L1 cache and reopens Storage files and recovery metadata.
            var unavailableTransport = new FixtureTransport(null);
            await using (ServiceProvider reopened = CreateHost(directory, unavailableTransport, timeSeries))
            {
                CurveSnapshot snapshot = await CreateBuilder(reopened).BuildAsync(CreateRecipe(), s_date);
                AssertIndependentDeposit(snapshot);
                unavailableTransport.Requests.Should().BeEmpty("the accepted data must survive provider restart");
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RealProviderThroughBuilder_PropagatesCancellationDuringTransport()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"curves-corra-{Guid.NewGuid():N}");
        using var cancellation = new CancellationTokenSource();
        var transport = new FixtureTransport(Payload, cancellation);
        try
        {
            await using ServiceProvider host = CreateHost(directory, transport, false);
            Func<Task> act = () => CreateBuilder(host).BuildAsync(CreateRecipe(), s_date, cancellation.Token);
            await act.Should().ThrowAsync<OperationCanceledException>();
            transport.Requests.Should().ContainSingle();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RealProviderThroughBuilder_MissingCanonicalSeriesCannotFabricateCurve()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"curves-corra-{Guid.NewGuid():N}");
        var transport = new FixtureTransport(Payload.Replace("AVG.INTWO", "UNRECOGNIZED", StringComparison.Ordinal));
        try
        {
            await using ServiceProvider host = CreateHost(directory, transport, false);
            Func<Task> act = () => CreateBuilder(host).BuildAsync(CreateRecipe(), s_date);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No curves resolved*");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealGovernmentZeroCsv_UsesNominalQuarterGridAndReopensStore(bool timeSeries)
    {
        const string csv = "Date,ZC025YR,ZC050YR,ZC100YR\n2024-01-12,0.0400,0.0410,0.0420\n";
        string directory = Path.Combine(Path.GetTempPath(), $"curves-boc-zero-{Guid.NewGuid():N}");
        var transport = new FixtureTransport(csv);
        try
        {
            await using (ServiceProvider first = CreateHost(directory, transport, timeSeries))
            {
                CurveSnapshot snapshot = await CreateBuilder(first).BuildAsync(CreateGovernmentRecipe(), s_date);
                AssertIndependentGovernmentCurve(snapshot);
                transport.Requests.Should().ContainSingle().Which.Query.Should().Contain("lookup_yield_curve.php");
                if (timeSeries)
                {
                    await first.GetRequiredService<ITimeSeriesStoreRegistry>()
                        .ResolveEngine<YieldCurveQuoteDatasetKey, YieldCurveQuote>().FlushAsync();
                    Directory.EnumerateFiles(Path.Combine(directory, "snapshots"), "*", SearchOption.AllDirectories)
                        .Should().BeEmpty("the time-series path must not fall back to legacy snapshots");
                }
            }
            var offline = new FixtureTransport(null);
            await using (ServiceProvider second = CreateHost(directory, offline, timeSeries))
            {
                AssertIndependentGovernmentCurve(await CreateBuilder(second).BuildAsync(CreateGovernmentRecipe(), s_date));
                offline.Requests.Should().BeEmpty();
            }
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static CurveGroupRecipe CreateGovernmentRecipe() => new("CAD-GOVERNMENT-ZERO", [
        new CurveRecipe("CAD-GOVERNMENT-ZERO", s_reference, CurveValueType.DiscountFactor, "ACT/365F",
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
            [new YieldCurveNode("3M", new Boutquin.MarketData.Abstractions.ReferenceData.Tenor("3M"), "ContinuousZero", "BOC-ZERO", "3M", "CAD-CORRA-OIS", s_reference),
             new YieldCurveNode("6M", new Boutquin.MarketData.Abstractions.ReferenceData.Tenor("6M"), "ContinuousZero", "BOC-ZERO", "6M", "CAD-CORRA-OIS", s_reference),
             new YieldCurveNode("1Y", new Boutquin.MarketData.Abstractions.ReferenceData.Tenor("1Y"), "ContinuousZero", "BOC-ZERO", "1Y", "CAD-CORRA-OIS", s_reference)])]);

    private static void AssertIndependentGovernmentCurve(CurveSnapshot snapshot)
    {
        var curve = (IDiscountCurve)snapshot.CurveGroup.GetCurve(s_reference);
        // Bank of Canada Working Paper 2004-48 §5.1: P(t,T) = exp(-(T-t) z(t,T)).
        // Published ZC025YR is exactly 0.25 years, including in this leap-year fixture.
        curve.DiscountFactor(s_date.AddMonths(3)).Should().BeApproximately(Math.Exp(-0.0400 * 0.25), 1e-13);
        curve.DiscountFactor(s_date.AddMonths(6)).Should().BeApproximately(Math.Exp(-0.0410 * 0.50), 1e-13);
        curve.DiscountFactor(s_date.AddYears(1)).Should().BeApproximately(Math.Exp(-0.0420), 1e-13);
        snapshot.Diagnostics.Repricing.Should().HaveCount(3).And.AllSatisfy(d => d.AbsoluteError.Should().BeLessThan(1e-10));
        snapshot.Provenance.Should().Contain(p => p.Provider.Value == "bankofcanada");
    }

    private static ServiceProvider CreateHost(string root, FixtureTransport transport, bool timeSeries)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IClock>(new FixedClock());
        // SnapshotStalenessPolicy uses wall time; keep this fixed historical fixture reusable across runs.
        services.AddSingleton(new SnapshotStalenessPolicy(TimeSpan.MaxValue));
        services.AddSingleton<IHttpDataTransport>(transport);
        services.AddMarketDataKernel(
            snapshotDirectory: Path.Combine(root, "snapshots"), rawDocumentDirectory: Path.Combine(root, "raw"),
            checkpointDirectory: Path.Combine(root, "checkpoints"), eventLogDirectory: Path.Combine(root, "events"),
            timeSeriesDirectory: Path.Combine(root, "timeseries"), configurePipeline: options => options.UseTimeSeriesStore = timeSeries);
        services.AddMarketDataBankOfCanada();
        // OvernightFixingRequest is an extension request: explicitly register its documented store seam.
        services.AddSingleton<IDatasetKeyBuilder<OvernightFixingRequest, ScalarObservationDatasetKey>, CorraKeyBuilder>();
        services.AddSingleton<IDatasetKeyBuilderCatalog>(new DatasetKeyBuilderCatalog(
            [typeof(IDatasetKeyBuilder<OvernightFixingRequest, ScalarObservationDatasetKey>),
             typeof(IDatasetKeyBuilder<YieldCurveQuoteRequest, YieldCurveQuoteDatasetKey>)]));
        return services.BuildServiceProvider();
    }

    private static CurveBuilder CreateBuilder(IServiceProvider host)
    {
        var conventions = new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("CAD-CORRA-OIS") };
        var reference = new SimpleReferenceDataProvider([new WeekendOnlyCalendar("CATO")], Array.Empty<RateBenchmark>(), conventions);
        return new CurveBuilder(host.GetRequiredService<IDataPipeline>(), new PiecewiseBootstrapCalibrator(), reference,
            NullLogger<CurveBuilder>.Instance);
    }

    private static CurveGroupRecipe CreateRecipe() => new("CAD-CORRA-QUALIFICATION", [
        new CurveRecipe("CAD-CORRA", s_reference, CurveValueType.DiscountFactor, "ACT/365F",
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
            [new OvernightFixingNode("CORRA ON", "CORRA", "CAD-CORRA-OIS", s_reference, lookbackDays: 4)])]);

    private static void AssertIndependentDeposit(CurveSnapshot snapshot)
    {
        // Independent simple-interest cash-flow equation: -1 + (1 + r/365) * DF = 0.
        const double expectedRate = 0.0501;
        const double tau = 1d / 365d;
        snapshot.CurveGroup.TryGetCurve(s_reference, out ICurve? raw).Should().BeTrue();
        var curve = raw.Should().BeAssignableTo<IDiscountCurve>().Subject;
        double df = curve.DiscountFactor(s_date.AddDays(1));
        df.Should().BeApproximately(1d / (1d + expectedRate * tau), 1e-13);
        ((1d + expectedRate * tau) * df - 1d).Should().BeApproximately(0d, 1e-13);
        snapshot.Diagnostics.Repricing.Should().ContainSingle().Which.MarketQuote.Should().BeApproximately(expectedRate, 1e-14);
        snapshot.Diagnostics.Repricing[0].AbsoluteError.Should().BeLessThan(1e-10);
        snapshot.Provenance.Should().Contain(p => p.Provider.Value == "boc-corra");
    }

    private sealed class CorraKeyBuilder : IDatasetKeyBuilder<OvernightFixingRequest, ScalarObservationDatasetKey>
    {
        public ScalarObservationDatasetKey Build(OvernightFixingRequest request) => new("boc-corra", request.BenchmarkId.Value);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FixtureTransport(string? payload, CancellationTokenSource? cancellation = null) : IHttpDataTransport
    {
        public List<Uri> Requests { get; } = [];
        public Task<Stream> GetAsync(Uri uri, IReadOnlyDictionary<string, string>? headers = null, CancellationToken cancellationToken = default)
        {
            Requests.Add(uri);
            cancellation?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            if (payload is null)
            {
                throw new HttpRequestException("Provider unavailable after restart.");
            }
            return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(payload)));
        }
    }
}
