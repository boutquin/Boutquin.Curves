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

namespace Boutquin.Curves.Serialization.Dto;

/// <summary>
/// Defines a multi-curve bootstrap configuration for one valuation context.
/// </summary>
/// <remarks>
/// JSON-serializable representation of a <c>CurveGroupDefinition</c>. Used for persisting curve
/// configurations to disk or transmitting definitions across service boundaries. The serializer
/// handles the polymorphic <c>CurveNodeDefinition</c> hierarchy via a discriminator field on each
/// node DTO. All date fields are stored as ISO 8601 strings to avoid timezone ambiguity in JSON.
/// </remarks>
/// <param name="GroupName">Curve name used for diagnostics and output labeling.</param>
/// <param name="ValuationDate">Valuation date for this calibration run.</param>
/// <param name="Curves">Collection of curve definitions in the group.</param>
/// <param name="SchemaVersion">Schema version tag for forward compatibility; defaults to <c>1.0</c>.</param>
public sealed record CurveGroupDefinitionDto(
    string GroupName,
    string ValuationDate,
    IReadOnlyList<CurveDefinitionDto> Curves,
    string SchemaVersion = "1.0");
