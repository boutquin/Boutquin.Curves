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

namespace Boutquin.Curves.Bootstrap;

/// <summary>
/// Represents the complete output of a curve calibration run, including calibrated curves and diagnostics.
/// </summary>
/// <remarks>
/// Bundles calibration outputs: the solved <see cref="ICurveGroup"/> ready for pricing and risk,
/// <see cref="BootstrapDiagnostics"/> for quality assessment (repricing residuals, structural
/// observations, numerical metrics), and an optional <see cref="CalibrationJacobian"/> for
/// explainability and hedging analysis. Consumers should always check diagnostics before using the
/// curve group in production, because a calibration that completes without exceptions can still
/// contain unacceptable repricing residuals.
/// </remarks>
/// <param name="CurveGroup">Calibrated curve group ready for pricing and risk workflows.</param>
/// <param name="Diagnostics">Repricing, structural, and numerical diagnostics describing calibration quality.</param>
/// <param name="Jacobian">Optional finite-difference sensitivity matrix linking quote bumps to implied-quote responses.</param>
public sealed record CurveCalibrationResult(
    ICurveGroup CurveGroup,
    BootstrapDiagnostics Diagnostics,
    CalibrationJacobian? Jacobian = null);
