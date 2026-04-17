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
/// $$\text{CA}(T_1, T_2) = \frac{1}{2}\sigma^2 B(T_1, T_2) \left[B(0, T_1)\frac{1 - e^{-2aT_1}}{2a} + B(T_1, T_2)\right]$$
///
/// where $B(t, T) = \frac{1 - e^{-a(T-t)}}{a}$ and $T_1$ is the futures expiry, $T_2$ is the
/// maturity (end of the accrual period).
///
/// When mean reversion $a \to 0$, the formula degenerates to the Ho-Lee limit:
/// $\text{CA} = \frac{1}{2}\sigma^2 T_1 (T_2 - T_1)$. This implementation guards against
/// numerical instability for very small $a$ by switching to the Ho-Lee formula below a threshold.
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="meanReversion"/> is negative or <paramref name="volatility"/> is non-positive.</exception>
    public HullWhiteConvexityAdjustment(double meanReversion, double volatility)
    {
        if (meanReversion < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(meanReversion), "Mean reversion must be non-negative.");
        }

        if (volatility <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(volatility), "Volatility must be strictly positive.");
        }

        _meanReversion = meanReversion;
        _volatility = volatility;
    }

    /// <inheritdoc />
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
        double bExpiry = B(0d, timeToExpiry, a);
        double bAccrual = B(timeToExpiry, timeToMaturity, a);
        double factor = (1d - Math.Exp(-2d * a * timeToExpiry)) / (2d * a);

        return 0.5 * sigma2 * bAccrual * (bExpiry * factor + bAccrual);
    }

    private static double B(double t, double T, double a)
    {
        return (1d - Math.Exp(-a * (T - t))) / a;
    }
}
