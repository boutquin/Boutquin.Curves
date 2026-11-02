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
    public void Shifted_AndLookback_AreDeliberatelyEquivalentInV1()
    {
        // D-1: v1 treats Shifted and Lookback as byte-identical for per-date computation. This is
        // a DELIBERATE, DOCUMENTED equivalence
        // — ISDA's 2021 Definitions distinguish them (Lookback also shifts which day-count fraction
        // the rate applies to; Shifted does not), but modelling that accrual-side distinction is a
        // pricing-model change, out of scope for this hardening pass. This test pins the current
        // v1 contract explicitly, rather than letting it hold silently through two similar-looking
        // but independently-authored test cases: if this assertion ever fails, either the
        // deliberate equivalence was broken unintentionally (investigate), or the ISDA distinction
        // was implemented on purpose (this test must be updated together with the <remarks> above
        // and the spec that authorized the divergence).
        var accrualDate = new DateOnly(2026, 4, 8);
        const int shiftDays = 3;

        DateOnly shifted = ObservationShiftHelper.ComputeObservationDate(
            accrualDate, ObservationShiftConvention.Shifted, s_calendar, shiftDays);
        DateOnly lookback = ObservationShiftHelper.ComputeObservationDate(
            accrualDate, ObservationShiftConvention.Lookback, s_calendar, shiftDays);

        shifted.Should().Be(lookback, "v1 deliberately treats Shifted and Lookback identically for per-date computation (see ComputeObservationDate remarks)");
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
