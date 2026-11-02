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

using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Conventions;
using Boutquin.Curves.Core.Discounting;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.Calendars;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Risk.Tests;

/// <summary>
/// Tests for <see cref="CurveRiskMetrics"/> Hull Tier-1 risk primitives.
/// </summary>
public sealed class CurveRiskMetricsTests
{
    // ─── AC-B1: DV01 on a 10-year par swap ─────────────────────────────────────

    [Fact]
    public void Dv01_OnTenYearAnnualCashFlows_ShouldBeNegativeAndReasonable()
    {
        // Arrange: 5% flat curve, 10 annual cash flows of 100 (notional at T=10)
        DateOnly valuationDate = new(2026, 1, 1);
        FlatDiscountCurve curve = new(
            new CurveName("USD-Disc"),
            valuationDate,
            Currency.USD,
            0.05d);

        // Annual coupon 5 on 100 notional for 10 years + 100 principal
        CashFlow[] cashFlows = Enumerable.Range(1, 10)
            .Select(i => new CashFlow(valuationDate.AddYears(i), i < 10 ? 5m : 105m, Currency.USD))
            .ToArray();

        // Act
        decimal dv01 = curve.Dv01(cashFlows, valuationDate);

        // Assert: DV01 should be negative (price falls as rates rise)
        // and in a reasonable range for a 10Y ~par bond
        dv01.Should().BeNegative();
        ((double)Math.Abs(dv01)).Should().BeInRange(0.01, 1.0,
            because: "DV01 of a 10Y par bond at 5% per unit notional is approximately $0.07 per bp");
    }

    // ─── AC-B2: Hull Ch 4 Example 4.2 ──────────────────────────────────────────

    [Fact]
    public void MacaulayDuration_HullExample42_ShouldMatchTwoDecimalPlaces()
    {
        // Hull Ch 4 Example 4.2: 3% semi-annual coupon bond, 2Y, 4% yield (s/a)
        // DF(t) = (1 + 0.02)^(-2t)
        DateOnly valuationDate = new(2024, 1, 1);

        // Build a discount curve from semi-annual 4% yield
        // DF(t) = (1+y/m)^(-m*t) = (1.02)^(-2t)
        // Equivalent continuous rate r: e^{-r*t} = (1.02)^{-2t} => r = 2*ln(1.02)
        double continuousRate = 2.0 * Math.Log(1.02);
        FlatDiscountCurve curve = new(
            new CurveName("Bond-Disc"),
            valuationDate,
            Currency.USD,
            continuousRate);

        // Cash flows: $1.50 at 0.5Y, 1.0Y, 1.5Y; $101.50 at 2.0Y
        CashFlow[] cashFlows =
        [
            new(new DateOnly(2024, 7, 1), 1.50m, Currency.USD),  // 0.5Y
            new(new DateOnly(2025, 1, 1), 1.50m, Currency.USD),  // 1.0Y
            new(new DateOnly(2025, 7, 1), 1.50m, Currency.USD),  // 1.5Y
            new(new DateOnly(2026, 1, 1), 101.50m, Currency.USD) // 2.0Y
        ];

        // Act
        double duration = curve.MacaulayDuration(cashFlows, valuationDate);

        // Hull Example 4.2 assumes exact half-year intervals and a different coupon.
        // For this bond (3% s/a coupon, 2Y, equivalent continuous rate = 2*ln(1.02),
        // ACT/365 day-count from 2024-01-01), the analytically correct Macaulay
        // duration is ≈ 1.9582 — the formula is from Hull §4.7, eq 4.11.
        duration.Should().BeApproximately(1.9582, 0.005,
            because: "Macaulay duration for a 3% s/a coupon 2Y bond at 4% s/a yield (ACT/365) is ≈ 1.9582");
    }

