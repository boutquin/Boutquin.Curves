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

using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Indices;

/// <summary>
/// Stores and resolves benchmark metadata by benchmark name.
/// </summary>
public sealed class BenchmarkCatalog
{
    private readonly Dictionary<BenchmarkName, RateBenchmark> _benchmarks = new();

    /// <summary>
    /// Adds or replaces a benchmark definition in the catalog.
    /// </summary>
    /// <param name="benchmark">Benchmark metadata to register.</param>
    /// <returns>The current catalog instance for fluent chaining.</returns>
    public BenchmarkCatalog Add(RateBenchmark benchmark)
    {
        _benchmarks[benchmark.Name] = benchmark;
        return this;
    }

    /// <summary>
    /// Resolves a benchmark by name.
    /// </summary>
    /// <param name="name">Benchmark name to resolve.</param>
    /// <returns>The benchmark mapped to <paramref name="name"/>.</returns>
    public RateBenchmark GetRequired(BenchmarkName name)
    {
        if (!_benchmarks.TryGetValue(name, out RateBenchmark? benchmark))
        {
            throw new KeyNotFoundException($"Benchmark '{name}' was not found.");
        }

        return benchmark;
    }

    /// <summary>
    /// Returns all benchmarks registered in the catalog.
    /// </summary>
    /// <returns>An immutable snapshot of benchmark metadata values.</returns>
    public IReadOnlyCollection<RateBenchmark> All() => _benchmarks.Values.ToArray();

    /// <summary>
    /// Creates a catalog pre-populated with standard benchmark definitions.
    /// </summary>
    /// <returns>A catalog containing the built-in benchmark set.</returns>
    public static BenchmarkCatalog CreateDefault()
    {
        return new BenchmarkCatalog()
            .Add(StandardBenchmarks.UsdSofr())
            .Add(StandardBenchmarks.GbpSonia())
            .Add(StandardBenchmarks.EurEstr())
            .Add(StandardBenchmarks.UsdLibor3M());
    }
}
