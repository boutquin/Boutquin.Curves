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
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Serialization;

/// <summary>
/// Round-tripped result of deserializing a curve-group JSON payload, carrying the group-level
/// <see cref="ValuationDate"/> and <see cref="GroupName"/> alongside the per-curve specs that
/// <see cref="CurveGroupDefinitionJsonSerializer.DeserializeSpecs"/> returns alone.
/// </summary>
/// <param name="ValuationDate">Group-level valuation (as-of) date recovered from the JSON payload.</param>
/// <param name="GroupName">Group-level name recovered from the JSON payload.</param>
/// <param name="Curves">Deserialized per-curve calibration specs, identical in content to <see cref="CurveGroupDefinitionJsonSerializer.DeserializeSpecs"/>'s result.</param>
public sealed record CurveGroupSpecs(
    DateOnly ValuationDate,
    string GroupName,
    IReadOnlyList<CurveCalibrationSpec> Curves)
{
    private IReadOnlyList<CurveCalibrationSpec> _curves = DefinitionCopies.List(Curves, nameof(Curves));

    /// <summary>Protected snapshot of the deserialized calibration specifications.</summary>
    public IReadOnlyList<CurveCalibrationSpec> Curves
    {
        get => _curves;
        init => _curves = DefinitionCopies.List(value, nameof(Curves));
    }
}

