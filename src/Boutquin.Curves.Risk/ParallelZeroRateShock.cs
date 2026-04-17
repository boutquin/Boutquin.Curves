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
using Boutquin.Curves.Core.Discounting;
using Boutquin.MarketData.Conventions;

namespace Boutquin.Curves.Risk;

/// <summary>
/// Applies a parallel shift to zero rates and re-derives implied discount factors.
/// </summary>
/// <remarks>
/// Applies a uniform zero-rate shift across all maturities. This is the most basic sensitivity
/// measure, useful for DV01 (dollar value of a basis point) calculations and stress testing. A
/// positive <paramref name="ShiftInBasisPoints"/> increases zero rates, which decreases discount
/// factors and typically decreases the present value of future cash flows. Non-discount curves
/// are returned unchanged.
/// </remarks>
/// <param name="Name">Curve name used for diagnostics and output labeling.</param>
/// <param name="ShiftInBasisPoints">Parallel shift magnitude, expressed in basis points.</param>
public sealed record ParallelZeroRateShock(string Name, double ShiftInBasisPoints) : CurveShockScenario(Name)
{
    /// <summary>
    /// Applies the configured parallel shift to a discount curve.
    /// </summary>
    /// <param name="curve">Curve instance to shock.</param>
    /// <returns>A shifted flat discount curve when <paramref name="curve"/> is discount-based; otherwise the original curve.</returns>
    public override ICurve Apply(ICurve curve)
    {
        ArgumentNullException.ThrowIfNull(curve);

        if (curve is IDiscountCurve discountCurve)
        {
            double shift = ShiftInBasisPoints / 10_000d;
            double zero = discountCurve.ZeroRate(discountCurve.ValuationDate.AddYears(5), CompoundingConvention.Continuous);
            return new FlatDiscountCurve(discountCurve.Name, discountCurve.ValuationDate, discountCurve.Currency, zero + shift, discountCurve.DayCount);
        }

        return curve;
    }
}
