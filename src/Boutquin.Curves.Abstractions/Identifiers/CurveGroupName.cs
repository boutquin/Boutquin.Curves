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

namespace Boutquin.Curves.Abstractions.Identifiers;

/// <summary>
/// Represents the logical name of a curve group snapshot.
/// </summary>
/// <remarks>
/// A curve group is a collection of related curves calibrated together from a
/// consistent set of market quotes — for instance, <c>"USD-SOFR-Discount"</c> might
/// bundle a discount curve and one or more forward curves derived from the same
/// SOFR-linked instruments. The group name represents the market-state snapshot at
/// a specific valuation date and flows through diagnostics, risk reports, and
/// serialized curve stores. Strong typing as a dedicated record prevents a group
/// name from being passed where a <see cref="CurveName"/> is expected, or vice versa.
/// </remarks>
/// <param name="Value">Curve group name value.</param>
public readonly record struct CurveGroupName(string Value)
{
    /// <summary>
    /// Returns the underlying group-name value.
    /// </summary>
    /// <returns>String representation of this curve group name.</returns>
    public override string ToString() => Value;
}
