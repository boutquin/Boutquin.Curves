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
/// Represents a user-facing curve identifier.
/// </summary>
/// <remarks>
/// Curve names uniquely identify discount or forward curves within a curve group.
/// By convention, names encode the currency, benchmark, and curve role as descriptive
/// codes — for instance, <c>"USD-SOFR-Disc"</c> identifies a U.S. dollar SOFR-based
/// discount curve. Wrapping the raw string in a dedicated value object prevents
/// accidental assignment of a curve name where a <see cref="CurveGroupName"/> or
/// other string identifier is expected, turning what would be a silent runtime bug
/// into a compile-time type error.
/// </remarks>
/// <param name="Value">Curve name value.</param>
public readonly record struct CurveName(string Value)
{
    /// <summary>
    /// Returns the underlying curve-name value.
    /// </summary>
    /// <returns>String representation of this curve name.</returns>
    public override string ToString() => Value;
}
