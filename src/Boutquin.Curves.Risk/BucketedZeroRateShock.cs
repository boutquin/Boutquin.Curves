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
/// Represents one pillar in a bucketed zero-rate shock profile, defining the shock magnitude
/// at a specific maturity measured in years from valuation date.
/// </summary>
/// <param name="MaturityYears">Maturity expressed as a year fraction from the valuation date (must be positive).</param>
/// <param name="ShiftInBasisPoints">
/// Zero-rate shift at this maturity in basis points (1 bp = 0.01%). Positive values shift rates up
/// (discount factors decrease); negative values shift rates down (discount factors increase).
/// The shocked discount factor is <c>df(t) * exp(-shift(t) * t)</c> where <c>shift(t)</c> is the
/// linearly interpolated shift in decimal form (basis points / 10 000).
/// </param>
public sealed record BucketedShockPoint(
    double MaturityYears,
    double ShiftInBasisPoints);

/// <summary>
/// Applies a maturity-dependent (bucketed) zero-rate shock to discount curves by linearly
/// interpolating between configured shock points and adjusting discount factors accordingly.
/// </summary>
/// <remarks>
/// Used to compute bucketed (key-rate) sensitivities by applying one <see cref="BucketedZeroRateShock"/>
/// per tenor point and measuring the valuation impact at that bucket. Each shock point specifies a
/// zero-rate shift magnitude at a given maturity; shifts between points are linearly interpolated in
/// basis-point space. For maturities before the first shock point, the first point's shift is used
/// (flat extrapolation). For maturities beyond the last shock point, the last point's shift is used.
/// Non-discount curves are returned unchanged.
/// </remarks>
public sealed record BucketedZeroRateShock : CurveShockScenario
{
    /// <summary>
    /// Shock profile pillars defining the zero-rate shift at each configured maturity.
    /// </summary>
    public IReadOnlyList<BucketedShockPoint> ShockPoints { get; }

    /// <summary>
    /// Creates a bucketed zero-rate shock scenario, storing a defensive copy of the shock points.
    /// </summary>
    /// <param name="name">Scenario label used in diagnostics and risk report output.</param>
    /// <param name="shockPoints">
    /// Zero-rate shock pillars; must be non-<see langword="null"/>, but need not be sorted —
    /// sorting by <see cref="BucketedShockPoint.MaturityYears"/> is applied at shock time.
    /// An empty list causes <see cref="Apply"/> to return the base curve unchanged.
    /// </param>
    /// <exception cref="ArgumentException">A pillar is null, a maturity is not positive and finite or is duplicated, or a shift is not finite.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shockPoints"/> is <see langword="null"/>.</exception>
    public BucketedZeroRateShock(
        string name,
        IReadOnlyList<BucketedShockPoint> shockPoints) : base(name)
    {
        ArgumentNullException.ThrowIfNull(shockPoints);
        BucketedShockPoint[] copy = shockPoints.OrderBy(static point => point?.MaturityYears).ToArray();
        for (int i = 0; i < copy.Length; i++)
        {
            BucketedShockPoint point = copy[i];
            if (point is null || !double.IsFinite(point.MaturityYears) || point.MaturityYears <= 0d ||
                !double.IsFinite(point.ShiftInBasisPoints) || (i > 0 && point.MaturityYears == copy[i - 1].MaturityYears))
            {
                throw new ArgumentException("Shock pillars must have unique positive finite maturities and finite shifts.", nameof(shockPoints));
            }
        }

        ShockPoints = Array.AsReadOnly(copy);
    }

    /// <summary>
    /// Applies the bucketed zero-rate shock to the given curve.
    /// </summary>
    /// <param name="curve">Base curve to transform; must not be <c>null</c>.</param>
    /// <returns>
    /// A shocked discount curve with adjusted discount factors when <paramref name="curve"/> implements
    /// <see cref="IDiscountCurve"/>; otherwise the original curve is returned unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curve"/> is <c>null</c>.</exception>
    /// <remarks>
    /// When <see cref="ShockPoints"/> is empty, the original curve is returned unchanged even for discount curves.
    /// Shock points are sorted by maturity before application. The shocked discount factor at time <c>t</c> is
    /// <c>df(t) * exp(-shift(t) * t)</c>, where <c>shift(t)</c> is the linearly interpolated shift in decimal form.
    /// </remarks>
    public override ICurve Apply(ICurve curve)
    {
        ArgumentNullException.ThrowIfNull(curve);

        if (curve is not IDiscountCurve discountCurve)
        {
            return curve;
        }

        if (ShockPoints.Count == 0)
        {
            return curve;
        }

        // The constructor captures a sorted, read-only profile.
        return new BucketedShiftedDiscountCurve(discountCurve, ShockPoints);
    }

