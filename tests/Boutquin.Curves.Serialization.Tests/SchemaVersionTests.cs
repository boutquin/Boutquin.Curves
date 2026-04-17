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

using System.Text.Json;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.Curves.Bootstrap;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Indices;
using Boutquin.Curves.Interpolation;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Serialization.Tests;

/// <summary>
/// Verifies schema version field is present in all serialized outputs.
/// </summary>
public sealed class SchemaVersionTests
{
    private static IReferenceDataProvider CreateRefData() => new SimpleReferenceDataProvider(
        new[] { new WeekendOnlyCalendar("USNY") },
        BenchmarkCatalog.CreateDefault().All(),
        new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });

    [Fact]
    public void CurveCalibrationInputJson_ContainsSchemaVersion()
    {
        CurveReference curveRef = new(CurveRole.Discount, CurrencyCode.USD, new BenchmarkName("SOFR"));
        CurveCalibrationInput input = new(
            new DateOnly(2024, 1, 15),
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-SOFR-DISC"),
                    curveRef,
                    CurveValueType.ZeroRate,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "Flat", "Flat"),
                    new[]
                    {
                        new ResolvedNode("USD-SOFR-O/N", new Tenor("O/N"), "Deposit", "USD-SOFR-DEP", curveRef, 0.053m)
                    })
            },
            CreateRefData());

        var serializer = new CurveGroupDefinitionJsonSerializer();
        string json = serializer.Serialize(input);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("schemaVersion").GetString().Should().Be("1.0");
    }

    [Fact]
    public void CurveCalibrationInputJson_Deserialize_IgnoresSchemaVersion()
    {
        CurveReference curveRef = new(CurveRole.Discount, CurrencyCode.USD, new BenchmarkName("SOFR"));
        CurveCalibrationInput input = new(
            new DateOnly(2024, 1, 15),
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-SOFR-DISC"),
                    curveRef,
                    CurveValueType.ZeroRate,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "Flat", "Flat"),
                    new[]
                    {
                        new ResolvedNode("USD-SOFR-O/N", new Tenor("O/N"), "Deposit", "USD-SOFR-DEP", curveRef, 0.053m)
                    })
            },
            CreateRefData());

        var serializer = new CurveGroupDefinitionJsonSerializer();
        string json = serializer.Serialize(input);

        // Should deserialize without error despite extra schemaVersion field.
        var roundTripped = serializer.DeserializeSpecs(json);
        roundTripped.Should().HaveCount(1);
        roundTripped[0].CurveName.Value.Should().Be("USD-SOFR-DISC");
    }
}
