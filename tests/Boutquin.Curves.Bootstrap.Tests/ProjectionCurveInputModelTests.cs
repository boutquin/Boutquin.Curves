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

using Boutquin.Curves.Abstractions;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Interpolation;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

/// <summary>
/// Tests for the M-2 optional projection-curve reference on <see cref="ResolvedNode"/>:
/// backward-compatible null behaviour (AC-6), the 0/1/2-entry two-curve shape (AC-6b),
/// and the named-but-unavailable projection-curve validation error (AC-6c).
/// </summary>
public sealed class ProjectionCurveInputModelTests
{
    private static readonly BenchmarkName s_sofr = new("SOFR");

    /// <summary>
    /// AC-6: a node with a null <see cref="ResolvedNode.ProjectionCurves"/> reference behaves
    /// exactly as a self-discounting node does. The calibrated discount factor for a single 1Y OIS
    /// at 4% under ACT/360 must equal the par-annuity simple-interest value 1 / (1 + rate * tau_ACT360),
    /// matching a plain self-discounting OIS with no projection reference.
    /// </summary>
    [Fact]
    public void Calibrate_WithNullProjectionCurves_ProducesUnchangedDiscountFactor()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference curveReference = new(CurveRole.Discount, Currency.USD);
        const double rate = 0.04;

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });

        CurveCalibrationInput input = new(
            valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveReference,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", curveReference, (decimal)rate, ProjectionCurves: null)
                    })
            },
            referenceData);

        CurveCalibrationResult result = new PiecewiseBootstrapCalibrator().Calibrate(input);

        IDiscountCurve curve = (IDiscountCurve)result.CurveGroup.GetCurve(curveReference);
        DateOnly pillar = valuationDate.AddYears(1);
        double tau = (pillar.DayNumber - valuationDate.DayNumber) / 360d; // ACT/360
        curve.DiscountFactor(pillar).Should().BeApproximately(1.0 / (1.0 + rate * tau), 1e-12);
    }

    /// <summary>
    /// AC-6b: the <see cref="ResolvedNode.ProjectionCurves"/> field carries the two-curve
    /// (Fa, Fb) shape for a BasisSwap node. This is a data-model check — no calibration is
    /// attempted because the real BasisSwap projection helpers land at M-3.
    /// </summary>
    [Fact]
    public void ResolvedNode_BasisSwap_CarriesTwoProjectionCurveReferences()
    {
        CurveReference target = new(CurveRole.Discount, Currency.USD, s_sofr);
        CurveReference refA = new(CurveRole.Forward, Currency.USD, new BenchmarkName("SOFR-3M"));
        CurveReference refB = new(CurveRole.Forward, Currency.USD, new BenchmarkName("SOFR-6M"));

        ResolvedNode node = new(
            "BASIS-5Y",
            new Tenor("5Y"),
            "BasisSwap",
            "USD-SOFR-OIS",
            target,
            0.001m,
            ProjectionCurves: new[] { refA, refB });

        node.ProjectionCurves.Should().NotBeNull();
        node.ProjectionCurves!.Count.Should().Be(2);
        node.ProjectionCurves[0].Should().Be(refA);
        node.ProjectionCurves[1].Should().Be(refB);
    }

    /// <summary>
    /// AC-6c: when a node names a projection curve that is neither its own target nor a prior
    /// spec in the same batch, validation rejects the input with a PROJECTION_CURVE_UNRESOLVED
    /// error, surfaced by the calibrator as an <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void Calibrate_WithUnavailableProjectionCurve_Throws()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference curveReference = new(CurveRole.Discount, Currency.USD);
        CurveReference missingProjection = new(CurveRole.Forward, Currency.USD, s_sofr);

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });

        CurveCalibrationInput input = new(
            valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveReference,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", curveReference, 0.04m,
                            ProjectionCurves: new[] { missingProjection })
                    })
            },
            referenceData);

        Action act = () => new PiecewiseBootstrapCalibrator().Calibrate(input);

        act.Should()
            .Throw<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("PROJECTION_CURVE_UNRESOLVED", StringComparison.Ordinal));
    }
}
