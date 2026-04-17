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

using Boutquin.Curves.Conventions.Calendars;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Conventions.Tests.Calendars;

/// <summary>
/// Tests for <see cref="ObservationShiftHelper"/> covering all observation shift conventions.
/// </summary>
public sealed class ObservationShiftHelperTests
{
    private static readonly WeekendOnlyCalendar s_calendar = new();

    [Fact]
    public void None_ReturnsOriginalDate()
    {
        // Arrange
        var accrualDate = new DateOnly(2026, 4, 8); // Wednesday

        // Act
        var result = ObservationShiftHelper.ComputeObservationDate(
            accrualDate, ObservationShiftConvention.None, s_calendar, shiftDays: 2);

        // Assert — None convention ignores shiftDays
        result.Should().Be(accrualDate);
    }

    [Fact]
    public void Shifted_AdvancesBackByShiftDays()
    {
        // Arrange — Wednesday Apr 8, shift back 2 business days = Monday Apr 6
        var accrualDate = new DateOnly(2026, 4, 8);

        // Act
        var result = ObservationShiftHelper.ComputeObservationDate(
            accrualDate, ObservationShiftConvention.Shifted, s_calendar, shiftDays: 2);

        // Assert
        result.Should().Be(new DateOnly(2026, 4, 6)); // Monday
    }

    [Fact]
    public void Shifted_CrossesWeekend()
    {
        // Arrange — Monday Apr 6, shift back 2 business days = Thu Apr 2
        var accrualDate = new DateOnly(2026, 4, 6);

        // Act
        var result = ObservationShiftHelper.ComputeObservationDate(
            accrualDate, ObservationShiftConvention.Shifted, s_calendar, shiftDays: 2);

        // Assert — crosses weekend to Thursday
        result.Should().Be(new DateOnly(2026, 4, 2));
    }

    [Fact]
    public void Lookback_AdvancesBackByShiftDays()
    {
        // Arrange — same behavior as Shifted for per-date computation
        var accrualDate = new DateOnly(2026, 4, 8); // Wednesday

        // Act
        var result = ObservationShiftHelper.ComputeObservationDate(
            accrualDate, ObservationShiftConvention.Lookback, s_calendar, shiftDays: 3);

        // Assert — 3 business days back from Wed Apr 8: Tue 7, Mon 6, Fri 3 = Apr 3
        result.Should().Be(new DateOnly(2026, 4, 3));
    }

    [Fact]
    public void Lockout_ReturnsOriginalDate()
    {
        // Arrange
        var accrualDate = new DateOnly(2026, 4, 8);

        // Act
        var result = ObservationShiftHelper.ComputeObservationDate(
            accrualDate, ObservationShiftConvention.Lockout, s_calendar, shiftDays: 5);

        // Assert — Lockout is a period-level freeze, not a per-date shift
        result.Should().Be(accrualDate);
    }

    [Fact]
    public void Shifted_WithZeroShiftDays_ReturnsOriginalDate()
    {
        // Arrange
        var accrualDate = new DateOnly(2026, 4, 8);

        // Act
        var result = ObservationShiftHelper.ComputeObservationDate(
            accrualDate, ObservationShiftConvention.Shifted, s_calendar, shiftDays: 0);

        // Assert — 0 business days = no movement
        result.Should().Be(accrualDate);
    }

    [Fact]
    public void Shifted_ThrowsOnNullCalendar()
    {
        // Arrange
        var accrualDate = new DateOnly(2026, 4, 8);

        // Act
        var act = () => ObservationShiftHelper.ComputeObservationDate(
            accrualDate, ObservationShiftConvention.Shifted, null!, shiftDays: 2);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
