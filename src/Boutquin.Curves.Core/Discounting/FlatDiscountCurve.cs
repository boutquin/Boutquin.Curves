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
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Core.Discounting;

/// <summary>
/// Represents a discount curve with a constant continuously compounded zero rate.
/// </summary>
/// <remarks>
/// This curve is primarily a baseline object: useful for sanity checks, unit tests, pedagogy,
/// and quick what-if analysis. Production valuation typically uses nodal curves, but a flat curve
/// is valuable to isolate product math from calibration complexity.
/// </remarks>
public sealed class FlatDiscountCurve : IDiscountCurve
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlatDiscountCurve"/> type.
    /// </summary>
    /// <param name="name">Curve name used for diagnostics and reporting.</param>
    /// <param name="valuationDate">Curve valuation date.</param>
    /// <param name="currency">Curve currency.</param>
    /// <param name="continuouslyCompoundedZeroRate">Constant annualized continuous zero rate applied at all maturities.</param>
    /// <param name="dayCount">Day-count convention used to convert dates into year fractions.</param>
    public FlatDiscountCurve(
        CurveName name,
        DateOnly valuationDate,
        CurrencyCode currency,
        double continuouslyCompoundedZeroRate,
        IYearFractionCalculator? dayCount = null)
    {
        if (!double.IsFinite(continuouslyCompoundedZeroRate))
        {
            throw new ArgumentOutOfRangeException(nameof(continuouslyCompoundedZeroRate), "Zero rate must be a finite number.");
        }

        Name = name;
        ValuationDate = valuationDate;
        Currency = currency;
        ContinuouslyCompoundedZeroRate = continuouslyCompoundedZeroRate;
        DayCount = dayCount ?? Actual365Fixed.Instance;
    }

    /// <summary>
    /// Curve name used for diagnostics and reporting.
    /// </summary>
    public CurveName Name { get; }

    /// <summary>
    /// Valuation date from which year fractions are measured.
    /// </summary>
    public DateOnly ValuationDate { get; }

    /// <summary>
    /// Currency in which this discount curve is expressed.
    /// </summary>
    public CurrencyCode Currency { get; }

    /// <summary>
    /// Day-count convention used to convert dates into year fractions for discounting.
    /// </summary>
    public IYearFractionCalculator DayCount { get; }

    /// <summary>
    /// Constant continuously compounded zero rate applied at all maturities.
    /// </summary>
    public double ContinuouslyCompoundedZeroRate { get; }

    /// <summary>
    /// Returns the discount factor at the requested date.
    /// </summary>
    /// <param name="date">Target date for discounting.</param>
    /// <returns>Discount factor implied by the constant zero-rate assumption.</returns>
    public double ValueAt(DateOnly date) => DiscountFactor(date);

    /// <summary>
    /// Computes the discount factor using continuous compounding from valuation date to target date.
    /// </summary>
    /// <param name="date">Target date for discounting.</param>
    /// <returns>Discount factor for <paramref name="date"/>.</returns>
    /// <remarks>
    /// Uses $P(t)=e^{-r t}$ with $r$ equal to <see cref="ContinuouslyCompoundedZeroRate"/> and
    /// $t$ from <see cref="DayCount"/>. Negative rates are supported mathematically and may produce
    /// discount factors above 1 for short maturities.
    /// </remarks>
    public double DiscountFactor(DateOnly date)
    {
        double t = DayCount.YearFraction(ValuationDate, date);
        return CurveMath.DiscountFactorFromContinuousZero(ContinuouslyCompoundedZeroRate, t);
    }

    /// <summary>
    /// Converts the curve discount factor at the target date to the requested compounding convention.
    /// </summary>
    /// <param name="date">Target date for the quoted zero rate.</param>
    /// <param name="compounding">Compounding basis used for the returned rate.</param>
    /// <returns>Zero rate quoted under <paramref name="compounding"/>.</returns>
    /// <remarks>
    /// This is a quote conversion only. The underlying curve state remains continuous-zero internally.
    /// </remarks>
    public double ZeroRate(DateOnly date, CompoundingConvention compounding)
    {
        double t = DayCount.YearFraction(ValuationDate, date);
        return CurveMath.ZeroRateFromDiscountFactor(DiscountFactor(date), t, compounding);
    }

    /// <summary>
    /// Returns the instantaneous forward rate implied by this flat curve.
    /// </summary>
    /// <param name="date">Target date, included for interface consistency.</param>
    /// <returns>The constant continuously compounded zero rate.</returns>
    public double InstantaneousForwardRate(DateOnly date) => ContinuouslyCompoundedZeroRate;

    /// <summary>
    /// Returns a new flat discount curve anchored at <paramref name="newValuationDate"/>
    /// with the same name, currency, day-count convention, and continuously compounded
    /// zero rate as this curve.
    /// </summary>
    /// <remarks>
    /// Rolling a flat curve is exact under the forwards-realize assumption: every
    /// forward rate is identical to the flat zero rate, so the rolled curve's implied
    /// rate is unchanged. For any date <c>t &gt;= newValuationDate</c>, the new
    /// curve's discount factor is <c>exp(-r · (t − newValuationDate))</c>, which
    /// equals <c>originalDF(t) / originalDF(newValuationDate)</c> algebraically.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="newValuationDate"/> precedes <see cref="ValuationDate"/>.
    /// </exception>
    public IDiscountCurve WithValuationDate(DateOnly newValuationDate)
    {
        if (newValuationDate < ValuationDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newValuationDate),
                "Cannot roll a flat discount curve backwards in time.");
        }

        return new FlatDiscountCurve(Name, newValuationDate, Currency, ContinuouslyCompoundedZeroRate, DayCount);
    }
}
