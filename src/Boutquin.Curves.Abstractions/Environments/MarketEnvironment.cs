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
using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Abstractions.Environments;

/// <summary>
/// Represents a fully resolved market environment snapshot used for pricing and risk.
/// </summary>
/// <param name="ValuationDate">Valuation date for the environment snapshot.</param>
/// <param name="Curves">Curve map keyed by <see cref="CurveReference"/>.</param>
/// <param name="Benchmarks">Benchmark metadata keyed by benchmark name.</param>
/// <param name="Fixings">Historical or current fixings keyed by project-defined string identifiers.</param>
public sealed record MarketEnvironment(
    DateOnly ValuationDate,
    IReadOnlyDictionary<CurveReference, ICurve> Curves,
    IReadOnlyDictionary<BenchmarkName, RateBenchmark> Benchmarks,
    IReadOnlyDictionary<string, decimal> Fixings);
