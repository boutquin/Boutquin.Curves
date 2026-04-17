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

using NumericsFlatForward = Boutquin.Numerics.Interpolation.FlatForwardInterpolator;

namespace Boutquin.Curves.Interpolation;

/// <summary>
/// Interpolates discount factors by holding the instantaneous forward rate constant
/// between each adjacent pair of curve nodes (flat-forward interpolation).
/// </summary>
/// <remarks>
/// Between nodes $(t_0, P_0)$ and $(t_1, P_1)$, the constant instantaneous forward rate is
/// $f = [\ln P_0 - \ln P_1] / (t_1 - t_0)$, and the interpolated discount factor satisfies
/// $P(t) = P_0 \cdot \exp(-f \cdot (t - t_0))$.
///
/// In continuous compounding, this formula is mathematically identical to log-linear discount-factor
/// interpolation — both implement piecewise-constant instantaneous forward rates between pillars.
/// The distinction is conceptual: <see cref="LogLinearDiscountFactorInterpolator"/> expresses the
/// formula as weighted interpolation in $\ln P$ space; <c>FlatForwardInterpolator</c> expresses
/// it explicitly in terms of a forward rate, making the forward-rate semantics visible in code.
///
/// The defining property is that the instantaneous forward curve $f(t)$ is a step function that
/// jumps only at node boundaries. This makes hedge attribution and calibration diagnostics
/// straightforward, and is the standard desk choice when forward-rate flatness within segments
/// is an acceptable approximation.
/// </remarks>
/// <example>
/// <code>
/// var settings = new InterpolationSettings(
///     InterpolatorKind.FlatForward, "FlatZero", "FlatForward");
/// var curve = new InterpolatedDiscountCurve(
///     name, valuationDate, currency, points, settings);
/// // curve holds instantaneous forwards constant between nodes
/// </code>
/// </example>
public sealed class FlatForwardInterpolator : INodalCurveInterpolator
{
    /// <summary>
    /// Interpolator name used for diagnostics and configuration lookup.
    /// </summary>
    public string Name => nameof(FlatForwardInterpolator);

    /// <summary>
    /// Computes an interpolated discount factor for the requested date by applying a
    /// piecewise-constant instantaneous forward rate between the bracketing nodes.
    /// </summary>
    /// <param name="valuationDate">Curve valuation date.</param>
    /// <param name="targetDate">Date where the discount factor is evaluated.</param>
    /// <param name="points">Sorted curve nodes expressed as discount factors.</param>
    /// <param name="dayCount">Day-count convention used to compute times in years.</param>
    /// <returns>Interpolated discount factor at <paramref name="targetDate"/>.</returns>
    /// <remarks>
    /// Boundary behavior: dates at or before the valuation date return 1; dates before the first
    /// node return the first node value; dates at or after the last node return the last node value.
    ///
    /// Derivation: setting $f = -\frac{\ln P_1 - \ln P_0}{t_1 - t_0}$ gives
    /// $\ln P(t) = \ln P_0 - f \cdot (t - t_0)$, which is equivalent to linear interpolation in
    /// $\ln P$ space. Both forms produce identical numerical results in floating-point arithmetic.
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

        double t = dayCount.YearFraction(valuationDate, targetDate);
        return NumericsFlatForward.Instance.Interpolate(t, xs, ys);
    }
}
