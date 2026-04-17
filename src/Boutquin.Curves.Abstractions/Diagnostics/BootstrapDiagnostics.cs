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

namespace Boutquin.Curves.Abstractions.Diagnostics;

/// <summary>
/// Groups diagnostic outputs produced during curve calibration.
/// </summary>
/// <remarks>
/// Aggregates every diagnostic emitted during a single bootstrap calibration run
/// into three complementary categories. Consumers should inspect
/// <see cref="Repricing"/> entries first: absolute errors above roughly 1e-8 suggest
/// that an instrument could not be exactly reproduced and may indicate data-quality
/// problems or convention mismatches. <see cref="Structural"/> entries surface
/// non-numerical observations such as proxy-data substitution (e.g., Treasury par
/// yields standing in for SOFR OIS quotes), missing holidays, or convention overrides.
/// <see cref="Numerical"/> entries report solver-level quality metrics — Jacobian
/// condition estimates, iteration counts, and convergence flags — that reveal whether
/// the calibration algorithm itself behaved well.
/// </remarks>
/// <param name="Repricing">Per-node repricing diagnostics comparing market and implied quotes.</param>
/// <param name="Structural">Structural diagnostics describing data or configuration issues.</param>
/// <param name="Numerical">Numerical diagnostics describing solver convergence and residual quality.</param>
public sealed record BootstrapDiagnostics(
    IReadOnlyList<RepricingDiagnostic> Repricing,
    IReadOnlyList<StructuralDiagnostic> Structural,
    IReadOnlyList<NumericalDiagnostic> Numerical)
{
    /// <summary>
    /// Empty diagnostics payload with no repricing, structural, or numerical entries.
    /// </summary>
    public static BootstrapDiagnostics Empty { get; } = new(
        Array.Empty<RepricingDiagnostic>(),
        Array.Empty<StructuralDiagnostic>(),
        Array.Empty<NumericalDiagnostic>());
}
