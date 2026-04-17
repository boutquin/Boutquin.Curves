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
/// Captures non-numerical calibration issues related to data shape, ordering, or configuration.
/// </summary>
/// <remarks>
/// Structural diagnostics record observations about the calibration environment that
/// are not captured by numerical residuals. Common examples include proxy-data usage
/// (e.g., Treasury par yields substituting for unavailable SOFR OIS quotes), missing
/// holiday-calendar entries that may shift pillar dates, or convention-override
/// warnings where a node's day-count or compounding rule differs from the curve
/// default. Each entry carries a machine-readable <see cref="Code"/> for
/// programmatic filtering, a human-readable <see cref="Message"/>, a
/// <see cref="Severity"/> level (info, warning, or error), and an optional
/// <see cref="Context"/> string that pinpoints the affected node or instrument
/// for targeted debugging.
/// </remarks>
/// <param name="Code">Machine-readable diagnostic code.</param>
/// <param name="Message">Human-readable description of the structural issue.</param>
/// <param name="Severity">Severity indicator such as info, warning, or error.</param>
/// <param name="Context">Optional context payload for pinpointing the issue location.</param>
public sealed record StructuralDiagnostic(
    string Code,
    string Message,
    string Severity,
    string? Context = null);
