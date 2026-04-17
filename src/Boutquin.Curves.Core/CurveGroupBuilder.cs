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

using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;

namespace Boutquin.Curves.Core;

/// <summary>
/// Incrementally assembles a curve group snapshot for a single valuation date.
/// </summary>
/// <seealso cref="Boutquin.Curves.Core.CurveGroup"/>
public sealed class CurveGroupBuilder
{
    private readonly Dictionary<CurveReference, ICurve> _curves = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CurveGroupBuilder"/> type.
    /// </summary>
    /// <param name="name">Logical name of the curve group being assembled.</param>
    /// <param name="valuationDate">Date at which all added curves are interpreted.</param>
    public CurveGroupBuilder(CurveGroupName name, DateOnly valuationDate)
    {
        Name = name;
        ValuationDate = valuationDate;
    }

    /// <summary>
    /// Logical name for the group under construction.
    /// </summary>
    public CurveGroupName Name { get; }

    /// <summary>
    /// Valuation date for the group under construction.
    /// </summary>
    public DateOnly ValuationDate { get; }

    /// <summary>
    /// Adds or replaces a curve for the provided reference key.
    /// </summary>
    /// <param name="reference">Curve key including role, currency, and optional benchmark identity.</param>
    /// <param name="curve">Resolved curve instance to store for the key.</param>
    /// <returns>The current builder instance for fluent chaining.</returns>
    public CurveGroupBuilder Add(CurveReference reference, ICurve curve)
    {
        _curves[reference] = curve;
        return this;
    }

    /// <summary>
    /// Materializes an immutable curve group snapshot from the currently added curves.
    /// </summary>
    /// <returns>A new <see cref="CurveGroup"/> instance.</returns>
    public CurveGroup Build() => new(Name, ValuationDate, _curves);
}
