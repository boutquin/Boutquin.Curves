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
/// Flat DTO representation of a typed curve reference (role, currency, optional benchmark).
/// </summary>
/// <remarks>
/// Used to persist the projection-curve references that a multi-curve node depends on, so that a
/// basis-swap or forward node round-trips with its full multi-curve identity rather than silently
/// collapsing to self-discounting.
/// </remarks>
/// <param name="Role">Curve role such as Discount, Forward, Basis, or Collateral.</param>
/// <param name="Currency">ISO currency code for the referenced curve's settlement currency.</param>
/// <param name="Benchmark">Benchmark name for forward/basis curves (e.g. "SOFR"); null when not benchmark-specific.</param>
public sealed record CurveReferenceDto(
    string Role,
    string Currency,
    string? Benchmark);
