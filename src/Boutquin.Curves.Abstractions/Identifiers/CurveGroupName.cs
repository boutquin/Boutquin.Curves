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

using System.Text.Json.Serialization;

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
///
/// <b>Default-value hazard:</b> because <c>CurveGroupName</c> is a value type, the
/// parameterless <c>default(CurveGroupName)</c> instance bypasses this constructor and
/// leaves <see cref="Value"/> as <see langword="null"/> at the CLR level. Code that
/// allocates arrays (<c>new CurveGroupName[N]</c>), uses uninitialized fields, or
/// produces explicit <c>default</c> literals yields such instances without invoking
/// this constructor. <see cref="ToString"/> returns <c>"&lt;uninitialized&gt;"</c>
/// for those instances so that logging and diagnostics do not throw a
/// <see cref="NullReferenceException"/>; however, using a default instance as a
/// dictionary key or wire identifier remains a logic error.
/// </remarks>
/// <exception cref="ArgumentException">
/// Thrown when the value supplied to the constructor is <see langword="null"/>,
/// empty, or consists solely of whitespace characters.
/// </exception>
public readonly record struct CurveGroupName
{
    /// <summary>
    /// The canonical string that identifies this curve group snapshot.
    /// Always non-null on instances created via <see cref="CurveGroupName(string)"/>.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new <see cref="CurveGroupName"/> with the specified value.
    /// </summary>
    /// <param name="value">Curve group name value. Must be non-null and non-whitespace.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    [JsonConstructor]
    public CurveGroupName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        Value = value;
    }

    /// <summary>
    /// Returns the underlying group-name value, or <c>"&lt;uninitialized&gt;"</c> for a
    /// <c>default(CurveGroupName)</c> instance whose <see cref="Value"/> is <see langword="null"/>.
    /// </summary>
    /// <returns>String representation of this curve group name. Never returns <see langword="null"/>.</returns>
    public override string ToString() => Value ?? "<uninitialized>";
}
