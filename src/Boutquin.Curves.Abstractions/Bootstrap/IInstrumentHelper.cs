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
using Boutquin.Curves.Abstractions.ReferenceData;

namespace Boutquin.Curves.Abstractions.Bootstrap;

/// <summary>
/// Defines pricing and solve operations for a specific calibration instrument node.
/// </summary>
public interface IInstrumentHelper
{
    /// <summary>
    /// Instrument label used in diagnostics.
    /// </summary>
    string Label { get; }

    /// <summary>
    /// Target curve reference solved by this helper during calibration.
    /// </summary>
    CurveReference TargetCurve { get; }

    /// <summary>
    /// Market quote value supplied for this calibration instrument.
    /// </summary>
    double QuoteValue { get; }

    /// <summary>
    /// Resolves the helper pillar date from valuation date and reference data.
    /// </summary>
    /// <param name="valuationDate">Calibration valuation date.</param>
    /// <param name="referenceData">Reference data used for calendar and convention rules.</param>
    /// <returns>Pillar date used by this helper.</returns>
    DateOnly PillarDate(DateOnly valuationDate, IReferenceDataProvider referenceData);

    /// <summary>
    /// Computes the implied quote from a calibrated curve group.
    /// </summary>
    /// <param name="curveGroup">Fully resolved curve group used for repricing.</param>
    /// <param name="valuationDate">Calibration valuation date.</param>
    /// <param name="referenceData">Reference data used by pricing logic.</param>
    /// <returns>Implied quote value for the instrument represented by this helper.</returns>
    double ImpliedQuote(
        ICurveGroup curveGroup,
        DateOnly valuationDate,
        IReferenceDataProvider referenceData);

    /// <summary>
    /// Solves and returns the node value required to fit the market quote.
    /// </summary>
    /// <param name="partialCurveGroup">Partially built curve group available at the current calibration step.</param>
    /// <param name="valuationDate">Calibration valuation date.</param>
    /// <param name="referenceData">Reference data used by solving logic.</param>
    /// <returns>Solved node value to append to the target curve.</returns>
    double SolveNodeValue(
        ICurveGroup partialCurveGroup,
        DateOnly valuationDate,
        IReferenceDataProvider referenceData);
}
