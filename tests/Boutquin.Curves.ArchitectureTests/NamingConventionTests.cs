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

using Boutquin.Curves.Abstractions.Bootstrap;
using Boutquin.Curves.Interpolation;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.ArchitectureTests;

public sealed class NamingConventionTests : BaseArchitectureTest
{
    [Fact]
    public void Interfaces_ShouldStartWithI()
    {
        foreach (var assembly in AllSourceAssemblies)
        {
            var result = Types
                .InAssembly(assembly)
                .That()
                .AreInterfaces()
                .Should()
                .HaveNameStartingWith("I")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: $"all interfaces must start with 'I' [{assembly.GetName().Name}: {GetFailingTypes(result)}]");
        }
    }

    [Fact]
    public void InterpolatorImplementations_ShouldEndWithInterpolator()
    {
        var result = Types
            .InAssembly(CurvesInterpolationAssembly)
            .That()
            .ImplementInterface(typeof(INodalCurveInterpolator))
            .Should()
            .HaveNameEndingWith("Interpolator")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"INodalCurveInterpolator implementations must end with 'Interpolator' [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void InstrumentHelperImplementations_ShouldEndWithInstrumentHelper()
    {
        var result = Types
            .InAssembly(CurvesBootstrapAssembly)
            .That()
            .ImplementInterface(typeof(IInstrumentHelper))
            .And()
            .AreNotAbstract()
            .Should()
            .HaveNameEndingWith("InstrumentHelper")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"IInstrumentHelper implementations must end with 'InstrumentHelper' [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void ConvexityAdjustmentImplementations_ShouldEndWithAdjustment()
    {
        var result = Types
            .InAssembly(CurvesBootstrapAssembly)
            .That()
            .ImplementInterface(typeof(IConvexityAdjustment))
            .Should()
            .HaveNameEndingWith("Adjustment")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"IConvexityAdjustment implementations must end with 'Adjustment' [{GetFailingTypes(result)}]");
    }

    [Fact]
    public void YearFractionCalculatorImplementations_ShouldEndWithCalculator()
    {
        var result = Types
            .InAssembly(ConventionsAssembly)
            .That()
            .ImplementInterface(typeof(IYearFractionCalculator))
            .Should()
            .HaveNameEndingWith("Calculator")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"IYearFractionCalculator implementations must end with 'Calculator' [{GetFailingTypes(result)}]");
    }
}
