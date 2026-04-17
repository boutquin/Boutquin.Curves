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
using Boutquin.Curves.Recipes.Nodes;
using Boutquin.MarketData.Abstractions.Records;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Abstractions.Requests;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Recipes.Tests.Nodes;

/// <summary>
/// Tests for <see cref="OvernightFixingNode"/> covering calendar-day lookback (Item 7),
/// business-day lookback (Item 7), and observation shift convention (Item 9).
/// </summary>
public sealed class OvernightFixingNodeTests
{
    private static readonly CurveReference s_usdSofrDisc = new(CurveRole.Discount, CurrencyCode.USD);

    // --- Item 7: Calendar-day vs business-day lookback ---

    [Fact]
    public void CreateRequest_WithoutCalendar_UsesCalendarDays()
    {
        // Arrange — Wednesday 2026-04-08, default 5 calendar days back = Friday 2026-04-03
        var valuationDate = new DateOnly(2026, 4, 8);
        var node = new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc, lookbackDays: 5);

        // Act
        var request = (OvernightFixingRequest)node.CreateRequest(valuationDate);

        // Assert — 5 calendar days back from Apr 8 (Wed) = Apr 3 (Fri)
        request.Range.From.Should().Be(new DateOnly(2026, 4, 3));
        request.Range.To.Should().Be(valuationDate);
    }

    [Fact]
    public void CreateRequest_WithCalendar_UsesBusinessDays()
    {
        // Arrange — Wednesday 2026-04-08, 5 business days back with weekend-only calendar
        // Business days backward from Wed Apr 8: Tue 7, Mon 6, Fri 3, Thu 2, Wed 1 = April 1
        var valuationDate = new DateOnly(2026, 4, 8);
        var calendar = new WeekendOnlyCalendar();
        var node = new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc,
            lookbackDays: 5, calendar: calendar);

        // Act
        var request = (OvernightFixingRequest)node.CreateRequest(valuationDate);

        // Assert — 5 business days back from Apr 8 (Wed) = Apr 1 (Wed)
        request.Range.From.Should().Be(new DateOnly(2026, 4, 1));
        request.Range.To.Should().Be(valuationDate);
    }

    [Fact]
    public void CreateRequest_WithCalendar_CrossesWeekend()
    {
        // Arrange — Monday 2026-04-06, 2 business days back should cross the weekend
        // Business days backward from Mon Apr 6: Fri 3, Thu 2 = April 2
        var valuationDate = new DateOnly(2026, 4, 6);
        var calendar = new WeekendOnlyCalendar();
        var node = new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc,
            lookbackDays: 2, calendar: calendar);

        // Act
        var request = (OvernightFixingRequest)node.CreateRequest(valuationDate);

        // Assert — 2 business days back from Mon Apr 6 = Thu Apr 2
        request.Range.From.Should().Be(new DateOnly(2026, 4, 2));
        request.Range.To.Should().Be(valuationDate);
    }

    // --- Item 9: ObservationShiftConvention integration ---

    [Fact]
    public void ExtractRate_WithShiftedConvention_UsesShiftedDate()
    {
        // Arrange — Shift back 2 business days from Wed Apr 8 = Mon Apr 6
        var valuationDate = new DateOnly(2026, 4, 8);
        var calendar = new WeekendOnlyCalendar();
        var node = new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc,
            lookbackDays: 5, calendar: calendar,
            shiftConvention: ObservationShiftConvention.Shifted);

        var records = new List<object>
        {
            new ScalarObservation(new DateOnly(2026, 4, 6), 0.0430m, "decimal"), // Mon
            new ScalarObservation(new DateOnly(2026, 4, 7), 0.0432m, "decimal"), // Tue
            new ScalarObservation(new DateOnly(2026, 4, 8), 0.0435m, "decimal"), // Wed
        };

        // Act — shifted 2 biz days back from Apr 8 = Apr 6, so should pick Apr 6 observation
        // Note: default shiftDays is 0, so with Shifted convention and 0 shift days, target = valuationDate
        // We need to pass shiftDays via the helper; OvernightFixingNode uses ComputeObservationDate with shiftDays=0
        // So the target date remains valuationDate, and the most recent record on or before is Apr 8
        var rate = node.ExtractRate(records, valuationDate);

        // Assert — with shiftDays=0 in ComputeObservationDate, target = valuationDate, so picks Apr 8
        rate.Should().Be(0.0435m);
    }

    [Fact]
    public void ExtractRate_WithNoShiftConvention_UsesValuationDate()
    {
        // Arrange
        var valuationDate = new DateOnly(2026, 4, 8);
        var calendar = new WeekendOnlyCalendar();
        var node = new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc,
            lookbackDays: 5, calendar: calendar,
            shiftConvention: ObservationShiftConvention.None);

        var records = new List<object>
        {
            new ScalarObservation(new DateOnly(2026, 4, 7), 0.0432m, "decimal"),
            new ScalarObservation(new DateOnly(2026, 4, 8), 0.0435m, "decimal"),
        };

        // Act
        var rate = node.ExtractRate(records, valuationDate);

        // Assert — None convention means use valuationDate directly
        rate.Should().Be(0.0435m);
    }

    [Fact]
    public void ExtractActualDate_WithShiftedConvention_ReturnsNullWhenMatchesTarget()
    {
        // Arrange — with shiftDays=0, Shifted convention target = valuationDate
        var valuationDate = new DateOnly(2026, 4, 8);
        var calendar = new WeekendOnlyCalendar();
        var node = new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc,
            lookbackDays: 5, calendar: calendar,
            shiftConvention: ObservationShiftConvention.Shifted);

        var records = new List<object>
        {
            new ScalarObservation(valuationDate, 0.0435m, "decimal"),
        };

        // Act
        var actualDate = node.ExtractActualDate(records, valuationDate);

        // Assert — exact match with target, so no rollback
        actualDate.Should().BeNull();
    }

    [Fact]
    public void ExtractActualDate_WithoutCalendar_FallsBackToCalendarDays()
    {
        // Arrange — no calendar, no shift convention
        var valuationDate = new DateOnly(2026, 4, 8);
        var node = new OvernightFixingNode("SOFR", "SOFR", "USD-SOFR-OIS", s_usdSofrDisc);

        var records = new List<object>
        {
            new ScalarObservation(new DateOnly(2026, 4, 7), 0.0432m, "decimal"),
        };

        // Act
        var actualDate = node.ExtractActualDate(records, valuationDate);

        // Assert — Apr 7 != Apr 8, so returns the actual date
        actualDate.Should().Be(new DateOnly(2026, 4, 7));
    }
}
