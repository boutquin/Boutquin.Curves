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
using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Recipes.Nodes;

/// <summary>
/// Describes one calibration node in a curve recipe: how to fetch data and extract a rate.
/// </summary>
/// <remarks>
/// Each node specification pairs a market-data request with an extraction strategy.
/// <see cref="CreateRequest"/> builds the appropriate <see cref="IDataRequest"/> for the
/// given valuation date, while <see cref="ExtractRate"/> interprets the fetched records
/// and returns the single decimal rate needed for calibration. Implementations handle
/// instrument-specific logic such as lookback windows for overnight fixings,
/// tenor matching for yield-curve quotes, and price-to-rate conversion for futures.
/// </remarks>
public interface ICurveNodeSpec
{
    /// <summary>
    /// Human-readable label identifying this node within the recipe (e.g., "SOFR ON", "3M SOFR Future Jun25").
    /// </summary>
    string Label { get; }

    /// <summary>
    /// Tenor of the calibration instrument (e.g., "ON", "3M", "1Y").
    /// </summary>
    Tenor Tenor { get; }

    /// <summary>
    /// Instrument type used by the calibrator to select the correct pricing logic (e.g., "Deposit", "OisFuture").
    /// </summary>
    string InstrumentType { get; }

    /// <summary>
    /// Convention code identifying the day-count, business-day, and payment conventions for this node.
    /// </summary>
    string ConventionCode { get; }

    /// <summary>
    /// The curve that this node calibrates into.
    /// </summary>
    CurveReference TargetCurve { get; }

    /// <summary>
    /// Builds a market-data request appropriate for the given valuation date.
    /// </summary>
    /// <param name="valuationDate">The as-of date for the calibration run.</param>
    /// <returns>A typed data request that can be dispatched through the data pipeline.</returns>
    IDataRequest CreateRequest(DateOnly valuationDate);

    /// <summary>
    /// Extracts a single calibration rate from fetched market-data records.
    /// </summary>
    /// <param name="records">Records returned by the data pipeline for the request created by <see cref="CreateRequest"/>.</param>
    /// <param name="valuationDate">The as-of date, used for filtering or date matching.</param>
    /// <returns>The extracted rate, or <see langword="null"/> if no suitable observation was found.</returns>
    decimal? ExtractRate(IReadOnlyList<object> records, DateOnly valuationDate);

    /// <summary>
    /// Returns the actual observation date used when the extracted rate does not correspond
    /// exactly to the valuation date (e.g., lookback to a prior business day).
    /// </summary>
    /// <param name="records">Records returned by the data pipeline.</param>
    /// <param name="valuationDate">The requested as-of date.</param>
    /// <returns>The actual date of the data used, or <see langword="null"/> when the date matches
    /// the valuation date or cannot be determined.</returns>
    DateOnly? ExtractActualDate(IReadOnlyList<object> records, DateOnly valuationDate) => null;
}
