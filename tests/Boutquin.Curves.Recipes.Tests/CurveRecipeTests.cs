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
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.Curves.Bootstrap;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Interpolation;
using Boutquin.Curves.Recipes.Nodes;
using Boutquin.Curves.Recipes.Testing;
using Boutquin.Domain.Enumerations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Curves.Recipes.Tests;

/// <summary>
/// Tests for <see cref="CurveRecipe"/> and <see cref="CurveGroupRecipe"/> record construction and equality.
/// </summary>
public sealed class CurveRecipeTests
{
    private static readonly CurveReference s_usdSofrDisc = new(CurveRole.Discount, Currency.USD);
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

    // -------------------------------------------------------------------------
    // AT-15 — CurveGroupRecipe null/empty Curves guard
    // -------------------------------------------------------------------------

    /// <summary>
    /// AT-15: CurveGroupRecipe must reject null Curves at construction rather than
    /// propagating as an NRE later inside BuildAsync.
    /// </summary>
    [Fact]
    public void CurveGroupRecipe_NullCurves_ThrowsAtConstruction()
    {
        Action act = () => _ = new CurveGroupRecipe("USD-SOFR", null!);

        act.Should().Throw<ArgumentNullException>(
            because: "passing null Curves must be caught at construction, not deferred to BuildAsync");
    }

    /// <summary>
    /// AT-15: CurveGroupRecipe must reject an empty Curves list at construction.
    /// An empty list produces a silent no-op build (zero curves resolved) which
    /// is indistinguishable from a data failure without explicit validation.
    /// </summary>
    [Fact]
    public void CurveGroupRecipe_EmptyCurves_ThrowsAtConstruction()
    {
        Action act = () => _ = new CurveGroupRecipe("USD-SOFR", Array.Empty<CurveRecipe>());

        act.Should().Throw<ArgumentException>(
            because: "passing an empty Curves list must be caught at construction");
    }

    // -------------------------------------------------------------------------
    // AT-16 — CurveRecipe null identity-field and null Nodes guard
    // -------------------------------------------------------------------------

    /// <summary>
    /// AT-16: CurveRecipe must reject null Nodes at construction.
    /// </summary>
    [Fact]
    public void CurveRecipe_NullNodes_ThrowsAtConstruction()
    {
        Action act = () => _ = new CurveRecipe(
            "USD-SOFR-DISC",
            s_usdSofrDisc,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_defaultInterpolation,
            null!);

        act.Should().Throw<ArgumentNullException>(
            because: "null Nodes must be caught at construction, not deferred to BuildAsync");
    }

    /// <summary>
    /// AT-16: CurveRecipe must reject a null CurveId at construction.
    /// </summary>
    [Fact]
    public void CurveRecipe_NullCurveId_ThrowsAtConstruction()
    {
        var nodes = new List<ICurveNodeSpec>
        {
            new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc),
        };

        Action act = () => _ = new CurveRecipe(
            null!,
            s_usdSofrDisc,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_defaultInterpolation,
            nodes);

