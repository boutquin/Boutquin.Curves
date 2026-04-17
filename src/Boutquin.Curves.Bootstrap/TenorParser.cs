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

using System.Globalization;
using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Bootstrap;

/// <summary>
/// Parses tenor strings and applies tenor offsets to valuation dates.
/// </summary>
/// <remarks>
/// Tenor normalization is foundational in calibration pipelines because node maturities are often expressed
/// as symbolic market tenors (for example 3M or 5Y) rather than explicit dates. This parser handles simple
/// calendar-period offsets and is intentionally strict about accepted formats.
/// </remarks>
public static class TenorParser
{
    /// <summary>
    /// Adds a tenor offset to a date.
    /// </summary>
    /// <param name="date">Base date to shift.</param>
    /// <param name="tenor">Tenor expression such as 3M, 2Y, 1W, or 10D.</param>
    /// <returns>Date obtained after applying the tenor offset to <paramref name="date"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="tenor"/> does not end with D, W, M, or Y.</exception>
    /// <remarks>
    /// This method applies pure calendar arithmetic via DateOnly AddDays/AddMonths/AddYears.
    /// Business-day adjustment is intentionally left to convention-aware schedule generation layers.
    /// Common mistake: assuming tenor resolution uses simple calendar arithmetic. '3M' from January 30
    /// resolves to April 30 (or the adjusted business date), not 'January 30 plus 90 days.' For
    /// month-end dates, roll conventions and business day adjustments can shift the pillar date by
    /// several days, which matters for short-dated instruments where a few days significantly changes
    /// the year fraction.
    /// </remarks>
    public static DateOnly AddTenor(DateOnly date, Tenor tenor)
    {
        string value = tenor.Value.Trim().ToUpperInvariant();

        // Overnight tenor: O/N or ON maps to T+1 calendar day.
        if (value is "ON" or "O/N")
        {
            return date.AddDays(1);
        }

        if (value.EndsWith('D'))
        {
            return date.AddDays(int.Parse(value[..^1], CultureInfo.InvariantCulture));
        }

        if (value.EndsWith('W'))
        {
            return date.AddDays(7 * int.Parse(value[..^1], CultureInfo.InvariantCulture));
        }

        if (value.EndsWith('M'))
        {
            return date.AddMonths(int.Parse(value[..^1], CultureInfo.InvariantCulture));
        }

        if (value.EndsWith('Y'))
        {
            return date.AddYears(int.Parse(value[..^1], CultureInfo.InvariantCulture));
        }

        throw new NotSupportedException($"Unsupported tenor format: '{tenor.Value}'.");
    }
}
