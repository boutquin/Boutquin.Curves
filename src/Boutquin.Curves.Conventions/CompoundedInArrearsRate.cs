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

using Boutquin.MarketData.Abstractions.Calendars;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Conventions;

/// <summary>
/// Computes the compounded-in-arrears overnight rate for a daily-fixing accrual window.
/// </summary>
/// <remarks>
/// <para>
/// The compounded-in-arrears formula (Hull Ch 4 p.100) compounds each overnight fixing
/// over its accrual period and annualises the result over the total window:
/// </para>
/// <code>
/// R_period = [ ∏(1 + r_i · ŷ_i) − 1 ] · (daysPerYear / D)
/// </code>
/// where r_i is the overnight rate on day i, ŷ_i = YearFraction(d_i, d_{i+1}) is the
/// per-overnight accrual fraction, and D is the total calendar days in the accrual period.
/// <para>
/// Post-LIBOR benchmarks (SOFR, SONIA, ESTR, TONA, SARON) use this compounding
/// methodology, with D = total calendar days from first fixing date to day after last fixing.
/// </para>
/// </remarks>
public static class CompoundedInArrearsRate
{
    /// <summary>
    /// Computes the compounded-in-arrears rate from a list of daily overnight fixings.
    /// </summary>
    /// <param name="dailyFixings">
    /// Ordered list of (FixingDate, Rate) pairs. The rate is expressed in decimal form
    /// (e.g. 0.05 for 5%). Each fixing covers the period from FixingDate to the next calendar day.
    /// </param>
    /// <param name="dayCount">Day-count calculator for overnight accrual fractions.</param>
    /// <param name="daysPerYear">
    /// Convention for annualisation denominator. Typically 360 for USD/EUR SOFR/ESTR,
    /// 365 for GBP SONIA.
    /// </param>
    /// <returns>Annualised compounded rate over the accrual window, expressed in decimal form.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="dailyFixings"/> or <paramref name="dayCount"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The annualization denominator is not positive.</exception>
    /// <exception cref="ArgumentException">Fixing dates are not strictly increasing.</exception>
    public static decimal Compute(
        IReadOnlyList<(DateOnly FixingDate, decimal Rate)> dailyFixings,
        IYearFractionCalculator dayCount,
        int daysPerYear = 360)
    {
        ArgumentNullException.ThrowIfNull(dailyFixings);
        ArgumentNullException.ThrowIfNull(dayCount);
        ValidateFixings(dailyFixings, daysPerYear);

        if (dailyFixings.Count == 0)
        {
            return 0m;
        }

        decimal product = 1m;
        foreach (var (fixingDate, rate) in dailyFixings)
        {
            // Each overnight period is from fixingDate to fixingDate + 1 calendar day
            DateOnly nextDay = fixingDate.AddDays(1);
            decimal accrualFraction = (decimal)dayCount.YearFraction(fixingDate, nextDay);
            product *= 1m + rate * accrualFraction;
        }

        // Total calendar days from first fixing to day after last fixing
        DateOnly firstDate = dailyFixings[0].FixingDate;
        DateOnly lastDayAfterLast = dailyFixings[dailyFixings.Count - 1].FixingDate.AddDays(1);
        int totalDays = lastDayAfterLast.DayNumber - firstDate.DayNumber;

        if (totalDays <= 0)
        {
            return 0m;
        }

        return (product - 1m) * (decimal)daysPerYear / totalDays;
    }

    /// <summary>
    /// Computes the compounded-in-arrears rate with a lookback shift on observation dates.
    /// </summary>
    /// <remarks>
    /// The lookback convention shifts the observation date back by
    /// <paramref name="lookbackBusinessDays"/> business days relative to each accrual day.
    /// With a lookback of 0, this method produces exactly the same result as <see cref="Compute"/>.
    /// </remarks>
    /// <param name="dailyFixings">
    /// Ordered list of (FixingDate, Rate) pairs representing the available overnight fixings.
    /// </param>
    /// <param name="dayCount">Day-count calculator for overnight accrual fractions.</param>
    /// <param name="lookbackBusinessDays">
    /// Number of business days to look back when selecting the rate for each accrual day.
    /// Must be non-negative.
    /// </param>
    /// <param name="calendar">Business calendar used for lookback business day shifts.</param>
    /// <param name="daysPerYear">
    /// Convention for annualisation denominator. Typically 360 for USD/EUR, 365 for GBP.
    /// </param>
    /// <returns>Annualised compounded rate with lookback applied, expressed in decimal form.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="dailyFixings"/>, <paramref name="dayCount"/>, or
    /// <paramref name="calendar"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="lookbackBusinessDays"/> is negative or <paramref name="daysPerYear"/> is not positive.
    /// </exception>
    /// <exception cref="ArgumentException">Fixing dates are not strictly increasing.</exception>
    public static decimal ComputeWithLookback(
        IReadOnlyList<(DateOnly FixingDate, decimal Rate)> dailyFixings,
        IYearFractionCalculator dayCount,
        int lookbackBusinessDays,
        IBusinessCalendar calendar,
        int daysPerYear = 360)
    {
        ArgumentNullException.ThrowIfNull(dailyFixings);
        ArgumentNullException.ThrowIfNull(dayCount);
        ArgumentNullException.ThrowIfNull(calendar);

        if (lookbackBusinessDays < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lookbackBusinessDays),
                "Lookback business days must be non-negative.");
        }

        if (lookbackBusinessDays == 0)
        {
            // Lookback=0 is identical to plain Compute
            return Compute(dailyFixings, dayCount, daysPerYear);
        }

        ValidateFixings(dailyFixings, daysPerYear);
        if (dailyFixings.Count == 0)
        {
            return 0m;
        }

        // Build a lookup from fixing date to rate
        var rateByDate = new Dictionary<DateOnly, decimal>(dailyFixings.Count);
        foreach (var (d, r) in dailyFixings)
        {
            rateByDate[d] = r;
        }

        decimal product = 1m;
        foreach (var (accrualDate, _) in dailyFixings)
        {
            // Shift the observation date back by lookbackBusinessDays business days
            DateOnly observationDate = accrualDate;
            for (int shift = 0; shift < lookbackBusinessDays; shift++)
            {
                do
                {
                    observationDate = observationDate.AddDays(-1);
                }
                while (!calendar.IsBusinessDay(observationDate));
            }

            // Look up the rate for the (shifted) observation date; fall back to accrual rate
            if (!rateByDate.TryGetValue(observationDate, out decimal rate))
            {
                rateByDate.TryGetValue(accrualDate, out rate);
            }

            DateOnly nextDay = accrualDate.AddDays(1);
            decimal accrualFraction = (decimal)dayCount.YearFraction(accrualDate, nextDay);
            product *= 1m + rate * accrualFraction;
        }

        DateOnly firstDate = dailyFixings[0].FixingDate;
        DateOnly lastDayAfterLast = dailyFixings[dailyFixings.Count - 1].FixingDate.AddDays(1);
        int totalDays = lastDayAfterLast.DayNumber - firstDate.DayNumber;

        if (totalDays <= 0)
        {
            return 0m;
        }

        return (product - 1m) * (decimal)daysPerYear / totalDays;
    }
    private static void ValidateFixings(IReadOnlyList<(DateOnly FixingDate, decimal Rate)> dailyFixings, int daysPerYear)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);
        for (int i = 1; i < dailyFixings.Count; i++)
        {
            if (dailyFixings[i].FixingDate <= dailyFixings[i - 1].FixingDate)
            {
                throw new ArgumentException("Fixing dates must be strictly increasing.", nameof(dailyFixings));
            }
        }
    }

}
