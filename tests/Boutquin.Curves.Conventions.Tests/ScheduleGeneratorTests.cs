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
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Conventions.Tests;

public sealed class ScheduleGeneratorTests
{
    [Theory]
    [InlineData(2026, 1, 1, 2026, 1, 1)]    // equal  (endDate == startDate → zero-length)
    [InlineData(2026, 3, 1, 2026, 1, 1)]    // before (endDate < startDate → negative span)
    public void Generate_ShouldThrowArgumentOutOfRangeException_WhenEndDateNotAfterStartDate(
        int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay)
    {
        WeekendOnlyCalendar calendar = new("USNY");
        DateOnly startDate = new(startYear, startMonth, startDay);
        DateOnly endDate = new(endYear, endMonth, endDay);

        Action act = () => ScheduleGenerator.Generate(
            startDate,
            endDate,
            PaymentFrequency.Quarterly,
            calendar,
            BusinessDayAdjustment.ModifiedFollowing,
            Thirty360.Instance);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("endDate");
    }

    [Fact]
    public void Generate_ShouldCreateQuarterlyPeriods_WithBusinessDayAdjustmentAndYearFractions()
    {
        WeekendOnlyCalendar calendar = new("USNY");

        IReadOnlyList<SchedulePeriod> periods = ScheduleGenerator.Generate(
            new DateOnly(2026, 1, 31),
            new DateOnly(2026, 7, 31),
            PaymentFrequency.Quarterly,
            calendar,
            BusinessDayAdjustment.ModifiedFollowing,
            Thirty360.Instance,
            paymentLagBusinessDays: 2,
            endOfMonth: true);

        periods.Should().HaveCount(2);
        periods[0].AccrualStartDate.Should().Be(new DateOnly(2026, 1, 31));
        periods[0].AccrualEndDate.Should().Be(new DateOnly(2026, 4, 30));
        periods[0].PaymentDate.DayOfWeek.Should().NotBe(DayOfWeek.Saturday);
        periods[0].PaymentDate.DayOfWeek.Should().NotBe(DayOfWeek.Sunday);
        periods[0].YearFraction.Should().BeApproximately(0.25d, 1e-12);

        periods[1].AccrualEndDate.Should().Be(new DateOnly(2026, 7, 31));
        periods[1].YearFraction.Should().BeApproximately(0.25d, 1e-12);
    }
    [Theory]
    [InlineData(2025, 30, false)]
    [InlineData(2024, 30, false)]

    [InlineData(2025, 30, true)]
    public void Generate_MonthlySchedule_PreservesOriginalDayAfterShortMonth(int year, int day, bool endOfMonth)
    {
        DateOnly start = new(year, 1, day);
        var periods = ScheduleGenerator.Generate(start, new DateOnly(year, 4, day), PaymentFrequency.Monthly,
            new WeekendOnlyCalendar(), BusinessDayAdjustment.None, Actual365Fixed.Instance, endOfMonth: endOfMonth);
        periods.Should().HaveCount(3);
        periods[1].AccrualEndDate.Should().Be(new DateOnly(year, 3, day));
        periods[2].AccrualEndDate.Should().Be(new DateOnly(year, 4, day));
    }

    [Theory]
    [InlineData(PaymentFrequency.Daily, 1)]
    [InlineData(PaymentFrequency.Weekly, 7)]
    public void Generate_EndOfMonthFlag_DoesNotChangeDayOrWeekIntervals(PaymentFrequency frequency, int days)
    {
        DateOnly start = new(2025, 1, 31);
        var periods = ScheduleGenerator.Generate(start, start.AddDays(3 * days), frequency, new WeekendOnlyCalendar(),
            BusinessDayAdjustment.None, Actual365Fixed.Instance, endOfMonth: true);
        periods.Should().HaveCount(3);
        periods.Select(period => period.AccrualEndDate.DayNumber - period.AccrualStartDate.DayNumber)
            .Should().OnlyContain(length => length == days);
    }

}
