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

namespace Boutquin.Curves.Conventions.Calendars.Schedule;

/// <summary>
/// Generates business-day-adjusted payment schedules from frequency and calendar rules.
/// </summary>
public sealed class BusinessScheduleGenerator
{
    /// <summary>
    /// Generates schedule periods between two dates using frequency and business-day rules.
    /// </summary>
    /// <param name="startDate">Unadjusted start date of the first accrual period.</param>
    /// <param name="endDate">Unadjusted terminal date of the schedule.</param>
    /// <param name="frequency">Coupon frequency used to step through accrual periods.</param>
    /// <param name="calendar">Calendar used to adjust period-end and payment dates.</param>
    /// <param name="adjustment">Business-day rule applied to generated period-end dates.</param>
    /// <param name="paymentLagBusinessDays">Business-day lag from adjusted period end to payment date.</param>
    /// <returns>Ordered schedule periods from <paramref name="startDate"/> to <paramref name="endDate"/>.</returns>
    public IReadOnlyList<SchedulePeriod> Generate(
        DateOnly startDate,
        DateOnly endDate,
        PaymentFrequency frequency,
        IBusinessCalendar calendar,
        BusinessDayAdjustment adjustment,
        int paymentLagBusinessDays = 0)
    {
        if (endDate <= startDate)
        {
            return Array.Empty<SchedulePeriod>();
        }

        int months = frequency switch
        {
            PaymentFrequency.Monthly => 1,
            PaymentFrequency.Quarterly => 3,
            PaymentFrequency.SemiAnnual => 6,
            PaymentFrequency.Annual => 12,
            PaymentFrequency.Term => 0,
            _ => 0
        };

        if (months == 0)
        {
            DateOnly paymentDate = calendar.Advance(calendar.Adjust(endDate, adjustment), paymentLagBusinessDays);
            return new[] { new SchedulePeriod(startDate, endDate, paymentDate) };
        }

        List<SchedulePeriod> periods = new();
        DateOnly periodStart = startDate;
        while (periodStart < endDate)
        {
            DateOnly unadjustedEnd = periodStart.AddMonths(months);
            DateOnly periodEnd = unadjustedEnd < endDate ? unadjustedEnd : endDate;
            DateOnly adjustedEnd = calendar.Adjust(periodEnd, adjustment);
            DateOnly paymentDate = calendar.Advance(adjustedEnd, paymentLagBusinessDays);
            periods.Add(new SchedulePeriod(periodStart, adjustedEnd, paymentDate));
            periodStart = adjustedEnd;
        }

        return periods;
    }
}
