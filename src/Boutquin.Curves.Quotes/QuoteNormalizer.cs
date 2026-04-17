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

using Boutquin.Curves.Abstractions.Quotes;

namespace Boutquin.Curves.Quotes;

/// <summary>
/// Normalizes raw market quotes into the canonical quote shape used by curve calibration.
/// </summary>
/// <remarks>
/// Two normalization paths are available: <see cref="Normalize"/> passes the raw value through unchanged,
/// while <see cref="NormalizeForNode"/> applies instrument-type-specific transformations (e.g. futures
/// price-to-rate, spread-to-decimal). The class is stateless and safe for concurrent use.
///
/// Pipeline context: QuoteNormalizer sits between raw adapter output and bootstrap input.
/// Adapters may provide rates in different units or conventions; the normalizer ensures all
/// quotes are in the decimal-rate form expected by instrument helpers.
/// </remarks>
/// <example>
/// <code>
/// var normalizer = new QuoteNormalizer();
/// NormalizedQuote normalized = normalizer.NormalizeForNode(
///     rawQuote, "OIS", QuoteValueType.Rate, "SOFR-OIS", "SOFR 1Y OIS");
/// // normalized.Value is in decimal form (0.0475, not 4.75)
/// </code>
/// </example>
/// <seealso cref="Boutquin.Curves.Abstractions.Quotes.MarketQuoteSet"/>
public sealed class QuoteNormalizer
{
    /// <summary>
    /// Creates a normalized quote by combining raw quote value and explicit metadata.
    /// </summary>
    /// <param name="quote">Raw market quote carrying identifier and numeric value.</param>
    /// <param name="valueType">Quote interpretation (rate, price, spread, or basis-point form).</param>
    /// <param name="instrumentType">Instrument type associated with the quote.</param>
    /// <param name="conventionCode">Convention code used by downstream helpers and pricers.</param>
    /// <param name="label">Human-readable label for diagnostics and reporting.</param>
    /// <returns>Normalized quote instance preserving quote identity and supplied metadata.</returns>
    /// <remarks>
    /// This overload passes the raw <c>quote.Value</c> through unchanged. Use <see cref="NormalizeForNode"/>
    /// when instrument-type-specific value transformations are needed.
    /// </remarks>
    public NormalizedQuote Normalize(
        MarketQuote quote,
        QuoteValueType valueType,
        MarketInstrumentType instrumentType,
        string conventionCode,
        string label)
    {
        return new NormalizedQuote(
            quote.Id,
            quote.Value,
            valueType,
            instrumentType,
            conventionCode,
            label);
    }

    /// <summary>
    /// Normalizes a quote using explicit node-type rules and canonical instrument mapping.
    /// </summary>
    /// <param name="quote">Raw market quote carrying identifier and numeric value.</param>
    /// <param name="nodeType">Bootstrap node type label such as Deposit, OisFuture, Fra, or FixedFloatSwap.</param>
    /// <param name="rawValueType">Raw quote interpretation before node-specific normalization rules.</param>
    /// <param name="conventionCode">Convention code used by downstream helpers and pricers.</param>
    /// <param name="label">Human-readable label for diagnostics and reporting.</param>
    /// <returns>Normalized quote with explicit node-type value transformations applied.</returns>
    /// <remarks>
    /// Futures quoted as prices are converted to rates via <c>(100 - price) / 100</c>.
    /// Basis swaps and FRAs quoted in spread form are converted from basis points to decimal via <c>value / 10000</c>.
    /// Unrecognized node types map to <see cref="MarketInstrumentType.Custom"/> with no value transformation.
    /// </remarks>
    public NormalizedQuote NormalizeForNode(
        MarketQuote quote,
        string nodeType,
        QuoteValueType rawValueType,
        string conventionCode,
        string label)
    {
        MarketInstrumentType instrumentType = MapInstrumentType(nodeType);
        decimal normalizedValue = quote.Value;
        QuoteValueType normalizedValueType = rawValueType;

        // Futures: convert IMM-style 100-minus-rate price to decimal rate.
        if ((instrumentType is MarketInstrumentType.OisFuture or MarketInstrumentType.StirFuture) && rawValueType == QuoteValueType.Price)
        {
            normalizedValue = (100m - quote.Value) / 100m;
            normalizedValueType = QuoteValueType.Rate;
        }
        // Basis/FRA: convert basis-points spread to decimal rate.
        else if ((instrumentType is MarketInstrumentType.BasisSwap or MarketInstrumentType.Fra) && rawValueType == QuoteValueType.Spread)
        {
            normalizedValue = quote.Value / 10000m;
            normalizedValueType = QuoteValueType.Rate;
        }

        return new NormalizedQuote(
            quote.Id,
            normalizedValue,
            normalizedValueType,
            instrumentType,
            conventionCode,
            label);
    }

    private static MarketInstrumentType MapInstrumentType(string nodeType)
    {
        return nodeType.Trim().ToUpperInvariant() switch
        {
            "DEPOSIT" => MarketInstrumentType.Deposit,
            "OIS" => MarketInstrumentType.Ois,
            "OISFUTURE" => MarketInstrumentType.OisFuture,
            "STIRFUTURE" => MarketInstrumentType.StirFuture,
            "FRA" => MarketInstrumentType.Fra,
            "FIXEDFLOATSWAP" => MarketInstrumentType.FixedFloatSwap,
            "BASISSWAP" => MarketInstrumentType.BasisSwap,
            _ => MarketInstrumentType.Custom
        };
    }
}
