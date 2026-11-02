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
/// Model-free bond-yield convexity adjustment based on Hull equation 30.1.
/// </summary>
/// <remarks>
/// <para>
/// When a bond futures contract is used to imply a forward yield, the daily
/// mark-to-market settlement of the futures creates a bias relative to the
/// equivalent OTC forward. This adjustment corrects for that bias using the
/// bond price function's second-order sensitivity to yield changes.
/// </para>
/// <para>
/// Hull equation 30.1:
/// <code>
/// CA = −½ · (G″(y_F) / G′(y_F)) · σ²_yF · T
/// </code>
/// where:
/// <list type="bullet">
///   <item><description>G(y) = bond price as a function of yield (e.g. annuity + principal)</description></item>
///   <item><description>y_F = forward yield</description></item>
///   <item><description>σ_yF = forward yield volatility (annual, decimal)</description></item>
///   <item><description>T = time to observation (year fraction from valuation date)</description></item>
/// </list>
/// The ratio G″/G′ is a yield-convexity measure specific to the bond's payment structure
/// and differs from the modified duration / convexity seen in cashflow-DV01 calculations.
/// </para>
/// </remarks>
public sealed class BondYieldConvexityAdjustment : IConvexityAdjustment
{
    private readonly double _forwardYield;
    private readonly double _forwardYieldVolatility;
    private readonly double _timeToObservation;
    private readonly Func<double, double> _bondPrice;
    private readonly Func<double, double> _bondPriceFirst;
    private readonly Func<double, double> _bondPriceSecond;

    /// <summary>
    /// Initializes a new instance of the <see cref="BondYieldConvexityAdjustment"/> type.
    /// </summary>
    /// <param name="forwardYield">Forward yield y_F at which the bond functions are evaluated.</param>
    /// <param name="forwardYieldVolatility">Annual forward yield volatility σ_yF (decimal, e.g. 0.01 for 1%).</param>
    /// <param name="timeToObservation">Year fraction T from valuation to observation date.</param>
    /// <param name="bondPrice">G(y) — bond price as a function of yield.</param>
    /// <param name="bondPriceFirst">G′(y) — first derivative of bond price with respect to yield.</param>
    /// <param name="bondPriceSecond">G″(y) — second derivative of bond price with respect to yield.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the function delegates is <c>null</c>.</exception>
    public BondYieldConvexityAdjustment(
        double forwardYield,
        double forwardYieldVolatility,
        double timeToObservation,
        Func<double, double> bondPrice,
        Func<double, double> bondPriceFirst,
        Func<double, double> bondPriceSecond)
    {
        ArgumentNullException.ThrowIfNull(bondPrice);
        ArgumentNullException.ThrowIfNull(bondPriceFirst);
        ArgumentNullException.ThrowIfNull(bondPriceSecond);

        _forwardYield = forwardYield;
        _forwardYieldVolatility = forwardYieldVolatility;
        _timeToObservation = timeToObservation;
        _bondPrice = bondPrice;
        _bondPriceFirst = bondPriceFirst;
        _bondPriceSecond = bondPriceSecond;
    }

    /// <summary>
    /// Computes the bond-yield convexity adjustment using Hull equation 30.1.
    /// </summary>
    /// <param name="timeToExpiry">
    /// Year fraction to futures contract expiry (accepted for interface compliance; not used
    /// because bond maturity is implicit in the G function delegates).
    /// </param>
    /// <param name="timeToMaturity">
    /// Year fraction to contract maturity / accrual end (accepted for interface compliance; not used).
    /// </param>
    /// <returns>
    /// Convexity adjustment in decimal form. Subtract from the bond-futures-implied yield to
    /// obtain the equivalent OIS-consistent forward yield. The sign follows the sign of
    /// <c>G″(y_F) / G′(y_F)</c> for the supplied bond-price function; for a typical bond (price
    /// convex and decreasing in yield) this is negative, making the adjustment positive, but the
    /// sign is not enforced by this implementation for an arbitrary caller-supplied G.
    /// </returns>
    public double Adjustment(double timeToExpiry, double timeToMaturity)
    {
        // CA = −½ · (G″(y_F) / G′(y_F)) · σ²_yF · T
        double gPrime = _bondPriceFirst(_forwardYield);
        double gDoublePrime = _bondPriceSecond(_forwardYield);

        double adjustment = -0.5 * (gDoublePrime / gPrime) * _forwardYieldVolatility * _forwardYieldVolatility * _timeToObservation;

        // A degenerate user-supplied G′(y_F) == 0 makes G″/G′ non-finite (NaN when G″ is also 0).
        // A NaN adjustment escapes the downstream InterpolatedDiscountCurve positivity guard
        // (NaN <= 0d is false) and silently corrupts the calibrated discount factor, so fail loud
        // at the source rather than return a non-finite value.
        if (!double.IsFinite(adjustment))
        {
            throw new InvalidOperationException(
                $"Bond-yield convexity adjustment is non-finite ({adjustment:G6}) at forward yield {_forwardYield:G6}: " +
                $"G′(y_F) = {gPrime:G6} is zero or yields a non-finite G″/G′ ratio.");
        }

        return adjustment;
    }
}
