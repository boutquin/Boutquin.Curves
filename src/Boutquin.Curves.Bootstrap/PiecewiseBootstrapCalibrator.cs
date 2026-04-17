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

using Boutquin.Curves.Abstractions.Bootstrap;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Diagnostics;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Bootstrap.Diagnostics;
using Boutquin.Curves.Bootstrap.Helpers;
using Boutquin.Curves.Bootstrap.Validation;
using Boutquin.Curves.Core;
using Boutquin.Curves.Core.Discounting;

namespace Boutquin.Curves.Bootstrap;

/// <summary>
/// Calibrates curve groups node-by-node using deterministic piecewise bootstrap logic.
/// </summary>
/// <remarks>
/// Piecewise bootstrap is the standard desk pattern for curve construction: solve one node at a time,
/// locking prior nodes so each new market quote determines one new degree of freedom. This produces
/// transparent calibration diagnostics and stable failure localization when market data is inconsistent.
///
/// Pipeline context: the calibrator is the core solver in the curve construction pipeline. It consumes
/// CurveCalibrationInput (specifications + pre-resolved rates + reference data) and produces
/// CurveCalibrationResult (calibrated curves + diagnostics + Jacobian). Upstream: data aggregation
/// and quote normalization. Downstream: curve querying, risk analysis, and serialization.
/// </remarks>
/// <example>
/// <code>
/// var calibrator = new PiecewiseBootstrapCalibrator();
/// var input = new CurveCalibrationInput(valuationDate, curveSpecs, referenceData);
/// var result = calibrator.Calibrate(input);
/// ICurveGroup curveGroup = result.CurveGroup;
/// // Inspect result.Diagnostics.Repricing for calibration quality
/// </code>
/// </example>
/// <seealso cref="ResolvedNode"/>
/// <seealso cref="Boutquin.Curves.Abstractions.Diagnostics.BootstrapDiagnostics"/>
/// <seealso cref="Boutquin.Curves.Core.Discounting.InterpolatedDiscountCurve"/>
public sealed class PiecewiseBootstrapCalibrator : ICurveCalibrator<CurveCalibrationInput, CurveCalibrationResult>
{
    private const decimal JacobianBump = 1e-6m;
    private const double JacobianIllConditionedThreshold = 1e12d;

    /// <summary>
    /// Calibrates all requested curves node-by-node and returns calibrated curves with bootstrap diagnostics.
    /// </summary>
    /// <param name="request">Calibration input containing valuation date, curve specifications with pre-resolved rates, and reference data.</param>
    /// <returns>Calibration result containing the calibrated curve group, repricing diagnostics, and calibration Jacobian.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when pre-calibration validation detects configuration errors (duplicate curve references, missing conventions,
    /// or unsupported interpolators) — unless <see cref="CurveCalibrationInput.SkipValidation"/> is set.
    /// Also thrown when the calibration Jacobian contains non-finite values.
    /// </exception>
    /// <exception cref="NotSupportedException">Thrown when a resolved node has an unrecognised instrument type.</exception>
    /// <remarks>
    /// Nodes are solved sequentially within each curve specification, ordered by tenor.
    /// After calibration, a finite-difference Jacobian is computed by bumping each input rate
    /// by <c>1e-6</c> and measuring the implied-quote sensitivity.
    /// The resulting Jacobian serves as an explainability artifact for risk teams: each entry approximates
    /// how one instrument's implied quote reacts to another instrument's market rate bump.
    ///
    /// Jacobian computation: each entry $J_{ij}$ approximates $\partial q_i / \partial r_j$ where $q_i$ is
    /// the implied quote for instrument $i$ and $r_j$ is the market rate for instrument $j$. The
    /// finite-difference approximation is $J_{ij} \approx [q_i(r_j + \epsilon) - q_i(r_j)] / \epsilon$
    /// with $\epsilon = 10^{-6}$. A well-conditioned Jacobian (condition estimate below $10^{12}$) indicates
    /// that each instrument's calibration is primarily sensitive to its own rate, confirming clean bootstrap
    /// structure.
    /// </remarks>
    public CurveCalibrationResult Calibrate(CurveCalibrationInput request) => CalibrateInternal(request, includeJacobian: true);

