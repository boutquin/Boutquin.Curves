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

namespace Boutquin.Curves.Bootstrap.Validation;

/// <summary>
/// Outcome of pre-calibration validation of a curve group definition.
/// </summary>
/// <param name="IsValid">Whether the definition passed all validation rules.</param>
/// <param name="Errors">Validation errors that block calibration.</param>
/// <param name="Warnings">Non-blocking validation observations.</param>
public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationEntry> Errors,
    IReadOnlyList<ValidationEntry> Warnings)
{
    /// <summary>
    /// A valid result with no errors or warnings.
    /// </summary>
    public static ValidationResult Valid { get; } = new(true, Array.Empty<ValidationEntry>(), Array.Empty<ValidationEntry>());
}

/// <summary>
/// A single validation finding.
/// </summary>
/// <param name="Code">Machine-readable validation rule code.</param>
/// <param name="Message">Human-readable description of the finding.</param>
/// <param name="Context">Optional context (e.g., curve name, node label) for localization.</param>
public sealed record ValidationEntry(string Code, string Message, string? Context = null);
