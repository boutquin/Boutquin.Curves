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

using Boutquin.Curves.Abstractions.Identifiers;

namespace Boutquin.Curves.Abstractions.Curves;

/// <summary>
/// Defines a read-only container of curves resolved for a single valuation date.
/// </summary>
public interface ICurveGroup
{
    /// <summary>
    /// Logical group name used for diagnostics and environment identity.
    /// </summary>
    CurveGroupName Name { get; }

    /// <summary>
    /// Valuation date shared by all curves in the group.
    /// </summary>
    DateOnly ValuationDate { get; }

    /// <summary>
    /// Returns a curve by its reference key.
    /// </summary>
    /// <param name="reference">Curve lookup key including role, currency, and optional benchmark.</param>
    /// <returns>The matching curve instance.</returns>
    ICurve GetCurve(CurveReference reference);

    /// <summary>
    /// Attempts to resolve a curve without throwing when the key is missing.
    /// </summary>
    /// <param name="reference">Curve lookup key including role, currency, and optional benchmark.</param>
    /// <param name="curve">Resolved curve when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a matching curve exists; otherwise <see langword="false"/>.</returns>
    bool TryGetCurve(CurveReference reference, out ICurve? curve);
}
