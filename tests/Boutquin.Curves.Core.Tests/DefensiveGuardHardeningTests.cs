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

// M-1 defensive-guard hardening tests:
// A-4: QuoteNormalizer.NormalizeForNode / .Normalize — null quote / nodeType guards
// A-5: CurveGroup..ctor — null curves guard

using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Abstractions.Quotes;
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

public sealed class DefensiveGuardHardeningTests
{
    // ── A-4a: QuoteNormalizer.Normalize — null quote ───────────────────────

    [Fact]
    public void QuoteNormalizer_Normalize_NullQuote_Throws_ArgumentNullException_NamedQuote()
    {
        // AC-G1: null quote must throw ArgumentNullException naming "quote"
        var normalizer = new QuoteNormalizer();

        Action act = () => normalizer.Normalize(
            null!,
            QuoteValueType.Rate,
            MarketInstrumentType.Ois,
            "SOFR-OIS",
            "SOFR 1Y");

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("quote");
    }

    // ── A-4b: QuoteNormalizer.NormalizeForNode — null quote ───────────────

    [Fact]
    public void QuoteNormalizer_NormalizeForNode_NullQuote_Throws_ArgumentNullException_NamedQuote()
    {
        // AC-G1: null quote must throw ArgumentNullException naming "quote"
        var normalizer = new QuoteNormalizer();

        Action act = () => normalizer.NormalizeForNode(
            null!,
            "OIS",
            QuoteValueType.Rate,
            "SOFR-OIS",
            "SOFR 1Y");

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("quote");
    }

    // ── A-4c: QuoteNormalizer.NormalizeForNode — null nodeType ────────────

    [Fact]
    public void QuoteNormalizer_NormalizeForNode_NullNodeType_Throws_ArgumentException_NamedNodeType()
    {
        // AC-G1: null nodeType must throw ArgumentNullException or ArgumentException naming "nodeType"
        var normalizer = new QuoteNormalizer();
        var quote = new MarketQuote(new QuoteId("q1"), 0.05m, "RATE", new DateOnly(2026, 6, 30));

        Action act = () => normalizer.NormalizeForNode(
            quote,
            null!,
            QuoteValueType.Rate,
            "SOFR-OIS",
            "SOFR 1Y");

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("nodeType");
    }

    // ── A-5: CurveGroup..ctor — null curves dict ──────────────────────────

    [Fact]
    public void CurveGroup_Constructor_NullCurves_Throws_ArgumentNullException_NamedCurves()
    {
        // AC-G1: null curves must throw ArgumentNullException naming "curves"
        // Currently passes null through, deferring NRE until first accessor.
        Action act = () => new CurveGroup(
            new CurveGroupName("TestGroup"),
            new DateOnly(2026, 6, 30),
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("curves");
    }
}
