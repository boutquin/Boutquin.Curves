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
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Risk;

/// <summary>
/// Applies a parallel shift to zero rates and re-derives implied discount factors.
/// </summary>
/// <remarks>
/// Applies a uniform zero-rate shift across all maturities. The shift is expressed in basis points
/// (1 bp = 0.0001) and is added directly to the continuously compounded zero rate at each maturity.
/// This is the most basic sensitivity measure, useful for DV01 (dollar value of a basis point)
/// calculations and stress testing. A positive <paramref name="ShiftInBasisPoints"/> increases zero
/// rates at every tenor, which decreases discount factors and typically decreases the present value
/// of future cash flows. The shocked curve preserves the full term structure of the input curve;
/// non-discount curves are returned unchanged.
/// </remarks>
/// <param name="Name">Curve name used for diagnostics and output labeling.</param>
/// <param name="ShiftInBasisPoints">Parallel shift magnitude, expressed in basis points.</param>
public sealed record ParallelZeroRateShock(string Name, double ShiftInBasisPoints) : CurveShockScenario(Name)
{
    private double _shiftInBasisPoints = ValidateShift(ShiftInBasisPoints);

    /// <summary>Finite parallel zero-rate shift, measured in basis points.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned shift is not finite.</exception>
    public double ShiftInBasisPoints
    {
        get => _shiftInBasisPoints;
        init => _shiftInBasisPoints = ValidateShift(value);
    }

    private static double ValidateShift(double shift)
    {
        if (!double.IsFinite(shift))
        {
            throw new ArgumentOutOfRangeException(nameof(shift), "Shift must be finite.");
        }

        return shift;
    }

    /// <summary>
    /// Applies the configured parallel shift to a discount curve.
    /// </summary>
    /// <param name="curve">Curve instance to shock.</param>
    /// <returns>
    /// A discount curve whose discount factors reflect a uniform <see cref="ShiftInBasisPoints"/>
    /// addition to the continuously compounded zero rate at every maturity when <paramref name="curve"/>
    /// is discount-based; otherwise the original curve is returned unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curve"/> is <c>null</c>.</exception>
    public override ICurve Apply(ICurve curve)
    {
        ArgumentNullException.ThrowIfNull(curve);

        if (curve is IDiscountCurve discountCurve)
        {
            return new ParallelShiftedDiscountCurve(discountCurve, ShiftInBasisPoints / 10_000d);
        }

        return curve;
    }

    /// <summary>
    /// Wraps an inner discount curve and applies a constant additive shift to every continuous
    /// zero rate, preserving the full term structure of the original curve.
    /// </summary>
    private sealed class ParallelShiftedDiscountCurve : IDiscountCurve
    {
        private readonly IDiscountCurve _inner;
        private readonly double _shiftDecimal;

        public ParallelShiftedDiscountCurve(IDiscountCurve inner, double shiftDecimal)
        {
            _inner = inner;
            _shiftDecimal = shiftDecimal;
        }

        public CurveName Name => _inner.Name;
        public DateOnly ValuationDate => _inner.ValuationDate;
        public Currency Currency => _inner.Currency;
        public IYearFractionCalculator DayCount => _inner.DayCount;

        public double ValueAt(DateOnly date) => DiscountFactor(date);

        public double DiscountFactor(DateOnly date)
        {
            if (date <= ValuationDate)
            {
                return _inner.DiscountFactor(date);
            }

            double t = DayCount.YearFraction(ValuationDate, date);
            // Shifted DF: base_df(t) * exp(-shift * t)
            return _inner.DiscountFactor(date) * Math.Exp(-_shiftDecimal * t);
        }

        public double ZeroRate(DateOnly date, CompoundingConvention compounding)
        {
            if (date <= ValuationDate)
            {
                return 0d;
            }

            double t = Math.Max(1e-12d, DayCount.YearFraction(ValuationDate, date));
            double df = DiscountFactor(date);
            return compounding switch
            {
                CompoundingConvention.Continuous => -Math.Log(df) / t,
                CompoundingConvention.Simple => (1d / df - 1d) / t,
                CompoundingConvention.Annual => Math.Pow(1d / df, 1d / t) - 1d,
                CompoundingConvention.SemiAnnual => 2d * (Math.Pow(1d / df, 1d / (2d * t)) - 1d),
                CompoundingConvention.Quarterly => 4d * (Math.Pow(1d / df, 1d / (4d * t)) - 1d),
                CompoundingConvention.Monthly => 12d * (Math.Pow(1d / df, 1d / (12d * t)) - 1d),
                _ => throw new ArgumentOutOfRangeException(nameof(compounding), compounding,
                    $"Unsupported compounding convention: {compounding}.")
            };
        }

        public double InstantaneousForwardRate(DateOnly date)
        {
            // IFR(t) = base_IFR(t) + shift, because d(-log DF_shifted)/dt = base_IFR(t) + shift.
            return _inner.InstantaneousForwardRate(date) + _shiftDecimal;
        }

        public IDiscountCurve WithValuationDate(DateOnly newValuationDate)
        {
            return new ParallelShiftedDiscountCurve(_inner.WithValuationDate(newValuationDate), _shiftDecimal);
        }
    }
}