        act.Should().Throw<ArgumentNullException>(
            because: "null CurveId must be rejected at construction");
    }

    /// <summary>
    /// AT-16: CurveRecipe must reject a null DayCountCode at construction.
    /// </summary>
    [Fact]
    public void CurveRecipe_NullDayCountCode_ThrowsAtConstruction()
    {
        var nodes = new List<ICurveNodeSpec>
        {
            new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc),
        };

        Action act = () => _ = new CurveRecipe(
            "USD-SOFR-DISC",
            s_usdSofrDisc,
            CurveValueType.DiscountFactor,
            null!,
            s_defaultInterpolation,
            nodes);

        act.Should().Throw<ArgumentNullException>(
            because: "null DayCountCode must be rejected at construction");
    }

    /// <summary>
    /// T-C6-4: CurveRecipe must reject a whitespace-only CurveId at construction,
    /// matching the sibling CurveGroupRecipe GroupName guard. A whitespace CurveId
    /// becomes a CurveName group key downstream rather than failing fast.
    /// </summary>
    [Fact]
    public void CurveRecipe_WhitespaceCurveId_ThrowsAtConstruction()
    {
        var nodes = new List<ICurveNodeSpec>
        {
            new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc),
        };

        Action act = () => _ = new CurveRecipe(
            "   ",
            s_usdSofrDisc,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_defaultInterpolation,
            nodes);

        act.Should().Throw<ArgumentException>(
            because: "whitespace-only CurveId must be caught at construction, matching the CurveGroupRecipe GroupName guard");
    }

    /// <summary>
    /// T-C6-4: CurveRecipe must reject a whitespace-only DayCountCode at construction.
    /// A whitespace DayCountCode defers a DayCountResolver failure into calibration
    /// instead of failing fast at construction.
    /// </summary>
    [Fact]
    public void CurveRecipe_WhitespaceDayCountCode_ThrowsAtConstruction()
    {
        var nodes = new List<ICurveNodeSpec>
        {
            new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc),
        };

        Action act = () => _ = new CurveRecipe(
            "USD-SOFR-DISC",
            s_usdSofrDisc,
            CurveValueType.DiscountFactor,
            "   ",
            s_defaultInterpolation,
            nodes);

        act.Should().Throw<ArgumentException>(
            because: "whitespace-only DayCountCode must be caught at construction before it reaches the DayCountResolver");
    }

    // -------------------------------------------------------------------------
    // F-L3-01 — CurveRecipe empty Nodes guard (sibling alignment with CurveGroupRecipe)
    // -------------------------------------------------------------------------

    /// <summary>
    /// F-L3-01: CurveRecipe must reject an empty Nodes list at construction.
    /// An empty Nodes list silently produces a pillar-less curve that is dropped
    /// without a DataIssue entry — mirroring the CurveGroupRecipe empty-Curves guard.
    /// </summary>
    [Fact]
    public void CurveRecipe_EmptyNodes_ThrowsAtConstruction()
    {
        Action act = () => _ = new CurveRecipe(
            "USD-SOFR-DISC",
            s_usdSofrDisc,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_defaultInterpolation,
            Array.Empty<ICurveNodeSpec>());

        act.Should().Throw<ArgumentException>(
            because: "passing an empty Nodes list must be caught at construction, matching the CurveGroupRecipe empty-Curves guard");
    }

    // -------------------------------------------------------------------------
    // F-L3-02 — CurveGroupRecipe GroupName null/whitespace guard
    // -------------------------------------------------------------------------

    /// <summary>
    /// F-L3-02: CurveGroupRecipe must reject a null GroupName at construction.
    /// A null GroupName propagates silently into CurveSnapshot.GroupName and
    /// into diagnostics — it must fail fast at construction like other parameters.
    /// </summary>
    [Fact]
    public void CurveGroupRecipe_NullGroupName_ThrowsAtConstruction()
    {
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

        Action act = () => _ = new CurveGroupRecipe(null!, new List<CurveRecipe> { curve });

        act.Should().Throw<ArgumentException>(
            because: "null GroupName must be caught at construction before it propagates to CurveSnapshot");
    }

    /// <summary>
    /// F-L3-02: CurveGroupRecipe must reject a whitespace-only GroupName at construction.
    /// </summary>
    [Fact]
    public void CurveGroupRecipe_WhitespaceGroupName_ThrowsAtConstruction()
    {
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

        Action act = () => _ = new CurveGroupRecipe("   ", new List<CurveRecipe> { curve });

        act.Should().Throw<ArgumentException>(
            because: "whitespace-only GroupName must be caught at construction");
    }

    [Fact]
    public void ShallowRecordClone_PreservesRecipeIdentity()
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

        // Each construction owns an independent protected collection. A shallow record clone
        // retains that immutable snapshot and its record equality identity.
        var recipeClone = recipe1 with { };
        recipe1.Should().Be(recipeClone);
    }
    [Fact]
    public void RecipeCollections_CaptureInputsAndProtectOutput()
    {
        var node = new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc);
        var nodes = new List<ICurveNodeSpec> { node };
        var curve = new CurveRecipe("DISC", s_usdSofrDisc, CurveValueType.DiscountFactor, "ACT/360", s_defaultInterpolation, nodes);
        var curves = new List<CurveRecipe> { curve };
        var group = new CurveGroupRecipe("USD", curves);
        nodes.Clear();
        curves.Clear();
        curve.Nodes.Should().ContainSingle().Which.Should().BeSameAs(node);
        group.Curves.Should().ContainSingle().Which.Should().BeSameAs(curve);
        Action mutate = () => ((IList<ICurveNodeSpec>)curve.Nodes).Clear();
        mutate.Should().Throw<NotSupportedException>();
        var replacements = new List<ICurveNodeSpec> { node };
        var copy = curve with { Nodes = replacements };
        replacements.Clear();
        copy.Nodes.Should().ContainSingle();
    }

    [Theory]
    [InlineData("curveReference")]
    [InlineData("interpolation")]
    public void Constructor_NullCurveReferenceOrInterpolation_Rejects(string parameter)
    {
        Action act = () => new CurveRecipe("DISC", parameter == "curveReference" ? null! : s_usdSofrDisc,
            CurveValueType.DiscountFactor, "ACT/360", parameter == "interpolation" ? null! : s_defaultInterpolation,
            [new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc)]);
        act.Should().Throw<ArgumentNullException>().WithParameterName(parameter);
    }

    [Theory]
    [InlineData("USD-D")]
    [InlineData("USD-P")]
    [InlineData("CAD-D")]
    [InlineData("CAD-P")]
    [InlineData("GBP-D")]
    [InlineData("GBP-P")]
    [InlineData("EUR-D")]
    [InlineData("EUR-P")]
    [InlineData("CHF-D")]
    [InlineData("CHF-P")]
    [InlineData("JPY-D")]
    [InlineData("JPY-P")]
    public async Task EveryStandardRecipe_ResolvesAndRepricesEveryDeclaredNode(string identity)
    {
        CurveGroupRecipe recipe = identity switch
        {
            "USD-D" => StandardCurveRecipes.UsdSofrDiscount(),
            "USD-P" => StandardCurveRecipes.UsdSofrProjection(),
            "CAD-D" => StandardCurveRecipes.CadCorraDiscount(),
            "CAD-P" => StandardCurveRecipes.CadCorraProjection(),
            "GBP-D" => StandardCurveRecipes.GbpSoniaDiscount(),
            "GBP-P" => StandardCurveRecipes.GbpSoniaProjection(),
            "EUR-D" => StandardCurveRecipes.EurEstrDiscount(),
            "EUR-P" => StandardCurveRecipes.EurEstrProjection(),
            "CHF-D" => StandardCurveRecipes.ChfSaronDiscount(),
            "CHF-P" => StandardCurveRecipes.ChfSaronProjection(),
            "JPY-D" => StandardCurveRecipes.JpyTonaDiscount(),
            "JPY-P" => StandardCurveRecipes.JpyTonaProjection(),
            _ => throw new ArgumentException("Unknown test recipe.", nameof(identity))
        };
        using var services = new ServiceCollection().AddAnalytics().BuildServiceProvider();
        var builder = new CurveBuilder(FixtureData.CreatePipeline(), new PiecewiseBootstrapCalibrator(),
            services.GetRequiredService<IReferenceDataProvider>(), NullLogger<CurveBuilder>.Instance);
        CurveSnapshot result = await builder.BuildAsync(recipe, FixtureData.FixtureDate);
        result.Diagnostics.Repricing.Should().HaveCount(recipe.Curves.Sum(curve => curve.Nodes.Count));
        result.Diagnostics.Repricing.Should().AllSatisfy(diagnostic => diagnostic.AbsoluteError.Should().BeLessThan(1e-10));
        foreach (CurveRecipe curve in recipe.Curves)
        {
            result.CurveGroup.TryGetCurve(curve.CurveReference, out _).Should().BeTrue();
        }
    }

}
