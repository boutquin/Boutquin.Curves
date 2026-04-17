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
/// Captures solver-level numerical diagnostics for a calibration run.
/// </summary>
/// <remarks>
/// Reports quality metrics from the iterative solver that calibrates curve nodes
/// to market quotes. Typical entries include Jacobian condition estimates,
/// convergence indicators, and terminal residual values. When <see cref="Converged"/>
/// is <see langword="false"/>, the calibration did not meet its tolerance criteria
/// within the allowed iteration budget, and the resulting curve should be treated
/// with caution. Entries flagged as warnings in <see cref="Message"/> — such as
/// near-singular Jacobians or slow convergence — should prompt a review of input
/// quote consistency, instrument overlap, or solver configuration.
/// </remarks>
/// <param name="SolverName">Name of the numerical method or solver implementation.</param>
/// <param name="Iterations">Number of iterations executed before termination.</param>
/// <param name="Converged">Indicates whether convergence criteria were met.</param>
/// <param name="Message">Human-readable status message from the solver.</param>
/// <param name="Residual">Optional terminal residual or objective value.</param>
public sealed record NumericalDiagnostic(
    string SolverName,
    int Iterations,
    bool Converged,
    string Message,
    double? Residual = null);
