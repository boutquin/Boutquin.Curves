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
using Boutquin.Curves.Interpolation;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Core.Discounting;

/// <summary>
/// Represents a nodal discount curve calibrated on discount-factor pillars with configurable interpolation and extrapolation.
/// </summary>
/// <remarks>
/// This class is the workhorse representation for production term structures: bootstrap solvers produce
/// node discount factors, then this curve evaluates values between nodes for pricing and risk. The default
/// interpolation setup (log-linear discount factors with flat-forward/flat-zero boundaries) matches common
/// market practice because it preserves positive discount factors and yields stable forward behavior.
///
/// Right extrapolation choice affects all tenors beyond the last calibrated node. FlatForward extends the
/// last segment's forward rate indefinitely — this is stable for risk because hedge sensitivities remain
/// well-behaved, but the implied discount factors can diverge materially for very long-dated pricing
/// (e.g., 50Y cross-currency swaps). FlatZero holds the last node's zero rate constant, which causes
/// discount factors to decay predictably but produces a forward-rate discontinuity at the last node that
/// can generate P&amp;L noise in sensitivity reports. If your book contains instruments beyond your last
/// calibrated pillar, choose the extrapolation mode that matches how your risk system attributes
/// long-end moves: FlatForward for forward-rate-based hedging, FlatZero for zero-rate-based reporting.
///
/// Flat-forward right extrapolation holds the forward rate $f$ from the last segment constant:
/// $P(t) = P(T_n) \cdot e^{-f(t - T_n)}$ for $t > T_n$, where
/// $f = -[\ln P(T_n) - \ln P(T_{n-1})] / (T_n - T_{n-1})$. Flat-zero extrapolation holds the zero rate
/// $z$ from the anchor node constant: $P(t) = e^{-z \cdot t}$ where
/// $z = -\ln P(T_{\text{anchor}}) / T_{\text{anchor}}$.
///
/// Pipeline context: this is the runtime curve used for pricing and risk after bootstrap calibration.
/// The PiecewiseBootstrapCalibrator constructs InterpolatedDiscountCurve instances as it solves each
/// node, and the final curve is stored in the ICurveGroup returned to consumers.
///
/// The multi-curve discounting framework, where OIS curves replaced single-curve LIBOR discounting,
/// emerged after the 2007-2008 financial crisis when the basis between LIBOR and OIS widened
/// significantly. This implementation follows the post-crisis convention where collateralized
/// derivatives are discounted using the overnight rate of the collateral currency.
/// </remarks>
/// <example>
/// <code>
/// var points = new[] { new CurvePoint(date1Y, 0.9512), new CurvePoint(date5Y, 0.7788) };
/// var curve = new InterpolatedDiscountCurve(
///     new CurveName("USD-SOFR-Disc"), valuationDate, CurrencyCode.USD, points);
/// double df = curve.DiscountFactor(date3Y);
/// double zero = curve.ZeroRate(date3Y, CompoundingConvention.Continuous);
/// </code>
/// </example>
/// <seealso cref="Boutquin.Curves.Interpolation.INodalCurveInterpolator"/>
public sealed class InterpolatedDiscountCurve : IDiscountCurve
{
    private const double TimeEpsilon = 1e-12d;

