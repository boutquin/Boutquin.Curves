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

using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Recipes.Tests;

/// <summary>
/// Tests for <see cref="StandardCurveRecipes"/> verifying node counts, tenor ordering,
/// convention resolvability, and curve-id uniqueness across all eight recipe methods.
/// </summary>
public sealed class StandardCurveRecipesTests
{
    [Fact]
    public void UsdSofrDiscount_Has_Expected_Node_Count()
    {
        // Act
        var group = StandardCurveRecipes.UsdSofrDiscount();

        // Assert
        group.Curves.Should().HaveCount(1);
        group.Curves[0].Nodes.Should().HaveCount(11);
    }

    [Fact]
    public void UsdSofrDiscount_Nodes_In_Ascending_Tenor_Order()
    {
        // Arrange
        var group = StandardCurveRecipes.UsdSofrDiscount();
        var tenors = group.Curves[0].Nodes.Select(n => n.Tenor.Value).ToList();

        // Act — convert each tenor to approximate days for comparison
        var days = tenors.Select(TenorToDays).ToList();

        // Assert
        days.Should().BeInAscendingOrder();
    }

    [Fact]
    public void UsdSofrDiscount_All_Convention_Codes_Resolvable()
    {
        // Arrange
        var registry = InstrumentConventionRegistry.CreateDefault();
        var group = StandardCurveRecipes.UsdSofrDiscount();

        // Act & Assert — none of the GetRequired calls should throw
        foreach (var node in group.Curves[0].Nodes)
        {
            var act = () => registry.GetRequired(node.ConventionCode);
            act.Should().NotThrow(
                $"convention '{node.ConventionCode}' for node '{node.Label}' should be resolvable");
        }
    }

    [Fact]
    public void CadCorraDiscount_Has_Expected_Node_Count()
    {
        // Act
        var group = StandardCurveRecipes.CadCorraDiscount();

        // Assert
        group.Curves.Should().HaveCount(1);
        group.Curves[0].Nodes.Should().HaveCount(10);
    }

    [Fact]
    public void GbpSoniaDiscount_Has_Expected_Node_Count()
    {
        // Act
        var group = StandardCurveRecipes.GbpSoniaDiscount();

        // Assert
        group.Curves.Should().HaveCount(1);
        group.Curves[0].Nodes.Should().HaveCount(11);
    }

    [Fact]
    public void EurEstrDiscount_Has_Expected_Node_Count()
    {
        // Act
        var group = StandardCurveRecipes.EurEstrDiscount();

        // Assert
        group.Curves.Should().HaveCount(1);
        group.Curves[0].Nodes.Should().HaveCount(11);
    }

    [Fact]
    public void Each_Recipe_Has_Unique_Curve_Id()
    {
        // Arrange
        var curveIds = new[]
        {
            StandardCurveRecipes.UsdSofrDiscount(),
            StandardCurveRecipes.UsdSofrProjection(),
            StandardCurveRecipes.CadCorraDiscount(),
            StandardCurveRecipes.CadCorraProjection(),
            StandardCurveRecipes.GbpSoniaDiscount(),
            StandardCurveRecipes.GbpSoniaProjection(),
            StandardCurveRecipes.EurEstrDiscount(),
            StandardCurveRecipes.EurEstrProjection(),
        }
        .SelectMany(g => g.Curves.Select(c => c.CurveId))
        .ToList();

        // Act & Assert
        curveIds.Should().HaveCount(8);
        curveIds.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// Converts a tenor string to approximate calendar days for ordering comparisons.
    /// </summary>
    private static int TenorToDays(string tenor)
    {
        if (string.Equals(tenor, "ON", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        var unit = tenor[^1];
        var number = int.Parse(tenor[..^1], System.Globalization.CultureInfo.InvariantCulture);

        return unit switch
        {
            'D' => number,
            'W' => number * 7,
            'M' => number * 30,
            'Y' => number * 365,
            _ => throw new ArgumentException($"Unsupported tenor unit: {unit}"),
        };
    }
}