/// <summary>
/// Serializes and deserializes curve calibration inputs to a stable JSON representation.
/// </summary>
/// <remarks>
/// The JSON schema uses camelCase property naming, indented formatting, and encodes dates as
/// <c>yyyy-MM-dd</c> strings. The class is stateless and safe for concurrent use. The DTO
/// <c>QuoteId</c> field is retained for backward compatibility but carries no semantic value
/// in the new model (nodes carry rates directly). <see cref="Serialize"/> writes the group-level
/// <c>groupName</c> (currently the first curve's name — see <see cref="Serialize"/> remarks) and
/// <c>valuationDate</c> fields; <see cref="DeserializeSpecs"/> discards both on read for
/// backward compatibility, while <see cref="DeserializeGroup"/> round-trips them.
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
    /// <remarks>
    /// The written <c>groupName</c> field is currently populated from
    /// <c>input.Curves[0].CurveName.Value</c> — <see cref="CurveCalibrationInput"/> has no
    /// distinct group-level name property today, so the first curve's name stands in for it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="input"/>.<see cref="CurveCalibrationInput.Curves"/> is empty.</exception>
    public string Serialize(CurveCalibrationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Curves.Count == 0)
        {
            throw new ArgumentException("Curves list must contain at least one curve specification.", nameof(input));
        }

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
                    node.Tenor.Value,
                    node.ProjectionCurves?.Select(reference => new CurveReferenceDto(
                        reference.Role.ToString(),
                        reference.Currency.ToString(),
                        reference.Benchmark?.Value)).ToArray())).ToArray())).ToArray());

        return JsonSerializer.Serialize(dto, _options);
    }

    /// <summary>
    /// Parses JSON into a list of <see cref="CurveCalibrationSpec"/> entries.
    /// </summary>
    /// <param name="json">JSON payload to deserialize.</param>
    /// <returns>Deserialized list of curve calibration specs.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the payload cannot be deserialized — malformed JSON, an unrecognised enum
    /// discriminator string, or a malformed date value in a jump point.
    /// </exception>
    /// <remarks>
    /// This method discards the payload's group-level <c>groupName</c> and <c>valuationDate</c>
    /// fields (pre-existing behaviour, retained for callers that only need the per-curve specs).
    /// Use <see cref="DeserializeGroup"/> to also recover those two fields.
    /// </remarks>
    public IReadOnlyList<CurveCalibrationSpec> DeserializeSpecs(string json)
    {
        return DeserializeAndMap(json).Specs;
    }

    /// <summary>
    /// Parses JSON into curve calibration specs alongside the group-level valuation date and
    /// group name that <see cref="DeserializeSpecs"/> alone discards.
    /// </summary>
    /// <param name="json">JSON payload to deserialize.</param>
    /// <returns>The round-tripped group name, valuation date, and per-curve specs.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the payload cannot be deserialized — malformed JSON, an unrecognised enum
    /// discriminator string, or a malformed date value.
    /// </exception>
    public CurveGroupSpecs DeserializeGroup(string json)
    {
        (CurveGroupDefinitionDto dto, IReadOnlyList<CurveCalibrationSpec> specs) = DeserializeAndMap(json);

        // groupName and valuationDate are required fields the serializer always writes; a payload
        // that omits (or nulls) either is malformed. Reject before ParseExact — otherwise a null
        // ValuationDate reaches DateOnly.ParseExact(null, ...) as ArgumentNullException (escaping the
        // catch below, violating the InvalidOperationException contract), and a null GroupName would
        // flow into the non-nullable CurveGroupSpecs.GroupName, breaking its own non-null annotation.
        if (dto.ValuationDate is null || dto.GroupName is null)
        {
            throw new InvalidOperationException("Failed to deserialize curve calibration input.");
        }

        DateOnly valuationDate;
        try
        {
            valuationDate = DateOnly.ParseExact(dto.ValuationDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Failed to deserialize curve calibration input.", ex);
        }

        return new CurveGroupSpecs(valuationDate, dto.GroupName, specs);
    }

    /// <summary>
    /// Deserializes the JSON payload into its DTO and maps every curve spec, under a single
    /// exception-wrapping boundary shared by <see cref="DeserializeSpecs"/> and
    /// <see cref="DeserializeGroup"/> — malformed JSON, an unrecognised enum discriminator, or a
    /// malformed jump-point date anywhere in <see cref="MapCurveSpec"/> all surface as
    /// <see cref="InvalidOperationException"/>, not the raw <see cref="JsonException"/> /
    /// <see cref="ArgumentException"/> / <see cref="FormatException"/> the mapping step can throw.
    /// </summary>
    private (CurveGroupDefinitionDto Dto, IReadOnlyList<CurveCalibrationSpec> Specs) DeserializeAndMap(string json)
    {
        try
        {
            CurveGroupDefinitionDto dto = JsonSerializer.Deserialize<CurveGroupDefinitionDto>(json, _options)
                ?? throw new InvalidOperationException("Failed to deserialize curve calibration input.");
            if (!string.Equals(dto.SchemaVersion, "1.0", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Failed to deserialize unsupported curve-definition schema version.");
            }
            ArgumentNullException.ThrowIfNull(dto.Curves);
            IReadOnlyList<CurveCalibrationSpec> specs = dto.Curves.Select(MapCurveSpec).ToArray();
            return (dto, specs);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException)
        {
            throw new InvalidOperationException("Failed to deserialize curve calibration input.", ex);
        }
    }

    private static CurveCalibrationSpec MapCurveSpec(CurveDefinitionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(dto.Nodes);
        CurveReference reference = new(
            ParseDefinedEnum<CurveRole>(dto.Role),
            ParseDefinedEnum<Currency>(dto.Currency, ignoreCase: true),
            string.IsNullOrWhiteSpace(dto.Benchmark) ? null : new BenchmarkName(dto.Benchmark));

        IReadOnlyList<ResolvedNode> nodes = dto.Nodes.Select(node => MapNode(reference, node)).ToArray();

        return new CurveCalibrationSpec(
            new CurveName(dto.CurveName),
            reference,
            ParseDefinedEnum<CurveValueType>(dto.ValueType),
            dto.DayCount,
            new InterpolationSettings(
                ParseDefinedEnum<InterpolatorKind>(dto.Interpolator),
                dto.LeftExtrapolator,
                dto.RightExtrapolator),
            nodes,
            dto.Jumps?.Select(MapJump).ToArray());
    }

    private static ResolvedNode MapNode(CurveReference reference, CurveNodeDefinitionDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        IReadOnlyList<CurveReference>? projectionCurves = dto.ProjectionCurves?
            .Select(MapReference)
            .ToArray();

        return new ResolvedNode(
            dto.Label,
            new Tenor(dto.Tenor),
            dto.Type,
            dto.Convention,
            reference,
            0m, // Rate not stored in definition JSON — set to zero; caller must supply rates separately.
            projectionCurves);
    }

    private static CurveReference MapReference(CurveReferenceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new CurveReference(
            ParseDefinedEnum<CurveRole>(dto.Role),
            ParseDefinedEnum<Currency>(dto.Currency, ignoreCase: true),
            string.IsNullOrWhiteSpace(dto.Benchmark) ? null : new BenchmarkName(dto.Benchmark));
    }
    private static CurvePoint MapJump(JumpPointDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new CurvePoint(
            DateOnly.ParseExact(dto.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture), dto.Value);
    }

    private static TEnum ParseDefinedEnum<TEnum>(string value, bool ignoreCase = false)
        where TEnum : struct, Enum
    {
        TEnum result = Enum.Parse<TEnum>(value, ignoreCase);
        if (!Enum.IsDefined(result))
        {
            throw new ArgumentException($"Undefined {typeof(TEnum).Name} value '{value}'.", nameof(value));
        }
        return result;
    }

}
