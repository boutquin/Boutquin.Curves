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
/// Provides canonical benchmark definitions used by default market configurations.
/// </summary>
public static class StandardBenchmarks
{
    /// <summary>
    /// Returns the canonical USD SOFR overnight benchmark definition.
    /// </summary>
    /// <returns>USD SOFR benchmark metadata.</returns>
    public static RateBenchmark UsdSofr() => new(
        new BenchmarkName("USD-SOFR"),
        CurrencyCode.USD,
        BenchmarkKind.OvernightRiskFree,
        null,
        0,
        1,
        "USNY",
        "ACT/360",
        true);

    /// <summary>
    /// Returns the canonical GBP SONIA overnight benchmark definition.
    /// </summary>
    /// <returns>GBP SONIA benchmark metadata.</returns>
    public static RateBenchmark GbpSonia() => new(
        new BenchmarkName("GBP-SONIA"),
        CurrencyCode.GBP,
        BenchmarkKind.OvernightRiskFree,
        null,
        0,
        1,
        "GBLO",
        "ACT/365F",
        true);

    /// <summary>
    /// Returns the canonical EUR ESTR overnight benchmark definition.
    /// </summary>
    /// <returns>EUR ESTR benchmark metadata.</returns>
    public static RateBenchmark EurEstr() => new(
        new BenchmarkName("EUR-ESTR"),
        CurrencyCode.EUR,
        BenchmarkKind.OvernightRiskFree,
        null,
        0,
        1,
        "TARGET",
        "ACT/360",
        true);

    /// <summary>
    /// Returns the canonical USD LIBOR 3M benchmark definition.
    /// </summary>
    /// <returns>USD LIBOR 3M benchmark metadata.</returns>
    public static RateBenchmark UsdLibor3M() => new(
        new BenchmarkName("USD-LIBOR-3M"),
        CurrencyCode.USD,
        BenchmarkKind.InterbankOffered,
        new Tenor("3M"),
        2,
        0,
        "USNY",
        "ACT/360",
        false);
}