    private readonly INodalCurveInterpolator _interpolator;
    private readonly CurvePoint[] _points;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterpolatedDiscountCurve"/> type.
    /// </summary>
    /// <param name="name">Curve name used for diagnostics and reporting.</param>
    /// <param name="valuationDate">Curve valuation date.</param>
    /// <param name="currency">Curve currency.</param>
    /// <param name="points">Discount-factor nodes used to build the curve.</param>
    /// <remarks>
    /// This overload applies library defaults designed for robust desk usage: log-linear interpolation
    /// in discount-factor space, left flat-zero extrapolation, right flat-forward extrapolation, and
    /// ACT/365F day count. To specify a custom interpolator or day-count convention, use the overload
    /// that accepts <see cref="InterpolationSettings"/> and <see cref="IYearFractionCalculator"/>.
    /// Common mistake: constructing a curve with a day-count convention that differs from the convention
    /// used to quote the input rates. For example, passing ACT/365F points to a curve configured with
    /// ACT/360 day count shifts all year fractions by ~1.4%, producing discount factor errors that grow
    /// with maturity and are difficult to diagnose because the curve still 'looks reasonable.'
    /// </remarks>
    public InterpolatedDiscountCurve(
        CurveName name,
        DateOnly valuationDate,
        CurrencyCode currency,
        IEnumerable<CurvePoint> points)
        : this(
            name,
            valuationDate,
            currency,
            points,
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
            null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InterpolatedDiscountCurve"/> type.
    /// </summary>
    /// <param name="name">Curve name used for diagnostics and reporting.</param>
    /// <param name="valuationDate">Curve valuation date.</param>
    /// <param name="currency">Curve currency.</param>
    /// <param name="points">Discount-factor nodes used to build the curve.</param>
    /// <param name="interpolation">Interpolation and extrapolation settings applied to this curve.</param>
    /// <param name="dayCount">Day-count convention used to convert dates into year fractions. Defaults to ACT/365F.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="points"/> is empty or extrapolator modes are blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any discount factor in <paramref name="points"/> is non-positive.</exception>
    public InterpolatedDiscountCurve(
        CurveName name,
        DateOnly valuationDate,
        CurrencyCode currency,
        IEnumerable<CurvePoint> points,
        InterpolationSettings interpolation,
        IYearFractionCalculator? dayCount = null)
    {
        Name = name;
        ValuationDate = valuationDate;
        Currency = currency;
        // Default to ACT/365F, the most common day-count convention for discount curve year fractions.
        DayCount = dayCount ?? Actual365Fixed.Instance;
        Interpolation = interpolation;
        _interpolator = InterpolatorFactory.Create(interpolation.Interpolator);
        // Sort by date so interpolation and extrapolation can assume monotonic ordering.
        _points = points.OrderBy(static point => point.Date).ToArray();

        if (_points.Length == 0)
        {
            throw new ArgumentException("At least one curve point is required.", nameof(points));
        }

        if (_points.Any(static point => point.Value <= 0d))
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Curve points must be strictly positive.");
        }

        if (string.IsNullOrWhiteSpace(Interpolation.LeftExtrapolator))
        {
            throw new ArgumentException("Left extrapolator mode is required.", nameof(interpolation));
        }

        if (string.IsNullOrWhiteSpace(Interpolation.RightExtrapolator))
        {
            throw new ArgumentException("Right extrapolator mode is required.", nameof(interpolation));
        }
    }

    /// <summary>Curve identifier used in diagnostics, repricing output, and risk report labels.</summary>
    public CurveName Name { get; }

    /// <summary>Anchor date from which all year fractions are measured; discount factor at this date is 1.0 by convention.</summary>
    public DateOnly ValuationDate { get; }

    /// <summary>Settlement currency of the instruments calibrated to this curve.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>Day-count convention used to convert calendar dates into year fractions for discounting and rate extraction.</summary>
    public IYearFractionCalculator DayCount { get; }

    /// <summary>Interpolation algorithm and left/right extrapolation modes applied when evaluating between or beyond calibration nodes.</summary>
    public InterpolationSettings Interpolation { get; }

    /// <summary>Calibration nodes sorted by date, each containing a date and its solved discount factor.</summary>
    public IReadOnlyList<CurvePoint> Points => _points;

    /// <summary>
    /// Returns the discount factor at the requested date.
    /// </summary>
    /// <param name="date">Target date for curve evaluation.</param>
    /// <returns>Discount factor implied by this curve.</returns>
    public double ValueAt(DateOnly date) => DiscountFactor(date);

    /// <summary>
    /// Computes the discount factor by interpolating between surrounding nodes.
    /// </summary>
    /// <param name="date">Target date for discounting.</param>
    /// <returns>Discount factor for <paramref name="date"/>.</returns>
    /// <remarks>
    /// Returns 1.0 for dates on or before valuation date. Applies the configured left extrapolation
    /// mode for dates before the first node, and right extrapolation mode beyond the last node.
    /// In normal market data, discount factors should remain positive and broadly decreasing by maturity;
    /// this method does not forcibly enforce monotonicity, so data quality controls should run upstream.
    /// </remarks>
    public double DiscountFactor(DateOnly date)
    {
        if (date <= ValuationDate)
        {
            return 1d;
        }

        if (date <= _points[0].Date)
        {
            return ApplyLeftExtrapolation(date);
        }

        if (date >= _points[^1].Date)
        {
            return ApplyRightExtrapolation(date);
        }

        return _interpolator.Interpolate(ValuationDate, date, _points, DayCount);
    }