    private CurveCalibrationResult CalibrateInternal(CurveCalibrationInput request, bool includeJacobian)
    {
        if (!request.SkipValidation)
        {
            ValidationResult validation = CurveCalibrationInputValidator.Validate(request);
            if (!validation.IsValid)
            {
                string errorSummary = string.Join("; ", validation.Errors.Select(e => $"[{e.Code}] {e.Message}"));
                throw new InvalidOperationException($"Curve calibration input validation failed: {errorSummary}");
            }
        }

        CurveGroupBuilder builder = new(new CurveGroupName(request.Curves[0].CurveName.Value), request.ValuationDate);
        List<(AbstractInstrumentHelper Helper, DateOnly PillarDate, double MarketQuote, int CurveIndex, int NodeIndex)> pendingRepricing = new();
        int solvedNodeCount = 0;

        for (int curveIndex = 0; curveIndex < request.Curves.Count; curveIndex++)
        {
            CurveCalibrationSpec curveSpec = request.Curves[curveIndex];
            List<CurvePoint> points = new();

            IReadOnlyList<ResolvedNode> sortedNodes = NodeSorter.Sort(curveSpec.Nodes, request.ValuationDate);

            for (int nodeIndex = 0; nodeIndex < sortedNodes.Count; nodeIndex++)
            {
                ResolvedNode node = sortedNodes[nodeIndex];
                double quote = (double)node.Rate;
                AbstractInstrumentHelper helper = CreateHelper(node, quote);
                DateOnly nodeDate = helper.PillarDate(request.ValuationDate, request.ReferenceData);
                double curveValue = helper.SolveNodeValue(builder.Build(), request.ValuationDate, request.ReferenceData);

                points.Add(new CurvePoint(nodeDate, curveValue));
                pendingRepricing.Add((helper, nodeDate, quote, curveIndex, nodeIndex));
                solvedNodeCount++;
            }

            ICurve curve = BuildCurve(curveSpec, request.ValuationDate, points);
            builder.Add(curveSpec.CurveReference, curve);
        }

        ICurveGroup calibratedCurveGroup = builder.Build();
        List<RepricingDiagnostic> repricing = new(pendingRepricing.Count);

        for (int i = 0; i < pendingRepricing.Count; i++)
        {
            (AbstractInstrumentHelper helper, DateOnly pillarDate, double marketQuote, _, _) = pendingRepricing[i];
            double impliedQuote = helper.ImpliedQuote(calibratedCurveGroup, request.ValuationDate, request.ReferenceData);
            repricing.Add(BootstrapDiagnosticsFactory.CreateFromImplied(
                helper.Label,
                helper.TargetCurve,
                pillarDate,
                marketQuote,
                impliedQuote,
                helper.InstrumentType));
        }

        BootstrapDiagnostics diagnostics = BootstrapDiagnosticsFactory.CreateDiagnostics(
            repricing,
            request.Curves.Count,
            solvedNodeCount);

        CalibrationJacobian? jacobian = includeJacobian
            ? BuildCalibrationJacobian(request, pendingRepricing, repricing)
            : null;

        if (jacobian is not null)
        {
            diagnostics = AppendJacobianQualityDiagnostics(diagnostics, jacobian);
        }

        return new CurveCalibrationResult(calibratedCurveGroup, diagnostics, jacobian);
    }

    private static BootstrapDiagnostics AppendJacobianQualityDiagnostics(BootstrapDiagnostics diagnostics, CalibrationJacobian jacobian)
    {
        bool dimensionsConsistent =
            jacobian.RowLabels.Count == jacobian.Values.GetLength(0) &&
            jacobian.ColumnLabels.Count == jacobian.Values.GetLength(1);

        bool allFinite = true;
        double maxAbs = 0d;
        double minAbsNonZero = double.PositiveInfinity;

        for (int row = 0; row < jacobian.Values.GetLength(0); row++)
        {
            for (int column = 0; column < jacobian.Values.GetLength(1); column++)
            {
                double value = jacobian.Values[row, column];
                if (!double.IsFinite(value))
                {
                    allFinite = false;
                    continue;
                }

                double absValue = Math.Abs(value);
                maxAbs = Math.Max(maxAbs, absValue);
                if (absValue > 0d)
                {
                    minAbsNonZero = Math.Min(minAbsNonZero, absValue);
                }
            }
        }

        double? conditionEstimate = double.IsPositiveInfinity(minAbsNonZero)
            ? null
            : maxAbs / Math.Max(minAbsNonZero, 1e-300d);

        // Policy: condition estimates above 1e12 are treated as ill-conditioned.
        bool illConditioned = conditionEstimate is not null && conditionEstimate > JacobianIllConditionedThreshold;
        bool jacobianHealthy = dimensionsConsistent && allFinite && !illConditioned;

        List<StructuralDiagnostic> structural = new(diagnostics.Structural)
        {
            new StructuralDiagnostic(
                "JACOBIAN_DIMENSIONS",
                dimensionsConsistent
                    ? "Jacobian dimensions match row/column labels."
                    : "Jacobian dimensions do not match row/column labels.",
                dimensionsConsistent ? "Info" : "Warning"),
            new StructuralDiagnostic(
                "JACOBIAN_FINITE",
                allFinite
                    ? "Jacobian contains finite numeric entries only."
                    : "Jacobian contains non-finite values (NaN or Infinity).",
                allFinite ? "Info" : "Warning")
        };

        string conditionMessage = conditionEstimate is null
            ? "Condition estimate unavailable (Jacobian is zero matrix)."
            : $"Condition estimate: {conditionEstimate.Value:E6}.";

        List<NumericalDiagnostic> numerical = new(diagnostics.Numerical)
        {
            new NumericalDiagnostic(
                "JacobianQuality",
                jacobian.Values.GetLength(0),
                jacobianHealthy,
                conditionMessage,
                conditionEstimate)
        };

        if (!allFinite)
        {
            throw new InvalidOperationException("Calibration Jacobian contains non-finite values.");
        }

        return new BootstrapDiagnostics(diagnostics.Repricing, structural, numerical);
    }

