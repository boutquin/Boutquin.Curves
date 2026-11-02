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

using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Conventions.Tests;

public sealed class YearFractionCalculatorTests
{
    [Fact]
    public void Actual365Fixed_ShouldComputeExpectedYearFraction()
    {
        var calculator = Actual365Fixed.Instance;

        double yearFraction = calculator.YearFraction(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 1));

        yearFraction.Should().BeApproximately(1d, 1e-12);
    }

    // F-21: parametrised Theory tests cover all five day-count conventions supported by
    // DayCountResolver plus the most common alias spellings. Expected values are hand-computed
    // from the ISDA 2006 Definitions so this is a pure known-answer test, not a round-trip.
    [Theory]
    // ACT/360 — actual days divided by fixed 360 denominator
    [InlineData("ACT/360", 2026, 1, 1, 2026, 7, 1, 181.0 / 360.0)]  // 31+28+31+30+31+30 = 181 actual days
    [InlineData("ACTUAL/360", 2026, 1, 1, 2027, 1, 1, 365.0 / 360.0)]  // alias; full non-leap year
    // ACT/365F — actual days divided by fixed 365 denominator
    [InlineData("ACT/365F", 2026, 1, 1, 2027, 1, 1, 365.0 / 365.0)]  // = 1.0 for non-leap year
    [InlineData("ACT/365", 2026, 1, 1, 2026, 4, 1, 90.0 / 365.0)]  // alias; Jan+Feb+Mar = 90 days
    // 30/360 Bond Basis — each month = 30, each year = 360; D2 capped only when capped D1 = 30
    [InlineData("30/360", 2026, 1, 1, 2026, 7, 1, 180.0 / 360.0)]  // 6 months × 30 = 180
    [InlineData("30/360", 2026, 1, 1, 2027, 1, 1, 360.0 / 360.0)]  // full year = 1.0
    [InlineData("30/360", 2026, 1, 15, 2026, 7, 31, 196.0 / 360.0)]  // D1=15 < 30 → D2 kept at 31; 180+16 = 196
    // 30E/360 Eurobond — D2 capped at 30 unconditionally, unlike Bond Basis
    [InlineData("30E/360", 2026, 1, 15, 2026, 7, 31, 195.0 / 360.0)]  // D2 capped: 180+15 = 195 (≠ 196 above)
    [InlineData("30E/360", 2026, 1, 31, 2026, 7, 31, 180.0 / 360.0)]  // both ends capped to 30; net = 0
    // ACT/ACT ICMA — 2-arg fallback: days / 365.25
    [InlineData("ACT/ACT", 2026, 1, 1, 2027, 1, 1, 365.0 / 365.25)] // 365 actual days, denominator 365.25
    public void DayCountResolver_ShouldReturnKnownYearFraction(
        string code,
        int startYear, int startMonth, int startDay,
        int endYear, int endMonth, int endDay,
        double expected)
    {
        IYearFractionCalculator calculator = DayCountResolver.Resolve(code);
        DateOnly start = new(startYear, startMonth, startDay);
        DateOnly end = new(endYear, endMonth, endDay);

        double actual = calculator.YearFraction(start, end);

        actual.Should().BeApproximately(expected, 1e-12);
    }
}
