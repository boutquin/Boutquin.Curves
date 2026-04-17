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
/// Provides a constant forward-rate curve.
/// </summary>
/// <remarks>
/// This is a pedagogical and testing curve: it isolates accrual/schedule mechanics from term-structure
/// shape effects. It is helpful when onboarding engineers to floating-rate valuation because expected
/// coupon behavior can be reasoned about analytically before introducing full bootstrap complexity.
///
/// FlatForwardCurve is typically used as an extrapolation stub or a pedagogical tool, not as a primary
/// calibration product. If you wire this into a pricing engine expecting real term-structure shape, every
/// swap in your book will value as though the yield curve is perfectly flat, which eliminates carry-and-roll
/// P&amp;L and makes all forward-starting swaps price identically regardless of tenor. For production
/// forward curves, use ForwardCurveFromDiscountCurves which derives forwards from the calibrated discount
/// curve and correctly reflects term structure shape, including the slope and curvature that drive
/// real-world hedge ratios.
/// </remarks>
/// <example>
/// <code>
/// var curve = new FlatForwardCurve(
///     new CurveName("USD-Flat"), valuationDate, CurrencyCode.USD,
///     new BenchmarkName("SOFR"), flatForwardRate: 0.05);
/// double rate = curve.ForwardRate(date1Y, date2Y); // returns 0.05
/// </code>
/// </example>
public sealed class FlatForwardCurve : IForwardCurve
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlatForwardCurve"/> type.
    /// </summary>
    /// <param name="name">Curve name.</param>
    /// <param name="valuationDate">Curve valuation date.</param>
    /// <param name="currency">Curve currency.</param>
    /// <param name="benchmark">Forward benchmark.</param>
    /// <param name="flatForwardRate">Constant forward rate value.</param>
    /// <param name="dayCount">Day-count convention calculator.</param>
    public FlatForwardCurve(
        CurveName name,
        DateOnly valuationDate,
        CurrencyCode currency,
        BenchmarkName benchmark,
        double flatForwardRate,
        IYearFractionCalculator? dayCount = null)
    {
        if (!double.IsFinite(flatForwardRate))
        {
            throw new ArgumentOutOfRangeException(nameof(flatForwardRate), "Flat forward rate must be a finite number.");
        }

        Name = name;
        ValuationDate = valuationDate;
        Currency = currency;
        Benchmark = benchmark;
        FlatForwardRate = flatForwardRate;
        DayCount = dayCount ?? Actual365Fixed.Instance;
    }

    /// <summary>Curve identifier used in diagnostics and reporting.</summary>
    public CurveName Name { get; }

    /// <summary>Market date at which the curve is anchored; year fractions are measured from this date.</summary>
    public DateOnly ValuationDate { get; }

    /// <summary>Currency in which forward rates are denominated.</summary>
    public CurrencyCode Currency { get; }

    /// <summary>Day-count convention used to convert accrual intervals into year fractions.</summary>
    public IYearFractionCalculator DayCount { get; }

    /// <summary>Benchmark identity whose forward rates this curve produces.</summary>
    public BenchmarkName Benchmark { get; }

    /// <summary>Continuously compounded forward rate applied uniformly at all maturities.</summary>
    public double FlatForwardRate { get; }

    /// <summary>
    /// Returns an implied discount factor from the constant forward rate.
    /// </summary>
    /// <param name="date">Evaluation date.</param>
    /// <returns>Implied discount factor.</returns>
    /// <remarks>
    /// Uses $P(t)=e^{-f t}$ where $f$ is <see cref="FlatForwardRate"/> and $t$ is measured from
    /// <see cref="ValuationDate"/> via <see cref="DayCount"/>.
    /// </remarks>
    public double ValueAt(DateOnly date)
    {
        double t = DayCount.YearFraction(ValuationDate, date);
        return Math.Exp(-FlatForwardRate * t);
    }

    /// <summary>
    /// Returns the constant forward rate for a valid accrual period.
    /// </summary>
    /// <param name="startDate">Accrual start date.</param>
    /// <param name="endDate">Accrual end date.</param>
    /// <returns>Forward rate.</returns>
    /// <remarks>
    /// Dates are validated for schedule sanity, but no tenor-specific conventions are applied here;
    /// those belong to schedule generation and instrument-convention layers.
    /// </remarks>
    public double ForwardRate(DateOnly startDate, DateOnly endDate)
    {
        if (endDate <= startDate)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate), "Forward end date must be after the start date.");
        }

        return FlatForwardRate;
    }

    /// <summary>
    /// Returns a new flat forward curve anchored at <paramref name="newValuationDate"/>
    /// with the same name, currency, benchmark, day-count convention, and flat
    /// forward rate. Flat forwards are invariant under time advancement.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="newValuationDate"/> precedes <see cref="ValuationDate"/>.
    /// </exception>
    public IForwardCurve WithValuationDate(DateOnly newValuationDate)
    {
        if (newValuationDate < ValuationDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newValuationDate),
                "Cannot roll a flat forward curve backwards in time.");
        }

        return new FlatForwardCurve(Name, newValuationDate, Currency, Benchmark, FlatForwardRate, DayCount);
    }
}
