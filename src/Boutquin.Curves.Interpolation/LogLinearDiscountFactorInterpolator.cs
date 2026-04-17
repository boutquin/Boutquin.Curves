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
using Boutquin.MarketData.DayCount;

using NumericsLogLinear = Boutquin.Numerics.Interpolation.LogLinearInterpolator;

namespace Boutquin.Curves.Interpolation;

/// <summary>
/// Interpolates discount factors by linear interpolation in log-discount-factor space.
/// </summary>
/// <remarks>
/// Log-linear interpolation is a common market default because linearity in $\ln P(t)$ preserves
/// positive discount factors and implies piecewise-constant instantaneous forwards between pillars.
/// That makes calibration behavior stable and easy to reason about for risk attribution.
///
/// Log-linear interpolation in discount-factor space is equivalent to piecewise-constant
/// instantaneous forward rates between nodes. This is the standard desk choice because it
/// preserves positive discount factors unconditionally and distributes hedge sensitivity evenly
/// within each segment. The trade-off is that forward rates jump discretely at node boundaries,
/// which can matter for products sensitive to forward-rate term structure shape. If you are
/// pricing swaptions or CMS products where the forward-rate smile between two pillars drives
/// valuation, consider cubic spline methods that produce continuous forwards at the cost of
/// potential negative forward rates in pathological calibration scenarios.
/// </remarks>
/// <example>
/// <code>
/// var settings = new InterpolationSettings(
///     InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward");
/// var curve = new InterpolatedDiscountCurve(
///     name, valuationDate, currency, points, settings);
/// // curve interpolates by log-linear blending in discount-factor space
/// </code>
/// </example>
public sealed class LogLinearDiscountFactorInterpolator : INodalCurveInterpolator
{
    /// <summary>
    /// Interpolator name used for diagnostics and configuration lookup.
    /// </summary>
    public string Name => nameof(LogLinearDiscountFactorInterpolator);

    /// <summary>
    /// Computes an interpolated discount factor for the requested date.
    /// </summary>
    /// <param name="valuationDate">Curve valuation date.</param>
    /// <param name="targetDate">Date where the discount factor is evaluated.</param>
    /// <param name="points">Sorted curve nodes expressed as discount factors.</param>
    /// <param name="dayCount">Day-count convention used to compute times in years.</param>
    /// <returns>Interpolated discount factor at <paramref name="targetDate"/>.</returns>
    /// <remarks>
    /// For interior points, computes a weighted average of neighboring log discount factors and then
    /// exponentiates back to level space. Boundary behavior is intentionally simple in this component:
    /// first and last node values are returned outside the interior segment.
    ///
    /// Derivation: linear interpolation in $\ln P$ space means
    /// $\ln P(t) = \ln P_i + (\ln P_{i+1} - \ln P_i) \cdot (t - t_i) / (t_{i+1} - t_i)$, which implies
    /// $P(t) = P_i^{1-w} \cdot P_{i+1}^{w}$ where $w = (t - t_i) / (t_{i+1} - t_i)$. This is equivalent
    /// to piecewise-constant instantaneous forward rates between nodes:
    /// $f(t) = -[\ln P_{i+1} - \ln P_i] / (t_{i+1} - t_i)$ for $t \in [t_i, t_{i+1})$.
    /// </remarks>
    public double Interpolate(DateOnly valuationDate, DateOnly targetDate, IReadOnlyList<CurvePoint> points, IYearFractionCalculator dayCount)
    {
        if (targetDate <= valuationDate)
        {
            return 1d;
        }

        int n = points.Count;
        Span<double> xs = n <= 128 ? stackalloc double[n] : new double[n];
        Span<double> ys = n <= 128 ? stackalloc double[n] : new double[n];
        for (int i = 0; i < n; i++)
        {
            xs[i] = dayCount.YearFraction(valuationDate, points[i].Date);
            ys[i] = points[i].Value;
        }

        double x = dayCount.YearFraction(valuationDate, targetDate);
        return NumericsLogLinear.Instance.Interpolate(x, xs, ys);
    }
}
