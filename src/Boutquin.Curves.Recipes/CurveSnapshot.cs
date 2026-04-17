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
using Boutquin.Curves.Abstractions.Diagnostics;
using Boutquin.MarketData.Abstractions.Results;

namespace Boutquin.Curves.Recipes;

/// <summary>
/// Immutable snapshot of a fully calibrated curve group together with its diagnostics and data-quality issues.
/// </summary>
/// <remarks>
/// Produced at the end of a recipe-driven calibration pipeline. The <see cref="CurveGroup"/>
/// is the calibrated result ready for pricing, while <see cref="Diagnostics"/> and
/// <see cref="DataIssues"/> provide full observability into the calibration and data-fetch
/// steps respectively. The optional <see cref="Jacobian"/> captures instrument-to-node
/// sensitivity for advanced users performing curve risk analysis.
/// </remarks>
/// <param name="GroupName">Logical name of the calibration group that produced this snapshot.</param>
/// <param name="ValuationDate">As-of date for which the curves were calibrated.</param>
/// <param name="CurveGroup">The calibrated curve group containing all discount and forward curves.</param>
/// <param name="Diagnostics">Bootstrap diagnostics covering repricing, structural, and numerical quality.</param>
/// <param name="DataIssues">Data-quality issues accumulated during the fetch and normalization pipeline.</param>
/// <param name="Provenance">Data provenance records indicating source, retrieval mode (api/cache/snapshot), and freshness.</param>
/// <param name="Jacobian">Optional calibration Jacobian for instrument-to-node sensitivity analysis.</param>
public sealed record CurveSnapshot(
    string GroupName,
    DateOnly ValuationDate,
    ICurveGroup CurveGroup,
    BootstrapDiagnostics Diagnostics,
    IReadOnlyList<DataIssue> DataIssues,
    IReadOnlyList<DataProvenance> Provenance,
    CalibrationJacobian? Jacobian = null);
