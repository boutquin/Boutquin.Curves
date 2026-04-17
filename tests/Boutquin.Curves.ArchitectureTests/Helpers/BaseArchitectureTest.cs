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

namespace Boutquin.Curves.ArchitectureTests.Helpers;

public abstract class BaseArchitectureTest
{
    protected static Assembly AbstractionsAssembly =>
        typeof(Abstractions.Identifiers.CurveName).Assembly;

    protected static Assembly ConventionsAssembly =>
        typeof(Conventions.Calendars.Schedule.BusinessScheduleGenerator).Assembly;

    protected static Assembly IndicesAssembly =>
        typeof(Indices.BenchmarkCatalog).Assembly;

    protected static Assembly QuotesAssembly =>
        typeof(Quotes.MarketInstrumentType).Assembly;

    protected static Assembly CurvesAssembly =>
        typeof(Curves.Core.CurveGroup).Assembly;

    protected static Assembly CurvesInterpolationAssembly =>
        typeof(Curves.Interpolation.FlatForwardInterpolator).Assembly;

    protected static Assembly CurvesBootstrapAssembly =>
        typeof(Curves.Bootstrap.ConvexityAdjustments.ConstantConvexityAdjustment).Assembly;

    protected static Assembly SerializationAssembly =>
        typeof(Serialization.CalibrationReportExporter).Assembly;

    protected static Assembly RiskAssembly =>
        typeof(Risk.BucketedZeroRateShock).Assembly;

    protected static Assembly RecipesAssembly =>
        typeof(Recipes.AnalyticsDependencyInjectionExtensions).Assembly;

    protected static IEnumerable<Assembly> AllSourceAssemblies =>
    [
        AbstractionsAssembly,
        ConventionsAssembly,
        IndicesAssembly,
        QuotesAssembly,
        CurvesAssembly,
        CurvesInterpolationAssembly,
        CurvesBootstrapAssembly,
        SerializationAssembly,
        RiskAssembly,
        RecipesAssembly,
    ];

    protected static string GetFailingTypes(TestResult result) =>
        result.FailingTypes != null
            ? string.Join(", ", result.FailingTypes.Select(t => t.FullName))
            : string.Empty;
}
