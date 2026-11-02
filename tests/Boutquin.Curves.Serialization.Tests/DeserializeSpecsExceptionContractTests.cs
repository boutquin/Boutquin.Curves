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

using FluentAssertions;

namespace Boutquin.Curves.Serialization.Tests;

/// <summary>
/// DEEP-ERR-01 — <see cref="CurveGroupDefinitionJsonSerializer.DeserializeSpecs"/> must
/// honour its documented exception contract: <see cref="InvalidOperationException"/> for
/// every malformed-input path, not raw <see cref="System.Text.Json.JsonException"/> /
/// <see cref="ArgumentException"/> / <see cref="FormatException"/>.
/// </summary>
public sealed class DeserializeSpecsExceptionContractTests
{
    private readonly CurveGroupDefinitionJsonSerializer _sut = new();

    /// <summary>
    /// Malformed JSON (syntax error) → documented <see cref="InvalidOperationException"/>,
    /// not raw <see cref="System.Text.Json.JsonException"/>.
    /// </summary>
    [Fact]
    public void DeserializeSpecs_MalformedJson_ThrowsInvalidOperationException()
    {
        const string badJson = "{ this is not valid json }";

        Action act = () => _sut.DeserializeSpecs(badJson);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*deserialize*");
    }

    /// <summary>
    /// Valid JSON structure but an unrecognised enum value (bad <c>role</c> discriminator)
    /// → documented <see cref="InvalidOperationException"/>,
    /// not raw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DeserializeSpecs_BadEnumValue_ThrowsInvalidOperationException()
    {
        // role = "NotAValidRole" — Enum.Parse<CurveRole> will throw ArgumentException
        const string json = """
            {
              "groupName": "test",
              "valuationDate": "2024-01-15",
              "curves": [
                {
                  "curveName": "TEST",
                  "role": "NotAValidRole",
                  "currency": "USD",
                  "benchmark": null,
                  "valueType": "ZeroRate",
                  "dayCount": "ACT/360",
                  "interpolator": "LogLinearDiscountFactor",
                  "leftExtrapolator": "Flat",
                  "rightExtrapolator": "Flat",
                  "jumps": null,
                  "nodes": []
                }
              ]
            }
            """;

        Action act = () => _sut.DeserializeSpecs(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*deserialize*");
    }

    /// <summary>
    /// Valid JSON and valid enum values but a malformed date string in a jump point
    /// → documented <see cref="InvalidOperationException"/>,
    /// not raw <see cref="FormatException"/>.
    /// </summary>
    [Fact]
    public void DeserializeSpecs_MalformedJumpDate_ThrowsInvalidOperationException()
    {
        const string json = """
            {
              "groupName": "test",
              "valuationDate": "2024-01-15",
              "curves": [
                {
                  "curveName": "TEST",
                  "role": "Discount",
                  "currency": "USD",
                  "benchmark": null,
                  "valueType": "ZeroRate",
                  "dayCount": "ACT/360",
                  "interpolator": "LogLinearDiscountFactor",
                  "leftExtrapolator": "Flat",
                  "rightExtrapolator": "Flat",
                  "jumps": [
                    { "date": "not-a-date", "value": 0.001 }
                  ],
                  "nodes": []
                }
              ]
            }
            """;

        Action act = () => _sut.DeserializeSpecs(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*deserialize*");
    }

    [Theory]
    [InlineData("role", "99")]
    [InlineData("currency", "99999")]
    [InlineData("valueType", "99")]
    [InlineData("interpolator", "99")]
    public void DeserializeSpecs_UndefinedNumericDiscriminator_Rejects(string field, string value)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(ValidCurvesBodyTemplate.Replace("{0}", ""))!;
        root["curves"]![0]![field] = value;
        Action act = () => _sut.DeserializeSpecs(root.ToJsonString());
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("curves")]
    [InlineData("curve")]
    [InlineData("nodes")]
    [InlineData("node")]
    [InlineData("jump")]
    public void DeserializeSpecs_NullStructuralElement_RejectsWithContractException(string location)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(ValidCurvesBodyTemplate.Replace("{0}", ""))!;
        switch (location)
        {
            case "curves": root["curves"] = null; break;
            case "curve": root["curves"]![0] = null; break;
            case "nodes": root["curves"]![0]!["nodes"] = null; break;
            case "node": root["curves"]![0]!["nodes"] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null); break;
            case "jump": root["curves"]![0]!["jumps"] = new System.Text.Json.Nodes.JsonArray((System.Text.Json.Nodes.JsonNode?)null); break;
        }
        Action act = () => _sut.DeserializeSpecs(root.ToJsonString());
        act.Should().Throw<InvalidOperationException>();
    }

    // A minimal, otherwise-valid single-curve payload used by the DeserializeGroup
    // required-field guard tests below. The {0} placeholder holds the group-level
    // header fields (groupName / valuationDate) so each test can omit exactly one.
    private const string ValidCurvesBodyTemplate = """
        {
          {0}
          "curves": [
            {
              "curveName": "TEST",
              "role": "Discount",
              "currency": "USD",
              "benchmark": null,
              "valueType": "ZeroRate",
              "dayCount": "ACT/360",
              "interpolator": "LogLinearDiscountFactor",
              "leftExtrapolator": "Flat",
              "rightExtrapolator": "Flat",
              "jumps": null,
              "nodes": []
            }
          ]
        }
        """;

    /// <summary>
    /// SERDE-DESERIALIZEGROUP-NULL-VALDATE-001 — a valid-curves payload that OMITS the
    /// required <c>valuationDate</c> field must surface as the documented
    /// <see cref="InvalidOperationException"/>, not the raw
    /// <see cref="ArgumentNullException"/> that <c>DateOnly.ParseExact(null, ...)</c> throws
    /// (which escapes the method's <c>catch (FormatException)</c>).
    /// </summary>
    [Fact]
    public void DeserializeGroup_MissingValuationDate_ThrowsInvalidOperationException()
    {
        string json = ValidCurvesBodyTemplate.Replace("{0}", "\"groupName\": \"test\",");

        Action act = () => _sut.DeserializeGroup(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*deserialize*");
    }

    /// <summary>
    /// SERDE-CURVEGROUPSPECS-GROUPNAME-NULL-002 — a valid-curves payload that OMITS the
    /// required <c>groupName</c> field must surface as the documented
    /// <see cref="InvalidOperationException"/> rather than returning a
    /// <see cref="CurveGroupSpecs"/> whose non-nullable <see cref="CurveGroupSpecs.GroupName"/>
    /// is <c>null</c> (an observable violation of the type's own non-null annotation).
    /// </summary>
    [Fact]
    public void DeserializeGroup_MissingGroupName_ThrowsInvalidOperationException()
    {
        string json = ValidCurvesBodyTemplate.Replace("{0}", "\"valuationDate\": \"2024-01-15\",");

        Action act = () => _sut.DeserializeGroup(json);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*deserialize*");
    }

    /// <summary>
    /// Scoping pin: <see cref="CurveGroupDefinitionJsonSerializer.DeserializeSpecs"/> DISCARDS
    /// both group-level header fields, so it must still succeed on the SAME payloads that
    /// <see cref="CurveGroupDefinitionJsonSerializer.DeserializeGroup"/> now rejects — proving
    /// the required-field guard is scoped to <c>DeserializeGroup</c> alone.
    /// </summary>
    [Fact]
    public void DeserializeSpecs_MissingGroupHeaderFields_StillSucceeds()
    {
        string missingValuationDate = ValidCurvesBodyTemplate.Replace("{0}", "\"groupName\": \"test\",");
        string missingGroupName = ValidCurvesBodyTemplate.Replace("{0}", "\"valuationDate\": \"2024-01-15\",");

        _sut.DeserializeSpecs(missingValuationDate).Should().ContainSingle();
        _sut.DeserializeSpecs(missingGroupName).Should().ContainSingle();
    }
}
