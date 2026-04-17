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

namespace Boutquin.Curves.Abstractions.Curves;

/// <summary>
/// Defines operations for discount curves used to convert future cash flows into present value.
/// </summary>
/// <remarks>
/// Discount curves are foundational in fixed-income pricing because almost every model eventually
/// multiplies projected cash flows by discount factors. Exposing discount factors, zero rates, and
/// instantaneous forwards allows consumers to work in the representation that best fits their product
/// math while keeping one consistent underlying curve state.
/// </remarks>
/// <example>
/// <code>
/// IDiscountCurve curve = snapshot.CurveGroup.GetCurve(reference) as IDiscountCurve;
/// double df = curve.DiscountFactor(maturityDate);
/// double zeroRate = curve.ZeroRate(maturityDate, CompoundingConvention.Continuous);
/// double fwdRate = curve.InstantaneousForwardRate(maturityDate);
/// </code>
/// </example>
public interface IDiscountCurve : ICurve
{
    /// <summary>
    /// Returns the discount factor for the requested date.
    /// </summary>
    /// <param name="date">Target date for discounting.</param>
    /// <returns>Discount factor at <paramref name="date"/>, typically in the range (0, 1] for future dates.</returns>
    /// <remarks>
    /// Conceptually, this is the price today of one unit of currency paid at <paramref name="date"/>.
    /// It is the most robust representation for arbitrage checks and curve interpolation.
    /// </remarks>
    double DiscountFactor(DateOnly date);

    /// <summary>
    /// Returns the zero rate implied by the curve at the requested date under a specific compounding convention.
    /// </summary>
    /// <param name="date">Target date for zero-rate quoting.</param>
    /// <param name="compounding">Compounding basis used for the returned zero rate.</param>
    /// <returns>Zero rate for <paramref name="date"/> and <paramref name="compounding"/>.</returns>
    /// <remarks>
    /// This is a quote-transformation API: the underlying economic state is still the discount curve.
    /// Different desks and systems quote zeros on different compounding bases, so callers must supply
    /// the intended convention explicitly to avoid silent misinterpretation.
    /// Common mistake: passing a compounding convention that differs from what your downstream system
    /// expects. For example, if your risk engine assumes continuous compounding but you pass
    /// CompoundingConvention.Annual, hedge ratios will be systematically wrong. Always verify the
    /// compounding convention expected by each consumer.
    /// </remarks>
    double ZeroRate(DateOnly date, CompoundingConvention compounding);

    /// <summary>
    /// Returns the instantaneous forward rate at the requested date.
    /// </summary>
    /// <param name="date">Target date for forward-rate evaluation.</param>
    /// <returns>Instantaneous forward rate at <paramref name="date"/>.</returns>
    /// <remarks>
    /// In continuous-time notation, this corresponds to $f(t) = -\frac{d}{dt}\ln P(t)$ where $P(t)$
    /// is the discount factor function. It is useful for intuition, scenario analysis, and smoothness checks.
    /// </remarks>
    double InstantaneousForwardRate(DateOnly date);

    /// <summary>
    /// Returns a new curve anchored at <paramref name="newValuationDate"/> under the
    /// forwards-realize assumption: the forward curve observed today is taken to be
    /// the curve in effect at that future date.
    /// </summary>
    /// <param name="newValuationDate">
    /// The new valuation date. Typically at or after the curve's current
    /// <see cref="ICurve.ValuationDate"/>; rolling backwards in time is not defined.
    /// </param>
    /// <returns>
    /// A new <see cref="IDiscountCurve"/> whose <see cref="ICurve.ValuationDate"/>
    /// equals <paramref name="newValuationDate"/> and whose discount factors on
    /// dates strictly after <paramref name="newValuationDate"/> are consistent with
    /// the original curve's forward-curve structure.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The contract: for any date <c>t &gt;= newValuationDate</c>, the rolled curve's
    /// <c>DiscountFactor(t)</c> equals <c>originalCurve.DiscountFactor(t) / originalCurve.DiscountFactor(newValuationDate)</c>.
    /// This makes the implied continuous rate at any fixed future date invariant in time
    /// — the standard "forwards realize" assumption used in backtesting and discrete-step
    /// simulators. The rolled curve is self-consistent in isolation: it does not require
    /// the original curve to remain alive.
    /// </para>
    /// <para>
    /// Concrete curves document their own rolling semantics (flat curves are trivial;
    /// nodal curves re-anchor their pillars; decorators roll their inner curve). This
    /// member is load-bearing for any code that advances time through a market snapshot:
    /// simulators, backtests, scenario analysis, path-based exotic pricers, and
    /// roll-out-of-cash reporting. Call sites that plainly clone the valuation date
    /// without rolling the curve silently produce time-stretched implied rates.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="newValuationDate"/> precedes
    /// <see cref="ICurve.ValuationDate"/> or produces a degenerate curve (for example,
    /// rolling past the last calibrated pillar of a nodal curve).
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// May be thrown by decorator-style curves whose inner curve does not implement
    /// roll-forward semantics.
    /// </exception>
    IDiscountCurve WithValuationDate(DateOnly newValuationDate);
}
