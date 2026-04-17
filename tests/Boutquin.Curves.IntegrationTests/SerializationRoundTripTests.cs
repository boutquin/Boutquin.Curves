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
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.Curves.Bootstrap;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Indices;
using Boutquin.Curves.Interpolation;
using Boutquin.Curves.Serialization;
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
        CurveReference curveReference = new(CurveRole.Discount, CurrencyCode.USD);
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

    [Fact]
    public void CurveCalibrationInput_ShouldRoundTripConfiguredJumps()
    {
        CurveReference curveReference = new(CurveRole.Discount, CurrencyCode.USD);
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
