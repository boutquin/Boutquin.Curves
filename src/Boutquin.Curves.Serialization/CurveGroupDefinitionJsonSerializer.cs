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

using System.Globalization;
using System.Text.Json;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Bootstrap;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Interpolation;
using Boutquin.Curves.Serialization.Dto;
using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Serialization;

/// <summary>
/// Serializes and deserializes curve calibration inputs to a stable JSON representation.
/// </summary>
/// <remarks>
/// The JSON schema uses camelCase property naming, indented formatting, and encodes dates as
/// <c>yyyy-MM-dd</c> strings. The class is stateless and safe for concurrent use. The DTO
/// <c>QuoteId</c> field is retained for backward compatibility but carries no semantic value
/// in the new model (nodes carry rates directly).
/// </remarks>
public sealed class CurveGroupDefinitionJsonSerializer
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Converts a calibration input to JSON.
    /// </summary>
    /// <param name="input">Calibration input to serialize.</param>
    /// <returns>Indented JSON text representing the calibration input.</returns>
    public string Serialize(CurveCalibrationInput input)
    {
        CurveGroupDefinitionDto dto = new(
            input.Curves[0].CurveName.Value,
            input.ValuationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            input.Curves.Select(curve => new CurveDefinitionDto(
                curve.CurveName.Value,
                curve.CurveReference.Role.ToString(),
                curve.CurveReference.Currency.ToString(),
                curve.CurveReference.Benchmark?.Value,
                curve.ValueType.ToString(),
                curve.DayCountCode,
                curve.Interpolation.Interpolator.ToString(),
                curve.Interpolation.LeftExtrapolator,
                curve.Interpolation.RightExtrapolator,
                curve.Jumps?.Select(jump => new JumpPointDto(
                    jump.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    jump.Value)).ToArray(),
                curve.Nodes.Select(node => new CurveNodeDefinitionDto(
                    node.InstrumentType,
                    node.Label,
                    node.Label, // QuoteId field — preserved for schema compat, uses label as identifier
                    node.ConventionCode,
                    node.Tenor.Value)).ToArray())).ToArray());

        return JsonSerializer.Serialize(dto, _options);
    }

    /// <summary>
    /// Parses JSON into a list of <see cref="CurveCalibrationSpec"/> entries.
    /// </summary>
    /// <param name="json">JSON payload to deserialize.</param>
    /// <returns>Deserialized list of curve calibration specs.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the payload cannot be deserialized.</exception>
    public IReadOnlyList<CurveCalibrationSpec> DeserializeSpecs(string json)
    {
        CurveGroupDefinitionDto dto = JsonSerializer.Deserialize<CurveGroupDefinitionDto>(json, _options)
            ?? throw new InvalidOperationException("Failed to deserialize curve calibration input.");

        return dto.Curves.Select(MapCurveSpec).ToArray();
    }

    private static CurveCalibrationSpec MapCurveSpec(CurveDefinitionDto dto)
    {
        CurveReference reference = new(
            Enum.Parse<CurveRole>(dto.Role),
            Enum.Parse<CurrencyCode>(dto.Currency, ignoreCase: true),
            string.IsNullOrWhiteSpace(dto.Benchmark) ? null : new BenchmarkName(dto.Benchmark));

        IReadOnlyList<ResolvedNode> nodes = dto.Nodes.Select(node => MapNode(reference, node)).ToArray();

        return new CurveCalibrationSpec(
            new CurveName(dto.CurveName),
            reference,
            Enum.Parse<CurveValueType>(dto.ValueType),
            dto.DayCount,
            new InterpolationSettings(
                Enum.Parse<InterpolatorKind>(dto.Interpolator),
                dto.LeftExtrapolator,
                dto.RightExtrapolator),
            nodes,
            dto.Jumps?.Select(jump => new CurvePoint(
                DateOnly.ParseExact(jump.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                jump.Value)).ToArray());
    }

    private static ResolvedNode MapNode(CurveReference reference, CurveNodeDefinitionDto dto)
    {
        return new ResolvedNode(
            dto.Label,
            new Tenor(dto.Tenor),
            dto.Type,
            dto.Convention,
            reference,
            0m); // Rate not stored in definition JSON — set to zero; caller must supply rates separately.
    }
}
