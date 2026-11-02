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
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.Curves.Bootstrap;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Interpolation;
using Boutquin.Curves.Serialization;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.IntegrationTests;

public sealed class SerializationRoundTripTests
{
    [Fact]
    public void CurveCalibrationInput_ShouldRoundTrip()
    {
        CurveReference curveReference = new(CurveRole.Discount, Currency.USD);
        IReferenceDataProvider referenceData = new SimpleReferenceDataProvider(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });

        CurveCalibrationInput input = new(
            new DateOnly(2026, 4, 9),
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
                        new ResolvedNode("1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", curveReference, 0.04m)
                    })
            },
            referenceData);

        CurveGroupDefinitionJsonSerializer serializer = new();
        string json = serializer.Serialize(input);
        IReadOnlyList<CurveCalibrationSpec> roundTrip = serializer.DeserializeSpecs(json);

        roundTrip.Should().HaveCount(1);
        roundTrip[0].CurveName.Value.Should().Be("USD-Disc");
    }

    // ─── B-2 (round-1 F-L4-05 C7): broaden round-trip assertions ─────────────────
    //
    // CurveCalibrationInput_ShouldRoundTrip above asserts only CurveName.Value — ~1 of the ~13
    // deserializable fields on CurveCalibrationSpec/ResolvedNode. A field-mapping regression in
    // MapCurveSpec/MapNode (e.g. swapping Role and ValueType, or dropping ConventionCode) would
    // pass every existing serialization test. This test asserts every field that
    // CurveGroupDefinitionJsonSerializer actually round-trips:
    //   CurveName, CurveReference (Role/Currency/Benchmark), ValueType, DayCountCode,
    //   Interpolation (Interpolator/LeftExtrapolator/RightExtrapolator),
    //   Node (Label, Tenor, InstrumentType, ConventionCode), Jumps (Date/Value, covered by the
    //   dedicated CurveCalibrationInput_ShouldRoundTripConfiguredJumps test) and ProjectionCurves
    //   (covered by the dedicated CurveCalibrationInput_ShouldRoundTripProjectionCurves test).
    //
    // Two fields are DELIBERATELY excluded from this assertion, not silently — both are documented
    // current behavior pending separate, already-tracked fixes:
    //   - ResolvedNode.Rate: the serializer comment states "Rate not stored in definition JSON" —
    //     this is the established DEFINITION-vs-CALIBRATED-INPUT schema split (the JSON persists
    //     curve DEFINITIONS for re-calibration, not resolved market rates), not a bug. Deserialized
    //     Rate is always 0m by design; not a round-trippable field.
    //   - CurveCalibrationInput.ValuationDate / CurveGroupDefinition.GroupName: DeserializeSpecs
    //     still discards both (its return type, IReadOnlyList<CurveCalibrationSpec>, has no slot
    //     for either) — this is DELIBERATE backward-compatible behavior, not a residual gap.
    //     Milestone M-4 (D-2) added the new, additive DeserializeGroup method that round-trips
    //     both fields; see CurveCalibrationInput_DeserializeGroup_ShouldRoundTripValuationDateAndGroupName
    //     below.
    [Fact]
    public void CurveCalibrationInput_ShouldRoundTripAllSpecAndNodeFields()
    {
        CurveReference curveReference = new(CurveRole.Forward, Currency.EUR, new BenchmarkName("ESTR"));
        IReferenceDataProvider referenceData = new SimpleReferenceDataProvider(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });

        CurveCalibrationInput input = new(
            new DateOnly(2026, 4, 9),
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("EUR-ESTR-FWD"),
                    curveReference,
                    CurveValueType.ZeroRate,
                    "ACT/365F",
                    new InterpolationSettings(InterpolatorKind.MonotoneCubic, "FlatZero", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("6M-Swap", new Tenor("6M"), "FixedFloatSwap", "USD-SOFR-OIS", curveReference, 0.04m)
                    })
            },
            referenceData);

        CurveGroupDefinitionJsonSerializer serializer = new();
        string json = serializer.Serialize(input);
        IReadOnlyList<CurveCalibrationSpec> roundTrip = serializer.DeserializeSpecs(json);

        roundTrip.Should().HaveCount(1);
        CurveCalibrationSpec spec = roundTrip[0];

        spec.CurveName.Value.Should().Be("EUR-ESTR-FWD");
        spec.CurveReference.Role.Should().Be(CurveRole.Forward);
        spec.CurveReference.Currency.Should().Be(Currency.EUR);
        spec.CurveReference.Benchmark.Should().Be(new BenchmarkName("ESTR"));
        spec.ValueType.Should().Be(CurveValueType.ZeroRate);
        spec.DayCountCode.Should().Be("ACT/365F");
        spec.Interpolation.Interpolator.Should().Be(InterpolatorKind.MonotoneCubic);
        spec.Interpolation.LeftExtrapolator.Should().Be("FlatZero");
        spec.Interpolation.RightExtrapolator.Should().Be("FlatForward");

        spec.Nodes.Should().HaveCount(1);
        ResolvedNode node = spec.Nodes[0];
        node.Label.Should().Be("6M-Swap");
        node.Tenor.Value.Should().Be("6M");
        node.InstrumentType.Should().Be("FixedFloatSwap");
        node.ConventionCode.Should().Be("USD-SOFR-OIS");
    }

    // ─── D-2: ValuationDate + GroupName round-trip ──────────────────────────────────────
    //
    // DeserializeSpecs (above) still discards the group-level valuationDate/groupName fields for
    // backward compatibility. DeserializeGroup is the new, additive method that round-trips them.
    [Fact]
    public void CurveCalibrationInput_DeserializeGroup_ShouldRoundTripValuationDateAndGroupName()
    {
        CurveReference curveReference = new(CurveRole.Discount, Currency.USD);
        DateOnly valuationDate = new(2026, 4, 9);
        IReferenceDataProvider referenceData = new SimpleReferenceDataProvider(
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
                        new ResolvedNode("1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", curveReference, 0.04m)
                    })
            },
            referenceData);

        CurveGroupDefinitionJsonSerializer serializer = new();
        string json = serializer.Serialize(input);
        CurveGroupSpecs roundTrip = serializer.DeserializeGroup(json);

        roundTrip.ValuationDate.Should().Be(valuationDate);
        roundTrip.GroupName.Should().Be("USD-Disc", because: "GroupName currently round-trips the first curve's name (see Serialize remarks)");
        roundTrip.Curves.Should().HaveCount(1);
        roundTrip.Curves[0].CurveName.Value.Should().Be("USD-Disc");
    }

    [Fact]
    public void CurveCalibrationInput_DeserializeSpecs_StillDiscardsValuationDateAndGroupName()
    {
        // Backward-compatibility pin: DeserializeSpecs' return type has no slot for
        // ValuationDate/GroupName, so it must keep discarding them — DeserializeGroup is the
        // additive method callers opt into for the round-tripped fields.
        CurveReference curveReference = new(CurveRole.Discount, Currency.USD);
        IReferenceDataProvider referenceData = new SimpleReferenceDataProvider(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });

        CurveCalibrationInput input = new(
            new DateOnly(2026, 4, 9),
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
                        new ResolvedNode("1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", curveReference, 0.04m)
                    })
            },
            referenceData);

        CurveGroupDefinitionJsonSerializer serializer = new();
        string json = serializer.Serialize(input);
        IReadOnlyList<CurveCalibrationSpec> roundTrip = serializer.DeserializeSpecs(json);

        roundTrip.Should().HaveCount(1);
        roundTrip[0].CurveName.Value.Should().Be("USD-Disc");
    }

    // ─── T-C7-5: ProjectionCurves must survive the round-trip ────────────────────

    [Fact]
    public void CurveCalibrationInput_ShouldRoundTripProjectionCurves()
    {
        CurveReference discReference = new(CurveRole.Discount, Currency.USD);
        CurveReference projReference =
            new(CurveRole.Forward, Currency.USD, new BenchmarkName("SOFR"));
        IReferenceDataProvider referenceData = new SimpleReferenceDataProvider(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });

        CurveCalibrationInput input = new(
            new DateOnly(2026, 4, 9),
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    discReference,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[]
                    {
                        // A multi-curve basis-swap node naming one projection (forwarding) curve.
                        new ResolvedNode(
                            "5Y-basis",
                            new Tenor("5Y"),
                            "BasisSwap",
                            "USD-SOFR-OIS",
                            discReference,
                            0.0123m,
                            new[] { projReference })
                    })
            },
            referenceData);

        CurveGroupDefinitionJsonSerializer serializer = new();
        string json = serializer.Serialize(input);
        IReadOnlyList<CurveCalibrationSpec> roundTrip = serializer.DeserializeSpecs(json);

        ResolvedNode node = roundTrip.Single().Nodes.Single();
        node.ProjectionCurves.Should().NotBeNull(
            because: "a basis-swap node's projection curves must not be silently dropped on round-trip");
        node.ProjectionCurves!.Should().ContainSingle();
        node.ProjectionCurves![0].Role.Should().Be(CurveRole.Forward);
        node.ProjectionCurves![0].Currency.Should().Be(Currency.USD);
        node.ProjectionCurves![0].Benchmark.Should().Be(new BenchmarkName("SOFR"));
    }

    [Fact]
    public void CurveCalibrationInput_ShouldRoundTripConfiguredJumps()
    {
        CurveReference curveReference = new(CurveRole.Discount, Currency.USD);
        DateOnly valuationDate = new(2026, 4, 9);
        IReferenceDataProvider referenceData = new SimpleReferenceDataProvider(
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
                        new ResolvedNode("1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", curveReference, 0.04m)
                    },
                    new[]
                    {
                        new CurvePoint(valuationDate.AddYears(1), 0.995d)
                    })
            },
            referenceData);

        CurveGroupDefinitionJsonSerializer serializer = new();
        string json = serializer.Serialize(input);
        IReadOnlyList<CurveCalibrationSpec> roundTrip = serializer.DeserializeSpecs(json);

        IReadOnlyList<CurvePoint> jumps = roundTrip.Single().Jumps
            ?? throw new InvalidOperationException("Expected jump points in roundtrip.");
        jumps.Should().ContainSingle();
        jumps[0].Date.Should().Be(valuationDate.AddYears(1));
        jumps[0].Value.Should().Be(0.995d);
    }
}
