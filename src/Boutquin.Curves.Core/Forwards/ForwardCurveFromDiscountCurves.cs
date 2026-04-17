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
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Core.Forwards;

/// <summary>
/// Derives forward rates from a projection discount curve while retaining discount-curve linkage for multi-curve workflows.
/// </summary>
/// <remarks>
/// In post-crisis market practice, projection and discounting are separated: floating coupons are
/// projected off an index curve and discounted off a collateral/funding curve. This type reflects that
/// architecture and provides a forward-only view over the projection curve for pricing components that
/// should not couple directly to full discount-curve behavior.
/// </remarks>
/// <seealso cref="Boutquin.Curves.Abstractions.Curves.IForwardCurve"/>
/// <seealso cref="Boutquin.Curves.Abstractions.Curves.IDiscountCurve"/>
public sealed class ForwardCurveFromDiscountCurves : IForwardCurve
{
    private readonly IDiscountCurve _discountCurve;
    private readonly IDiscountCurve _projectionCurve;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForwardCurveFromDiscountCurves"/> type.
    /// </summary>
    /// <param name="name">Curve name used for diagnostics and reporting.</param>
    /// <param name="benchmark">Benchmark for which forward rates are produced.</param>
    /// <param name="discountCurve">Discounting curve used by downstream valuation workflows.</param>
    /// <param name="projectionCurve">Projection curve used to infer forward rates.</param>
    /// <remarks>
    /// The current implementation computes forwards from <paramref name="projectionCurve"/> only.
    /// <paramref name="discountCurve"/> is retained to preserve explicit wiring in dependency graphs
    /// and support future extensions where discount context participates in diagnostics or adjustments.
    /// </remarks>
    public ForwardCurveFromDiscountCurves(
        CurveName name,
        BenchmarkName benchmark,
        IDiscountCurve discountCurve,
        IDiscountCurve projectionCurve)
    {
        Name = name;
        Benchmark = benchmark;
        _discountCurve = discountCurve;
        _projectionCurve = projectionCurve;
    }

    /// <summary>
    /// Curve name used for diagnostics and reporting.
    /// </summary>
    public CurveName Name { get; }

    /// <summary>
    /// Valuation date inherited from the projection curve.
    /// </summary>
    public DateOnly ValuationDate => _projectionCurve.ValuationDate;

    /// <summary>
    /// Currency inherited from the projection curve.
    /// </summary>
    public CurrencyCode Currency => _projectionCurve.Currency;

    /// <summary>
    /// Day-count convention used for forward accrual fractions.
    /// </summary>
    public IYearFractionCalculator DayCount => _projectionCurve.DayCount;

    /// <summary>
    /// Benchmark identity whose forward rates this curve produces.
    /// </summary>
    public BenchmarkName Benchmark { get; }

    /// <summary>
    /// Returns the projection-curve discount factor at the requested date.
    /// </summary>
    /// <param name="date">Target date for projection-curve evaluation.</param>
    /// <returns>Projection-curve discount factor for <paramref name="date"/>.</returns>
    /// <remarks>
    /// This accessor exposes the underlying projection state for components that sample the curve directly.
    /// For coupon generation, prefer <see cref="ForwardRate"/> to avoid repeated formula duplication.
    /// </remarks>
    public double ValueAt(DateOnly date) => _projectionCurve.DiscountFactor(date);

    /// <summary>
    /// Computes the simple forward rate implied between two dates.
    /// </summary>
    /// <param name="startDate">Accrual start date.</param>
    /// <param name="endDate">Accrual end date. Must be after <paramref name="startDate"/>.</param>
    /// <returns>Forward rate implied by the projection curve over the accrual interval.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="endDate"/> is not strictly after <paramref name="startDate"/>.</exception>
    /// <remarks>
    /// Implements the textbook relation $F=\frac{P(t_0)}{P(t_1)}-1$ divided by accrual year fraction,
    /// where $P$ is the projection discount curve. A small floor on accrual avoids numeric blow-ups on
    /// degenerate intervals produced by bad schedule inputs.
    ///
    /// Derivation: the forward rate between dates $t_1$ and $t_2$ is derived from the discount curve as
    /// $F(t_1, t_2) = [P(t_1) / P(t_2) - 1] / \tau$ where $\tau$ is the year fraction between the dates
    /// under the curve's day-count convention. This is the no-arbitrage forward rate implied by the term
    /// structure: it is the rate at which one can lock in borrowing between $t_1$ and $t_2$ today.
    /// </remarks>
    public double ForwardRate(DateOnly startDate, DateOnly endDate)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate), "Forward end date must be after the start date.");
        }

        double startDf = _projectionCurve.DiscountFactor(startDate);
        double endDf = _projectionCurve.DiscountFactor(endDate);
        double accrual = Math.Max(1e-12, DayCount.YearFraction(startDate, endDate));
        return (startDf / endDf - 1d) / accrual;
    }

    /// <summary>
    /// Rolls both the discount and projection curves forward to
    /// <paramref name="newValuationDate"/>, preserving the benchmark and name.
    /// </summary>
    /// <remarks>
    /// Because forwards are computed as ratios of projection-curve discount factors,
    /// rolling the projection curve under the forwards-realize assumption preserves
    /// the forward rate between any two dates strictly after
    /// <paramref name="newValuationDate"/>. The discount curve is rolled in lockstep
    /// so multi-curve wiring stays consistent downstream.
    /// </remarks>
    public IForwardCurve WithValuationDate(DateOnly newValuationDate)
    {
        IDiscountCurve rolledDiscount = _discountCurve.WithValuationDate(newValuationDate);
        IDiscountCurve rolledProjection = _projectionCurve.WithValuationDate(newValuationDate);
        return new ForwardCurveFromDiscountCurves(Name, Benchmark, rolledDiscount, rolledProjection);
    }
}
