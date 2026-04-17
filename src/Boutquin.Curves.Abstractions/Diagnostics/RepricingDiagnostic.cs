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

namespace Boutquin.Curves.Abstractions.Diagnostics;

/// <summary>
/// Captures repricing quality for a calibrated instrument node.
/// </summary>
/// <remarks>
/// After bootstrap calibration, each instrument's market quote is compared to the
/// quote implied by the calibrated curve. Small absolute errors — typically below
/// 1e-8 — confirm that the curve correctly reproduces the input instrument. Larger
/// residuals indicate that the instrument could not be exactly fitted, which may
/// signal data-quality issues (stale or erroneous quotes), convention mismatches
/// between the instrument definition and the curve's day-count or compounding
/// rules, or proxy substitution where an approximate instrument replaces a missing
/// direct quote. The <see cref="SignedError"/> preserves directionality, helping
/// distinguish systematic bias from random noise, while <see cref="WarningFlags"/>
/// carries additional context such as proxy-usage indicators or stale-data markers.
/// </remarks>
/// <param name="Label">Instrument or node label used in reports.</param>
/// <param name="TargetCurve">Curve reference associated with the repriced instrument.</param>
/// <param name="PillarDate">Resolved pillar date used for calibration and repricing.</param>
/// <param name="MarketQuote">Observed market quote used as calibration target.</param>
/// <param name="ImpliedQuote">Quote implied by the calibrated curve state.</param>
/// <param name="AbsoluteError">Absolute repricing error, typically <c>|MarketQuote - ImpliedQuote|</c>.</param>
/// <param name="SignedError">Signed repricing error, preserving directionality.</param>
/// <param name="InstrumentType">Instrument classification used by helper logic.</param>
/// <param name="WarningFlags">Optional warning flags indicating degraded data or fit conditions.</param>
public sealed record RepricingDiagnostic(
    string Label,
    CurveReference TargetCurve,
    DateOnly PillarDate,
    double MarketQuote,
    double ImpliedQuote,
    double AbsoluteError,
    double SignedError,
    string InstrumentType,
    IReadOnlyList<string>? WarningFlags = null);
