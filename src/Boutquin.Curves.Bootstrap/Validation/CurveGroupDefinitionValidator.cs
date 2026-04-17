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

using Boutquin.Curves.Interpolation;

namespace Boutquin.Curves.Bootstrap.Validation;

/// <summary>
/// Validates a <see cref="CurveCalibrationInput"/> before calibration to catch
/// configuration errors early with clear diagnostic messages.
/// </summary>
/// <remarks>
/// Pre-flight validation prevents confusing runtime exceptions during bootstrap.
/// Invalid inputs (duplicate curve references, duplicate node labels, unresolvable conventions,
/// unsupported interpolators) produce clear, categorized error messages instead of
/// <c>KeyNotFoundException</c> or <c>InvalidOperationException</c> deep in the calibration pipeline.
///
/// Validation rules:
/// 1. No duplicate <c>CurveReference</c> within the input
/// 2. No duplicate node labels within a curve
/// 3. All <c>ConventionCode</c> values resolve in <c>IReferenceDataProvider</c>
/// 4. All <c>InterpolatorKind</c> values are supported
/// 5. Left/right extrapolator mode names are valid
/// </remarks>
public static class CurveCalibrationInputValidator
{
    private static readonly HashSet<string> s_validExtrapolators = new(StringComparer.OrdinalIgnoreCase)
    {
        "FlatZero",
        "FlatForward",
        "Linear",
        "None"
    };

    /// <summary>
    /// Validates a curve calibration input against reference data.
    /// </summary>
    /// <param name="input">Calibration input to validate.</param>
    /// <returns>Validation result with categorized errors and warnings.</returns>
    public static ValidationResult Validate(CurveCalibrationInput input)
    {
        List<ValidationEntry> errors = [];
        List<ValidationEntry> warnings = [];

        ValidateDuplicateCurveReferences(input, errors);
        ValidateDuplicateNodeLabels(input, errors);
        ValidateConventionResolution(input, errors);
        ValidateInterpolationSettings(input, errors, warnings);

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }

    private static void ValidateDuplicateCurveReferences(CurveCalibrationInput input, List<ValidationEntry> errors)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (CurveCalibrationSpec curve in input.Curves)
        {
            string key = $"{curve.CurveReference.Role}:{curve.CurveReference.Currency}:{curve.CurveReference.Benchmark?.Value ?? ""}";
            if (!seen.Add(key))
            {
                errors.Add(new ValidationEntry(
                    "DUPLICATE_CURVE_REF",
                    $"Duplicate CurveReference '{key}' in calibration input.",
                    curve.CurveName.Value));
            }
        }
    }

    private static void ValidateDuplicateNodeLabels(CurveCalibrationInput input, List<ValidationEntry> errors)
    {
        foreach (CurveCalibrationSpec curve in input.Curves)
        {
            HashSet<string> labels = new(StringComparer.Ordinal);
            foreach (ResolvedNode node in curve.Nodes)
            {
                if (!labels.Add(node.Label))
                {
                    errors.Add(new ValidationEntry(
                        "DUPLICATE_NODE_LABEL",
                        $"Duplicate node label '{node.Label}' in curve '{curve.CurveName.Value}'.",
                        curve.CurveName.Value));
                }
            }
        }
    }

    private static void ValidateConventionResolution(CurveCalibrationInput input, List<ValidationEntry> errors)
    {
        foreach (CurveCalibrationSpec curve in input.Curves)
        {
            foreach (ResolvedNode node in curve.Nodes)
            {
                try
                {
                    input.ReferenceData.GetConvention(node.ConventionCode);
                }
                catch (KeyNotFoundException)
                {
                    errors.Add(new ValidationEntry(
                        "UNRESOLVABLE_CONVENTION",
                        $"Convention '{node.ConventionCode}' required by node '{node.Label}' cannot be resolved.",
                        node.Label));
                }
            }
        }
    }

    private static void ValidateInterpolationSettings(CurveCalibrationInput input, List<ValidationEntry> errors, List<ValidationEntry> warnings)
    {
        foreach (CurveCalibrationSpec curve in input.Curves)
        {
            // Check InterpolatorKind
            if (!Enum.IsDefined(curve.Interpolation.Interpolator))
            {
                errors.Add(new ValidationEntry(
                    "UNSUPPORTED_INTERPOLATOR",
                    $"InterpolatorKind '{curve.Interpolation.Interpolator}' is not a recognized value.",
                    curve.CurveName.Value));
            }
            else
            {
                // Check if factory can create it
                try
                {
                    InterpolatorFactory.Create(curve.Interpolation.Interpolator);
                }
                catch (NotSupportedException)
                {
                    warnings.Add(new ValidationEntry(
                        "UNIMPLEMENTED_INTERPOLATOR",
                        $"InterpolatorKind '{curve.Interpolation.Interpolator}' is recognized but not yet implemented.",
                        curve.CurveName.Value));
                }
            }

            // Check extrapolator names
            if (!s_validExtrapolators.Contains(curve.Interpolation.LeftExtrapolator))
            {
                warnings.Add(new ValidationEntry(
                    "UNKNOWN_LEFT_EXTRAPOLATOR",
                    $"Left extrapolator '{curve.Interpolation.LeftExtrapolator}' is not a recognized mode.",
                    curve.CurveName.Value));
            }

            if (!s_validExtrapolators.Contains(curve.Interpolation.RightExtrapolator))
            {
                warnings.Add(new ValidationEntry(
                    "UNKNOWN_RIGHT_EXTRAPOLATOR",
                    $"Right extrapolator '{curve.Interpolation.RightExtrapolator}' is not a recognized mode.",
                    curve.CurveName.Value));
            }
        }
    }
}
