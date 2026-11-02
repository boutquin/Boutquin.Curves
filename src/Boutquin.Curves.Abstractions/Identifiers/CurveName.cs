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
///
/// <b>Default-value hazard:</b> because <c>CurveName</c> is a value type, the
/// parameterless <c>default(CurveName)</c> instance bypasses this constructor and
/// leaves <see cref="Value"/> as <see langword="null"/> at the CLR level. Code that
/// allocates arrays (<c>new CurveName[N]</c>), uses uninitialized fields, or
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
public readonly record struct CurveName
{
    /// <summary>
    /// The canonical string that identifies this curve within a group.
    /// Always non-null on instances created via <see cref="CurveName(string)"/>.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new <see cref="CurveName"/> with the specified value.
    /// </summary>
    /// <param name="value">Curve name value. Must be non-null and non-whitespace.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="value"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    [JsonConstructor]
    public CurveName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));
        Value = value;
    }

    /// <summary>
    /// Returns the underlying curve-name value, or <c>"&lt;uninitialized&gt;"</c> for a
    /// <c>default(CurveName)</c> instance whose <see cref="Value"/> is <see langword="null"/>.
    /// </summary>
    /// <returns>String representation of this curve name. Never returns <see langword="null"/>.</returns>
    public override string ToString() => Value ?? "<uninitialized>";
}
