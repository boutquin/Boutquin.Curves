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

using Boutquin.Curves.Abstractions;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Indices.Tests;

public sealed class BenchmarkCatalogTests
{
    [Fact]
    public void DefaultCatalog_ShouldContainUsdSofr()
    {
        BenchmarkCatalog catalog = BenchmarkCatalog.CreateDefault();
        var benchmark = catalog.GetRequired(new BenchmarkName("USD-SOFR"));

        benchmark.Currency.Should().Be(Currency.USD);
    }
    [Fact]
    public void DefaultCatalog_ContainsEveryDeclaredBenchmarkAndRejectsUnknownLookup()
    {
        var catalog = BenchmarkCatalog.CreateDefault();
        RateBenchmark[] expected = [StandardBenchmarks.UsdSofr, StandardBenchmarks.CadCorra,
            StandardBenchmarks.GbpSonia, StandardBenchmarks.EurEstr, StandardBenchmarks.ChfSaron, StandardBenchmarks.JpyTona];
        catalog.All().Should().HaveCount(expected.Length);
        foreach (var benchmark in expected)
        {
            catalog.GetRequired(benchmark.Name).Should().Be(benchmark);
        }
        Action missing = () => catalog.GetRequired(new BenchmarkName("UNKNOWN"));
        missing.Should().Throw<KeyNotFoundException>().WithMessage("*UNKNOWN*");
    }

}
