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

namespace Boutquin.Curves.Interpolation;

/// <summary>
/// Identifies the interpolation algorithm used between curve nodes.
/// </summary>
/// <remarks>
/// Interpolation choice is a model-governance control, not only an implementation detail.
/// Different choices redistribute curvature and risk between pillars, which can affect hedging and
/// PnL explain. Store this value explicitly alongside curve definitions and calibration diagnostics.
/// </remarks>
public enum InterpolatorKind
{
    /// <summary>
    /// Linear interpolation on log discount factors.
    /// </summary>
    /// <remarks>
    /// Common production default because it preserves positive discount factors and implies piecewise
    /// constant instantaneous forwards between nodes.
    /// </remarks>
    LogLinearDiscountFactor = 0,

    /// <summary>
    /// Linear interpolation on zero rates.
    /// </summary>
    /// <remarks>
    /// Often chosen for intuitive curve-shape control in quote space, though it can imply non-flat
    /// forwards inside segments and may be more sensitive to sparse long-end nodes.
    /// </remarks>
    LinearZeroRate = 1,

    /// <summary>
    /// Piecewise-flat forward-rate interpolation.
    /// </summary>
    /// <remarks>
    /// Targets stable forward segments that can align with desk intuition for bucketed risk.
    /// </remarks>
    FlatForward = 2,

    /// <summary>
    /// Monotone cubic interpolation constrained to avoid oscillations.
    /// </summary>
    /// <remarks>
    /// Intended to provide smoother first derivatives while limiting overshoot versus unconstrained cubic splines.
    /// </remarks>
    MonotoneCubic = 3,

    /// <summary>
    /// Monotone-convex interpolation tailored for yield-curve shape stability.
    /// </summary>
    /// <remarks>
    /// Designed for term-structure applications where positivity, monotonicity, and convexity discipline
    /// matter for scenario analysis and risk attribution.
    /// </remarks>
    MonotoneConvex = 4
}
