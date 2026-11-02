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
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Interpolation;
using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Bootstrap;

/// <summary>
/// Top-level input for a curve calibration run, bundling the valuation date,
/// curve specifications, and reference data needed by the bootstrap calibrator.
/// </summary>
/// <remarks>
/// This record replaces raw calibration requests with a fully resolved input:
/// every node already carries its extracted market rate (see <see cref="ResolvedNode"/>),
/// eliminating the need for the calibrator to perform any data fetching.
/// Set <see cref="SkipValidation"/> to <see langword="true"/> only in test harnesses
/// where input consistency has already been verified externally.
/// </remarks>
/// <param name="ValuationDate">As-of date for the calibration run.</param>
/// <param name="Curves">Per-curve specifications with pre-resolved node rates.</param>
/// <param name="ReferenceData">Provider supplying holiday calendars, conventions, and other static data.</param>
/// <param name="SkipValidation">When <see langword="true"/>, bypass pre-calibration consistency checks.</param>
/// <param name="ConvexityAdjustment">
/// Optional model supplying the futures-to-forward convexity correction applied to <c>OisFuture</c>
/// nodes during the bootstrap. When <see langword="null"/> (the default), futures rates are used
/// raw — the backward-compatible behaviour. When supplied, the calibrator threads it into the
/// <see cref="Helpers.OisFutureInstrumentHelper"/> so the forward (and therefore the calibrated
/// discount factor) reflects <c>forward = futures − adjustment</c>. Non-OisFuture instrument
/// helpers ignore this value.
/// </param>
public sealed record CurveCalibrationInput(
    DateOnly ValuationDate,
    IReadOnlyList<CurveCalibrationSpec> Curves,
    IReferenceDataProvider ReferenceData,
    bool SkipValidation = false,
    IConvexityAdjustment? ConvexityAdjustment = null)
{
    private IReadOnlyList<CurveCalibrationSpec> _curves = BootstrapInputSnapshot.Copy(Curves, nameof(Curves));

    /// <summary>Curve specifications captured for this calibration input. The collection is copied on construction and init assignment.</summary>
    public IReadOnlyList<CurveCalibrationSpec> Curves
    {
        get => _curves;
        init => _curves = BootstrapInputSnapshot.Copy(value, nameof(value));
    }
}

/// <summary>
/// Specification for a single curve within a calibration input, including interpolation
/// settings and fully resolved calibration nodes.
/// </summary>
/// <remarks>
/// Each specification is self-contained: it carries the curve identity, conventions,
/// interpolation configuration, and an ordered list of <see cref="ResolvedNode"/> entries
/// whose rates have already been extracted from market data. The calibrator consumes
/// this directly without further data-pipeline interaction.
/// </remarks>
/// <param name="CurveName">Logical name for the curve (e.g., "USD-SOFR-DISC").</param>
/// <param name="CurveReference">Typed curve reference identifying role, currency, and optional benchmark.</param>
/// <param name="ValueType">Requested ordinate representation. The piecewise calibrator accepts only discount-factor storage; rate quotes are available from the calibrated curve.</param>
/// <param name="DayCountCode">Day-count convention code applied during calibration (e.g., "ACT/360").</param>
/// <param name="Interpolation">Interpolation and extrapolation settings for the calibrated curve.</param>
/// <param name="Nodes">
/// Ordered (by ascending pillar date) list of resolved calibration nodes with pre-extracted rates.
/// Must be non-<see langword="null"/> and non-empty. The calibrator sorts nodes by resolved pillar date;
/// an empty list is rejected by validation or curve construction when validation is bypassed.
/// </param>
/// <param name="Jumps">Optional deterministic jump adjustments applied on configured dates for event handling.</param>
public sealed record CurveCalibrationSpec(
    CurveName CurveName,
    CurveReference CurveReference,
    CurveValueType ValueType,
    string DayCountCode,
    InterpolationSettings Interpolation,
    IReadOnlyList<ResolvedNode> Nodes,
    IReadOnlyList<CurvePoint>? Jumps = null)
{
    private IReadOnlyList<ResolvedNode> _nodes = BootstrapInputSnapshot.Copy(Nodes, nameof(Nodes));

    /// <summary>Resolved calibration nodes in their declared order. The collection is copied on construction and init assignment.</summary>
    public IReadOnlyList<ResolvedNode> Nodes
    {
        get => _nodes;
        init => _nodes = BootstrapInputSnapshot.Copy(value, nameof(value));
    }

    private IReadOnlyList<CurvePoint>? _jumps = BootstrapInputSnapshot.CopyOptional(Jumps);

    /// <summary>Optional discrete jump factors; null means no jump overlay. The collection is copied on construction and init assignment.</summary>
    public IReadOnlyList<CurvePoint>? Jumps
    {
        get => _jumps;
        init => _jumps = BootstrapInputSnapshot.CopyOptional(value);
    }
}

/// <summary>
/// A fully resolved calibration node carrying the extracted market rate alongside its metadata.
/// </summary>
/// <remarks>
/// Produced by running a node spec against fetched market data.
/// The <see cref="Rate"/> is the final decimal value consumed by the bootstrap solver;
/// all lookback, tenor-matching, and price-to-rate conversion logic has already been applied.
/// </remarks>
/// <param name="Label">Human-readable label matching the originating node spec.</param>
/// <param name="Tenor">Calibration tenor of the instrument.</param>
/// <param name="InstrumentType">Instrument type consumed by the calibrator's pricing logic.</param>
/// <param name="ConventionCode">Convention code for day-count and payment rules.</param>
/// <param name="TargetCurve">The curve this node calibrates into.</param>
/// <param name="Rate">Pre-extracted market rate ready for calibration.</param>
/// <param name="ProjectionCurves">
/// Optional forward (projection) curve references that supply this node's floating-leg forwards
/// (Fx) during the pillar solve. When <see langword="null"/> or empty, the node is self-discounting:
/// forwards are read from its own <paramref name="TargetCurve"/> using the self-discounting pricing relation.
/// A single-curve forward (FixedFloat/OIS) names 0 or 1 reference; a two-curve basis swap names 2
/// references (Fa then Fb). The semantics of each position are interpreted by the instrument helpers.
/// </param>
public sealed record ResolvedNode(
    string Label,
    Tenor Tenor,
    string InstrumentType,
    string ConventionCode,
    CurveReference TargetCurve,
    decimal Rate,
    IReadOnlyList<CurveReference>? ProjectionCurves = null)
{
    private IReadOnlyList<CurveReference>? _projectionCurves = BootstrapInputSnapshot.CopyOptional(ProjectionCurves);

    /// <summary>Optional projection-curve references; null selects self-discounting. The collection is copied on construction and init assignment.</summary>
    public IReadOnlyList<CurveReference>? ProjectionCurves
    {
        get => _projectionCurves;
        init => _projectionCurves = BootstrapInputSnapshot.CopyOptional(value);
    }
}