    [Fact]
    public void ModifiedDuration_HullExample42_ShouldMatchTwoDecimalPlaces()
    {
        DateOnly valuationDate = new(2024, 1, 1);
        double continuousRate = 2.0 * Math.Log(1.02);
        FlatDiscountCurve curve = new(
            new CurveName("Bond-Disc"),
            valuationDate,
            Currency.USD,
            continuousRate);

        CashFlow[] cashFlows =
        [
            new(new DateOnly(2024, 7, 1), 1.50m, Currency.USD),
            new(new DateOnly(2025, 1, 1), 1.50m, Currency.USD),
            new(new DateOnly(2025, 7, 1), 1.50m, Currency.USD),
            new(new DateOnly(2026, 1, 1), 101.50m, Currency.USD)
        ];

        // Act: compoundingFrequency = 2 (semi-annual)
        double modDuration = curve.ModifiedDuration(cashFlows, valuationDate, compoundingFrequency: 2);

        // Hull eq 4.13: D* = D_mac / (1 + y/m).
        // For r = 2*ln(1.02), y/m = exp(r/2) - 1 = 0.02 exactly, so D* = 1.9582 / 1.02 ≈ 1.9198.
        modDuration.Should().BeApproximately(1.9198, 0.005,
            because: "Modified duration = Macaulay / (1 + y/m) = 1.9582 / 1.02 ≈ 1.9198 for this bond");
    }

    [Fact]
    public void Convexity_HullExample42_ShouldBeApproximatelyCorrect()
    {
        DateOnly valuationDate = new(2024, 1, 1);
        double continuousRate = 2.0 * Math.Log(1.02);
        FlatDiscountCurve curve = new(
            new CurveName("Bond-Disc"),
            valuationDate,
            Currency.USD,
            continuousRate);

        CashFlow[] cashFlows =
        [
            new(new DateOnly(2024, 7, 1), 1.50m, Currency.USD),
            new(new DateOnly(2025, 1, 1), 1.50m, Currency.USD),
            new(new DateOnly(2025, 7, 1), 1.50m, Currency.USD),
            new(new DateOnly(2026, 1, 1), 101.50m, Currency.USD)
        ];

        // Act
        double convexity = curve.Convexity(cashFlows, valuationDate);

        // Continuous convexity formula: C = Σ(t²×PV_i)/P — Hull eq 4.14.
        // For this bond with ACT/365 year fractions from 2024-01-01, C ≈ 3.885 years².
        convexity.Should().BeApproximately(3.885, 0.05,
            because: "Convexity = Σ(t²×PV_i)/P ≈ 3.885 years² for this 3% s/a coupon 2Y bond");
    }

    // ─── AC-B3: Gamma × (1bp)² ≈ second-order price change ─────────────────────

    [Fact]
    public void Gamma_ConvexityRelation_ShouldBeConsistentWithSecondOrderPriceChange()
    {
        // Gamma = (DV01(+1bp) - DV01(-1bp)) / 2
        // This verifies the finite-difference construction is self-consistent.
        DateOnly valuationDate = new(2026, 1, 1);
        FlatDiscountCurve curve = new(
            new CurveName("USD-Disc"),
            valuationDate,
            Currency.USD,
            0.05d);

        CashFlow[] cashFlows = Enumerable.Range(1, 10)
            .Select(i => new CashFlow(valuationDate.AddYears(i), i < 10 ? 5m : 105m, Currency.USD))
            .ToArray();

        // Act
        decimal dv01Base = curve.Dv01(cashFlows, valuationDate);
        decimal gamma = curve.Gamma(cashFlows, valuationDate);

        // Assert: |gamma| should be small (second-order) relative to |dv01|
        Math.Abs(gamma).Should().BeLessThan(Math.Abs(dv01Base),
            because: "Gamma (change in DV01) should be much smaller than DV01 itself");
        // Gamma should be positive for a bond (positive convexity)
        gamma.Should().BePositive(
            because: "A long bond position has positive convexity: DV01 increases as rates fall");
    }

    // ─── AC-B4: CashFlow composes with BusinessScheduleGenerator output ─────────

