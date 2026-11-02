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
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.Helpers;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Interpolation;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

/// <summary>
/// Verifies M-1 seam: each solved pillar is published to the partial group before the next node solves.
/// AC-4: the in-progress discount curve is visible to subsequent nodes during calibration.
/// AC-5: intermediate coupon dates (between locked pillars) are resolvable via interpolation.
/// </summary>
public sealed class CalibrationSeamTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);

    /// <summary>
    /// AC-4 + AC-5: When calibrating a 3-node OIS curve, node 2's SolveNodeValue receives a
    /// partialCurveGroup that contains the in-progress discount curve with nodes 0 and 1
    /// already locked. This proves the mutable-working-builder seam is wired and not inert.
    /// </summary>
    [Fact]
    public void Calibrate_ThreeNodeOisCurve_ThirdNodeReceivesFirstTwoPillarsInPartialGroup()
    {
        CurveReference discountRef = new(CurveRole.Discount, Currency.USD);

        ICurveGroup? capturedGroupAtNode2 = null;
        CapturingCalibrator calibrator = new(interceptAtNodeIndex: 2, onCapture: group => capturedGroupAtNode2 = group);

        SimpleReferenceDataProvider referenceData = BuildReferenceData();

        CurveCalibrationInput request = new(
            s_valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-SOFR"),
                    discountRef,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("6M-OIS", new Tenor("6M"), "Ois", "USD-SOFR-OIS", discountRef, 0.05m),
                        new ResolvedNode("1Y-OIS", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", discountRef, 0.048m),
                        new ResolvedNode("2Y-OIS", new Tenor("2Y"), "Ois", "USD-SOFR-OIS", discountRef, 0.045m),
                    })
            },
            referenceData,
            SkipValidation: true);

        calibrator.Calibrate(request);

        // AC-4: partial group at node 2 must contain the in-progress discount curve
        capturedGroupAtNode2.Should().NotBeNull(
            because: "the capture callback must fire when node 2 is solved");

        bool hasCurve = capturedGroupAtNode2!.TryGetCurve(discountRef, out ICurve? partialCurve);
        hasCurve.Should().BeTrue(
            because: "the in-progress discount curve must be in the partial group when node 2 solves (AC-4)");

        partialCurve.Should().BeAssignableTo<IDiscountCurve>(
            because: "the in-progress curve must implement IDiscountCurve");

        IDiscountCurve partialDiscount = (IDiscountCurve)partialCurve!;

        // AC-4: prior pillars (nodes 0 and 1) are present and return positive DFs
        DateOnly pillar6M = s_valuationDate.AddMonths(6);
        DateOnly pillar1Y = s_valuationDate.AddYears(1);

        partialDiscount.DiscountFactor(pillar6M).Should().BePositive(
            because: "node 0's 6M pillar must be locked in the partial group when node 2 solves (AC-4)");
        partialDiscount.DiscountFactor(pillar1Y).Should().BePositive(
            because: "node 1's 1Y pillar must be locked in the partial group when node 2 solves (AC-4)");

        // AC-5: intermediate dates between locked pillars are resolvable via interpolation
        DateOnly coupon9M = s_valuationDate.AddMonths(9); // between the 6M and 1Y pillars
        double df9M = partialDiscount.DiscountFactor(coupon9M);
        df9M.Should().BePositive(
            because: "intermediate coupon dates between locked pillars must be interpolatable (AC-5)");
        df9M.Should().BeLessThan(partialDiscount.DiscountFactor(pillar6M),
            because: "discount factor at 9M must be less than at 6M (time value)");
        df9M.Should().BeGreaterThan(partialDiscount.DiscountFactor(pillar1Y),
            because: "discount factor at 9M must be greater than at 1Y (monotone)");
    }

    private static SimpleReferenceDataProvider BuildReferenceData()
    {
        return new SimpleReferenceDataProvider(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });
    }

    // --- Test infrastructure ---

    /// <summary>
    /// Subclass of PiecewiseBootstrapCalibrator that intercepts CreateHelper for a specified node index,
    /// wrapping it with a capturing delegate to record the partialCurveGroup passed to SolveNodeValue.
    /// </summary>
    private sealed class CapturingCalibrator(int interceptAtNodeIndex, Action<ICurveGroup> onCapture)
        : PiecewiseBootstrapCalibrator
    {
        private int _nodeCallIndex;

        protected override AbstractInstrumentHelper CreateHelper(
            ResolvedNode node,
            double quote,
            Boutquin.Curves.Abstractions.Bootstrap.IConvexityAdjustment? convexityAdjustment = null)
        {
            AbstractInstrumentHelper inner = base.CreateHelper(node, quote, convexityAdjustment);
            return _nodeCallIndex++ == interceptAtNodeIndex
                ? new InterceptingHelper(inner, onCapture)
                : inner;
        }
    }

    /// <summary>
    /// Wraps an AbstractInstrumentHelper and fires a callback with the partialCurveGroup
    /// when SolveNodeValue is called, then delegates to the inner helper.
    /// </summary>
    private sealed class InterceptingHelper(AbstractInstrumentHelper inner, Action<ICurveGroup> onSolve)
        : AbstractInstrumentHelper(inner.Label, inner.TargetCurve, inner.QuoteValue, inner.InstrumentType)
    {
        public override DateOnly PillarDate(DateOnly valuationDate, IReferenceDataProvider referenceData)
            => inner.PillarDate(valuationDate, referenceData);

        public override double SolveNodeValue(ICurveGroup partialCurveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
        {
            onSolve(partialCurveGroup);
            return inner.SolveNodeValue(partialCurveGroup, valuationDate, referenceData);
        }

        public override double ImpliedQuote(ICurveGroup curveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
            => inner.ImpliedQuote(curveGroup, valuationDate, referenceData);
    }
}
