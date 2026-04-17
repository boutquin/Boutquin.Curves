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

using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Abstractions.Curves;

/// <summary>
/// Defines operations for curves that produce forward rates for a benchmark index.
/// </summary>
/// <remarks>
/// In a modern multi-curve stack, forward curves are used to project floating coupons while
/// discount curves are used for present-valuing those coupons. Keeping this interface separate
/// from <see cref="IDiscountCurve"/> makes that distinction explicit for model governance.
/// </remarks>
/// <example>
/// <code>
/// IForwardCurve projection = curveGroup.GetForwardCurve(benchmarkRef);
/// double rate = projection.ForwardRate(accrualStart, accrualEnd);
/// // Use rate to project floating coupon in swap pricing
/// </code>
/// </example>
/// <seealso cref="Boutquin.Curves.Abstractions.Curves.IDiscountCurve"/>
public interface IForwardCurve : ICurve
{
    /// <summary>
    /// Benchmark identity whose forward rates this curve produces.
    /// </summary>
    /// <remarks>
    /// Typical examples include SOFR, CORRA, SONIA, or term-reference rates used in legacy portfolios.
    /// The benchmark is part of curve identity and should flow through diagnostics and risk labels.
    /// </remarks>
    BenchmarkName Benchmark { get; }

    /// <summary>
    /// Returns the implied forward rate over an accrual interval.
    /// </summary>
    /// <param name="startDate">Accrual start date.</param>
    /// <param name="endDate">Accrual end date.</param>
    /// <returns>Forward rate implied between <paramref name="startDate"/> and <paramref name="endDate"/> under the curve's day-count convention.</returns>
    /// <remarks>
    /// This method is commonly consumed by swap pricers and coupon projection engines. Callers are
    /// responsible for supplying valid accrual boundaries generated from product conventions and calendars.
    /// </remarks>
    double ForwardRate(DateOnly startDate, DateOnly endDate);

    /// <summary>
    /// Returns a new forward curve anchored at <paramref name="newValuationDate"/>
    /// under the forwards-realize assumption: forwards implied by the curve today
    /// are taken to be the forwards in effect at that future date.
    /// </summary>
    /// <param name="newValuationDate">
    /// The new valuation date. Typically at or after the curve's current
    /// <see cref="ICurve.ValuationDate"/>; rolling backwards in time is not defined.
    /// </param>
    /// <returns>
    /// A new <see cref="IForwardCurve"/> whose <see cref="ICurve.ValuationDate"/>
    /// equals <paramref name="newValuationDate"/>. Forward rates between dates
    /// after the new valuation date should agree with the original curve's
    /// forward rates over the same interval.
    /// </returns>
    /// <remarks>
    /// See <see cref="IDiscountCurve.WithValuationDate(DateOnly)"/> for the broader
    /// discussion of why curve-rolling is an abstraction-level concern: simulators
    /// and multi-step pricers that advance time need each curve in a market snapshot
    /// to track the new valuation date.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="newValuationDate"/> precedes
    /// <see cref="ICurve.ValuationDate"/>.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// May be thrown by derived or decorator curves whose inner curves do not implement
    /// roll-forward semantics.
    /// </exception>
    IForwardCurve WithValuationDate(DateOnly newValuationDate);
}