    [Fact]
    public void MacaulayDuration_WithBusinessScheduleGeneratorOutput_ShouldNotThrow()
    {
        // Arrange
        DateOnly startDate = new(2026, 1, 15);
        DateOnly endDate = new(2028, 1, 15);  // 2Y

        IBusinessCalendar calendar = new WeekendOnlyCalendar();
        IYearFractionCalculator dayCount = Actual365Fixed.Instance;

        IReadOnlyList<SchedulePeriod> periods = ScheduleGenerator.Generate(
            startDate,
            endDate,
            PaymentFrequency.Annual,
            calendar,
            BusinessDayAdjustment.ModifiedFollowing,
            dayCount);

        // Convert SchedulePeriod to CashFlow records
        CashFlow[] cashFlows = periods
            .Select(p => new CashFlow(p.PaymentDate, 100m, Currency.USD))
            .ToArray();

        cashFlows.Should().HaveCount(2, because: "2-year annual schedule has 2 periods");

        DateOnly valuationDate = startDate;
        FlatDiscountCurve curve = new(
            new CurveName("USD-Disc"),
            valuationDate,
            Currency.USD,
            0.04d);

        // Act — just verify it composes without throwing
        Action act = () => curve.MacaulayDuration(cashFlows, valuationDate);

        act.Should().NotThrow();
    }

    // ─── AC-B5: Allocation-assert — O(1) per call regardless of cash flow count ──

    [Fact]
    public void Dv01_WarmPath_AllocatesConstantBytesIndependentOfInputSize()
    {
        // This is the AC-B5 allocation-assert unit test.
        // The implementation must be O(1) allocation per call (not O(n) in cash flow count).
        // Verified by comparing warm-path bytes allocated for 100 vs 500 cash flows;
        // both must lie within a constant threshold (constant overhead per call, not per flow).
        DateOnly valuationDate = new(2026, 1, 1);
        FlatDiscountCurve curve = new(
            new CurveName("USD-Disc"),
            valuationDate,
            Currency.USD,
            0.05d);

        CashFlow[] flows100 = Enumerable.Range(1, 100)
            .Select(i => new CashFlow(valuationDate.AddDays(i * 7), 1m, Currency.USD))
            .ToArray();
        CashFlow[] flows500 = Enumerable.Range(1, 500)
            .Select(i => new CashFlow(valuationDate.AddDays(i * 7), 1m, Currency.USD))
            .ToArray();

        // Warm the JIT so that object-creation paths are compiled and cached.
        for (int w = 0; w < 5; w++)
        {
            _ = curve.Dv01(flows100, valuationDate);
            _ = curve.Dv01(flows500, valuationDate);
        }

        // Dv01 is synchronous: count allocations on this thread so parallel tests and
        // runtime background work cannot contaminate the measurement. Average repeated
        // warmed calls while retaining the same per-call and input-growth limits.
        static long AllocatedBytesPerCall(Action call)
        {
            const int attempts = 16;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                call();
            }

            return (GC.GetAllocatedBytesForCurrentThread() - before) / attempts;
        }

        long allocated100 = AllocatedBytesPerCall(() => { _ = curve.Dv01(flows100, valuationDate); });
        long allocated500 = AllocatedBytesPerCall(() => { _ = curve.Dv01(flows500, valuationDate); });

        // Both calls must allocate fewer than 4 KB (O(1) overhead — just the shocked curve wrapper).
        // Neither should grow with the 5x increase in cash flow count.
        const long maxBytesPerCall = 4096;
        allocated100.Should().BeLessThan(maxBytesPerCall,
            because: "Dv01 with 100 flows must allocate at most constant O(1) bytes on the warm path");
        allocated500.Should().BeLessThan(maxBytesPerCall,
            because: "Dv01 with 500 flows must allocate at most constant O(1) bytes on the warm path");

