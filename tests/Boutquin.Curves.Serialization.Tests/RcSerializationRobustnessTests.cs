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

using System.Text.Json.Nodes;
using Boutquin.Curves.Bootstrap;
using Boutquin.Curves.Bootstrap.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Serialization.Tests;

public sealed class RcSerializationRobustnessTests
{
    private const string Definition = """
        {"schemaVersion":"1.0","groupName":"CAD","valuationDate":"2024-01-12","curves":[
        {"curveName":"CAD-DISC","role":"Discount","currency":"CAD","benchmark":"CORRA",
         "valueType":"DiscountFactor","dayCount":"ACT/365F","interpolator":"LogLinearDiscountFactor",
         "leftExtrapolator":"FlatZero","rightExtrapolator":"FlatForward",
         "jumps":[{"date":"2024-12-31","value":0.999}],
         "nodes":[{"type":"FixedFloatSwap","label":"1Y","quoteId":"RATE","convention":"CAD-CORRA-OIS","tenor":"1Y",
          "projectionCurves":[{"role":"Forward","currency":"CAD","benchmark":"CORRA"}]}]}]}
        """;

    [Fact]
    public void DefinitionRoundTrip_PreservesEverySupportedCalibrationMetadataField()
    {
        var serializer = new CurveGroupDefinitionJsonSerializer();
        CurveGroupSpecs group = serializer.DeserializeGroup(Definition);
        var input = new CurveCalibrationInput(group.ValuationDate, group.Curves,
            new SimpleReferenceDataProvider([], [], []));
        CurveGroupSpecs roundTrip = serializer.DeserializeGroup(serializer.Serialize(input));
        roundTrip.ValuationDate.Should().Be(new DateOnly(2024, 1, 12));
        // The serializer documents that the first curve name supplies the group name.
        roundTrip.GroupName.Should().Be("CAD-DISC");
        var curve = roundTrip.Curves.Single();
        curve.CurveName.Value.Should().Be("CAD-DISC");
        curve.CurveReference.Role.Should().Be(Abstractions.Curves.CurveRole.Discount);
        curve.CurveReference.Currency.Should().Be(Boutquin.Domain.Enumerations.Currency.CAD);
        curve.CurveReference.Benchmark!.Value.Value.Should().Be("CORRA");
        curve.ValueType.Should().Be(Bootstrap.Definitions.CurveValueType.DiscountFactor);
        curve.DayCountCode.Should().Be("ACT/365F");
        curve.Interpolation.Interpolator.Should().Be(Interpolation.InterpolatorKind.LogLinearDiscountFactor);
        curve.Interpolation.LeftExtrapolator.Should().Be("FlatZero");
        curve.Interpolation.RightExtrapolator.Should().Be("FlatForward");
        curve.Jumps.Should().ContainSingle();
        curve.Jumps![0].Date.Should().Be(new DateOnly(2024, 12, 31));
        curve.Jumps[0].Value.Should().Be(0.999);
        var node = curve.Nodes.Single();
        node.Label.Should().Be("1Y");
        node.Tenor.Value.Should().Be("1Y");
        node.InstrumentType.Should().Be("FixedFloatSwap");
        node.ConventionCode.Should().Be("CAD-CORRA-OIS");
        node.TargetCurve.Should().Be(curve.CurveReference);
        node.Rate.Should().Be(0m, "definition JSON deliberately excludes quote values");
        node.ProjectionCurves.Should().ContainSingle();
        node.ProjectionCurves![0].Role.Should().Be(Abstractions.Curves.CurveRole.Forward);
        node.ProjectionCurves[0].Currency.Should().Be(Boutquin.Domain.Enumerations.Currency.CAD);
        node.ProjectionCurves[0].Benchmark!.Value.Value.Should().Be("CORRA");
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("")]
    [InlineData(null)]
    public void Definition_UnsupportedSchemaVersion_Rejects(string? version)
    {
        JsonNode root = JsonNode.Parse(Definition)!;
        root["schemaVersion"] = version;
        Action act = () => new CurveGroupDefinitionJsonSerializer().DeserializeSpecs(root.ToJsonString());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Csv_QuotedCommasEscapedQuotesAndMultilineFields_ArePreserved()
    {
        const string csv = "QuoteId,Value,FieldName,AsOfDate,Notes\nRATE,0.05,Rate,2024-01-12,\"a,b \"\"quoted\"\"\nnext line\"\n";
        var quote = CsvQuoteLoader.Load(csv).Quotes.Values.Single();
        quote.Notes.Should().Be("a,b \"quoted\"\nnext line");
        quote.Value.Should().Be(0.05m);
    }

    [Fact]
    public void Csv_MixedAsOfDates_PreservesDocumentedPerQuoteDates()
    {
        const string csv = "QuoteId,Value,FieldName,AsOfDate\nA,0.05,Rate,2024-01-12\nB,0.06,Rate,2024-01-11";
        var snapshot = CsvQuoteLoader.Load(csv);
        snapshot.AsOfDate.Should().Be(new DateOnly(2024, 1, 12));
        snapshot.Quotes[new Abstractions.Identifiers.QuoteId("B")].AsOfDate.Should().Be(new DateOnly(2024, 1, 11));
    }

    [Theory]
    [InlineData("QuoteId,Value,FieldName,AsOfDate\n\"unterminated,0.05,Rate,2024-01-12")]
    [InlineData("QuoteId,Value,FieldName,AsOfDate\n,0.05,Rate,2024-01-12")]
    public void Csv_MalformedQuotedFieldOrEmptyIdentity_UsesFormatException(string csv)
    {
        Action act = () => CsvQuoteLoader.Load(csv);
        act.Should().Throw<FormatException>();
    }
}
