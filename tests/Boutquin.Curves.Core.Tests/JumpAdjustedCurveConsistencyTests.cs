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
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Core.Discounting;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

/// <summary>
/// Verifies that <see cref="JumpAdjustedDiscountCurve"/>'s rate accessors are consistent with its own
/// jump-adjusted discount factor, per spec fix-jump-adjusted-curve-rate-consistency.
/// </summary>
public sealed class JumpAdjustedCurveConsistencyTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 2, 1);
    private const Currency Usd = Currency.USD;

    private static FlatDiscountCurve CreateBaseCurve(double rate = 0.05)
        => new(new CurveName("USD-Base"), s_valuationDate, Usd, rate);

    // Jumps straddling the query horizon: one before the first query, one mid-horizon.
    private static CurvePoint[] TwoJumps() =>
    [
        new(new DateOnly(2026, 3, 1), 0.99d),
        new(new DateOnly(2026, 9, 1), 0.995d),
    ];

    // Dates exercised by the round-trip ACs: before the first jump, between the two jumps, after all jumps.
    public static TheoryData<DateOnly> FutureQueryDates() =>
    [
        new DateOnly(2026, 2, 15),
        new DateOnly(2026, 6, 1),
        new DateOnly(2027, 2, 1),
    ];

    // --- AC-1: continuous round-trip exp(-ZeroRate*t) == DiscountFactor ---

    [Theory]
    [MemberData(nameof(FutureQueryDates))]
    public void ZeroRate_Continuous_RoundTripsToDiscountFactor(DateOnly queryDate)
    {
        var curve = new JumpAdjustedDiscountCurve(CreateBaseCurve(), TwoJumps());

        double t = curve.DayCount.YearFraction(s_valuationDate, queryDate);
        double z = curve.ZeroRate(queryDate, CompoundingConvention.Continuous);

        Math.Exp(-z * t).Should().BeApproximately(curve.DiscountFactor(queryDate), 1e-12);
    }

    // --- AC-2: ZeroRate returns 0 for dates on or before valuation ---

    [Fact]
    public void ZeroRate_AtOrBeforeValuationDate_ReturnsZero()
    {
        var curve = new JumpAdjustedDiscountCurve(CreateBaseCurve(), TwoJumps());

        curve.ZeroRate(new DateOnly(2026, 1, 15), CompoundingConvention.Continuous).Should().Be(0d);
        curve.ZeroRate(s_valuationDate, CompoundingConvention.Continuous).Should().Be(0d);
    }

    // --- AC-3: InstantaneousForwardRate equals the inner curve (jumps are measure-zero in f(t)) ---

    [Fact]
    public void InstantaneousForwardRate_EqualsInnerCurve()
    {
        FlatDiscountCurve inner = CreateBaseCurve();
        var curve = new JumpAdjustedDiscountCurve(inner, TwoJumps());

        var queryDate = new DateOnly(2027, 2, 1);
        curve.InstantaneousForwardRate(queryDate)
            .Should().Be(inner.InstantaneousForwardRate(queryDate));
    }

    // --- AC-4: annual round-trip (1 + ZeroRate)^(-t) == DiscountFactor ---

    [Theory]
    [MemberData(nameof(FutureQueryDates))]
    public void ZeroRate_Annual_RoundTripsToDiscountFactor(DateOnly queryDate)
    {
        var curve = new JumpAdjustedDiscountCurve(CreateBaseCurve(), TwoJumps());

        double t = curve.DayCount.YearFraction(s_valuationDate, queryDate);
        double z = curve.ZeroRate(queryDate, CompoundingConvention.Annual);

        Math.Pow(1d + z, -t).Should().BeApproximately(curve.DiscountFactor(queryDate), 1e-12);
    }

    // --- AC-5: no-jump curve is behaviour-identical to its inner curve ---

    [Theory]
    [MemberData(nameof(FutureQueryDates))]
    public void NoJumpCurve_MatchesInnerCurve(DateOnly queryDate)
    {
        FlatDiscountCurve inner = CreateBaseCurve();
        var curve = new JumpAdjustedDiscountCurve(inner, []);

        curve.DiscountFactor(queryDate)
            .Should().BeApproximately(inner.DiscountFactor(queryDate), 1e-12);
        curve.ZeroRate(queryDate, CompoundingConvention.Continuous)
            .Should().BeApproximately(inner.ZeroRate(queryDate, CompoundingConvention.Continuous), 1e-12);
        curve.ZeroRate(queryDate, CompoundingConvention.Annual)
            .Should().BeApproximately(inner.ZeroRate(queryDate, CompoundingConvention.Annual), 1e-12);
    }
}
