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
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Recipes.Tests.Nodes;

/// <summary>
/// Validates that the public curve-node constructors guard their inputs (CF-C5-4 / F-L2-04),
/// failing fast at construction time rather than deferring crashes to obscure downstream sites.
/// </summary>
public sealed class NodeConstructorValidationTests
{
    private static readonly CurveReference s_curve = new(CurveRole.Discount, Currency.USD);

    // --- OvernightFixingNode ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OvernightFixingNode_WithNullOrWhitespaceLabel_Throws(string? label)
    {
        var act = () => new OvernightFixingNode(label!, "SOFR", "USD-SOFR-OIS", s_curve);
        act.Should().Throw<ArgumentException>().WithParameterName(nameof(label));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OvernightFixingNode_WithNullOrWhitespaceBenchmarkId_Throws(string? benchmarkId)
    {
        var act = () => new OvernightFixingNode("SOFR", benchmarkId!, "USD-SOFR-OIS", s_curve);
        act.Should().Throw<ArgumentException>().WithParameterName(nameof(benchmarkId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OvernightFixingNode_WithNullOrWhitespaceConventionCode_Throws(string? conventionCode)
    {
        var act = () => new OvernightFixingNode("SOFR", "SOFR", conventionCode!, s_curve);
        act.Should().Throw<ArgumentException>().WithParameterName(nameof(conventionCode));
    }

    [Fact]
    public void OvernightFixingNode_WithNullTargetCurve_Throws()
    {
        var act = () => new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("targetCurve");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OvernightFixingNode_WithNonPositiveLookbackDays_Throws(int lookbackDays)
    {
        var act = () => new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_curve, lookbackDays: lookbackDays);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(lookbackDays));
    }

    [Fact]
    public void OvernightFixingNode_WithNegativeShiftDays_Throws()
    {
        var act = () => new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_curve, shiftDays: -1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("shiftDays");
    }

    // --- YieldCurveNode ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void YieldCurveNode_WithNullOrWhitespaceLabel_Throws(string? label)
    {
        var act = () => new YieldCurveNode(label!, new Tenor("1Y"), "Swap", "USD-TREASURY", "1Y", "USD-SOFR-OIS", s_curve);
        act.Should().Throw<ArgumentException>().WithParameterName(nameof(label));
    }

    [Fact]
    public void YieldCurveNode_WithNullTargetCurve_Throws()
    {
        var act = () => new YieldCurveNode("1Y", new Tenor("1Y"), "Swap", "USD-TREASURY", "1Y", "USD-SOFR-OIS", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("targetCurve");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void YieldCurveNode_WithNullOrWhitespaceCurveId_Throws(string? curveId)
    {
        var act = () => new YieldCurveNode("1Y", new Tenor("1Y"), "Swap", curveId!, "1Y", "USD-SOFR-OIS", s_curve);
        act.Should().Throw<ArgumentException>().WithParameterName(nameof(curveId));
    }

    // --- FuturesSettlementNode ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FuturesSettlementNode_WithNullOrWhitespaceLabel_Throws(string? label)
    {
        var act = () => new FuturesSettlementNode(label!, new Tenor("3M"), "SR3", "2026-06", "USD-SOFR-OIS", s_curve);
        act.Should().Throw<ArgumentException>().WithParameterName(nameof(label));
    }

    [Fact]
    public void FuturesSettlementNode_WithNullTargetCurve_Throws()
    {
        var act = () => new FuturesSettlementNode("SR3", new Tenor("3M"), "SR3", "2026-06", "USD-SOFR-OIS", null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("targetCurve");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FuturesSettlementNode_WithNullOrWhitespaceProductCode_Throws(string? productCode)
    {
        var act = () => new FuturesSettlementNode("SR3", new Tenor("3M"), productCode!, "2026-06", "USD-SOFR-OIS", s_curve);
        act.Should().Throw<ArgumentException>().WithParameterName(nameof(productCode));
    }

    [Theory]
    [InlineData("Jun26")]
    [InlineData("2026/06")]
    [InlineData("2026-13")]
    public void FuturesSettlementNode_WithMalformedContractMonth_ThrowsAtConstruction(string contractMonth)
    {
        // A non-whitespace but malformed contractMonth must fail fast at construction
        // (CF-C5-4 / F-L2-04) rather than deferring an ArgumentException to ExtractRate,
        // which escapes CurveBuilder's fetch try/catch and aborts the whole BuildAsync.
        var act = () => new FuturesSettlementNode("SR3", new Tenor("3M"), "SR3", contractMonth, "USD-SOFR-OIS", s_curve);
        act.Should().Throw<ArgumentException>().WithParameterName(nameof(contractMonth));
    }
}