    private CalibrationJacobian BuildCalibrationJacobian(
        CurveCalibrationInput request,
        IReadOnlyList<(AbstractInstrumentHelper Helper, DateOnly PillarDate, double MarketQuote, int CurveIndex, int NodeIndex)> pendingRepricing,
        IReadOnlyList<RepricingDiagnostic> baseRepricing)
    {
        // Build a flat list of (curveIndex, nodeIndex) to identify unique bump targets.
        // Distinct is load-bearing: duplicate labels would produce a wrong-shaped Jacobian.
        var bumpTargets = pendingRepricing.Select(item => (item.CurveIndex, item.NodeIndex)).Distinct().ToArray();
        string[] rowLabels = pendingRepricing.Select(static item => item.Helper.Label).ToArray();
        string[] columnLabels = bumpTargets.Select(t => pendingRepricing.First(p => p.CurveIndex == t.CurveIndex && p.NodeIndex == t.NodeIndex).Helper.Label).ToArray();
        double[,] values = new double[rowLabels.Length, columnLabels.Length];

        for (int column = 0; column < bumpTargets.Length; column++)
        {
            (int curveIdx, int nodeIdx) = bumpTargets[column];
            CurveCalibrationInput bumpedInput = BumpRate(request, curveIdx, nodeIdx, JacobianBump);
            // includeJacobian: false prevents recursive Jacobian computation inside the finite-difference loop.
            CurveCalibrationResult bumpedResult = CalibrateInternal(bumpedInput, includeJacobian: false);

            for (int row = 0; row < pendingRepricing.Count; row++)
            {
                AbstractInstrumentHelper helper = pendingRepricing[row].Helper;
                double bumpedImpliedQuote = helper.ImpliedQuote(bumpedResult.CurveGroup, request.ValuationDate, request.ReferenceData);
                double baseImpliedQuote = baseRepricing[row].ImpliedQuote;
                values[row, column] = (bumpedImpliedQuote - baseImpliedQuote) / (double)JacobianBump;
            }
        }

        return new CalibrationJacobian(rowLabels, columnLabels, values);
    }

    private static CurveCalibrationInput BumpRate(CurveCalibrationInput input, int curveIndex, int nodeIndex, decimal bump)
    {
        List<CurveCalibrationSpec> bumpedCurves = new(input.Curves.Count);
        for (int c = 0; c < input.Curves.Count; c++)
        {
            CurveCalibrationSpec spec = input.Curves[c];
            if (c != curveIndex)
            {
                bumpedCurves.Add(spec);
                continue;
            }

            // Need to sort nodes the same way the calibrator does, then bump by sorted index.
            IReadOnlyList<ResolvedNode> sortedNodes = NodeSorter.Sort(spec.Nodes, input.ValuationDate);
            List<ResolvedNode> bumpedNodes = new(sortedNodes.Count);
            for (int n = 0; n < sortedNodes.Count; n++)
            {
                ResolvedNode node = sortedNodes[n];
                bumpedNodes.Add(n == nodeIndex ? node with { Rate = node.Rate + bump } : node);
            }

            bumpedCurves.Add(spec with { Nodes = bumpedNodes });
        }

        return input with { Curves = bumpedCurves };
    }

    private static AbstractInstrumentHelper CreateHelper(ResolvedNode node, double quote)
    {
        return node.InstrumentType switch
        {
            "Deposit" => new DepositInstrumentHelper(node, quote),
            "Ois" => new OisInstrumentHelper(node, quote),
            "FixedFloatSwap" => new FixedFloatSwapInstrumentHelper(node, quote),
            "Fra" => new FraInstrumentHelper(node, quote),
            "OisFuture" => new OisFutureInstrumentHelper(node, quote),
            "BasisSwap" => new BasisSwapInstrumentHelper(node, quote),
            _ => throw new NotSupportedException($"Unsupported instrument type: {node.InstrumentType}.")
        };
    }

    private static ICurve BuildCurve(CurveCalibrationSpec spec, DateOnly valuationDate, IReadOnlyList<CurvePoint> points)
    {
        IDiscountCurve discountCurve = new InterpolatedDiscountCurve(
            spec.CurveName,
            valuationDate,
            spec.CurveReference.Currency,
            points,
            spec.Interpolation);

        if (spec.Jumps is { Count: > 0 })
        {
            discountCurve = new JumpAdjustedDiscountCurve(discountCurve, spec.Jumps);
        }

        return spec.CurveReference.Role switch
        {
            CurveRole.Discount or CurveRole.Collateral or CurveRole.Borrow or CurveRole.Dividend => discountCurve,
            CurveRole.Forward when spec.CurveReference.Benchmark is not null => new Boutquin.Curves.Core.Forwards.ForwardCurveFromDiscountCurves(
                spec.CurveName,
                spec.CurveReference.Benchmark.Value,
                discountCurve,
                discountCurve),
            _ => discountCurve
        };
    }
}
