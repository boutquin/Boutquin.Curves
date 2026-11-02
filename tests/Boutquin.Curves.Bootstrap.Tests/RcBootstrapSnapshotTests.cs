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
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.Inputs;
using Boutquin.Curves.Bootstrap.Instruments;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Bootstrap.Validation;
using Boutquin.Curves.Interpolation;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

public sealed class RcBootstrapSnapshotTests
{
    private static readonly CurveReference s_reference = new(CurveRole.Discount, Currency.USD);
    private static readonly DateOnly s_date = new(2026, 1, 2);
    private static ResolvedNode Node() => new("node", new Tenor("1Y"), "Deposit", "USD-SOFR-OIS", s_reference, 0.05m);
    private static CurveCalibrationSpec Spec() => new(new CurveName("curve"), s_reference, CurveValueType.DiscountFactor,
        "ACT/360", new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"), [Node()]);

    private static void AssertCopied<T>(T item, Func<List<T>, IReadOnlyList<T>> construct, Func<List<T>, IReadOnlyList<T>> assign)
    {
        List<T> input = [item];
        IReadOnlyList<T> constructorSnapshot = construct(input);
        IReadOnlyList<T> assignedSnapshot = assign(input);
        input.Clear();
        constructorSnapshot.Should().ContainSingle("constructor inputs must be copied");
        assignedSnapshot.Should().ContainSingle("init and with assignments must be copied");
        if (constructorSnapshot is IList<T> mutableView)
        {
            Action act = mutableView.Clear;
            act.Should().Throw<NotSupportedException>();
        }
    }

    [Fact]
    public void CalibrationInput_CurvesAreProtectedOnConstructionAndWith()
    {
        SimpleReferenceDataProvider data = new([], [], []);
        CurveCalibrationInput baseline = new(s_date, [Spec()], data);
        AssertCopied(Spec(), list => new CurveCalibrationInput(s_date, list, data).Curves,
            list => (baseline with { Curves = list }).Curves);
    }

    [Fact]
    public void CalibrationSpec_NodesAndJumpsAreProtectedOnConstructionAndWith()
    {
        CurveCalibrationSpec baseline = Spec();
        AssertCopied(Node(), list => new CurveCalibrationSpec(baseline.CurveName, s_reference, baseline.ValueType,
            baseline.DayCountCode, baseline.Interpolation, list).Nodes, list => (baseline with { Nodes = list }).Nodes);
        AssertCopied(new CurvePoint(s_date.AddDays(30), 0.99),
            list => new CurveCalibrationSpec(baseline.CurveName, s_reference, baseline.ValueType,
                baseline.DayCountCode, baseline.Interpolation, baseline.Nodes, list).Jumps!,
            list => (baseline with { Jumps = list }).Jumps!);
    }

    [Fact]
    public void ResolvedNode_ProjectionReferencesAreProtectedOnConstructionAndWith()
    {
        AssertCopied(s_reference,
            list => new ResolvedNode("node", new Tenor("1Y"), "FixedFloatSwap", "USD", s_reference, 0.05m, list).ProjectionCurves!,
            list => (Node() with { ProjectionCurves = list }).ProjectionCurves!);
    }

    [Fact]
    public void CompatibilityInputs_QuoteListsAreProtectedOnConstructionAndWith()
    {
        OisBootstrapInput ois = new("ois", s_date, [], [], Actual360.Instance);
        AssertCopied(new DepositQuote(1, 0.05),
            list => new OisBootstrapInput("ois", s_date, list, [], Actual360.Instance).Deposits,
            list => (ois with { Deposits = list }).Deposits);
        AssertCopied(new OisSwapQuote(12, 0.05),
            list => new OisBootstrapInput("ois", s_date, [], list, Actual360.Instance).OisSwaps,
            list => (ois with { OisSwaps = list }).OisSwaps);
        SwapBootstrapInput swap = new("swap", s_date, [], Actual360.Instance);
        AssertCopied(new InterestRateSwapQuote(12, 0.05),
            list => new SwapBootstrapInput("swap", s_date, list, Actual360.Instance).Swaps,
            list => (swap with { Swaps = list }).Swaps);
    }

    [Fact]
    public void ValidationResult_FindingsAreProtectedOnConstructionAndWith()
    {
        AssertCopied(new ValidationEntry("TEST", "test"),
            list => new ValidationResult(false, list, []).Errors,
            list => (ValidationResult.Valid with { Errors = list }).Errors);
        AssertCopied(new ValidationEntry("TEST", "test"),
            list => new ValidationResult(true, [], list).Warnings,
            list => (ValidationResult.Valid with { Warnings = list }).Warnings);
    }
}