    /// <summary>
    /// Converts the curve discount factor at the target date to the requested compounding convention.
    /// </summary>
    /// <param name="date">Target date for the quoted zero rate.</param>
    /// <param name="compounding">Compounding basis used for the returned rate.</param>
    /// <returns>Zero rate quoted under <paramref name="compounding"/>.</returns>
    /// <remarks>
    /// Rate conversion is performed from discount factors, not from an independently interpolated
    /// zero-rate curve. This avoids quote-space inconsistencies when switching compounding bases.
    /// </remarks>
    public double ZeroRate(DateOnly date, CompoundingConvention compounding)
    {
        double t = DayCount.YearFraction(ValuationDate, date);
        return CurveMath.ZeroRateFromDiscountFactor(DiscountFactor(date), t, compounding);
    }

    /// <summary>
    /// Estimates the instantaneous forward rate via a centered finite-difference on log discount factors.
    /// </summary>
    /// <param name="date">Target date where the forward rate is evaluated.</param>
    /// <returns>Instantaneous forward rate estimate at <paramref name="date"/>.</returns>
    /// <remarks>
    /// The implementation approximates $f(t) = -\frac{d}{dt}\ln P(t)$ numerically using a small
    /// two-sided bump around <paramref name="date"/>. This is intended for analytics and diagnostics,
    /// not as a substitute for instrument-level repricing in sensitivity calculations.
    ///
    /// The centered finite-difference approximation uses a 2-day window ($\epsilon \approx 1/365$).
    /// Accuracy is limited by interpolation granularity: if two nodes are close together, the forward
    /// rate estimate may oscillate. For smooth forward curves, consider using the analytic derivative of
    /// the interpolator when available rather than this numerical approximation.
    /// </remarks>
    public double InstantaneousForwardRate(DateOnly date)
    {
        // ~1 day in year-fraction units; prevents zero-denominator in finite difference.
        const double epsilon = 1d / 3650d;
        DateOnly leftDate = date.AddDays(-1);
        DateOnly rightDate = date.AddDays(1);
        double t0 = Math.Max(0d, DayCount.YearFraction(ValuationDate, leftDate));
        double t1 = Math.Max(epsilon, DayCount.YearFraction(ValuationDate, rightDate));
        double df0 = DiscountFactor(leftDate);
        double df1 = DiscountFactor(rightDate);
        return -(Math.Log(df1) - Math.Log(df0)) / Math.Max(epsilon, t1 - t0);
    }

