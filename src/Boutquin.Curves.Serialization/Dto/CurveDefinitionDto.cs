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
/// Defines the metadata and node set required to construct a single curve during bootstrap.
/// </summary>
/// <remarks>
/// JSON-serializable representation of a single curve definition within a group. Maps 1:1 to the
/// domain <c>CurveDefinition</c> type; the DTO schema uses explicit, human-readable field names
/// rather than positional encoding so that persisted JSON files remain inspectable and editable
/// without specialized tooling. Each field value is a string identifier that the deserializer
/// resolves to the corresponding domain enum or factory type.
/// </remarks>
/// <param name="CurveName">Curve identifier used for diagnostics and output labeling.</param>
/// <param name="Role">Curve role such as Discount, Forward, Collateral, or Borrow.</param>
/// <param name="Currency">ISO currency code for the curve's settlement currency.</param>
/// <param name="Benchmark">Benchmark name for forward curves (e.g. "SOFR"); null for discount curves.</param>
/// <param name="ValueType">Ordinate type solved during calibration (e.g. DiscountFactor).</param>
/// <param name="DayCount">Day-count convention code used for accrual calculations (e.g. ACT/360).</param>
/// <param name="Interpolator">Interpolation algorithm identifier (e.g. LogLinearDiscountFactor).</param>
/// <param name="LeftExtrapolator">Extrapolation mode applied before the first calibration node (e.g. FlatZero).</param>
/// <param name="RightExtrapolator">Extrapolation mode applied beyond the last calibration node (e.g. FlatForward).</param>
/// <param name="Jumps">Optional multiplicative jump factors applied at configured dates; null when no jumps are configured.</param>
/// <param name="Nodes">Instrument node definitions whose market quotes drive curve calibration.</param>
public sealed record CurveDefinitionDto(
    string CurveName,
    string Role,
    string Currency,
    string? Benchmark,
    string ValueType,
    string DayCount,
    string Interpolator,
    string LeftExtrapolator,
    string RightExtrapolator,
    IReadOnlyList<JumpPointDto>? Jumps,
    IReadOnlyList<CurveNodeDefinitionDto> Nodes);
