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

using NumericsLinear = Boutquin.Numerics.Interpolation.LinearInterpolator<double>;

namespace Boutquin.Curves.Interpolation;

/// <summary>
/// Interpolates discount factors by linearly interpolating zero rates between curve nodes.
/// </summary>
/// <remarks>
/// This interpolator converts node discount factors to zero rates, interpolates rates in time,
/// then maps back to discount factors. It is often used when curve designers prefer direct shape
/// control in zero-rate space rather than in log-discount-factor space.
///
/// Linear zero-rate interpolation produces straight-line zero curves, which is visually intuitive
/// but implies discontinuous instantaneous forward rates at every node. If your hedging strategy
/// depends on forward-rate smoothness (e.g., swaption pricing or forward-starting swap valuation),
/// these jumps create artificial P&amp;L sensitivity at node boundaries. Traders hedging a
/// forward-starting swap will see risk concentrated at the nearest pillar dates rather than
/// distributed across the accrual period, which makes delta-hedging less stable. Prefer
/// <see cref="LogLinearDiscountFactorInterpolator"/> for desk-level production curves where
/// forward-rate continuity within segments matters for hedge attribution.
/// </remarks>
/// <example>
/// <code>
/// var settings = new InterpolationSettings(
///     InterpolatorKind.LinearZeroRate, "FlatZero", "FlatForward");
/// var curve = new InterpolatedDiscountCurve(
///     name, valuationDate, currency, points, settings);
/// // curve interpolates by linearly blending zero rates between nodes
/// </code>
/// </example>
public sealed class LinearZeroRateInterpolator : INodalCurveInterpolator
{
    /// <summary>
    /// Interpolator name used for diagnostics and configuration lookup.
    /// </summary>
    public string Name => nameof(LinearZeroRateInterpolator);

    /// <summary>
    /// Computes an interpolated discount factor for the requested date.
    /// </summary>
    /// <param name="valuationDate">Curve valuation date.</param>
    /// <param name="targetDate">Date where the discount factor is evaluated.</param>
    /// <param name="points">Sorted curve nodes expressed as discount factors.</param>
    /// <param name="dayCount">Day-count convention used to compute times in years.</param>
    /// <returns>Interpolated discount factor at <paramref name="targetDate"/>.</returns>
    /// <remarks>
    /// Uses $z(t)=-\ln P(t)/t$ at node times and linearly interpolates $z(t)$ over time. For targets
    /// before the first node, the first-node zero rate is held constant. For targets beyond the last
    /// node, this implementation returns the last node discount factor as a compatibility behavior.
    ///
    /// Derivation: given node discount factors $P_i$ at times $t_i$, the zero rate at each node is
    /// $z_i = -\ln P_i / t_i$. For target time $t$ between nodes $i$ and $i+1$, the interpolated zero rate
    /// is $z(t) = z_i + (z_{i+1} - z_i) \cdot (t - t_i) / (t_{i+1} - t_i)$. The discount factor is then
    /// $P(t) = e^{-z(t) \cdot t}$. This is linear in zero-rate space but nonlinear in discount-factor space.
    /// </remarks>
    /// <exception cref="ArgumentException">A node is null, its discount factor is not finite and positive, or node dates are not strictly ascending.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="points"/> or <paramref name="dayCount"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="points"/> is empty, or when any node date in
    /// <paramref name="points"/> is at or before <paramref name="valuationDate"/>.
    /// </exception>
    public double Interpolate(DateOnly valuationDate, DateOnly targetDate, IReadOnlyList<CurvePoint> points, IYearFractionCalculator dayCount)
    {
        InterpolationInputValidator.Validate(points, dayCount);

        // Guard: a node at or before the valuation date produces xs[i] <= 0, where the zero-rate
        // transform z_i = -ln(P_i)/t_i is singular. The legacy Math.Max(1e-12, xs[i]) floor masked
        // this by substituting a ~1e10 zero rate, which then underflowed exp(-z·t) to a silent DF=0
        // across the entire interior (an arbitrage-violating discount factor). Reject the node with a
        // clear domain message naming the offending node date instead of returning corrupt output.
        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].Date <= valuationDate)
            {
                throw new ArgumentException(
                    $"All node dates must be strictly after the valuation date ({valuationDate:yyyy-MM-dd}). " +
                    $"Node {i} has date {points[i].Date:yyyy-MM-dd}, which is at or before the valuation date.",
                    nameof(points));
            }
        }

        if (targetDate <= valuationDate)
        {
            return 1d;
        }

        // Preserve the right-edge shortcut: Curves returns the last node DF directly rather
        // than exp(-z_last * t_target), which diverges past the last pillar.
        if (targetDate >= points[^1].Date)
        {
            return points[^1].Value;
        }

        int n = points.Count;
        Span<double> xs = n <= 128 ? stackalloc double[n] : new double[n];
        Span<double> ys = n <= 128 ? stackalloc double[n] : new double[n];
        for (int i = 0; i < n; i++)
        {
            xs[i] = dayCount.YearFraction(valuationDate, points[i].Date);
            ys[i] = -Math.Log(points[i].Value) / Math.Max(1e-12, xs[i]);
        }

        double x = dayCount.YearFraction(valuationDate, targetDate);
        double z = NumericsLinear.Instance.Interpolate(x, xs, ys);
        return Math.Exp(-z * x);
    }
}
