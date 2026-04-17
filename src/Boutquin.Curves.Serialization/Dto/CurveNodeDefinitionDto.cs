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
/// Describes a bootstrap node and its market-data binding within a curve definition.
/// </summary>
/// <remarks>
/// Flat DTO representation of a polymorphic <c>CurveNodeDefinition</c>. The <paramref name="Type"/>
/// field acts as a discriminator that determines which concrete definition type to instantiate
/// during deserialization (e.g., Deposit, OisSwap, FixedFloatSwap). All instrument-specific fields
/// are present on this single DTO, but only the subset relevant to the declared node type is
/// populated; unused fields carry default or empty values.
/// </remarks>
/// <param name="Type">JSON node type field.</param>
/// <param name="Label">Node label used in diagnostics and reporting.</param>
/// <param name="QuoteId">Identifier of the market quote bound to this node.</param>
/// <param name="Convention">Convention code used for schedule and accrual rules.</param>
/// <param name="Tenor">Tenor value used to derive maturity timing.</param>
public sealed record CurveNodeDefinitionDto(
    string Type,
    string Label,
    string QuoteId,
    string Convention,
    string Tenor);