    /// <summary>
    /// Returns a new interpolated discount curve anchored at <paramref name="newValuationDate"/>.
    /// Pillars are re-anchored under the forwards-realize assumption: at any date
    /// <c>t &gt;= newValuationDate</c> the new curve's discount factor equals
    /// <c>originalDF(t) / originalDF(newValuationDate)</c>.
    /// </summary>
    /// <param name="newValuationDate">
    /// The new valuation date. Must be strictly before the last pillar — rolling
    /// past the last calibrated node produces a degenerate curve.
    /// </param>
    /// <returns>
    /// A new <see cref="InterpolatedDiscountCurve"/> whose pillars are rescaled by
    /// <c>1 / originalDF(newValuationDate)</c>, preserving forward rates over any
    /// interval on or after <paramref name="newValuationDate"/>. The original
    /// interpolation and extrapolation settings and day-count convention are
    /// preserved. Pillars at or before <paramref name="newValuationDate"/> are
    /// dropped from the rolled curve.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Derivation: let <c>P(t)</c> be the original curve's discount factor and
    /// <c>s = newValuationDate</c>. Under the forwards-realize assumption, the
    /// rolled curve's discount factor at <c>t &gt;= s</c> is
    /// <c>P_s(t) = P(t) / P(s)</c>, which implies the same forward rate between
    /// any two dates <c>t1, t2 &gt;= s</c>. Building the rolled curve with pillars
    /// at the original pillar dates on or after <c>s</c>, each rescaled by
    /// <c>1 / P(s)</c>, reproduces that discount-factor surface exactly at the
    /// pillar nodes.
    /// </para>
    /// <para>
    /// Left extrapolation on the rolled curve covers the window
    /// [newValuationDate, firstRemainingPillar]; the rolled curve's anchor
    /// (DF = 1 at newValuationDate) is implicit.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="newValuationDate"/> precedes this curve's
    /// <see cref="ValuationDate"/>, or is on or after the last calibrated pillar.
    /// </exception>
    public IDiscountCurve WithValuationDate(DateOnly newValuationDate)
    {
        if (newValuationDate < ValuationDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newValuationDate),
                "Cannot roll an interpolated discount curve backwards in time.");
        }

        if (newValuationDate >= _points[^1].Date)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newValuationDate),
                "Rolling past the last calibrated pillar produces a degenerate curve. "
                + "Extend the curve or roll to an earlier date.");
        }

        if (newValuationDate == ValuationDate)
        {
            return new InterpolatedDiscountCurve(Name, ValuationDate, Currency, _points, Interpolation, DayCount);
        }

        double anchorDf = DiscountFactor(newValuationDate);
        List<CurvePoint> rolledPoints = new(_points.Length);
        foreach (CurvePoint pillar in _points)
        {
            if (pillar.Date <= newValuationDate)
            {
                continue;
            }

            rolledPoints.Add(new CurvePoint(pillar.Date, pillar.Value / anchorDf));
        }

        return new InterpolatedDiscountCurve(
            Name,
            newValuationDate,
            Currency,
            rolledPoints,
            Interpolation,
            DayCount);
    }

    private double ApplyLeftExtrapolation(DateOnly date)
    {
        if (Interpolation.LeftExtrapolator.Equals("FlatZero", StringComparison.OrdinalIgnoreCase))
        {
            return ExtrapolateFlatZero(date, _points[0]);
        }

        if (Interpolation.LeftExtrapolator.Equals("FlatForward", StringComparison.OrdinalIgnoreCase))
        {
            return ExtrapolateLeftFlatForward(date);
        }

        throw new NotSupportedException($"Unsupported left extrapolator mode: {Interpolation.LeftExtrapolator}.");
    }

    private double ApplyRightExtrapolation(DateOnly date)
    {
        if (Interpolation.RightExtrapolator.Equals("FlatZero", StringComparison.OrdinalIgnoreCase))
        {
            return ExtrapolateFlatZero(date, _points[^1]);
        }

        if (Interpolation.RightExtrapolator.Equals("FlatForward", StringComparison.OrdinalIgnoreCase))
        {
            return ExtrapolateRightFlatForward(date);
        }

        throw new NotSupportedException($"Unsupported right extrapolator mode: {Interpolation.RightExtrapolator}.");
    }

    private double ExtrapolateFlatZero(DateOnly targetDate, CurvePoint anchor)
    {
        double anchorTime = Math.Max(TimeEpsilon, DayCount.YearFraction(ValuationDate, anchor.Date));
        double anchorZeroRate = -Math.Log(anchor.Value) / anchorTime;
        double targetTime = Math.Max(0d, DayCount.YearFraction(ValuationDate, targetDate));
        return Math.Exp(-anchorZeroRate * targetTime);
    }

    private double ExtrapolateLeftFlatForward(DateOnly targetDate)
    {
        // With only one node, forward rate is undefined — fall back to flat zero extrapolation.
        if (_points.Length < 2)
        {
            return ExtrapolateFlatZero(targetDate, _points[0]);
        }

        CurvePoint first = _points[0];
        CurvePoint second = _points[1];
        double forward = SegmentForwardRate(first, second);
        double firstTime = Math.Max(TimeEpsilon, DayCount.YearFraction(ValuationDate, first.Date));
        double targetTime = Math.Max(0d, DayCount.YearFraction(ValuationDate, targetDate));
        return first.Value * Math.Exp(forward * (firstTime - targetTime));
    }

    private double ExtrapolateRightFlatForward(DateOnly targetDate)
    {
        // With only one node, forward rate is undefined — fall back to flat zero extrapolation.
        if (_points.Length < 2)
        {
            return ExtrapolateFlatZero(targetDate, _points[^1]);
        }

        CurvePoint penultimate = _points[^2];
        CurvePoint last = _points[^1];
        double forward = SegmentForwardRate(penultimate, last);
        double lastTime = Math.Max(TimeEpsilon, DayCount.YearFraction(ValuationDate, last.Date));
        double targetTime = Math.Max(lastTime, DayCount.YearFraction(ValuationDate, targetDate));
        return last.Value * Math.Exp(-forward * (targetTime - lastTime));
    }

    private double SegmentForwardRate(CurvePoint left, CurvePoint right)
    {
        double leftTime = Math.Max(TimeEpsilon, DayCount.YearFraction(ValuationDate, left.Date));
        double rightTime = Math.Max(TimeEpsilon, DayCount.YearFraction(ValuationDate, right.Date));
        return -Math.Log(right.Value / left.Value) / Math.Max(TimeEpsilon, rightTime - leftTime);
    }
}
