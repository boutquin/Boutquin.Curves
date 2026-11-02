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
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Conventions.Tests;

/// <summary>
/// Tests for <see cref="CompoundedInArrearsRate"/> (Hull Ch 4 p.100 formula).
/// </summary>
public sealed class CompoundedInArrearsRateTests
{
    /// <summary>
    /// AC-C3: Compute on a 5-day SOFR window reproduces Hull Ch 4 p.100 formula.
    /// Uses flat 5% overnight rate, ACT/360, 5 daily fixings (Mon–Fri).
    /// Expected: [(1 + 0.05/360)^5 - 1] * 360/5
    /// </summary>
    [Fact]
    public void Compute_FlatRate_ReproducesHullFormula()
    {
        var startDate = new DateOnly(2026, 1, 5); // Monday
        var fixings = new List<(DateOnly FixingDate, decimal Rate)>();
        for (int i = 0; i < 5; i++)
        {
            fixings.Add((startDate.AddDays(i), 0.05m));
        }

        var dayCount = Actual360.Instance;
        decimal result = CompoundedInArrearsRate.Compute(fixings, dayCount, daysPerYear: 360);

        // Expected from Hull: R = [(1 + r_i * d_i)^n - 1] * (daysPerYear / D)
        // Each d_i = 1/360 (1 calendar day, ACT/360)
        // D = 5 (total calendar days from first fixing to day after last)
        // R = [(1 + 0.05 * (1/360))^5 - 1] * (360/5)
        double d_i = 1.0 / 360.0;
        double product = Math.Pow(1.0 + 0.05 * d_i, 5) - 1.0;
        double expected = product * (360.0 / 5.0);

        ((double)result).Should().BeApproximately(expected, 1e-10,
            "CompoundedInArrearsRate.Compute must reproduce Hull Ch 4 p.100 formula");
    }

    /// <summary>
    /// AC-C4: ComputeWithLookback with lookback=0 must equal plain Compute exactly.
    /// </summary>
    [Fact]
    public void ComputeWithLookback_ZeroLookback_EqualsCompute()
    {
        var startDate = new DateOnly(2026, 3, 2); // Monday
        var fixings = new List<(DateOnly FixingDate, decimal Rate)>
        {
            (startDate, 0.053m),
            (startDate.AddDays(1), 0.0525m),
            (startDate.AddDays(2), 0.054m),
            (startDate.AddDays(3), 0.0535m),
            (startDate.AddDays(4), 0.052m),
        };

        var dayCount = Actual360.Instance;
        var calendar = new WeekendOnlyCalendar("USD");

        decimal plain = CompoundedInArrearsRate.Compute(fixings, dayCount, daysPerYear: 360);
        decimal withLookback = CompoundedInArrearsRate.ComputeWithLookback(
            fixings, dayCount, lookbackBusinessDays: 0, calendar, daysPerYear: 360);

        withLookback.Should().Be(plain,
            "ComputeWithLookback with lookback=0 must equal plain Compute");
    }

    [Fact]
    public void Compute_SingleFixing_ReturnsApproximatelyFlatRate()
    {
        var fixings = new List<(DateOnly FixingDate, decimal Rate)>
        {
            (new DateOnly(2026, 1, 5), 0.04m),
        };

        var dayCount = Actual360.Instance;
        decimal result = CompoundedInArrearsRate.Compute(fixings, dayCount, daysPerYear: 360);

        // Single day: R = [(1 + 0.04/360) - 1] * (360/1) = 0.04/360 * 360 = 0.04
        ((double)result).Should().BeApproximately(0.04, 1e-10,
            "single-day compounding should return exactly the flat rate");
    }

    [Fact]
    public void Compute_EmptyFixings_ReturnsZero()
    {
        var fixings = new List<(DateOnly FixingDate, decimal Rate)>();
        var dayCount = Actual360.Instance;

        decimal result = CompoundedInArrearsRate.Compute(fixings, dayCount, daysPerYear: 360);

        result.Should().Be(0m, "empty fixings should return zero");
    }

