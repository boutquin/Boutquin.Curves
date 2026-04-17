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
/// Represents an immutable snapshot of resolved curves for a single valuation date.
/// </summary>
/// <seealso cref="Boutquin.Curves.Core.CurveGroupBuilder"/>
public sealed class CurveGroup : ICurveGroup
{
    private readonly IReadOnlyDictionary<CurveReference, ICurve> _curves;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurveGroup"/> type.
    /// </summary>
    /// <param name="name">Logical name of the curve group.</param>
    /// <param name="valuationDate">Date at which all curves in this group are evaluated.</param>
    /// <param name="curves">Curve map keyed by role, currency, and optional benchmark.</param>
    public CurveGroup(CurveGroupName name, DateOnly valuationDate, IReadOnlyDictionary<CurveReference, ICurve> curves)
    {
        Name = name;
        ValuationDate = valuationDate;
        _curves = curves;
    }

    /// <summary>
    /// Logical name of this curve group, used for diagnostics and environment identity.
    /// </summary>
    public CurveGroupName Name { get; }

    /// <summary>
    /// Valuation date shared by all curves in this group.
    /// </summary>
    public DateOnly ValuationDate { get; }

    /// <summary>
    /// Returns the curve identified by the provided reference.
    /// </summary>
    /// <param name="reference">Curve lookup key including role, currency, and benchmark identity.</param>
    /// <returns>The matching curve instance.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the requested curve reference does not exist in this snapshot.</exception>
    public ICurve GetCurve(CurveReference reference)
    {
        if (!_curves.TryGetValue(reference, out ICurve? curve))
        {
            throw new KeyNotFoundException($"Curve reference '{reference}' was not found in group '{Name}'.");
        }

        return curve;
    }

    /// <summary>
    /// Attempts to resolve a curve without throwing when the reference is missing.
    /// </summary>
    /// <param name="reference">Curve lookup key including role, currency, and benchmark identity.</param>
    /// <param name="curve">The resolved curve instance when the lookup succeeds; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a matching curve is found; otherwise <see langword="false"/>.</returns>
    public bool TryGetCurve(CurveReference reference, out ICurve? curve) => _curves.TryGetValue(reference, out curve);

    /// <summary>
    /// Exposes the underlying curve map for read-only iteration.
    /// </summary>
    public IReadOnlyDictionary<CurveReference, ICurve> AsDictionary() => _curves;
}
