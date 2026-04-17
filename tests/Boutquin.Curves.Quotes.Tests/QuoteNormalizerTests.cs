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
using Boutquin.Curves.Abstractions.Quotes;
using FluentAssertions;

namespace Boutquin.Curves.Quotes.Tests;

public sealed class QuoteNormalizerTests
{
    [Fact]
    public void Normalize_ShouldMapInputQuote()
    {
        QuoteNormalizer normalizer = new();
        MarketQuote quote = new(new QuoteId("Q1"), 99.125m, "Last", new DateOnly(2026, 4, 9));

        NormalizedQuote normalized = normalizer.Normalize(quote, QuoteValueType.Price, MarketInstrumentType.StirFuture, "USD-SR3", "Jun26");

        normalized.ConventionCode.Should().Be("USD-SR3");
    }

    [Fact]
    public void NormalizeForNode_ShouldConvertOisFuturePriceToRate()
    {
        QuoteNormalizer normalizer = new();
        MarketQuote quote = new(new QuoteId("Q2"), 99.125m, "Last", new DateOnly(2026, 4, 9));

        NormalizedQuote normalized = normalizer.NormalizeForNode(
            quote,
            "OisFuture",
            QuoteValueType.Price,
            "USD-SR3",
            "Jun26");

        normalized.InstrumentType.Should().Be(MarketInstrumentType.OisFuture);
        normalized.ValueType.Should().Be(QuoteValueType.Rate);
        normalized.Value.Should().BeApproximately(0.00875m, 0.0000001m);
    }

    [Fact]
    public void NormalizeForNode_ShouldConvertBasisSwapSpreadFromBpsToDecimalRate()
    {
        QuoteNormalizer normalizer = new();
        MarketQuote quote = new(new QuoteId("Q3"), 12.5m, "Mid", new DateOnly(2026, 4, 9));

        NormalizedQuote normalized = normalizer.NormalizeForNode(
            quote,
            "BasisSwap",
            QuoteValueType.Spread,
            "USD-BASIS",
            "5Y Basis");

        normalized.InstrumentType.Should().Be(MarketInstrumentType.BasisSwap);
        normalized.ValueType.Should().Be(QuoteValueType.Rate);
        normalized.Value.Should().BeApproximately(0.00125m, 0.0000001m);
    }
}
