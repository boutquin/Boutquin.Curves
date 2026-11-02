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

using Boutquin.Curves.Abstractions;
using Boutquin.Curves.Abstractions.Bootstrap;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.Curves.Bootstrap.ConvexityAdjustments;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.Helpers;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Core;
using Boutquin.Curves.Interpolation;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

/// <summary>
/// Pins the OIS-future convexity wiring: a non-null Hull-White adjustment must bite (T1 != T2),
/// closing the F-L4-05 end-to-end coverage gap and the F-L7-3 / CF-C1-3 (tau, tau) degeneracy.
/// </summary>
public sealed class OisFutureConvexityWiringTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);

    private static (ResolvedNode Node, IReferenceDataProvider RefData) BuildFutureNode()
    {
        CurveReference curveReference = new(CurveRole.Discount, Currency.USD);
        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SR3") });

        // 2Y expiry so the adjustment is materially above zero.
        ResolvedNode node = new("FUT-2Y", new Tenor("2Y"), "OisFuture", "USD-SR3", curveReference, 0.043m);
        return (node, referenceData);
    }

    [Fact]
    public void SolveNodeValue_WithHullWhiteAdjustment_AppliesNonZeroConvexity()
    {
        (ResolvedNode node, IReferenceDataProvider refData) = BuildFutureNode();
        ICurveGroup emptyGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_valuationDate).Build();

        var unadjusted = new OisFutureInstrumentHelper(node, (double)node.Rate);
        var adjusted = new OisFutureInstrumentHelper(
            node,
            (double)node.Rate,
            new HullWhiteConvexityAdjustment(0.03, 0.015));

        double dfUnadjusted = unadjusted.SolveNodeValue(emptyGroup, s_valuationDate, refData);
        double dfAdjusted = adjusted.SolveNodeValue(emptyGroup, s_valuationDate, refData);

        // The convexity adjustment lowers the forward rate (forward = futures - CA), so the
        // discount factor must be strictly larger than the unadjusted one. With (tau, tau) the
        // Hull-White adjustment is identically zero (B(T1,T1) = 0) and these would be equal.
        dfAdjusted.Should().BeGreaterThan(dfUnadjusted);
        (dfAdjusted - dfUnadjusted).Should().BeGreaterThan(1e-9);
    }

    /// <summary>
    /// An <see cref="IConvexityAdjustment"/> that returns a non-finite (NaN) adjustment, modelling a
    /// degenerate implementation (e.g. a BondYieldConvexityAdjustment fed a zero yield) that does NOT
    /// guard at its own constructor. Used to exercise the application-seam guard in
    /// <see cref="OisFutureInstrumentHelper.SolveNodeValue"/>.
    /// </summary>
    private sealed class NonFiniteConvexityAdjustment : IConvexityAdjustment
    {
        public double Adjustment(double timeToExpiry, double timeToMaturity) => double.NaN;
    }

    [Fact]
    public void SolveNodeValue_WithNonFiniteConvexityAdjustment_ThrowsRatherThanProducingNaNDiscountFactor()
    {
        // T-C1-2 seam guard: a non-finite convexity adjustment (here NaN, as a degenerate
        // IConvexityAdjustment implementation can produce) flows into adjustedRate = futures - adjustment
        // and then Math.Exp(-NaN * tau) = NaN. A NaN discount factor escapes the downstream
        // InterpolatedDiscountCurve positivity guard (NaN <= 0d is false) and silently poisons the
        // calibrated curve. The helper must reject a non-finite adjustment at the application seam.
        //
        // Defense-in-depth note (M-1 A-9): ConstantConvexityAdjustment now ALSO guards non-finite
        // values at its own constructor, so it can no longer reach this seam — that ctor guard is
        // pinned separately in DefensiveGuardHardeningTests. This test deliberately uses a test-double
        // adjustment that does NOT self-guard, to keep the application-seam guard itself under test
        // (it remains the only defense for IConvexityAdjustment implementations that lack a ctor guard).
        (ResolvedNode node, IReferenceDataProvider refData) = BuildFutureNode();
        ICurveGroup emptyGroup = new CurveGroupBuilder(new CurveGroupName("g"), s_valuationDate).Build();

        var helper = new OisFutureInstrumentHelper(
            node,
            (double)node.Rate,
            new NonFiniteConvexityAdjustment());

        Action act = () => helper.SolveNodeValue(emptyGroup, s_valuationDate, refData);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-finite*");
    }

    private static (CurveReference Reference, CurveCalibrationInput Input) BuildFutureCalibrationInput(
        double rate,
        IConvexityAdjustment? convexity)
    {
        CurveReference curveReference = new(CurveRole.Discount, Currency.USD);
        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SR3") });

        // 2Y expiry so a maturity-dependent adjustment is materially above zero; a constant
        // adjustment is flat regardless of expiry, so the same node serves both tests.
        ResolvedNode node = new("FUT-2Y", new Tenor("2Y"), "OisFuture", "USD-SR3", curveReference, (decimal)rate);

        CurveCalibrationInput input = new(
            s_valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveReference,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[] { node })
            },
            referenceData,
            ConvexityAdjustment: convexity);

        return (curveReference, input);
    }

    [Fact]
    public void Calibrate_WithConvexityAdjustment_AppliesAdjustmentEndToEnd()
    {
        // AC-7: a constant adjustment supplied through the calibrator's public seam must lower the
        // forward rate (forward = futures - CA), so the calibrated terminal DF equals
        // exp(-(rate - adjustment) * tau) and is STRICTLY GREATER than the unadjusted exp(-rate * tau).
        // This proves the previously-dead CreateHelper -> OisFutureInstrumentHelper convexity wiring
        // is now live on the production calibrator path.
        const double rate = 0.043;
        const double adjustment = 0.0010; // 10 bp

        (CurveReference curveReference, CurveCalibrationInput input) =
            BuildFutureCalibrationInput(rate, new ConstantConvexityAdjustment(adjustment));

        CurveCalibrationResult result = new PiecewiseBootstrapCalibrator().Calibrate(input);

        IDiscountCurve curve = (IDiscountCurve)result.CurveGroup.GetCurve(curveReference);
        DateOnly pillar = TenorParser.AddTenor(s_valuationDate, new Tenor("2Y"));
        double tau = (pillar.DayNumber - s_valuationDate.DayNumber) / 360d; // ACT/360 (USD-SR3)

        double expectedAdjusted = Math.Exp(-(rate - adjustment) * tau);
        double unadjusted = Math.Exp(-rate * tau);

        curve.DiscountFactor(pillar).Should().BeApproximately(expectedAdjusted, 1e-9);
        curve.DiscountFactor(pillar).Should().BeGreaterThan(unadjusted);
    }

    [Fact]
    public void Calibrate_WithConvexityAdjustment_RepricingResidualIsZero()
    {
        // T-C1-3: when a non-zero convexity adjustment is active, the OisFuture solve embeds
        // forward = futures - CA into the calibrated DF. ImpliedQuote must invert that DF and add
        // the SAME convexity adjustment back, so the solve -> reprice round-trip is exact. Before
        // the fix, ImpliedQuote returned (futures - CA), so AbsoluteError = |futures - (futures-CA)|
        // = CA — a non-zero residual on an otherwise-exact calibration.
        const double rate = 0.043;
        const double adjustment = 0.0025; // 25 bp

        (CurveReference _, CurveCalibrationInput input) =
            BuildFutureCalibrationInput(rate, new ConstantConvexityAdjustment(adjustment));

        CurveCalibrationResult result = new PiecewiseBootstrapCalibrator().Calibrate(input);

        result.Diagnostics.Repricing.Should().ContainSingle();
        result.Diagnostics.Repricing[0].AbsoluteError.Should().BeApproximately(0d, 1e-10);
    }

    [Fact]
    public void Calibrate_WithoutConvexityAdjustment_ProducesRawRateDiscountFactor()
    {
        // AC-8: with NO convexity model configured (the default), an OisFuture node calibrates to the
        // exact raw-rate DF exp(-rate * tau) it produces today — the seam is backward compatible.
        const double rate = 0.043;

        (CurveReference curveReference, CurveCalibrationInput input) =
            BuildFutureCalibrationInput(rate, convexity: null);

        CurveCalibrationResult result = new PiecewiseBootstrapCalibrator().Calibrate(input);

        IDiscountCurve curve = (IDiscountCurve)result.CurveGroup.GetCurve(curveReference);
        DateOnly pillar = TenorParser.AddTenor(s_valuationDate, new Tenor("2Y"));
        double tau = (pillar.DayNumber - s_valuationDate.DayNumber) / 360d; // ACT/360 (USD-SR3)

        curve.DiscountFactor(pillar).Should().BeApproximately(Math.Exp(-rate * tau), 1e-9);
    }
}
