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
using Boutquin.Curves.Abstractions.ReferenceData;

namespace Boutquin.Curves.Bootstrap.Helpers;

/// <summary>
/// Provides shared metadata and contract methods for bootstrap instrument helpers.
/// </summary>
/// <remarks>
/// Base class for instrument-specific bootstrap solvers. Each concrete helper knows how to solve one
/// type of instrument for its implied discount factor (<see cref="SolveNodeValue"/>) and how to
/// reprice the instrument from a calibrated curve (<see cref="ImpliedQuote"/>). The
/// <c>PiecewiseBootstrapCalibrator</c> creates the appropriate helper for each node definition
/// during sequential calibration, calling <see cref="SolveNodeValue"/> to extend the curve one
/// node at a time. After the full curve is built, <see cref="ImpliedQuote"/> is called on each
/// helper to produce repricing diagnostics that verify calibration accuracy.
/// </remarks>
public abstract class AbstractInstrumentHelper : IInstrumentHelper
{
    /// <summary>
    /// Initializes shared helper metadata used by concrete bootstrap instruments.
    /// </summary>
    /// <param name="label">Display label for diagnostics and calibration traces.</param>
    /// <param name="targetCurve">Curve reference that this helper contributes to.</param>
    /// <param name="quoteValue">Observed market quote associated with the helper.</param>
    /// <param name="instrumentType">Instrument-type label for diagnostics grouping.</param>
    protected AbstractInstrumentHelper(string label, CurveReference targetCurve, double quoteValue, string instrumentType)
    {
        Label = label;
        TargetCurve = targetCurve;
        QuoteValue = quoteValue;
        InstrumentType = instrumentType;
    }

    /// <summary>Display label identifying this instrument in calibration traces and diagnostics output.</summary>
    public string Label { get; }

    /// <summary>Curve reference (role + currency) that this helper contributes a solved node to during bootstrap.</summary>
    public CurveReference TargetCurve { get; }

    /// <summary>Observed market quote value used as the calibration target for this instrument.</summary>
    public double QuoteValue { get; }

    /// <summary>Instrument-type label (e.g. Deposit, Ois, FixedFloatSwap) used for diagnostics grouping and repricing reporting.</summary>
    public string InstrumentType { get; }

    /// <summary>
    /// Resolves the pillar date associated with the helper instrument.
    /// </summary>
    /// <param name="valuationDate">Curve valuation date.</param>
    /// <param name="referenceData">Reference data used to resolve calendars, benchmarks, and conventions.</param>
    /// <returns>Resolved pillar date for calibration ordering and node placement.</returns>
    public abstract DateOnly PillarDate(DateOnly valuationDate, IReferenceDataProvider referenceData);

    /// <summary>
    /// Returns the quote implied by the provided curve group.
    /// </summary>
    /// <param name="curveGroup">Current curve snapshot used for repricing.</param>
    /// <param name="valuationDate">Valuation date for the implied quote calculation.</param>
    /// <param name="referenceData">Reference data used by instrument conventions and calendars.</param>
    /// <returns>The model-implied quote value.</returns>
    /// <remarks>
    /// Default implementation echoes the observed quote, which is useful for helpers that do not yet
    /// implement repricing logic. Concrete helpers should override when instrument repricing is available.
    /// </remarks>
    public virtual double ImpliedQuote(ICurveGroup curveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        return QuoteValue;
    }

    /// <summary>
    /// Solves the curve-node value implied by this helper instrument.
    /// </summary>
    /// <param name="partialCurveGroup">Partially solved curve set available to the helper during calibration.</param>
    /// <param name="valuationDate">Curve valuation date.</param>
    /// <param name="referenceData">Reference data used to resolve calendars, benchmarks, and conventions.</param>
    /// <returns>Solved node value consistent with the helper quote and conventions.</returns>
    public abstract double SolveNodeValue(ICurveGroup partialCurveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData);

    /// <summary>
    /// Resolves the discount factor at <paramref name="date"/> from the target curve when available.
    /// </summary>
    /// <param name="curveGroup">Curve container potentially containing the target discount curve.</param>
    /// <param name="date">Date at which to evaluate the discount factor.</param>
    /// <returns>Resolved discount factor, or <see langword="null"/> when no discount curve is available.</returns>
    /// <remarks>
    /// Returning <see langword="null"/> instead of throwing allows calibration diagnostics to continue and
    /// report missing-curve conditions without hard-failing the entire run.
    /// </remarks>
    protected double? TryGetDiscountFactor(ICurveGroup curveGroup, DateOnly date)
    {
        if (!curveGroup.TryGetCurve(TargetCurve, out ICurve? curve))
        {
            return null;
        }

        if (curve is not IDiscountCurve discountCurve)
        {
            return null;
        }

        return discountCurve.DiscountFactor(date);
    }
}
