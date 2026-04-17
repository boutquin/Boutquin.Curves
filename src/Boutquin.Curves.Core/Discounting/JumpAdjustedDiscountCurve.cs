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
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Core.Discounting;

/// <summary>
/// Decorates a discount curve with multiplicative jump adjustments at specified dates.
/// </summary>
/// <remarks>
/// Wraps an underlying discount curve with discrete jump adjustments at specified dates.
/// Jumps typically correspond to central bank meeting dates where the overnight rate may
/// change by a known or anticipated amount. The adjustment multiplies the underlying
/// discount factor by a cumulative product of all jump factors for dates on or before the
/// query date. Jump dates are sorted at construction time so that the cumulative product
/// is computed in chronological order during each discount factor evaluation.
/// </remarks>
public sealed class JumpAdjustedDiscountCurve : IDiscountCurve
{
    private readonly IReadOnlyList<CurvePoint> _jumps;
    private readonly IDiscountCurve _innerCurve;

    /// <summary>
    /// Initializes a new instance of the <see cref="JumpAdjustedDiscountCurve"/> type.
    /// </summary>
    /// <param name="innerCurve">Base discount curve evaluated before jump adjustments.</param>
    /// <param name="jumps">Jump multipliers applied on or after each jump date.</param>
    public JumpAdjustedDiscountCurve(IDiscountCurve innerCurve, IEnumerable<CurvePoint> jumps)
    {
        _innerCurve = innerCurve;
        _jumps = jumps.OrderBy(static jump => jump.Date).ToArray();
    }

    /// <summary>
    /// Curve name inherited from the underlying discount curve.
    /// </summary>
    public CurveName Name => _innerCurve.Name;

    /// <summary>
    /// Valuation date inherited from the underlying discount curve.
    /// </summary>
    public DateOnly ValuationDate => _innerCurve.ValuationDate;

    /// <summary>
    /// Currency inherited from the underlying discount curve.
    /// </summary>
    public CurrencyCode Currency => _innerCurve.Currency;

    /// <summary>
    /// Day-count convention inherited from the underlying discount curve.
    /// </summary>
    public IYearFractionCalculator DayCount => _innerCurve.DayCount;

    /// <summary>
    /// Returns the adjusted discount factor at the requested date.
    /// </summary>
    /// <returns>Discount factor after applying jump multipliers through the target date.</returns>
    public double ValueAt(DateOnly date) => DiscountFactor(date);

    /// <summary>
    /// Computes the adjusted discount factor by combining base-curve and jump effects.
    /// </summary>
    /// <param name="date">Target date for discounting.</param>
    /// <returns>Discount factor including all jumps up to <paramref name="date"/>.</returns>
    public double DiscountFactor(DateOnly date)
    {
        double baseDf = _innerCurve.DiscountFactor(date);
        double jumpAdjustment = _jumps.Where(jump => jump.Date <= date).Aggregate(1d, static (acc, jump) => acc * jump.Value);
        return baseDf * jumpAdjustment;
    }

    /// <summary>
    /// Returns the zero rate from the underlying curve for the requested date and compounding basis.
    /// </summary>
    /// <param name="date">Target date for the returned zero-rate quote.</param>
    /// <param name="compounding">The compounding convention applied to the derived zero rate.</param>
    /// <returns>Zero rate quoted by the underlying curve.</returns>
    public double ZeroRate(DateOnly date, CompoundingConvention compounding) => _innerCurve.ZeroRate(date, compounding);

    /// <summary>
    /// Returns the instantaneous forward rate from the underlying curve.
    /// </summary>
    /// <returns>Instantaneous forward rate at <paramref name="date"/>.</returns>
    public double InstantaneousForwardRate(DateOnly date) => _innerCurve.InstantaneousForwardRate(date);

    /// <summary>
    /// Rolls the inner curve forward to <paramref name="newValuationDate"/> and
    /// drops any jumps that occurred on or before that date. Remaining jumps keep
    /// their original dates and multipliers.
    /// </summary>
    /// <remarks>
    /// A jump's effect applies multiplicatively to discount factors on or after
    /// its date. If the rolled valuation is past a jump date, that jump has already
    /// been realized in the pre-roll discount factor and must be removed to avoid
    /// double-counting on subsequent discount-factor queries.
    /// </remarks>
    public IDiscountCurve WithValuationDate(DateOnly newValuationDate)
    {
        IDiscountCurve rolledInner = _innerCurve.WithValuationDate(newValuationDate);
        List<CurvePoint> remainingJumps = new(_jumps.Count);
        foreach (CurvePoint jump in _jumps)
        {
            if (jump.Date > newValuationDate)
            {
                remainingJumps.Add(jump);
            }
        }

        return new JumpAdjustedDiscountCurve(rolledInner, remainingJumps);
    }
}
