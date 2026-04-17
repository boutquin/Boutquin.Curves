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
using Boutquin.Curves.Recipes.Nodes;
using Boutquin.MarketData.Abstractions.Records;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Abstractions.Requests;
using FluentAssertions;
using MdReferenceData = Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Recipes.Tests.Nodes;

/// <summary>
/// Tests for <see cref="ICurveNodeSpec"/> implementations: <see cref="OvernightFixingNode"/>,
/// <see cref="YieldCurveNode"/>, and <see cref="FuturesSettlementNode"/>.
/// </summary>
public sealed class CurveNodeSpecTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly CurveReference s_usdSofrDisc = new(CurveRole.Discount, CurrencyCode.USD);

    // --- OvernightFixingNode ---

    [Fact]
    public void OvernightFixingNode_CreateRequest_Returns_OvernightFixingRequest()
    {
        // Arrange
        var node = new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc);

        // Act
        var request = node.CreateRequest(s_valuationDate);

        // Assert
        request.Should().BeOfType<OvernightFixingRequest>();
        request.DatasetKey.Should().Be("SOFR");
    }

    [Fact]
    public void OvernightFixingNode_ExtractRate_Picks_Most_Recent_Observation()
    {
        // Arrange
        var node = new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc);
        var records = new List<object>
        {
            new ScalarObservation(s_valuationDate.AddDays(-2), 0.0430m, "decimal"),
            new ScalarObservation(s_valuationDate.AddDays(-1), 0.0432m, "decimal"),
            new ScalarObservation(s_valuationDate, 0.0435m, "decimal"),
        };

        // Act
        var rate = node.ExtractRate(records, s_valuationDate);

        // Assert
        rate.Should().Be(0.0435m);
    }

    [Fact]
    public void OvernightFixingNode_ExtractRate_Returns_Null_When_No_Records()
    {
        // Arrange
        var node = new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc);

        // Act
        var rate = node.ExtractRate(new List<object>(), s_valuationDate);

        // Assert
        rate.Should().BeNull();
    }

    // --- YieldCurveNode ---

    [Fact]
    public void YieldCurveNode_CreateRequest_Returns_YieldCurveQuoteRequest()
    {
        // Arrange
        var node = new YieldCurveNode("2Y", new Tenor("2Y"), "FixedFloatSwap", "UST", "2Y", "USD-SOFR-OIS", s_usdSofrDisc);

        // Act
        var request = node.CreateRequest(s_valuationDate);

        // Assert
        request.Should().BeOfType<YieldCurveQuoteRequest>();
        request.DatasetKey.Should().Be("UST");
    }

    [Fact]
    public void YieldCurveNode_ExtractRate_Matches_By_Tenor()
    {
        // Arrange
        var node = new YieldCurveNode("2Y", new Tenor("2Y"), "FixedFloatSwap", "UST", "2Y", "USD-SOFR-OIS", s_usdSofrDisc);
        var records = new List<object>
        {
            new YieldCurveQuote("1Y", 0.040m),
            new YieldCurveQuote("2Y", 0.042m),
            new YieldCurveQuote("5Y", 0.045m),
        };

        // Act
        var rate = node.ExtractRate(records, s_valuationDate);

        // Assert
        rate.Should().Be(0.042m);
    }

    [Fact]
    public void YieldCurveNode_ExtractRate_Returns_Null_When_Tenor_Not_Found()
    {
        // Arrange
        var node = new YieldCurveNode("10Y", new Tenor("10Y"), "FixedFloatSwap", "UST", "10Y", "USD-SOFR-OIS", s_usdSofrDisc);
        var records = new List<object>
        {
            new YieldCurveQuote("1Y", 0.040m),
            new YieldCurveQuote("2Y", 0.042m),
        };

        // Act
        var rate = node.ExtractRate(records, s_valuationDate);

        // Assert
        rate.Should().BeNull();
    }

    // --- FuturesSettlementNode ---

    [Fact]
    public void FuturesSettlementNode_ExtractRate_Converts_Price_To_Rate()
    {
        // Arrange
        var node = new FuturesSettlementNode("SR3-Jun26", new Tenor("3M"), "SR3", "2026-06", "CME-SOFR-FUT", s_usdSofrDisc);
        var records = new List<object>
        {
            new FuturesSettlement(s_valuationDate, new MdReferenceData.FuturesProductCode("SR3"), new MdReferenceData.ContractMonth("2026-06"), 95.70m, null),
        };

        // Act
        var rate = node.ExtractRate(records, s_valuationDate);

        // Assert
        rate.Should().Be(0.0430m); // (100 - 95.70) / 100
    }

    [Fact]
    public void FuturesSettlementNode_ExtractRate_Matches_Contract_Month()
    {
        // Arrange
        var node = new FuturesSettlementNode("SR3-Sep26", new Tenor("6M"), "SR3", "2026-09", "CME-SOFR-FUT", s_usdSofrDisc);
        var records = new List<object>
        {
            new FuturesSettlement(s_valuationDate, new MdReferenceData.FuturesProductCode("SR3"), new MdReferenceData.ContractMonth("2026-06"), 95.70m, null),
            new FuturesSettlement(s_valuationDate, new MdReferenceData.FuturesProductCode("SR3"), new MdReferenceData.ContractMonth("2026-09"), 95.50m, null),
        };

        // Act
        var rate = node.ExtractRate(records, s_valuationDate);

        // Assert
        rate.Should().Be(0.0450m); // (100 - 95.50) / 100
    }

    [Fact]
    public void FuturesSettlementNode_CreateRequest_Returns_FuturesSettlementRequest()
    {
        // Arrange
        var node = new FuturesSettlementNode("SR3-Jun26", new Tenor("3M"), "SR3", "2026-06", "CME-SOFR-FUT", s_usdSofrDisc);

        // Act
        var request = node.CreateRequest(s_valuationDate);

        // Assert
        request.Should().BeOfType<FuturesSettlementRequest>();
        request.DatasetKey.Should().Be("SR3");
    }
}
