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
/// 1. At least one curve specification is present (empty input is rejected before any indexing)
/// 2. No duplicate <c>CurveReference</c> within the input
/// 3. No duplicate node labels within a curve
/// 4. All <c>ConventionCode</c> values resolve in <c>IReferenceDataProvider</c>
/// 5. All <c>InterpolatorKind</c> values are supported
/// 6. Left/right extrapolator mode names are valid
/// 7. Every projection-curve reference named by a node resolves to its own target curve or to a
///    prior curve specification in the same input batch
/// </remarks>
public static class CurveCalibrationInputValidator
{
    // Must stay in lock-step with InterpolatedDiscountCurve.s_knownExtrapolatorModes — the runtime
    // curve constructor throws ArgumentException for any mode outside this set, so the pre-flight
    // validator must reject exactly the same set (it previously also listed "Linear"/"None", which
    // the runtime rejects, defeating the validator's purpose of catching the error before construction).
    private static readonly HashSet<string> s_validExtrapolators = new(StringComparer.OrdinalIgnoreCase)
    {
        "FlatZero",
        "FlatForward"
    };

    /// <summary>
    /// Validates a curve calibration input against reference data.
    /// </summary>
    /// <param name="input">Calibration input to validate.</param>
    /// <returns>Validation result with categorized errors and warnings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is <see langword="null"/>.</exception>
    public static ValidationResult Validate(CurveCalibrationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        List<ValidationEntry> errors = [];
        List<ValidationEntry> warnings = [];

        ValidateNonEmptyCurves(input, errors);
        ValidateDuplicateCurveReferences(input, errors);
        ValidateNonEmptyNodes(input, errors);
        ValidateDuplicateNodeLabels(input, errors);
        ValidateConventionResolution(input, errors);
        ValidateInterpolationSettings(input, errors, warnings);
        ValidateProjectionCurveAvailability(input, errors);

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }

    private static void ValidateNonEmptyCurves(CurveCalibrationInput input, List<ValidationEntry> errors)
    {
        if (input.Curves.Count == 0)
        {
            errors.Add(new ValidationEntry(
                "EMPTY_CURVES",
                "Calibration input contains no curve specifications; at least one curve is required.",
                string.Empty));
        }
    }

    private static void ValidateNonEmptyNodes(CurveCalibrationInput input, List<ValidationEntry> errors)
    {
        foreach (CurveCalibrationSpec curve in input.Curves)
        {
            if (curve.Nodes.Count == 0)
            {
                errors.Add(new ValidationEntry(
                    "EMPTY_NODES",
                    $"Curve '{curve.CurveName.Value}' contains no resolved nodes; at least one node is required.",
                    curve.CurveName.Value));
            }
        }
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

    private static void ValidateProjectionCurveAvailability(CurveCalibrationInput input, List<ValidationEntry> errors)
    {
        for (int i = 0; i < input.Curves.Count; i++)
        {
            CurveCalibrationSpec curve = input.Curves[i];
            foreach (ResolvedNode node in curve.Nodes)
            {
                if (node.ProjectionCurves is null)
                {
                    continue;
                }

                foreach (CurveReference projection in node.ProjectionCurves)
                {
                    // A node may name its own target (equivalent to self-discounting) or any curve
                    // calibrated earlier in the same batch (a prior spec, index j < i).
                    bool isSelfReference = projection == curve.CurveReference;
                    bool resolvedByPriorSpec = false;
                    for (int j = 0; j < i && !resolvedByPriorSpec; j++)
                    {
                        if (input.Curves[j].CurveReference == projection)
                        {
                            resolvedByPriorSpec = true;
                        }
                    }

                    if (!isSelfReference && !resolvedByPriorSpec)
                    {
                        string reference = $"{projection.Role}:{projection.Currency}:{projection.Benchmark?.Value ?? ""}";
                        errors.Add(new ValidationEntry(
                            "PROJECTION_CURVE_UNRESOLVED",
                            $"Projection curve '{reference}' named by node '{node.Label}' in curve '{curve.CurveName.Value}' is not available; "
                                + "it is neither the node's own target curve nor a curve calibrated by a prior specification in this input.",
                            node.Label));
                    }
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

            // Check extrapolator names. The runtime InterpolatedDiscountCurve ctor throws
            // ArgumentException for any mode outside s_validExtrapolators, so an unrecognized mode is a
            // BLOCKING error (IsValid == false), not a warning — otherwise the calibrator proceeds on an
            // IsValid==true input and a raw ArgumentException detonates deep in curve construction,
            // defeating the validator's pre-flight contract.
            if (!s_validExtrapolators.Contains(curve.Interpolation.LeftExtrapolator))
            {
                errors.Add(new ValidationEntry(
                    "UNKNOWN_LEFT_EXTRAPOLATOR",
                    $"Left extrapolator '{curve.Interpolation.LeftExtrapolator}' is not a recognized mode.",
                    curve.CurveName.Value));
            }

            if (!s_validExtrapolators.Contains(curve.Interpolation.RightExtrapolator))
            {
                errors.Add(new ValidationEntry(
                    "UNKNOWN_RIGHT_EXTRAPOLATOR",
                    $"Right extrapolator '{curve.Interpolation.RightExtrapolator}' is not a recognized mode.",
                    curve.CurveName.Value));
            }
        }
    }
}
