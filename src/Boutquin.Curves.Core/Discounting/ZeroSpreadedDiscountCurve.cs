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
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Core.Internal;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Core.Discounting;

/// <summary>
/// Applies a constant additive spread to an underlying curve's continuously compounded zero rates.
/// </summary>
/// <remarks>
/// The spreaded zero rate at maturity $t$ is $z_{\text{spread}}(t) = z_{\text{base}}(t) + s$ where $s$
/// is the additive spread in decimal form (e.g., 0.0050 for 50 basis points). The resulting discount
/// factor is $P(t) = e^{-z_{\text{spread}}(t) \cdot t}$.
///
/// Use cases include credit/liquidity overlays, fallback spread construction (e.g., LIBOR-to-SOFR
/// transition spreads), and scenario analysis where a flat spread shift is applied on top of a
/// calibrated curve without re-bootstrapping.
/// </remarks>
public sealed class ZeroSpreadedDiscountCurve : IDiscountCurve
{
    private readonly IDiscountCurve _underlying;
    private readonly double _spread;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZeroSpreadedDiscountCurve"/> type.
    /// </summary>
    /// <param name="name">Curve name for the spreaded curve.</param>
    /// <param name="underlying">Base discount curve to which the spread is applied.</param>
    /// <param name="spread">Additive zero-rate spread in decimal form (e.g., 0.0050 for 50 bp).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="underlying"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="spread"/> is not a finite number (NaN or ±Infinity).</exception>
    public ZeroSpreadedDiscountCurve(CurveName name, IDiscountCurve underlying, double spread)
    {
        ArgumentNullException.ThrowIfNull(underlying);

        // Only non-finite spreads (NaN, ±Infinity) are rejected. A negative additive spread is
        // intentionally permitted: it models a tightening overlay (e.g. a negative fallback spread),
        // unlike MultiplicativeSpreadDiscountCurve where a non-positive factor is arbitrage-violating.
        if (!double.IsFinite(spread))
        {
            throw new ArgumentOutOfRangeException(nameof(spread), "Spread must be a finite number.");
        }

        Name = name;
        _underlying = underlying;
        _spread = spread;
    }

    /// <summary>Identifier of this spread-adjusted curve.</summary>
    public CurveName Name { get; }

    /// <summary>Valuation date inherited from the underlying curve.</summary>
    public DateOnly ValuationDate => _underlying.ValuationDate;

    /// <summary>Currency inherited from the underlying curve.</summary>
    public Currency Currency => _underlying.Currency;

    /// <summary>Day-count calculator borrowed from the underlying curve.</summary>
    public IYearFractionCalculator DayCount => _underlying.DayCount;

    /// <summary>Returns the spread-adjusted discount factor.</summary>
    /// <param name="date">Evaluation date.</param>
    /// <returns>The value returned by <see cref="DiscountFactor"/>.</returns>
    public double ValueAt(DateOnly date) => DiscountFactor(date);

    /// <summary>Applies this curve's spread to the underlying discount factor.</summary>
    /// <param name="date">Evaluation date.</param>
    /// <returns>One on or before valuation; otherwise the underlying discount factor multiplied by the exponential spread adjustment.</returns>
    public double DiscountFactor(DateOnly date)
    {
        if (date <= ValuationDate)
        {
            return 1d;
        }

        double t = DayCount.YearFraction(ValuationDate, date);
        double baseZero = _underlying.ZeroRate(date, CompoundingConvention.Continuous);
        return CurveMath.DiscountFactorFromContinuousZero(baseZero + _spread, t);
    }

    /// <summary>Converts this curve's adjusted discount factor into a zero-rate quote.</summary>
    /// <param name="date">Quote maturity.</param>
    /// <param name="compounding">Requested compounding convention.</param>
    /// <returns>Zero on or before valuation; otherwise a rate consistent with this curve's discount factor.</returns>
    public double ZeroRate(DateOnly date, CompoundingConvention compounding)
    {
        if (date <= ValuationDate)
        {
            return 0d;
        }

        double t = DayCount.YearFraction(ValuationDate, date);
        return CurveMath.ZeroRateFromDiscountFactor(DiscountFactor(date), t, compounding);
    }

    /// <summary>Applies the constant instantaneous-forward spread to the underlying forward.</summary>
    /// <param name="date">Evaluation date.</param>
    /// <returns>The underlying instantaneous forward plus the additive rate spread.</returns>
    public double InstantaneousForwardRate(DateOnly date)
    {
        return _underlying.InstantaneousForwardRate(date) + _spread;
    }

    /// <summary>
    /// Rolls the underlying curve forward to <paramref name="newValuationDate"/>
    /// and re-wraps it with the same additive zero-rate spread.
    /// </summary>
    /// <remarks>
    /// The spread is a rate-space shift independent of the valuation date, so
    /// preserving it under rolling is correct. Forward rates implied by the rolled
    /// curve equal the forward rates implied by the original curve plus the spread,
    /// over any interval on or after the new valuation date.
    /// </remarks>
    public IDiscountCurve WithValuationDate(DateOnly newValuationDate)
    {
        IDiscountCurve rolledUnderlying = _underlying.WithValuationDate(newValuationDate);
        return new ZeroSpreadedDiscountCurve(Name, rolledUnderlying, _spread);
    }
}
