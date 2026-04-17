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

namespace Boutquin.Curves.Abstractions.Identifiers;

/// <summary>
/// Represents a stable quote identifier used as the key in quote sets.
/// </summary>
/// <remarks>
/// Quote IDs follow a <c>"SOURCE:INSTRUMENT-TENOR"</c> convention. The source prefix
/// (e.g., <c>"NYFED"</c>, <c>"CME"</c>, <c>"UST"</c>) identifies the data provider
/// and enables the data-aggregation layer to route fetch requests to the correct
/// adapter. The instrument-tenor suffix (e.g., <c>"SOFR"</c>, <c>"USD-SOFR-OIS-1M"</c>,
/// <c>"PAR-10Y"</c>) maps the quote to a specific curve node during bootstrap
/// calibration. Wrapping the raw string in a strongly-typed record ensures that
/// quote identifiers cannot be accidentally interchanged with other string-based
/// identifiers such as <see cref="CurveName"/> or <see cref="BenchmarkName"/>.
/// </remarks>
/// <param name="Value">Quote identifier value.</param>
public readonly record struct QuoteId(string Value)
{
    /// <summary>
    /// Returns the underlying quote-id value.
    /// </summary>
    /// <returns>String representation of this quote id.</returns>
    public override string ToString() => Value;
}