    /// <summary>
    /// B-3 (round-1 F-L4-04 C7): only the lookback=0 path was tested
    /// (<see cref="ComputeWithLookback_ZeroLookback_EqualsCompute"/>), which short-circuits to
    /// <see cref="CompoundedInArrearsRate.Compute"/> without exercising the observation-date
    /// shift loop at all. This test uses a genuinely positive lookback (1 business day) with
    /// fixings that differ day-to-day, so the shifted-observation-date rate selection actually
    /// changes the compounded result relative to the unshifted (lookback=0) computation —
    /// proving the lookback path is live, not merely reachable.
    /// </summary>
    [Fact]
    public void ComputeWithLookback_PositiveLookback_ShiftsObservationDateAndChangesResult()
    {
        var startDate = new DateOnly(2026, 3, 2); // Monday
        var fixings = new List<(DateOnly FixingDate, decimal Rate)>
        {
            (startDate, 0.050m),               // Mon
            (startDate.AddDays(1), 0.060m),     // Tue
            (startDate.AddDays(2), 0.070m),     // Wed
            (startDate.AddDays(3), 0.080m),     // Thu
            (startDate.AddDays(4), 0.090m),     // Fri
        };

        var dayCount = Actual360.Instance;
        var calendar = new WeekendOnlyCalendar("USD");

        decimal plain = CompoundedInArrearsRate.Compute(fixings, dayCount, daysPerYear: 360);
        decimal withLookback = CompoundedInArrearsRate.ComputeWithLookback(
            fixings, dayCount, lookbackBusinessDays: 1, calendar, daysPerYear: 360);

        // With a 1-business-day lookback, each accrual day's rate is drawn from the PRECEDING
        // business day's fixing instead of its own. Because the fixings strictly increase day
        // over day, shifting every observation one day earlier strictly lowers every rate used
        // in the compounding product (except the first day, which has no preceding fixing in
        // this window and falls back to its own accrual-date rate per the documented fallback).
        // The shifted product must therefore differ from the unshifted (lookback=0) result.
        withLookback.Should().NotBe(plain,
            "a positive lookback must shift the observation date and select a different fixing, " +
            "changing the compounded rate relative to the unshifted computation");

        // The strictly-increasing fixture means every shifted (later-day) rate is LOWER than the
        // corresponding unshifted rate, so the lookback-adjusted compounded rate must be lower too.
        withLookback.Should().BeLessThan(plain,
            "shifting observation back by 1 business day pulls in strictly lower preceding-day " +
            "rates from this monotonically increasing fixture, lowering the compounded result");
    }
    [Theory]
    [InlineData(0)]
    [InlineData(-360)]
    public void Compute_NonPositiveAnnualization_Rejects(int daysPerYear)
    {
        (DateOnly, decimal)[] fixings = [(new DateOnly(2026, 1, 5), 0.05m)];
        Action plain = () => CompoundedInArrearsRate.Compute(fixings, Actual360.Instance, daysPerYear);
        Action lookback = () => CompoundedInArrearsRate.ComputeWithLookback(fixings, Actual360.Instance, 1,
            new WeekendOnlyCalendar(), daysPerYear);
        plain.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(daysPerYear));
        lookback.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(daysPerYear));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Compute_UnorderedOrDuplicateFixings_Rejects(int secondDayOffset)
    {
        DateOnly first = new(2026, 1, 5);
        (DateOnly, decimal)[] fixings = [(first, 0.05m), (first.AddDays(secondDayOffset), 0.06m)];
        Action plain = () => CompoundedInArrearsRate.Compute(fixings, Actual360.Instance);
        Action lookback = () => CompoundedInArrearsRate.ComputeWithLookback(fixings, Actual360.Instance, 1,
            new WeekendOnlyCalendar());
        plain.Should().Throw<ArgumentException>().WithParameterName("dailyFixings");
        lookback.Should().Throw<ArgumentException>().WithParameterName("dailyFixings");
    }

    [Fact]
    public void Compute_VaryingRates_AgreesWithIndependentProductOracle()
    {
        DateOnly start = new(2024, 2, 23);
        for (int sample = 0; sample < 64; sample++)
        {
            var fixings = new List<(DateOnly FixingDate, decimal Rate)>();
            double factor = 1d;
            int count = sample % 10 + 1;
            for (int day = 0; day < count; day++)
            {
                decimal rate = ((sample * 17 + day * 13) % 111 - 30) / 1000m;
                fixings.Add((start.AddDays(day), rate));
                factor *= 1d + (double)rate / 360d;
            }
            double expected = (factor - 1d) * 360d / count;
            ((double)CompoundedInArrearsRate.Compute(fixings, Actual360.Instance))
                .Should().BeApproximately(expected, 1e-10, $"independent Hull product sample {sample}");
        }
    }

    [Fact]
    public void ObservationShift_UndefinedConvention_RejectsInsteadOfSilentlyIgnoring()
    {
        Action act = () => ObservationShiftHelper.ComputeObservationDate(new DateOnly(2026, 1, 5),
            (ObservationShiftConvention)99, new WeekendOnlyCalendar(), 2);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("convention");
    }

}
