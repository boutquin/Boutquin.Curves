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

namespace Boutquin.Curves.Bootstrap.Diagnostics;

/// <summary>
/// Constructs calibration diagnostics payloads from repricing outcomes and metadata.
/// </summary>
/// <remarks>
/// Assembles diagnostic records from raw calibration state: repricing residuals produced by
/// instrument helpers (the difference between the observed market quote and the quote implied
/// by the calibrated curve), structural observations from the calibration pipeline (curve count,
/// node count), and numerical metrics from Jacobian computation (convergence status, maximum
/// absolute error). Each factory method produces an immutable diagnostic record suitable for
/// logging, monitoring dashboards, or downstream quality gates.
/// </remarks>
public static class BootstrapDiagnosticsFactory
{
    /// <summary>
    /// Creates a diagnostics payload for a piecewise calibration run.
    /// </summary>
    /// <param name="repricing">Repricing diagnostics produced by the calibration run.</param>
    /// <param name="curveCount">Number of curves calibrated in the run.</param>
    /// <param name="nodeCount">Number of solved nodes across all curves.</param>
    /// <returns>A diagnostics package with repricing, structural, and numerical sections.</returns>
    /// <remarks>
    /// The <c>NumericalDiagnostic.Passed</c> flag is always <c>true</c> for piecewise bootstrap because
    /// each node is solved deterministically (no iterative convergence). The <c>maxAbsError</c> metric
    /// is in native quote units (decimal rate, not basis points).
    /// </remarks>
    public static BootstrapDiagnostics CreateDiagnostics(IEnumerable<RepricingDiagnostic> repricing, int curveCount, int nodeCount)
    {
        RepricingDiagnostic[] repricingArray = repricing.ToArray();
        double maxAbsError = repricingArray.Length == 0 ? 0d : repricingArray.Max(static x => x.AbsoluteError);

        return new BootstrapDiagnostics(
            repricingArray,
            new[]
            {
                new StructuralDiagnostic("CURVE_COUNT", $"Calibrated {curveCount} curve(s).", "Info"),
                new StructuralDiagnostic("NODE_COUNT", $"Solved {nodeCount} node(s).", "Info")
            },
            new[]
            {
                new NumericalDiagnostic(
                    "PiecewiseBootstrap",
                    nodeCount,
                    true,
                    "Piecewise deterministic calibration completed.",
                    maxAbsError)
            });
    }

    /// <summary>
    /// Creates an exact-repricing diagnostic entry for a single node.
    /// </summary>
    /// <param name="label">Node label shown in diagnostics output.</param>
    /// <param name="targetCurve">Curve reference repriced by this node.</param>
    /// <param name="pillarDate">Resolved pillar date of the node.</param>
    /// <param name="quote">Observed market quote value; used as both market and implied quote to produce a zero residual by construction.</param>
    /// <param name="instrumentType">Instrument type label used for diagnostics grouping.</param>
    /// <returns>A repricing diagnostic with zero residual by construction.</returns>
    public static RepricingDiagnostic CreateExact(
        string label,
        CurveReference targetCurve,
        DateOnly pillarDate,
        double quote,
        string instrumentType)
    {
        return new RepricingDiagnostic(
            label,
            targetCurve,
            pillarDate,
            quote,
            quote,
            0d,
            0d,
            instrumentType);
    }

    /// <summary>
    /// Creates a repricing diagnostic from market and implied quotes produced by an instrument helper.
    /// </summary>
    /// <param name="label">Node label shown in diagnostics output.</param>
    /// <param name="targetCurve">Curve reference repriced by this node.</param>
    /// <param name="pillarDate">Resolved pillar date of the node.</param>
    /// <param name="marketQuote">Observed market quote used for calibration.</param>
    /// <param name="impliedQuote">Quote implied by the calibrated curve set.</param>
    /// <param name="instrumentType">Instrument type label used for diagnostics grouping.</param>
    /// <returns>A repricing diagnostic with signed and absolute residuals.</returns>
    public static RepricingDiagnostic CreateFromImplied(
        string label,
        CurveReference targetCurve,
        DateOnly pillarDate,
        double marketQuote,
        double impliedQuote,
        string instrumentType)
    {
        double signedError = impliedQuote - marketQuote;
        double absoluteError = Math.Abs(signedError);

        return new RepricingDiagnostic(
            label,
            targetCurve,
            pillarDate,
            marketQuote,
            impliedQuote,
            absoluteError,
            signedError,
            instrumentType);
    }
}
