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

namespace Boutquin.Curves.ArchitectureTests;

public sealed class DependencyTests : BaseArchitectureTest
{
    [Fact]
    public void Abstractions_ShouldNotDependOnAnyOtherAnalyticsAssembly()
    {
        var result = Types
            .InAssembly(AbstractionsAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Boutquin.Curves.Conventions",
                "Boutquin.Curves.Indices",
                "Boutquin.Curves.Quotes",
                "Boutquin.Curves.Core",
                "Boutquin.Curves.Interpolation",
                "Boutquin.Curves.Bootstrap",
                "Boutquin.Curves.Serialization",
                "Boutquin.Curves.Risk",
                "Boutquin.Curves.Recipes",
                "Boutquin.Curves.Examples")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Abstractions is the foundation layer and must not depend on higher layers [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void Conventions_ShouldNotDependOnHigherLayers()
    {
        var result = Types
            .InAssembly(ConventionsAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Boutquin.Curves.Indices",
                "Boutquin.Curves.Quotes",
                "Boutquin.Curves.Core",
                "Boutquin.Curves.Bootstrap",
                "Boutquin.Curves.Serialization",
                "Boutquin.Curves.Risk",
                "Boutquin.Curves.Recipes",
                "Boutquin.Curves.Examples")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Conventions is infrastructure and must not depend on domain or composition layers [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void CurvesInterpolation_ShouldNotDependOnHigherLayers()
    {
        // Interpolation depends only on Abstractions at the project level.
        // NetArchTest detects transitive assembly references through shared Abstractions types,
        // so we check only for genuine higher-layer dependencies (Bootstrap and above).
        var result = Types
            .InAssembly(CurvesInterpolationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Boutquin.Curves.Bootstrap",
                "Boutquin.Curves.Serialization",
                "Boutquin.Curves.Risk",
                "Boutquin.Curves.Recipes",
                "Boutquin.Curves.Examples")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Curves.Interpolation must not depend on Bootstrap or higher layers [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void Indices_ShouldNotDependOnHigherLayers()
    {
        var result = Types
            .InAssembly(IndicesAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Boutquin.Curves.Core",
                "Boutquin.Curves.Interpolation",
                "Boutquin.Curves.Bootstrap",
                "Boutquin.Curves.Serialization",
                "Boutquin.Curves.Risk",
                "Boutquin.Curves.Recipes",
                "Boutquin.Curves.Examples")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Indices must not depend on Curves or composition layers [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void Quotes_ShouldNotDependOnHigherLayers()
    {
        var result = Types
            .InAssembly(QuotesAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Boutquin.Curves.Core",
                "Boutquin.Curves.Interpolation",
                "Boutquin.Curves.Bootstrap",
                "Boutquin.Curves.Serialization",
                "Boutquin.Curves.Risk",
                "Boutquin.Curves.Recipes",
                "Boutquin.Curves.Examples")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Quotes must not depend on Curves or composition layers [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void Curves_ShouldNotDependOnHigherLayers()
    {
        var result = Types
            .InAssembly(CurvesAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Boutquin.Curves.Bootstrap",
                "Boutquin.Curves.Serialization",
                "Boutquin.Curves.Risk",
                "Boutquin.Curves.Recipes",
                "Boutquin.Curves.Examples")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Curves must not depend on Bootstrap, Serialization, Risk, or Recipes [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void Bootstrap_ShouldNotDependOnHigherLayers()
    {
        var result = Types
            .InAssembly(CurvesBootstrapAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Boutquin.Curves.Serialization",
                "Boutquin.Curves.Risk",
                "Boutquin.Curves.Recipes",
                "Boutquin.Curves.Examples")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Bootstrap must not depend on Serialization, Risk, or Recipes [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void Risk_ShouldNotDependOnSerializationOrRecipes()
    {
        var result = Types
            .InAssembly(RiskAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Boutquin.Curves.Serialization",
                "Boutquin.Curves.Recipes",
                "Boutquin.Curves.Examples")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Risk must not depend on Serialization or Recipes [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void Serialization_ShouldNotDependOnRiskOrRecipes()
    {
        var result = Types
            .InAssembly(SerializationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Boutquin.Curves.Risk",
                "Boutquin.Curves.Recipes",
                "Boutquin.Curves.Examples")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Serialization must not depend on Risk or Recipes [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void NoAnalyticsAssembly_ShouldDependOnTradingOrOptionPricing()
    {
        foreach (var assembly in AllSourceAssemblies)
        {
            var result = Types
                .InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny(
                    "Boutquin.Trading",
                    "Boutquin.OptionPricing")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: $"Analytics must not depend on Trading or OptionPricing [{assembly.GetName().Name}: {GetFailingTypes(result)}]");
        }
    }

    [Fact]
    public void OnlyRecipes_ShouldDependOnMarketDataOrchestration()
    {
        var nonRecipeAssemblies = AllSourceAssemblies
            .Where(a => a != RecipesAssembly);

        foreach (var assembly in nonRecipeAssemblies)
        {
            var result = Types
                .InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny("Boutquin.MarketData.Orchestration")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: $"Only Recipes may depend on MarketData.Orchestration [{assembly.GetName().Name}: {GetFailingTypes(result)}]");
        }
    }
}
