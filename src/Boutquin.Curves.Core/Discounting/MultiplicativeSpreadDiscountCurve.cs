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
/// Applies a constant multiplicative spread to an underlying curve's discount factors.
/// </summary>
/// <remarks>
/// The spreaded discount factor at maturity $t$ is $P_{\text{spread}}(t) = P_{\text{base}}(t) \cdot m^t$
/// where $m$ is the multiplicative spread factor and $t$ is the year fraction from valuation.
/// A spread factor of 1.0 leaves the curve unchanged. A factor less than 1.0 increases implied rates
/// (models additional credit/liquidity risk); a factor greater than 1.0 decreases implied rates.
///
/// This representation is natural for survival-probability overlays where the credit spread is
/// modeled as a multiplicative hazard rate applied to the risk-free discount curve.
/// </remarks>
public sealed class MultiplicativeSpreadDiscountCurve : IDiscountCurve
{
    private readonly IDiscountCurve _underlying;
    private readonly double _spreadFactor;
    private readonly double _logSpreadFactor;

    /// <summary>
    /// Initializes a new instance of the <see cref="MultiplicativeSpreadDiscountCurve"/> type.
    /// </summary>
    /// <param name="name">Curve name for the spreaded curve.</param>
    /// <param name="underlying">Base discount curve to which the spread is applied.</param>
    /// <param name="spreadFactor">Multiplicative discount-factor spread per unit time (e.g., 0.999 for ~10 bp/year).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="underlying"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="spreadFactor"/> is non-positive or not a finite number (NaN or ±Infinity).</exception>
    public MultiplicativeSpreadDiscountCurve(CurveName name, IDiscountCurve underlying, double spreadFactor)
    {
        ArgumentNullException.ThrowIfNull(underlying);

        if (!double.IsFinite(spreadFactor) || spreadFactor <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(spreadFactor), "Spread factor must be a finite, strictly positive number.");
        }

        Name = name;
        _underlying = underlying;
        _spreadFactor = spreadFactor;
        _logSpreadFactor = Math.Log(spreadFactor);
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
        return _underlying.DiscountFactor(date) * Math.Exp(_logSpreadFactor * t);
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
    /// <returns>The underlying instantaneous forward minus the logarithm of the multiplicative spread factor.</returns>
    public double InstantaneousForwardRate(DateOnly date)
    {
        return _underlying.InstantaneousForwardRate(date) - _logSpreadFactor;
    }

    /// <summary>
    /// Rolls the underlying curve forward to <paramref name="newValuationDate"/>
    /// and re-wraps it with the same multiplicative spread factor.
    /// </summary>
    /// <remarks>
    /// The spread factor's effect is <c>exp(log_m · t)</c> where <c>t</c> is the
    /// year fraction from valuation — a per-unit-time multiplicative shift that
    /// commutes with time advancement. Rolling simply advances the valuation anchor
    /// for that accrual.
    /// </remarks>
    public IDiscountCurve WithValuationDate(DateOnly newValuationDate)
    {
        IDiscountCurve rolledUnderlying = _underlying.WithValuationDate(newValuationDate);
        return new MultiplicativeSpreadDiscountCurve(Name, rolledUnderlying, _spreadFactor);
    }
}
