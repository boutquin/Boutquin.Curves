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
/// <param name="CurveId">Unique identifier for this curve within the recipe group.</param>
/// <param name="CurveReference">Typed curve reference identifying the role, currency, and optional benchmark.</param>
/// <param name="ValueType">Whether the curve stores discount factors, zero rates, or forward rates.</param>
/// <param name="DayCountCode">Day-count convention code applied during calibration (e.g., "ACT/360").</param>
/// <param name="Interpolation">Interpolation and extrapolation settings for the calibrated curve.</param>
/// <param name="Nodes">Ordered list of calibration node specifications that define the curve's term structure.</param>
public sealed record CurveRecipe(
    string CurveId,
    CurveReference CurveReference,
    CurveValueType ValueType,
    string DayCountCode,
    InterpolationSettings Interpolation,
    IReadOnlyList<ICurveNodeSpec> Nodes);

/// <summary>
/// Groups multiple <see cref="CurveRecipe"/> instances that must be calibrated together.
/// </summary>
/// <remarks>
/// Multi-curve calibration (e.g., simultaneous SOFR discount and forward curves) requires
/// that all constituent curves share a single valuation date and are bootstrapped in a
/// coordinated pass. The group name serves as the logical identifier for the entire
/// calibration bundle in diagnostics and snapshot storage.
/// </remarks>
/// <param name="GroupName">Logical name for the calibration group (e.g., "USD-SOFR").</param>
/// <param name="Curves">Ordered list of curve recipes in the group.</param>
public sealed record CurveGroupRecipe(
    string GroupName,
    IReadOnlyList<CurveRecipe> Curves);
