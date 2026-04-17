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

namespace Boutquin.Curves.Abstractions.Fixings;

/// <summary>
/// Provides storage and retrieval of historical benchmark fixings keyed by benchmark identifier
/// and observation date.
/// </summary>
/// <remarks>
/// Any instrument that references past fixings (e.g., an already-accruing OIS swap) requires
/// a fixings store to value correctly. The store is populated from adapter feeds or fixture data
/// and queried during bootstrap calibration and instrument pricing.
///
/// Keys use the format <c>"CCY-BENCHMARK"</c> (e.g., <c>"USD-SOFR"</c>, <c>"GBP-SONIA"</c>,
/// <c>"EUR-ESTR"</c>). This is a convention, not an enum, so the store remains extensible to
/// arbitrary benchmark identifiers without recompilation.
/// </remarks>
public interface IFixingsStore
{
    /// <summary>
    /// Retrieves a fixing value for the specified benchmark and date.
    /// </summary>
    /// <param name="fixingKey">Benchmark identifier (e.g., <c>"USD-SOFR"</c>).</param>
    /// <param name="date">Observation date for the fixing.</param>
    /// <returns>The fixing value published for <paramref name="fixingKey"/> on <paramref name="date"/>.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no fixing exists for the given key and date combination.
    /// </exception>
    decimal GetFixing(string fixingKey, DateOnly date);

    /// <summary>
    /// Attempts to retrieve a fixing value without throwing on missing data.
    /// </summary>
    /// <param name="fixingKey">Benchmark identifier.</param>
    /// <param name="date">Observation date.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the fixing value.</param>
    /// <returns><see langword="true"/> if a fixing was found; otherwise <see langword="false"/>.</returns>
    bool TryGetFixing(string fixingKey, DateOnly date, out decimal value);

    /// <summary>
    /// Adds or replaces a single fixing observation.
    /// </summary>
    /// <param name="fixingKey">Benchmark identifier.</param>
    /// <param name="date">Observation date.</param>
    /// <param name="value">Published fixing value.</param>
    void AddFixing(string fixingKey, DateOnly date, decimal value);

    /// <summary>
    /// Returns all fixings for a given date, keyed by benchmark identifier.
    /// </summary>
    /// <param name="date">Observation date to query.</param>
    /// <returns>Dictionary of benchmark key to fixing value for the requested date. Empty if no fixings exist.</returns>
    IReadOnlyDictionary<string, decimal> GetFixingsForDate(DateOnly date);

    /// <summary>
    /// Returns the complete time series for a single benchmark.
    /// </summary>
    /// <param name="fixingKey">Benchmark identifier.</param>
    /// <returns>Sorted dictionary of date to fixing value. Empty if the key is unknown.</returns>
    IReadOnlyDictionary<DateOnly, decimal> GetTimeSeries(string fixingKey);
}
