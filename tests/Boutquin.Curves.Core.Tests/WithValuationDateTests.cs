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
using Boutquin.Curves.Core.Forwards;
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

/// <summary>
/// Verifies the <see cref="IDiscountCurve.WithValuationDate"/> and
/// <see cref="IForwardCurve.WithValuationDate"/> contracts for every concrete curve
/// shipped in <c>Boutquin.Curves.Core</c>. The load-bearing invariant for all
/// implementations is:
/// <code>
/// rolled.DiscountFactor(t) == original.DiscountFactor(t) / original.DiscountFactor(s)
/// </code>
/// for any <c>t &gt;= news_valuationDate = s</c>. This pins down the forwards-realize
/// semantics required by simulators and backtests.
/// </summary>
public sealed class WithValuationDateTests
{
    private static readonly DateOnly s_valuation = new(2026, 4, 15);
    private static readonly DateOnly s_step = new(2026, 10, 15);
    private static readonly CurveName s_name = new("USD-OIS");
    private const CurrencyCode Usd = CurrencyCode.USD;

    private static void AssertForwardsRealize(IDiscountCurve original, IDiscountCurve rolled, DateOnly step, params DateOnly[] probeDates)
    {
        rolled.ValuationDate.Should().Be(step);
        double anchor = original.DiscountFactor(step);
        foreach (DateOnly t in probeDates)
        {
            double expected = original.DiscountFactor(t) / anchor;
            double actual = rolled.DiscountFactor(t);
            actual.Should().BeApproximately(expected, 1e-10,
                $"rolled DF({t:O}) must satisfy forwards-realize identity");
        }
    }

    [Fact]
    public void FlatDiscountCurve_PreservesRateAndShiftss_valuation()
    {
        FlatDiscountCurve original = new(s_name, s_valuation, Usd, 0.04d);

        IDiscountCurve rolled = original.WithValuationDate(s_step);

        rolled.ValuationDate.Should().Be(s_step);
        // Rate invariance: DF(step + ΔT) under the rolled curve equals exp(-r·ΔT).
        DateOnly oneYearOut = s_step.AddDays(365);
        rolled.DiscountFactor(oneYearOut).Should().BeApproximately(Math.Exp(-0.04d), 1e-12d);
        // Implied rate at any maturity is still 4% under the rolled curve.
        rolled.InstantaneousForwardRate(oneYearOut).Should().BeApproximately(0.04d, 1e-12d);
    }

    [Fact]
    public void FlatDiscountCurve_SatisfiesForwardsRealizeIdentity()
    {
        FlatDiscountCurve original = new(s_name, s_valuation, Usd, 0.035d);

        IDiscountCurve rolled = original.WithValuationDate(s_step);

        AssertForwardsRealize(original, rolled, s_step,
            s_step.AddDays(30), s_step.AddDays(91), s_step.AddDays(365));
    }

