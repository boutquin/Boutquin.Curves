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

using Boutquin.Curves.Abstractions.Bootstrap;

namespace Boutquin.Curves.Bootstrap.ConvexityAdjustments;

/// <summary>
/// Hull-White model-implied convexity adjustment for interest-rate futures.
/// </summary>
/// <remarks>
/// Under the Hull-White one-factor model with mean reversion $a$ and volatility $\sigma$:
///
/// $$\text{CA}(T_1, T_2) = \frac{1}{2}\sigma^2 B(T_1, T_2) \cdot \frac{1 - e^{-2aT_1}}{2a}$$
///
/// where $B(t, T) = \frac{1 - e^{-a(T-t)}}{a}$ and $T_1$ is the futures expiry, $T_2$ is the
/// maturity (end of the accrual period).
///
/// The coefficient is chosen so the general mean-reverting form is continuous with the Ho-Lee
/// branch: as $a \to 0$, $\frac{1 - e^{-2aT_1}}{2a} \to T_1$ and $B(T_1, T_2) \to (T_2 - T_1)$,
/// so $\text{CA} \to \frac{1}{2}\sigma^2 T_1 (T_2 - T_1)$ — the explicit Ho-Lee limit. This
/// implementation switches to the closed Ho-Lee formula below a small mean-reversion threshold
/// to avoid catastrophic cancellation in the $1/a$ terms for very small $a$.
/// </remarks>
public sealed class HullWhiteConvexityAdjustment : IConvexityAdjustment
{
    private const double MeanReversionFloor = 1e-8;

    private readonly double _meanReversion;
    private readonly double _volatility;

    /// <summary>
    /// Initializes a new instance of the <see cref="HullWhiteConvexityAdjustment"/> type.
    /// </summary>
    /// <param name="meanReversion">Mean reversion speed $a$ of the short rate process.</param>
    /// <param name="volatility">Volatility $\sigma$ of the short rate process.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="meanReversion"/> is negative or non-finite, or <paramref name="volatility"/> is non-positive or non-finite.</exception>
    public HullWhiteConvexityAdjustment(double meanReversion, double volatility)
    {
        if (!double.IsFinite(meanReversion) || meanReversion < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(meanReversion), "Mean reversion must be finite and non-negative.");
        }

        if (!double.IsFinite(volatility) || volatility <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(volatility), "Volatility must be finite and strictly positive.");
        }

        _meanReversion = meanReversion;
        _volatility = volatility;
    }

    /// <summary>Computes the Hull-White futures-to-forward rate adjustment for an expiry and accrual end.</summary>
    /// <param name="timeToExpiry">Year fraction to futures expiry.</param>
    /// <param name="timeToMaturity">Year fraction to the end of the underlying accrual period.</param>
    /// <returns>Zero for a non-positive expiry or an accrual end at or before expiry; otherwise the model adjustment in decimal rates.</returns>
    public double Adjustment(double timeToExpiry, double timeToMaturity)
    {
        if (timeToExpiry <= 0d || timeToMaturity <= timeToExpiry)
        {
            return 0d;
        }

        double sigma2 = _volatility * _volatility;

        if (_meanReversion < MeanReversionFloor)
        {
            // Ho-Lee limit: CA = 0.5 * sigma^2 * T1 * (T2 - T1)
            return 0.5 * sigma2 * timeToExpiry * (timeToMaturity - timeToExpiry);
        }

        double a = _meanReversion;
        double bAccrual = B(timeToExpiry, timeToMaturity, a);

        // (1 - e^{-2 a T1}) / (2a) -> T1 as a -> 0, so 0.5 * sigma^2 * B(T1,T2) * factor
        // converges to the explicit Ho-Lee branch 0.5 * sigma^2 * T1 * (T2 - T1) above,
        // satisfying the a -> 0+ continuity contract (the previous bExpiry*factor + bAccrual
        // bracket converged to T1^2 + (T2 - T1) instead, breaking continuity).
        double factor = (1d - Math.Exp(-2d * a * timeToExpiry)) / (2d * a);

        return 0.5 * sigma2 * bAccrual * factor;
    }

    private static double B(double t, double T, double a)
    {
        return (1d - Math.Exp(-a * (T - t))) / a;
    }
}
