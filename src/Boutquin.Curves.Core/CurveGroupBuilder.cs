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
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> or <paramref name="curve"/> is null.</exception>
    public CurveGroupBuilder Add(CurveReference reference, ICurve curve)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(curve);
        _curves[reference] = curve;
        return this;
    }

    /// <summary>
    /// Materializes an immutable curve group snapshot from the currently added curves.
    /// </summary>
    /// <returns>A new <see cref="CurveGroup"/> instance.</returns>
    /// <remarks>
    /// A defensive copy is taken so that subsequent calls to <see cref="Add"/> on this builder
    /// do not retroactively mutate the returned snapshot. Each call to <c>Build()</c> produces
    /// an independent, immutable view of the curves added up to that point.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when any added curve has a <see cref="ICurve.ValuationDate"/> that differs from
    /// the builder's <see cref="ValuationDate"/>, which would produce an inconsistent group snapshot.
    /// </exception>
    public CurveGroup Build()
    {
        foreach ((CurveReference reference, ICurve curve) in _curves)
        {
            if (curve.ValuationDate != ValuationDate)
            {
                throw new InvalidOperationException(
                    $"Curve '{reference}' has valuation date {curve.ValuationDate:yyyy-MM-dd} " +
                    $"but the group valuation date is {ValuationDate:yyyy-MM-dd}. " +
                    "All curves in a group must share the same valuation date.");
            }
        }

        return new CurveGroup(Name, ValuationDate, new Dictionary<CurveReference, ICurve>(_curves));
    }
}
