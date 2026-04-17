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
using Boutquin.Curves.Recipes;
using Boutquin.Curves.Recipes.Testing;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ---------------------------------------------------------------------------
//  Wire up the DI container with Analytics services and a fake data pipeline.
// ---------------------------------------------------------------------------
var services = new ServiceCollection();

services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

// Register the fake pipeline so CurveBuilder receives it via DI.
var pipeline = FixtureData.CreatePipeline();
services.AddSingleton<Boutquin.MarketData.Abstractions.Contracts.IDataPipeline>(pipeline);

// Register calibrator, reference data, and CurveBuilder.
services.AddAnalytics();

var provider = services.BuildServiceProvider();
var builder = provider.GetRequiredService<CurveBuilder>();

// ---------------------------------------------------------------------------
//  Build all four standard discount curves and display results.
// ---------------------------------------------------------------------------
var valuationDate = FixtureData.FixtureDate;
Console.WriteLine($"Valuation date: {valuationDate:yyyy-MM-dd}");
Console.WriteLine(new string('-', 60));

// Each recipe defines a CurveReference used to retrieve the curve from the group.
(string Label, CurveGroupRecipe Recipe, CurveReference Reference)[] curves =
[
    ("USD SOFR Discount", StandardCurveRecipes.UsdSofrDiscount(),
        new CurveReference(CurveRole.Discount, CurrencyCode.USD, new BenchmarkName("SOFR"))),
    ("CAD CORRA Discount", StandardCurveRecipes.CadCorraDiscount(),
        new CurveReference(CurveRole.Discount, CurrencyCode.CAD, new BenchmarkName("CORRA"))),
    ("GBP SONIA Discount", StandardCurveRecipes.GbpSoniaDiscount(),
        new CurveReference(CurveRole.Discount, CurrencyCode.GBP, new BenchmarkName("SONIA"))),
    ("EUR ESTR Discount", StandardCurveRecipes.EurEstrDiscount(),
        new CurveReference(CurveRole.Discount, CurrencyCode.EUR, new BenchmarkName("ESTR"))),
];

foreach (var (label, recipe, curveRef) in curves)
{
    var snapshot = await builder.BuildAsync(recipe, valuationDate).ConfigureAwait(false);

    Console.WriteLine($"\n{label}  (group: {snapshot.GroupName})");

    ICurve curve = snapshot.CurveGroup.GetCurve(curveRef);

    if (curve is IDiscountCurve discountCurve)
    {
        DateOnly oneYear = valuationDate.AddYears(1);
        DateOnly fiveYear = valuationDate.AddYears(5);
        DateOnly tenYear = valuationDate.AddYears(10);

        Console.WriteLine($"  DF(spot)   = {discountCurve.DiscountFactor(valuationDate):F8}");
        Console.WriteLine($"  DF(1Y)     = {discountCurve.DiscountFactor(oneYear):F8}");
        Console.WriteLine($"  DF(5Y)     = {discountCurve.DiscountFactor(fiveYear):F8}");
        Console.WriteLine($"  DF(10Y)    = {discountCurve.DiscountFactor(tenYear):F8}");
    }
    else
    {
        Console.WriteLine("  (curve is not a discount curve)");
    }

    if (snapshot.DataIssues.Count > 0)
    {
        Console.WriteLine($"  Data issues: {snapshot.DataIssues.Count}");
        foreach (var issue in snapshot.DataIssues)
        {
            Console.WriteLine($"    [{issue.Severity}] {issue.Code}: {issue.Message}");
        }
    }

    Console.WriteLine($"  Diagnostics: {snapshot.Diagnostics.Repricing.Count} repricing check(s), " +
                      $"{snapshot.Diagnostics.Numerical.Count} numerical check(s)");
}

Console.WriteLine($"\n{new string('-', 60)}");
Console.WriteLine("Done.");
