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
/// Defines a calibratable node in a curve definition.
/// </summary>
public interface ICurveNode
{
    /// <summary>
    /// Node label used in diagnostics and reporting.
    /// </summary>
    string Label { get; }

    /// <summary>
    /// Curve reference this node contributes to during calibration.
    /// </summary>
    CurveReference TargetCurve { get; }

    /// <summary>
    /// Resolves the effective node date from valuation date and reference data.
    /// </summary>
    /// <param name="valuationDate">Calibration valuation date.</param>
    /// <param name="referenceData">Reference-data provider used to resolve calendars, tenors, and conventions.</param>
    /// <returns>Resolved node date for calibration.</returns>
    DateOnly ResolveNodeDate(DateOnly valuationDate, IReferenceDataProvider referenceData);
}