    [Fact]
    public void FlatDiscountCurve_BackwardsInTime_Throws()
    {
        FlatDiscountCurve original = new(s_name, s_valuation, Usd, 0.04d);
        DateOnly beforeValuation = s_valuation.AddDays(-1);

        Action act = () => original.WithValuationDate(beforeValuation);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void InterpolatedDiscountCurve_SatisfiesForwardsRealizeIdentity()
    {
        CurvePoint[] pillars = new[]
        {
            new CurvePoint(s_valuation.AddDays(91), 0.9900d),
            new CurvePoint(s_valuation.AddDays(182), 0.9780d),
            new CurvePoint(s_valuation.AddDays(365), 0.9550d),
            new CurvePoint(s_valuation.AddDays(730), 0.9100d),
        };
        InterpolatedDiscountCurve original = new(s_name, s_valuation, Usd, pillars);

        IDiscountCurve rolled = original.WithValuationDate(s_step);

        AssertForwardsRealize(original, rolled, s_step,
            s_step.AddDays(10),
            s_step.AddDays(90),
            s_step.AddDays(180),
            s_step.AddDays(365));
    }

    [Fact]
    public void InterpolatedDiscountCurve_DropsPillarsAtOrBeforeNewAnchor()
    {
        CurvePoint[] pillars = new[]
        {
            new CurvePoint(s_valuation.AddDays(30), 0.9975d),
            new CurvePoint(s_valuation.AddDays(182), 0.9780d),  // past by s_step (s_step is s_valuation + 183)
            new CurvePoint(s_valuation.AddDays(365), 0.9550d),
            new CurvePoint(s_valuation.AddDays(730), 0.9100d),
        };
        InterpolatedDiscountCurve original = new(s_name, s_valuation, Usd, pillars);

        InterpolatedDiscountCurve rolled = (InterpolatedDiscountCurve)original.WithValuationDate(s_step);

        rolled.Points.Should().OnlyContain(p => p.Date > s_step);
    }

    [Fact]
    public void InterpolatedDiscountCurve_RollingPastLastPillar_Throws()
    {
        CurvePoint[] pillars = new[]
        {
            new CurvePoint(s_valuation.AddDays(91), 0.99d),
            new CurvePoint(s_valuation.AddDays(365), 0.955d),
        };
        InterpolatedDiscountCurve original = new(s_name, s_valuation, Usd, pillars);

        Action act = () => original.WithValuationDate(s_valuation.AddDays(400));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ZeroSpreadedDiscountCurve_RollsUnderlyingAndPreservesSpread()
    {
        FlatDiscountCurve baseCurve = new(s_name, s_valuation, Usd, 0.03d);
        ZeroSpreadedDiscountCurve original = new(new CurveName("USD-OIS+50bp"), baseCurve, 0.005d);

        IDiscountCurve rolled = original.WithValuationDate(s_step);

        AssertForwardsRealize(original, rolled, s_step,
            s_step.AddDays(90), s_step.AddDays(365));
    }

    [Fact]
    public void MultiplicativeSpreadDiscountCurve_RollsUnderlyingAndPreservesFactor()
    {
        FlatDiscountCurve baseCurve = new(s_name, s_valuation, Usd, 0.03d);
        MultiplicativeSpreadDiscountCurve original = new(
            new CurveName("USD-OIS×m"), baseCurve, 0.999d);

        IDiscountCurve rolled = original.WithValuationDate(s_step);

        AssertForwardsRealize(original, rolled, s_step,
            s_step.AddDays(90), s_step.AddDays(365));
    }

    [Fact]
    public void JumpAdjustedDiscountCurve_DropsPastJumpsRollsFutureJumpsUnchanged()
    {
        FlatDiscountCurve baseCurve = new(s_name, s_valuation, Usd, 0.03d);
        CurvePoint[] jumps = new[]
        {
            new CurvePoint(s_valuation.AddDays(60), 1.001d),     // past by s_step — should be dropped
            new CurvePoint(s_valuation.AddDays(300), 1.002d),    // future — should survive
        };
        JumpAdjustedDiscountCurve original = new(baseCurve, jumps);

        IDiscountCurve rolled = original.WithValuationDate(s_step);

        // Past jump is absorbed into rolled DF anchor (dropped to avoid double-count).
        // Future jump still applies. Compose: rolled.DF(t_after_future_jump)
        //     == original.DF(t_after_future_jump) / original.DF(s_step)
        DateOnly afterFutureJump = s_valuation.AddDays(365);
        double expected = original.DiscountFactor(afterFutureJump) / original.DiscountFactor(s_step);
        double actual = rolled.DiscountFactor(afterFutureJump);
        actual.Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void FlatForwardCurve_PreservesRateAndShiftss_valuation()
    {
        FlatForwardCurve original = new(
            new CurveName("USD-SOFR"), s_valuation, Usd, new BenchmarkName("SOFR"), 0.035d);

        IForwardCurve rolled = original.WithValuationDate(s_step);

        rolled.ValuationDate.Should().Be(s_step);
        rolled.ForwardRate(s_step.AddDays(30), s_step.AddDays(120))
            .Should().BeApproximately(0.035d, 1e-12d);
    }

    [Fact]
    public void ForwardCurveFromDiscountCurves_ForwardsInvariantOverIntervalsAfters_step()
    {
        FlatDiscountCurve discount = new(new CurveName("USD-OIS"), s_valuation, Usd, 0.03d);
        FlatDiscountCurve projection = new(new CurveName("USD-SOFR"), s_valuation, Usd, 0.032d);
        ForwardCurveFromDiscountCurves original = new(
            new CurveName("USD-SOFR-FWD"), new BenchmarkName("SOFR"), discount, projection);

        IForwardCurve rolled = original.WithValuationDate(s_step);

        rolled.ValuationDate.Should().Be(s_step);
        DateOnly start = s_step.AddDays(90);
        DateOnly end = s_step.AddDays(180);
        rolled.ForwardRate(start, end).Should().BeApproximately(
            original.ForwardRate(start, end), 1e-10d);
    }

    [Fact]
    public void FlatForwardCurve_BackwardsInTime_Throws()
    {
        FlatForwardCurve original = new(
            new CurveName("USD-SOFR"), s_valuation, Usd, new BenchmarkName("SOFR"), 0.035d);

        Action act = () => original.WithValuationDate(s_valuation.AddDays(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