    private sealed class BucketedShiftedDiscountCurve : IDiscountCurve
    {
        private readonly IDiscountCurve _innerCurve;
        private readonly IReadOnlyList<BucketedShockPoint> _shockPoints;

        public BucketedShiftedDiscountCurve(IDiscountCurve innerCurve, IReadOnlyList<BucketedShockPoint> shockPoints)
        {
            _innerCurve = innerCurve;
            _shockPoints = shockPoints;
        }

        public CurveName Name => _innerCurve.Name;

        public DateOnly ValuationDate => _innerCurve.ValuationDate;

        public Currency Currency => _innerCurve.Currency;

        public IYearFractionCalculator DayCount => _innerCurve.DayCount;

        public double ValueAt(DateOnly date) => DiscountFactor(date);

        public double DiscountFactor(DateOnly date)
        {
            if (date <= ValuationDate)
            {
                return _innerCurve.DiscountFactor(date);
            }

            double t = DayCount.YearFraction(ValuationDate, date);
            double shift = InterpolateShiftInBasisPoints(t) / 10_000d;
            return _innerCurve.DiscountFactor(date) * Math.Exp(-shift * t);
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
            // ~1 day in year-fraction units; prevents zero-denominator in finite difference.
            const double epsilon = 1d / 3650d;

            // At or within one day of the valuation date the centered difference would require
            // leftDate < ValuationDate, which causes YearFraction to throw (endDate < startDate).
            // Use a one-sided right difference instead so the result is defined at the front boundary.
            if (date <= ValuationDate.AddDays(1))
            {
                DateOnly rightDate = ValuationDate.AddDays(2);
                double t0 = 0d;
                double t1 = Math.Max(epsilon, DayCount.YearFraction(ValuationDate, rightDate));
                double df0 = 1d; // DF at valuation date is always 1
                double df1 = DiscountFactor(rightDate);
                return -(Math.Log(df1) - Math.Log(df0)) / Math.Max(epsilon, t1 - t0);
            }

            DateOnly leftDate = date.AddDays(-1);
            DateOnly rightDateCentered = date.AddDays(1);
            double tLeft = Math.Max(0d, DayCount.YearFraction(ValuationDate, leftDate));
            double tRight = Math.Max(epsilon, DayCount.YearFraction(ValuationDate, rightDateCentered));
            double dfLeft = DiscountFactor(leftDate);
            double dfRight = DiscountFactor(rightDateCentered);
            return -(Math.Log(dfRight) - Math.Log(dfLeft)) / Math.Max(epsilon, tRight - tLeft);
        }

        /// <summary>
        /// Rolls the inner curve forward and re-wraps it with the same bucketed
        /// shock profile. Shock points are maturity-based (year fractions from
        /// valuation), not date-anchored, so they apply unchanged after rolling.
        /// </summary>
        public IDiscountCurve WithValuationDate(DateOnly newValuationDate)
        {
            IDiscountCurve rolledInner = _innerCurve.WithValuationDate(newValuationDate);
            return new BucketedShiftedDiscountCurve(rolledInner, _shockPoints);
        }

        private double InterpolateShiftInBasisPoints(double maturityYears)
        {
            if (maturityYears <= _shockPoints[0].MaturityYears)
            {
                return _shockPoints[0].ShiftInBasisPoints;
            }

            if (maturityYears >= _shockPoints[^1].MaturityYears)
            {
                return _shockPoints[^1].ShiftInBasisPoints;
            }

            for (int i = 1; i < _shockPoints.Count; i++)
            {
                BucketedShockPoint left = _shockPoints[i - 1];
                BucketedShockPoint right = _shockPoints[i];
                if (maturityYears <= right.MaturityYears)
                {
                    double w = (maturityYears - left.MaturityYears) / Math.Max(1e-12d, right.MaturityYears - left.MaturityYears);
                    return left.ShiftInBasisPoints + ((right.ShiftInBasisPoints - left.ShiftInBasisPoints) * w);
                }
            }

            return _shockPoints[^1].ShiftInBasisPoints;
        }
    }
}
