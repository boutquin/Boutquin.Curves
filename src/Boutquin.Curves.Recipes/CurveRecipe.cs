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
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Interpolation;
using Boutquin.Curves.Recipes.Nodes;

namespace Boutquin.Curves.Recipes;

/// <summary>
/// Declarative specification for building and calibrating a single interest-rate curve.
/// </summary>
/// <remarks>
/// A recipe is the bridge between market-data fetching and curve calibration. It captures
/// what data to fetch (via <see cref="Nodes"/>), how to interpolate (via <see cref="Interpolation"/>),
/// and the curve identity and conventions needed to drive the bootstrap calibrator.
/// Recipes are composable: a <see cref="CurveGroupRecipe"/> bundles multiple curves that
/// must be calibrated together (e.g., a SOFR discount curve and an overnight forward curve).
/// </remarks>
public sealed record CurveRecipe
{
    /// <summary>Unique curve identifier within the recipe group.</summary>
    public string CurveId { get; init; }

    /// <summary>Curve role, currency, and optional benchmark used for lookup and calibration.</summary>
    public CurveReference CurveReference { get; init; }

    /// <summary>Requested calibration ordinate; the piecewise calibrator currently supports discount factors.</summary>
    public CurveValueType ValueType { get; init; }

    /// <summary>Time-axis day-count convention, such as "ACT/360".</summary>
    public string DayCountCode { get; init; }

    /// <summary>Interpolation and extrapolation settings for the calibrated curve.</summary>
    public InterpolationSettings Interpolation { get; init; }

    /// <summary>Protected snapshot of the ordered calibration nodes defining this term structure.</summary>
    public IReadOnlyList<ICurveNodeSpec> Nodes
    {
        get => _nodes;
        init => _nodes = SnapshotList.Copy(value, nameof(Nodes));
    }

    private IReadOnlyList<ICurveNodeSpec> _nodes = [];

    /// <summary>
    /// Initialises a <see cref="CurveRecipe"/> with full validation of all identity and data fields.
    /// </summary>
    /// <param name="curveId">Unique identifier for this curve within the recipe group.</param>
    /// <param name="curveReference">Typed curve reference identifying the role, currency, and optional benchmark.</param>
    /// <param name="valueType">Whether the curve stores discount factors, zero rates, or forward rates.</param>
    /// <param name="dayCountCode">Day-count convention code applied during calibration (e.g., "ACT/360").</param>
    /// <param name="interpolation">Interpolation and extrapolation settings for the calibrated curve.</param>
    /// <param name="nodes">Ordered list of calibration node specifications that define the curve's term structure.</param>
    public CurveRecipe(
        string curveId,
        CurveReference curveReference,
        CurveValueType valueType,
        string dayCountCode,
        InterpolationSettings interpolation,
        IReadOnlyList<ICurveNodeSpec> nodes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(curveId);
        ArgumentException.ThrowIfNullOrWhiteSpace(dayCountCode);
        ArgumentNullException.ThrowIfNull(curveReference);
        ArgumentNullException.ThrowIfNull(interpolation);
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count == 0)
        {
            throw new ArgumentException("Nodes must contain at least one node specification.", nameof(nodes));
        }
        CurveId = curveId;
        CurveReference = curveReference;
        ValueType = valueType;
        DayCountCode = dayCountCode;
        Interpolation = interpolation;
        Nodes = nodes;
    }
}

/// <summary>
/// Groups multiple <see cref="CurveRecipe"/> instances that must be calibrated together.
/// </summary>
/// <remarks>
/// Multi-curve calibration (e.g., simultaneous SOFR discount and forward curves) requires
/// that all constituent curves share a single valuation date and are bootstrapped in a
/// coordinated pass. The group name serves as the logical identifier for the entire
/// calibration bundle in diagnostics and snapshot storage.
/// </remarks>
public sealed record CurveGroupRecipe
{
    /// <summary>Logical calibration-group name, such as "USD-SOFR".</summary>
    public string GroupName { get; init; }

    /// <summary>Protected snapshot of curve recipes in calibration order.</summary>
    public IReadOnlyList<CurveRecipe> Curves
    {
        get => _curves;
        init => _curves = SnapshotList.Copy(value, nameof(Curves));
    }

    private IReadOnlyList<CurveRecipe> _curves = [];

    /// <summary>
    /// Initialises a <see cref="CurveGroupRecipe"/> with validation that the curve list is non-null
    /// and non-empty.
    /// </summary>
    /// <param name="groupName">Logical name for the calibration group (e.g., "USD-SOFR").</param>
    /// <param name="curves">Ordered list of curve recipes in the group. Must contain at least one entry.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curves"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="curves"/> is empty.</exception>
    public CurveGroupRecipe(string groupName, IReadOnlyList<CurveRecipe> curves)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupName);
        ArgumentNullException.ThrowIfNull(curves);
        if (curves.Count == 0)
        {
            throw new ArgumentException("Curves must contain at least one recipe.", nameof(curves));
        }
        GroupName = groupName;
        Curves = curves;
    }
}
