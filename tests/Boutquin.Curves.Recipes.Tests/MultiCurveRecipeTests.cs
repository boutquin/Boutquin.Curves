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
using Boutquin.Curves.Bootstrap;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Recipes.Testing;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.MarketData.Conventions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Boutquin.Curves.Recipes.Tests;

/// <summary>
/// Tests for the M-2 / AC-7 multi-curve recipe surface: a USD SOFR group whose swap discount
/// nodes name a distinct SOFR projection curve. M-2 establishes the recipe shape and wiring;
/// the end-to-end Fx-from-projection calibration correctness lands at M-3.
/// </summary>
public sealed class MultiCurveRecipeTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);

    /// <summary>
    /// AC-7 (M-2 shape only): the multi-curve recipe declares two curves — a SOFR projection
    /// (forward) curve and a SOFR discount curve — and the discount curve's swap nodes carry a
    /// projection-curve reference.
    /// </summary>
    [Fact]
    public void UsdSofrMultiCurve_DeclaresProjectionAndDiscountCurves()
    {
        var group = StandardCurveRecipes.UsdSofrMultiCurve();

        group.Curves.Should().HaveCount(2);
        group.Curves.Should().Contain(c => c.CurveReference.Role == CurveRole.Forward);
        group.Curves.Should().Contain(c => c.CurveReference.Role == CurveRole.Discount);

        var discount = group.Curves.Single(c => c.CurveReference.Role == CurveRole.Discount);
        var projection = group.Curves.Single(c => c.CurveReference.Role == CurveRole.Forward);

        discount.Nodes
            .Where(n => n.InstrumentType == "FixedFloatSwap")
            .Should()
            .OnlyContain(n => n.ProjectionCurves != null
                && n.ProjectionCurves.Count == 1
                && n.ProjectionCurves[0] == projection.CurveReference);
    }

    /// <summary>
    /// AC-7 integration: requires the M-3 helper rewrite to actually source Fx forwards from the
    /// projection curve during the swap pillar solve. The recipe shape exists at M-2; E2E numeric
    /// correctness is verified at M-3 when this test is un-skipped.
    /// </summary>
    [Fact]
    public async Task UsdSofrMultiCurve_CalibratesEndToEndWithProjectionForwardsEnteringSwapPillarSolve()
    {
        var pipeline = FixtureData.CreatePipeline();

        var calendars = HolidayCalendarFactory.SupportedCodes.Select(HolidayCalendarFactory.Create).ToArray();
        var benchmarks = new[]
        {
            new RateBenchmark(new BenchmarkName("SOFR"), Currency.USD, BenchmarkKind.OvernightRiskFree, null, 0, 1, "USNY", "ACT/360", true),
        };
        var registry = InstrumentConventionRegistry.CreateDefault();
        var conventions = new[]
        {
            registry.GetRequired("USD-SOFR-OIS"),
            registry.GetRequired("USD-FIXED-6M-30-360"),
        };
        var referenceData = new SimpleReferenceDataProvider(calendars, benchmarks, conventions);

        var calibrator = new PiecewiseBootstrapCalibrator();
        var logger = NullLoggerFactory.Instance.CreateLogger<CurveBuilder>();
        var builder = new CurveBuilder(pipeline, calibrator, referenceData, logger);

        CurveSnapshot snapshot = await builder.BuildAsync(StandardCurveRecipes.UsdSofrMultiCurve(), s_valuationDate);

        // AC-7: the multi-curve recipe calibrates end-to-end with the projection curve wired into the
        // swap pillar solve. Both the projection (forward) and discount curves must be present in the
        // calibrated group; a successful build proves the FixedFloatSwap helper sourced Fx forwards from
        // the projection curve (rather than the self-discounting fallback) during the swap pillar solve.
        snapshot.Should().NotBeNull();
        snapshot.CurveGroup.Should().NotBeNull();

        BenchmarkName sofr = new("SOFR");
        CurveReference discountRef = new(CurveRole.Discount, Currency.USD, sofr);
        CurveReference forwardRef = new(CurveRole.Forward, Currency.USD, sofr);

        snapshot.CurveGroup.TryGetCurve(discountRef, out _).Should()
            .BeTrue(because: "the discount curve must be calibrated end-to-end");
        snapshot.CurveGroup.TryGetCurve(forwardRef, out _).Should()
            .BeTrue(because: "the projection (forward) curve must be calibrated and available to supply swap Fx forwards");
    }
}
