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

namespace Boutquin.Curves.Abstractions.Diagnostics;

/// <summary>
/// Represents the calibration Jacobian matrix linking instrument errors to curve-node perturbations.
/// </summary>
/// <remarks>
/// The Jacobian captures how each calibration instrument's implied quote responds
/// to a small bump in each input quote. Rows correspond to instrument labels
/// (matching <see cref="RowLabels"/>), and columns correspond to the quote IDs or
/// node parameters being solved (matching <see cref="ColumnLabels"/>). A
/// well-conditioned, diagonal-dominant matrix indicates clean, independent
/// calibration — each instrument primarily constrains its own node. Significant
/// off-diagonal magnitude reveals cross-instrument sensitivity, meaning a change
/// in one input quote materially affects the implied value of a different
/// instrument. Condition-number estimates above approximately 1e12 suggest
/// ill-conditioning, which can amplify small data errors into large curve
/// distortions and should prompt a review of instrument selection or quote quality.
/// </remarks>
/// <param name="RowLabels">Row labels, typically instrument or node error identifiers.</param>
/// <param name="ColumnLabels">Column labels, typically solved node parameters.</param>
/// <param name="Values">Jacobian values arranged as <c>Rows x Columns</c>.</param>
public sealed record CalibrationJacobian(
    IReadOnlyList<string> RowLabels,
    IReadOnlyList<string> ColumnLabels,
    double[,] Values)
{
    private IReadOnlyList<string> _rowLabels = SnapshotCopies.List(RowLabels, nameof(RowLabels));
    private IReadOnlyList<string> _columnLabels = SnapshotCopies.List(ColumnLabels, nameof(ColumnLabels));
    private double[,] _values = CopyValues(RowLabels, ColumnLabels, Values, nameof(Values));

    /// <summary>Protected instrument-label snapshot in matrix-row order.</summary>
    public IReadOnlyList<string> RowLabels
    {
        get => _rowLabels;
        init => _rowLabels = SnapshotCopies.List(value, nameof(RowLabels));
    }

    /// <summary>Protected parameter-label snapshot in matrix-column order.</summary>
    public IReadOnlyList<string> ColumnLabels
    {
        get => _columnLabels;
        init => _columnLabels = SnapshotCopies.List(value, nameof(ColumnLabels));
    }

    /// <summary>Copy of the Jacobian entries; modifying the returned array does not change this snapshot.</summary>
    /// <remarks>Cache this copy locally before iterating. Constructor dimensions must match the supplied labels.</remarks>
    public double[,] Values
    {
        get => (double[,])_values.Clone();
        init => _values = CopyValues(RowLabels, ColumnLabels, value, nameof(Values));
    }

    private static double[,] CopyValues(IReadOnlyList<string> rows, IReadOnlyList<string> columns, double[,] values, string parameter)
    {
        ArgumentNullException.ThrowIfNull(rows, nameof(RowLabels));
        ArgumentNullException.ThrowIfNull(columns, nameof(ColumnLabels));
        ArgumentNullException.ThrowIfNull(values, parameter);
        if (rows.Count != values.GetLength(0) || columns.Count != values.GetLength(1))
        {
            throw new ArgumentException("Jacobian dimensions must match row and column label counts.", parameter);
        }
        return (double[,])values.Clone();
    }
}
