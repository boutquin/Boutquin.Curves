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
/// Creates fallback-adjusted benchmark definitions from benchmark identity and convention inputs.
/// </summary>
/// <remarks>
/// IBOR fallback methodology follows the ISDA IBOR Fallbacks Protocol (2020) and ISDA Fallbacks
/// Supplement, which define the spread-adjusted replacement rates for legacy LIBOR contracts. The
/// Bloomberg IBOR Fallback Rate Adjustments provide the official spread values used to construct
/// fallback rates.
/// </remarks>
public static class FallbackAdjustedBenchmarkFactory
{
    /// <summary>
    /// Builds a fallback-adjusted benchmark definition.
    /// </summary>
    /// <param name="name">Human-readable benchmark name.</param>
    /// <param name="currency">Currency in which the benchmark is quoted.</param>
    /// <param name="tenor">Benchmark tenor for index fixing and projection.</param>
    /// <param name="calendarCode">Calendar used for fixing and settlement date adjustments.</param>
    /// <param name="dayCountCode">Day-count convention used for accrual calculations.</param>
    /// <returns>A <see cref="RateBenchmark"/> configured for fallback-adjusted rates.</returns>
    public static RateBenchmark Create(
        string name,
        CurrencyCode currency,
        Tenor tenor,
        string calendarCode,
        string dayCountCode)
    {
        return new RateBenchmark(
            new BenchmarkName(name),
            currency,
            BenchmarkKind.FallbackAdjusted,
            tenor,
            2,
            1,
            calendarCode,
            dayCountCode,
            false);
    }
}
