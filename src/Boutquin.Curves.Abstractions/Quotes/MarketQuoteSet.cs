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

using Boutquin.Curves.Abstractions.Identifiers;

namespace Boutquin.Curves.Abstractions.Quotes;

/// <summary>
/// Represents a coherent set of market quotes for a single valuation date.
/// </summary>
/// <remarks>
/// Pipeline context: MarketQuoteSet is the bridge between data aggregation and downstream consumers.
/// The aggregation service collects quotes from multiple adapters into this set.
/// </remarks>
/// <param name="AsOfDate">Market date associated with all quotes in the set.</param>
/// <param name="Quotes">Dictionary of quotes keyed by <see cref="QuoteId"/>.</param>
public sealed record MarketQuoteSet(
    DateOnly AsOfDate,
    IReadOnlyDictionary<QuoteId, MarketQuote> Quotes)
{
    /// <summary>
    /// Returns a quote by id and throws when the quote is missing.
    /// </summary>
    /// <param name="quoteId">Identifier of the quote to retrieve.</param>
    /// <returns>Quote associated with <paramref name="quoteId"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the quote id is not present in <see cref="Quotes"/>.</exception>
    public MarketQuote GetRequired(QuoteId quoteId)
    {
        if (!Quotes.TryGetValue(quoteId, out MarketQuote? quote))
        {
            throw new KeyNotFoundException($"Quote '{quoteId}' was not found in the quote set.");
        }

        return quote;
    }
}
