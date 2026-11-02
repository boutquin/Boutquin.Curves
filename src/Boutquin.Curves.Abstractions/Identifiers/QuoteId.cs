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

using System.Text.Json.Serialization;
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
///
/// <b>Default-value hazard:</b> because <c>QuoteId</c> is a value type, the
/// parameterless <c>default(QuoteId)</c> instance bypasses this constructor and
/// leaves <see cref="Value"/> as <see langword="null"/> at the CLR level. Code that
/// allocates arrays (<c>new QuoteId[N]</c>), uses uninitialized fields, or produces
/// explicit <c>default</c> literals yields such instances without invoking this
/// constructor. <see cref="ToString"/> returns <c>"&lt;uninitialized&gt;"</c> for
/// those instances so that logging and diagnostics do not throw a
/// <see cref="NullReferenceException"/>; however, using a default instance as a
/// dictionary key or wire identifier remains a logic error.
/// </remarks>
/// <exception cref="ArgumentException">
/// Thrown when the value supplied to the constructor is <see langword="null"/>,
/// empty, or consists solely of whitespace characters.
/// </exception>
public readonly record struct QuoteId
{
    /// <summary>
    /// The canonical string that identifies this market quote.
    /// Always non-null on instances created via <see cref="QuoteId(string)"/>.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new <see cref="QuoteId"/> with the specified value.
    /// </summary>
    /// <param name="value">Quote identifier value. Must be non-null and non-whitespace.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    [JsonConstructor]
    public QuoteId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        Value = value;
    }

    /// <summary>
    /// Returns the underlying quote-id value, or <c>"&lt;uninitialized&gt;"</c> for a
    /// <c>default(QuoteId)</c> instance whose <see cref="Value"/> is <see langword="null"/>.
    /// </summary>
    /// <returns>String representation of this quote id. Never returns <see langword="null"/>.</returns>
    public override string ToString() => Value ?? "<uninitialized>";
}
