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

using Boutquin.MarketData.Conventions;

namespace Boutquin.Curves.Core.Internal;

/// <summary>
/// Discount-factor, zero-rate, and compounding conversion utilities for curve implementations.
/// </summary>
/// <remarks>
/// Static methods for converting between discount factors and zero rates across the continuous,
/// simple, and periodic compounding families. All formulas assume time expressed in year-fraction
/// units and rates in decimal form (e.g., 0.05 for 5%). These conversions are the numerical
/// foundation shared by interpolated discount curves, bootstrap solvers, and risk analytics.
///
/// The conversion formulas between discount factors and zero rates depend on compounding convention.
/// For discount factor $P$ at time $t$: continuous compounding gives $r = -\ln P / t$ and $P = e^{-rt}$;
/// simple compounding gives $r = (1/P - 1) / t$ and $P = 1 / (1 + rt)$; periodic compounding with $n$
/// periods per year gives $r = n[(1/P)^{1/(nt)} - 1]$ and $P = (1 + r/n)^{-nt}$.
///
/// Time-horizon contract: the conversion methods distinguish two regimes at the boundary. A
/// <em>negative</em> year fraction is treated as a data error (a target date preceding valuation)
/// and throws <see cref="ArgumentOutOfRangeException"/>, forcing the caller to fix the upstream
/// time calculation rather than letting a silently-zeroed rate propagate downstream. A
/// <em>zero</em> year fraction is a legitimate degenerate case (target equals valuation) and
/// returns the mathematical convention at the boundary: a zero rate from
/// <see cref="ZeroRateFromDiscountFactor"/> (since the rate limit is undefined and zero is the
/// conventional choice), and a unit discount factor from <see cref="DiscountFactorFromContinuousZero"/>
/// (since $P(0) = e^{-r \cdot 0} = 1$ for any finite rate).
/// </remarks>
internal static class CurveMath
{
    /// <summary>
    /// Converts a discount factor to a zero rate under the requested compounding convention.
    /// </summary>
    /// <param name="discountFactor">Discount factor at maturity. Must be strictly positive.</param>
    /// <param name="timeInYears">Year-fraction horizon between valuation and maturity. Must be non-negative.</param>
    /// <param name="compounding">Compounding basis for the returned zero rate.</param>
    /// <returns>Zero rate consistent with the provided inputs. Returns zero when <paramref name="timeInYears"/> is exactly zero.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="timeInYears"/> is negative (the target date precedes valuation, which is a
    /// caller-side data error) or when <paramref name="discountFactor"/> is non-positive (no-arbitrage violation).
    /// </exception>
    public static double ZeroRateFromDiscountFactor(double discountFactor, double timeInYears, CompoundingConvention compounding)
    {
        if (timeInYears < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(timeInYears), timeInYears, "Time in years must be non-negative — a negative year fraction indicates a target date preceding valuation.");
        }

        if (timeInYears == 0d)
        {
            // Boundary convention: zero-rate limit at t=0 is undefined; return zero.
            return 0d;
        }

        if (discountFactor <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(discountFactor), "Discount factor must be strictly positive.");
        }

        return compounding switch
        {
            CompoundingConvention.Continuous => -Math.Log(discountFactor) / timeInYears,
            CompoundingConvention.Simple => (1d / discountFactor - 1d) / timeInYears,
            CompoundingConvention.Annual => Compound(discountFactor, timeInYears, 1d),
            CompoundingConvention.SemiAnnual => Compound(discountFactor, timeInYears, 2d),
            CompoundingConvention.Quarterly => Compound(discountFactor, timeInYears, 4d),
            CompoundingConvention.Monthly => Compound(discountFactor, timeInYears, 12d),
            _ => throw new NotSupportedException($"Unsupported compounding convention: {compounding}.")
        };
    }

    /// <summary>
    /// Converts a continuously compounded zero rate to a discount factor.
    /// </summary>
    /// <param name="rate">Continuously compounded zero rate.</param>
    /// <param name="timeInYears">Year-fraction horizon between valuation and maturity. Must be non-negative.</param>
    /// <returns>Discount factor implied by <paramref name="rate"/> over <paramref name="timeInYears"/>. Returns one when <paramref name="timeInYears"/> is exactly zero.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="timeInYears"/> is negative, which indicates a target date preceding valuation
    /// and is treated as a caller-side data error rather than silently producing a discount factor greater than one.
    /// </exception>
    public static double DiscountFactorFromContinuousZero(double rate, double timeInYears)
    {
        if (timeInYears < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(timeInYears), timeInYears, "Time in years must be non-negative — a negative year fraction indicates a target date preceding valuation.");
        }

        // At t = 0, P = e^{-r·0} = 1 for any finite rate; short-circuit to avoid
        // unnecessary floating-point work at the valuation-date boundary.
        if (timeInYears == 0d)
        {
            return 1d;
        }

        return Math.Exp(-rate * timeInYears);
    }

    private static double Compound(double discountFactor, double timeInYears, double frequency)
    {
        return frequency * (Math.Pow(1d / discountFactor, 1d / (frequency * timeInYears)) - 1d);
    }
}
