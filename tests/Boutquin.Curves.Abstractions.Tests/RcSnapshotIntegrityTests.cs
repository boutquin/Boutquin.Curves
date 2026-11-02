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

using System.Text.Json;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Environments;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Abstractions.Quotes;
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Abstractions.Tests;

public sealed class RcSnapshotIntegrityTests
{
    [Fact]
    public void MarketQuoteSet_CopiesConstructorAndInitInputs_AndProtectsOutput()
    {
        var id = new QuoteId("RATE");
        var quote = new MarketQuote(id, 0.05m, "Rate", new DateOnly(2024, 1, 2));
        var source = new Dictionary<QuoteId, MarketQuote> { [id] = quote };
        var snapshot = new MarketQuoteSet(quote.AsOfDate, source);
        source.Clear();
        snapshot.GetRequired(id).Should().Be(quote);
        Action mutate = () => ((IDictionary<QuoteId, MarketQuote>)snapshot.Quotes).Clear();
        mutate.Should().Throw<NotSupportedException>();
        var replacement = new Dictionary<QuoteId, MarketQuote> { [id] = quote };
        var changed = snapshot with { Quotes = replacement };
        replacement.Clear();
        changed.GetRequired(id).Should().Be(quote);
    }

    [Fact]
    public void MarketEnvironment_CopiesAllMaps_AndPreservesDictionaryStringComparer()
    {
        var curves = new Dictionary<CurveReference, ICurve>();
        var benchmarks = new Dictionary<BenchmarkName, RateBenchmark>();
        var fixings = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["SOFR"] = 0.05m };
        var snapshot = new MarketEnvironment(new DateOnly(2024, 1, 2), curves, benchmarks, fixings);
        curves.Add(new CurveReference(CurveRole.Discount, Boutquin.Domain.Enumerations.Currency.USD), null!);
        benchmarks.Add(StandardBenchmarks.UsdSofr.Name, StandardBenchmarks.UsdSofr);
        fixings["SOFR"] = 0.09m;
        snapshot.Curves.Should().BeEmpty();
        snapshot.Benchmarks.Should().BeEmpty();
        snapshot.Fixings["sofr"].Should().Be(0.05m);
        Action mutate = () => ((IDictionary<string, decimal>)snapshot.Fixings)["SOFR"] = 0.12m;
        mutate.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Identifiers_SystemTextJson_RoundTripPreservesValue()
    {
        var curve = new CurveName("USD-DISC");
        var group = new CurveGroupName("USD");
        var quote = new QuoteId("SOFR");
        JsonSerializer.Deserialize<CurveName>(JsonSerializer.Serialize(curve)).Should().Be(curve);
        JsonSerializer.Deserialize<CurveGroupName>(JsonSerializer.Serialize(group)).Should().Be(group);
        JsonSerializer.Deserialize<QuoteId>(JsonSerializer.Serialize(quote)).Should().Be(quote);
    }
}
