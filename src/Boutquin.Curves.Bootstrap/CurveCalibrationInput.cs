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
public sealed record CurveCalibrationInput(
    DateOnly ValuationDate,
    IReadOnlyList<CurveCalibrationSpec> Curves,
    IReferenceDataProvider ReferenceData,
    bool SkipValidation = false);

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
/// <param name="ValueType">Whether the curve stores discount factors, zero rates, or forward rates.</param>
/// <param name="DayCountCode">Day-count convention code applied during calibration (e.g., "ACT/360").</param>
/// <param name="Interpolation">Interpolation and extrapolation settings for the calibrated curve.</param>
/// <param name="Nodes">Ordered list of resolved calibration nodes with pre-extracted rates.</param>
/// <param name="Jumps">Optional deterministic jump adjustments applied on configured dates for event handling.</param>
public sealed record CurveCalibrationSpec(
    CurveName CurveName,
    CurveReference CurveReference,
    CurveValueType ValueType,
    string DayCountCode,
    InterpolationSettings Interpolation,
    IReadOnlyList<ResolvedNode> Nodes,
    IReadOnlyList<CurvePoint>? Jumps = null);

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
public sealed record ResolvedNode(
    string Label,
    Tenor Tenor,
    string InstrumentType,
    string ConventionCode,
    CurveReference TargetCurve,
    decimal Rate);
