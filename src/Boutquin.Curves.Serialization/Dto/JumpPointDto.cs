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
/// Represents a jump adjustment point in serialized curve configuration.
/// </summary>
/// <param name="Date">Jump date encoded as <c>yyyy-MM-dd</c>; parsed by the deserializer into <see cref="DateOnly"/>.</param>
/// <param name="Value">Multiplicative factor applied to the discount factor on or after <paramref name="Date"/>; typically near 1.0.</param>
/// <remarks>
/// Serialization representation of a jump discontinuity in the discount curve, typically arising from
/// central bank meeting dates where the overnight rate may change discretely. The <paramref name="Date"/>
/// and <paramref name="Value"/> fields are both required for correct curve reconstruction. Jump values
/// multiply the underlying discount factor, so a value of 0.99 represents a 1% downward shift in the
/// discount factor at the jump date. Validation of the value range is deferred to the curve
/// construction layer, not enforced at the DTO level.
/// </remarks>
public sealed record JumpPointDto(
    string Date,
    double Value);
