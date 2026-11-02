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

namespace Boutquin.Curves.Risk;

/// <summary>
/// Identifies a principal component in a PCA-based curve factor model.
/// </summary>
/// <remarks>
/// In empirical PCA of yield-curve changes (USD Treasuries, SOFR, etc.), the dominant
/// factors consistently map to:
/// <list type="bullet">
///   <item><description><see cref="Level"/> — parallel shift of the entire curve (~70–90% of variance)</description></item>
///   <item><description><see cref="Slope"/> — steepening/flattening between short and long end (~10–20% of variance)</description></item>
///   <item><description><see cref="Curvature"/> — butterfly move around the belly (~5–10% of variance)</description></item>
/// </list>
/// Integer values map directly to zero-based eigenvector column indices in
/// <c>Boutquin.Numerics.LinearAlgebra.PcaResult.Eigenvectors</c>.
/// </remarks>
public enum PrincipalComponent
{
    /// <summary>First principal component — parallel shift of the entire curve.</summary>
    Level = 0,

    /// <summary>Second principal component — slope (steepening/flattening).</summary>
    Slope = 1,

    /// <summary>Third principal component — curvature (butterfly).</summary>
    Curvature = 2,
}
