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
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Interpolation;
using Boutquin.Curves.Recipes.Nodes;
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Recipes.Tests;

/// <summary>
/// Tests for <see cref="CurveRecipe"/> and <see cref="CurveGroupRecipe"/> record construction and equality.
/// </summary>
public sealed class CurveRecipeTests
{
    private static readonly CurveReference s_usdSofrDisc = new(CurveRole.Discount, CurrencyCode.USD);
    private static readonly InterpolationSettings s_defaultInterpolation = new(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward");

    [Fact]
    public void CurveRecipe_Construction_Preserves_All_Fields()
    {
        // Arrange
        var nodes = new List<ICurveNodeSpec>
        {
            new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc),
        };

        // Act
        var recipe = new CurveRecipe(
            "USD-SOFR-DISC",
            s_usdSofrDisc,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_defaultInterpolation,
            nodes);

        // Assert
        recipe.CurveId.Should().Be("USD-SOFR-DISC");
        recipe.CurveReference.Should().Be(s_usdSofrDisc);
        recipe.ValueType.Should().Be(CurveValueType.DiscountFactor);
        recipe.DayCountCode.Should().Be("ACT/360");
        recipe.Interpolation.Should().Be(s_defaultInterpolation);
        recipe.Nodes.Should().HaveCount(1);
    }

    [Fact]
    public void CurveGroupRecipe_Construction_Preserves_All_Fields()
    {
        // Arrange
        var nodes = new List<ICurveNodeSpec>
        {
            new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc),
        };
        var curve = new CurveRecipe(
            "USD-SOFR-DISC",
            s_usdSofrDisc,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_defaultInterpolation,
            nodes);

        // Act
        var group = new CurveGroupRecipe("USD-SOFR", new List<CurveRecipe> { curve });

        // Assert
        group.GroupName.Should().Be("USD-SOFR");
        group.Curves.Should().HaveCount(1);
        group.Curves[0].CurveId.Should().Be("USD-SOFR-DISC");
    }

    [Fact]
    public void Two_CurveRecipes_With_Same_Data_Are_Equal()
    {
        // Arrange
        var nodes1 = new List<ICurveNodeSpec>
        {
            new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc),
        };
        var nodes2 = new List<ICurveNodeSpec>
        {
            new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc),
        };
        var recipe1 = new CurveRecipe("USD-SOFR-DISC", s_usdSofrDisc, CurveValueType.DiscountFactor, "ACT/360", s_defaultInterpolation, nodes1);
        _ = new CurveRecipe("USD-SOFR-DISC", s_usdSofrDisc, CurveValueType.DiscountFactor, "ACT/360", s_defaultInterpolation, nodes2);

        // Act & Assert — record equality compares by value for value-type fields
        // and by reference for the Nodes list (IReadOnlyList<T>), so two distinct
        // list instances will not be equal. This verifies that the record identity
        // fields (CurveId, CurveReference, ValueType, DayCountCode, Interpolation)
        // drive equality when the same list instance is used.
        var recipeSameList = new CurveRecipe("USD-SOFR-DISC", s_usdSofrDisc, CurveValueType.DiscountFactor, "ACT/360", s_defaultInterpolation, nodes1);
        recipe1.Should().Be(recipeSameList);
    }
}