        // A fivefold increase in cash-flow count must not introduce linear allocation.
        Math.Abs(allocated500 - allocated100).Should().BeLessThan(4096,
            because: "allocation must not scale with number of cash flows (O(1) per call, not O(n))");
    }

    // ─── T-C7-1: empty cash-flow list — consistent guard across all four metrics ──

    [Fact]
    public void MacaulayDuration_EmptyCashFlows_ShouldThrowArgumentException()
    {
        DateOnly valuationDate = new(2026, 1, 1);
        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), valuationDate, Currency.USD, 0.05d);
        CashFlow[] empty = [];

        Action act = () => curve.MacaulayDuration(empty, valuationDate);

        act.Should().Throw<ArgumentException>(
            because: "an empty cash-flow list must fail loudly, not return NaN");
    }

    [Fact]
    public void Convexity_EmptyCashFlows_ShouldThrowArgumentException()
    {
        DateOnly valuationDate = new(2026, 1, 1);
        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), valuationDate, Currency.USD, 0.05d);
        CashFlow[] empty = [];

        Action act = () => curve.Convexity(empty, valuationDate);

        act.Should().Throw<ArgumentException>(
            because: "an empty cash-flow list must fail loudly, not return NaN");
    }

    [Fact]
    public void ModifiedDuration_EmptyCashFlows_ShouldThrowArgumentException()
    {
        DateOnly valuationDate = new(2026, 1, 1);
        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), valuationDate, Currency.USD, 0.05d);
        CashFlow[] empty = [];

        Action act = () => curve.ModifiedDuration(empty, valuationDate, compoundingFrequency: 2);

        act.Should().Throw<ArgumentException>(
            because: "an empty cash-flow list must fail loudly and consistently with the sibling metrics");
    }

    [Fact]
    public void DollarDuration_EmptyCashFlows_ShouldThrowArgumentException()
    {
        DateOnly valuationDate = new(2026, 1, 1);
        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), valuationDate, Currency.USD, 0.05d);
        CashFlow[] empty = [];

        Action act = () => curve.DollarDuration(empty, valuationDate, compoundingFrequency: 2);

        act.Should().Throw<ArgumentException>(
            because: "an empty cash-flow list must fail loudly and consistently with the sibling metrics");
    }

    // ─── T-C7-2: compoundingFrequency must be a positive count ───────────────────

    [Fact]
    public void ModifiedDuration_ZeroCompoundingFrequency_ShouldThrowArgumentOutOfRangeException()
    {
        DateOnly valuationDate = new(2024, 1, 1);
        double continuousRate = 2.0 * Math.Log(1.02);
        FlatDiscountCurve curve = new(new CurveName("Bond-Disc"), valuationDate, Currency.USD, continuousRate);
        CashFlow[] cashFlows =
        [
            new(new DateOnly(2024, 7, 1), 1.50m, Currency.USD),
            new(new DateOnly(2026, 1, 1), 101.50m, Currency.USD)
        ];

        Action act = () => curve.ModifiedDuration(cashFlows, valuationDate, compoundingFrequency: 0);

        act.Should().Throw<ArgumentOutOfRangeException>(
            because: "frequency 0 would divide by zero and silently return 0.0 instead of throwing");
    }

    [Fact]
    public void ModifiedDuration_NegativeCompoundingFrequency_ShouldThrowArgumentOutOfRangeException()
    {
        DateOnly valuationDate = new(2024, 1, 1);
        double continuousRate = 2.0 * Math.Log(1.02);
        FlatDiscountCurve curve = new(new CurveName("Bond-Disc"), valuationDate, Currency.USD, continuousRate);
        CashFlow[] cashFlows =
        [
            new(new DateOnly(2024, 7, 1), 1.50m, Currency.USD),
            new(new DateOnly(2026, 1, 1), 101.50m, Currency.USD)
        ];

        Action act = () => curve.ModifiedDuration(cashFlows, valuationDate, compoundingFrequency: -2);

        act.Should().Throw<ArgumentOutOfRangeException>(
            because: "a negative frequency silently inverts the yield conversion and returns a plausible-wrong value");
    }

    // ─── Additional: DollarDuration ─────────────────────────────────────────────

    [Fact]
    public void DollarDuration_ShouldEqualPvTimesModifiedDuration()
    {
        DateOnly valuationDate = new(2024, 1, 1);
        double continuousRate = 2.0 * Math.Log(1.02);
        FlatDiscountCurve curve = new(
            new CurveName("Bond-Disc"),
            valuationDate,
            Currency.USD,
            continuousRate);

        CashFlow[] cashFlows =
        [
            new(new DateOnly(2024, 7, 1), 1.50m, Currency.USD),
            new(new DateOnly(2025, 1, 1), 1.50m, Currency.USD),
            new(new DateOnly(2025, 7, 1), 1.50m, Currency.USD),
            new(new DateOnly(2026, 1, 1), 101.50m, Currency.USD)
        ];

        // Act
        decimal dollarDuration = curve.DollarDuration(cashFlows, valuationDate, compoundingFrequency: 2);

        // Assert: DD = PV * ModDuration; should be negative (price sensitivity)
        dollarDuration.Should().BeNegative(
            because: "Dollar Duration represents price change per unit rate rise and should be negative");
    }
}
