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
}
