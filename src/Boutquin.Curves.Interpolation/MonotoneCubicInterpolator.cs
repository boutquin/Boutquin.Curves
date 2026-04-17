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

using NumericsMonotoneCubic = Boutquin.Numerics.Interpolation.MonotoneCubicInterpolator;

namespace Boutquin.Curves.Interpolation;

/// <summary>
/// Interpolates discount factors using monotone-preserving cubic Hermite interpolation in
/// log-discount-factor space.
/// </summary>
/// <remarks>
/// Uses the Fritsch-Carlson method to constrain cubic Hermite tangents, preventing oscillations
/// that unconstrained cubic splines exhibit on yield-curve data. The interpolation operates in
/// log-DF space, which preserves positive discount factors by construction and provides smooth
/// instantaneous forward rates suitable for swaption pricing and forward-starting swap valuation.
///
/// For production curves where forward-rate smoothness matters (e.g., swaption vol surface
/// construction, CMS pricing), monotone cubic is the industry standard choice. It eliminates
/// the forward-rate jumps of log-linear interpolation while avoiding the spurious oscillations
/// of unconstrained cubic splines.
///
/// Algorithm: Fritsch and Carlson (1980), "Monotone Piecewise Cubic Interpolation",
/// SIAM J. Numer. Anal. 17(2), pp. 238-246.
/// </remarks>
public sealed class MonotoneCubicInterpolator : INodalCurveInterpolator
{
    /// <summary>
    /// Interpolator name used for diagnostics and configuration lookup.
    /// </summary>
    public string Name => nameof(MonotoneCubicInterpolator);

    /// <summary>
    /// Computes an interpolated discount factor for the requested date using monotone cubic
    /// Hermite interpolation in log-DF space.
    /// </summary>
    /// <param name="valuationDate">Curve valuation date.</param>
    /// <param name="targetDate">Date where the discount factor is evaluated.</param>
    /// <param name="points">Sorted curve nodes expressed as discount factors.</param>
    /// <param name="dayCount">Day-count convention used to compute times in years.</param>
    /// <returns>Interpolated discount factor at <paramref name="targetDate"/>.</returns>
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
            ys[i] = Math.Log(points[i].Value);
        }

        double x = dayCount.YearFraction(valuationDate, targetDate);
        double logDf = NumericsMonotoneCubic.Instance.Interpolate(x, xs, ys);
        return Math.Exp(logDf);
    }
}
