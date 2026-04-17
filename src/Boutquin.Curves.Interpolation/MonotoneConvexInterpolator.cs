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

using NumericsMonotoneConvex = Boutquin.Numerics.Interpolation.MonotoneConvexInterpolator;

namespace Boutquin.Curves.Interpolation;

/// <summary>
/// Interpolates discount factors using the Hagan-West (2006) monotone-convex method,
/// which guarantees non-negative instantaneous forward rates across the full curve maturity range.
/// </summary>
/// <remarks>
/// The monotone-convex method constructs a smooth, continuous instantaneous forward rate curve $f(t)$
/// that is non-negative everywhere, making it suitable for use in arbitrage-free pricing models.
/// Unlike log-linear interpolation (which produces piecewise-constant forwards with discontinuous
/// jumps at nodes) or unconstrained cubic splines (which can produce negative forward rates), this
/// method achieves smooth forwards while preserving the non-negativity constraint required by
/// no-arbitrage curve theory.
///
/// Algorithm summary:
/// <list type="number">
///   <item>Augment the node set with a virtual node at $t=0$, $P=1$.</item>
///   <item>Compute per-segment discrete forward rates $F_k = [\ln P_{k-1} - \ln P_k] / h_k$.</item>
///   <item>Estimate tangents $g_i$ at each interior node as a time-weighted average of the
///         adjacent segment forwards; extrapolate to boundary nodes.</item>
///   <item>Clamp each $g_i$ to $[0, \min(2F_k, 2F_{k+1})]$ for non-negativity.</item>
///   <item>Integrate the resulting cubic forward polynomial within each segment to obtain the
///         normalized cumulative return $R(t) = -\ln P(t)$.</item>
/// </list>
///
/// Reference: Hagan, P. S. and West, G. (2006), "Interpolation Methods for Curve Construction",
/// Applied Mathematical Finance 13(2), pp. 89–129.
/// </remarks>
/// <example>
/// <code>
/// var settings = new InterpolationSettings(
///     InterpolatorKind.MonotoneConvex, "FlatZero", "FlatForward");
/// var curve = new InterpolatedDiscountCurve(
///     name, valuationDate, currency, points, settings);
/// // curve produces non-negative instantaneous forward rates throughout
/// </code>
/// </example>
public sealed class MonotoneConvexInterpolator : INodalCurveInterpolator
{
    /// <summary>
    /// Interpolator name used for diagnostics and configuration lookup.
    /// </summary>
    public string Name => nameof(MonotoneConvexInterpolator);

    /// <summary>
    /// Computes an interpolated discount factor for the requested date using the Hagan-West
    /// monotone-convex algorithm.
    /// </summary>
    /// <param name="valuationDate">Curve valuation date.</param>
    /// <param name="targetDate">Date where the discount factor is evaluated.</param>
    /// <param name="points">Sorted curve nodes expressed as discount factors.</param>
    /// <param name="dayCount">Day-count convention used to compute times in years.</param>
    /// <returns>Interpolated discount factor at <paramref name="targetDate"/>.</returns>
    /// <remarks>
    /// The interpolated value satisfies $P(t) = \exp(-R(t))$ where $R(t)$ is the normalized
    /// cumulative return obtained by integrating the cubic instantaneous forward curve. The
    /// algorithm guarantees $f(t) \geq 0$ for all $t$ when all input discount factors are
    /// monotonically decreasing (a standard normal yield-curve assumption).
    ///
    /// Boundary behavior: dates at or before the valuation date return 1; dates before the first
    /// node return the first node value; dates at or after the last node return the last node value
    /// (via flat extrapolation in the kernel).
    ///
    /// This wrapper converts dates to year fractions and discount factors to normalized cumulative
    /// return (NCR) values before delegating to <see cref="NumericsMonotoneConvex"/>, then
    /// converts the returned NCR back to a discount factor via $P = \exp(-\text{NCR})$.
    /// </remarks>
    public double Interpolate(DateOnly valuationDate, DateOnly targetDate, IReadOnlyList<CurvePoint> points, IYearFractionCalculator dayCount)
    {
        if (targetDate <= valuationDate)
        {
            return 1d;
        }

        if (targetDate <= points[0].Date)
        {
            return points[0].Value;
        }

        // Build augmented arrays: xs[0]=0/ys[0]=0 is the virtual origin (P(0)=1 → NCR=0),
        // followed by the actual curve nodes as (year fraction, NCR = -ln(DF)).
        int n = points.Count;
        int N = n + 1;

        double[] xs = new double[N];
        double[] ys = new double[N];
        xs[0] = 0.0;
        ys[0] = 0.0;
        for (int i = 0; i < n; i++)
        {
            xs[i + 1] = dayCount.YearFraction(valuationDate, points[i].Date);
            ys[i + 1] = -Math.Log(points[i].Value);
        }

        double x = dayCount.YearFraction(valuationDate, targetDate);
        double ncr = NumericsMonotoneConvex.Instance.Interpolate(x, xs, ys);
        return Math.Exp(-ncr);
    }
}
