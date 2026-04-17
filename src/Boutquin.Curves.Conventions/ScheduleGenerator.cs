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
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Conventions;

/// <summary>
/// Generates forward accrual and payment schedules from convention metadata.
/// </summary>
/// <remarks>
/// Builds accrual period schedules from swap conventions: generates period boundaries by
/// applying roll conventions, business day adjustments, and stub handling. Schedule
/// generation is critical for swap pricing because it determines the exact dates used
/// for year-fraction calculations and cash flow timing. Only forward generation is
/// supported; backward stub periods are not produced. When the final unadjusted period
/// extends past <c>endDate</c>, it is clamped to produce a short front or back stub
/// rather than rolling beyond maturity.
/// </remarks>
public static class ScheduleGenerator
{
    /// <summary>
    /// Generates a forward schedule from <paramref name="startDate"/> to <paramref name="endDate"/>.
    /// </summary>
    /// <param name="startDate">Unadjusted schedule start date.</param>
    /// <param name="endDate">Unadjusted schedule end date.</param>
    /// <param name="frequency">Accrual/payment frequency.</param>
    /// <param name="calendar">Business calendar used for date adjustment and lag advancement.</param>
    /// <param name="businessDayAdjustment">Business-day adjustment applied to period boundaries.</param>
    /// <param name="yearFractionCalculator">Day-count engine used for accrual factors.</param>
    /// <param name="paymentLagBusinessDays">Lag in business days from period end to payment date.</param>
    /// <param name="endOfMonth">Whether generated coupon dates should preserve end-of-month semantics.</param>
    /// <returns>Generated schedule periods in chronological order; empty when <paramref name="endDate"/> is on or before <paramref name="startDate"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="frequency"/> is not a recognised value.</exception>
    /// <remarks>
    /// When <paramref name="endOfMonth"/> is <c>true</c> and <paramref name="startDate"/> falls on the last day
    /// of its month, generated period boundaries are rolled to the last day of each target month.
    /// Year fractions are computed over unadjusted accrual dates, not business-day adjusted dates.
    /// </remarks>
    public static IReadOnlyList<SchedulePeriod> Generate(
        DateOnly startDate,
        DateOnly endDate,
        PaymentFrequency frequency,
        IBusinessCalendar calendar,
        BusinessDayAdjustment businessDayAdjustment,
        IYearFractionCalculator yearFractionCalculator,
        int paymentLagBusinessDays = 0,
        bool endOfMonth = false)
    {
        if (endDate <= startDate)
        {
            return Array.Empty<SchedulePeriod>();
        }

        List<SchedulePeriod> periods = new();
        DateOnly accrualStart = startDate;
        // Preserve EOM anchor across all generated periods when the start date is month-end.
        bool anchorIsEndOfMonth = endOfMonth && IsEndOfMonth(startDate);

        while (accrualStart < endDate)
        {
            DateOnly unadjustedEnd = frequency == PaymentFrequency.Term
                ? endDate
                : AddByFrequency(accrualStart, frequency, anchorIsEndOfMonth);

            // Clamp final stub period to maturity rather than rolling past it.
            if (unadjustedEnd > endDate)
            {
                unadjustedEnd = endDate;
            }

            DateOnly periodStart = calendar.Adjust(accrualStart, businessDayAdjustment);
            DateOnly periodEnd = calendar.Adjust(unadjustedEnd, businessDayAdjustment);
            DateOnly paymentBase = paymentLagBusinessDays == 0
                ? periodEnd
                : calendar.Advance(periodEnd, paymentLagBusinessDays);
            DateOnly paymentDate = calendar.Adjust(paymentBase, businessDayAdjustment);

            periods.Add(new SchedulePeriod(
                accrualStart,
                unadjustedEnd,
                periodStart,
                periodEnd,
                paymentDate,
                yearFractionCalculator.YearFraction(accrualStart, unadjustedEnd)));

            // Terminal: last period consumed exactly at maturity.
            if (unadjustedEnd == endDate)
            {
                break;
            }

            accrualStart = unadjustedEnd;
        }

        return periods;
    }

    private static DateOnly AddByFrequency(DateOnly date, PaymentFrequency frequency, bool anchorIsEndOfMonth)
    {
        DateOnly result = frequency switch
        {
            PaymentFrequency.Daily => date.AddDays(1),
            PaymentFrequency.Weekly => date.AddDays(7),
            PaymentFrequency.Monthly => date.AddMonths(1),
            PaymentFrequency.Quarterly => date.AddMonths(3),
            PaymentFrequency.SemiAnnual => date.AddMonths(6),
            PaymentFrequency.Annual => date.AddYears(1),
            PaymentFrequency.Term => date,
            _ => throw new NotSupportedException($"Unsupported payment frequency: {frequency}.")
        };

        if (!anchorIsEndOfMonth)
        {
            return result;
        }

        int lastDay = DateTime.DaysInMonth(result.Year, result.Month);
        return new DateOnly(result.Year, result.Month, lastDay);
    }

    private static bool IsEndOfMonth(DateOnly date)
    {
        return date.Day == DateTime.DaysInMonth(date.Year, date.Month);
    }
}
