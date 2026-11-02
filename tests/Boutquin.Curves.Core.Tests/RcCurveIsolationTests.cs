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
using Boutquin.Curves.Interpolation;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

public sealed class RcCurveIsolationTests
{
    private static readonly DateOnly s_valuation = new(2026, 1, 2);

    [Fact]
    public void CurveGroup_PublicConstructorTakesDictionarySnapshot()
    {
        CurveReference reference = new(CurveRole.Discount, Currency.USD);
        ICurve original = new FlatDiscountCurve(new CurveName("original"), s_valuation, Currency.USD, 0.03);
        Dictionary<CurveReference, ICurve> input = new() { [reference] = original };
        CurveGroup group = new(new CurveGroupName("snapshot"), s_valuation, input);
        input.Clear();
        group.GetCurve(reference).Should().BeSameAs(original);
    }

    [Fact]
    public void CurvePoints_CannotMutateTheCurveThroughPublicView()
    {
        InterpolatedDiscountCurve curve = Create(InterpolatorKind.LogLinearDiscountFactor);
        double before = curve.DiscountFactor(s_valuation.AddDays(180));
        if (curve.Points is IList<CurvePoint> list)
        {
            Action act = () => list[0] = new CurvePoint(s_valuation.AddDays(365), 0.1);
            act.Should().Throw<NotSupportedException>();
        }
        curve.DiscountFactor(s_valuation.AddDays(180)).Should().Be(before);
    }

    [Theory]
    [InlineData(InterpolatorKind.LogLinearDiscountFactor)]
    [InlineData(InterpolatorKind.LinearZeroRate)]
    [InlineData(InterpolatorKind.FlatForward)]
    [InlineData(InterpolatorKind.MonotoneCubic)]
    [InlineData(InterpolatorKind.MonotoneConvex)]
    public void Rolling_PreservesFullDiscountSurface(InterpolatorKind kind)
    {
        IDiscountCurve original = Create(kind);
        DateOnly rollDate = s_valuation.AddDays(500);
        IDiscountCurve rolled = original.WithValuationDate(rollDate);
        foreach (int offset in new[] { 1, 90, 229, 400, 700, 1200 })
        {
            DateOnly target = rollDate.AddDays(offset);
            double expected = original.DiscountFactor(target) / original.DiscountFactor(rollDate);
            rolled.DiscountFactor(target).Should().BeApproximately(expected, 1e-12,
                $"{kind} must preserve the full conditional discount surface at {target}");
        }
        IDiscountCurve rolledTwice = rolled.WithValuationDate(rollDate.AddDays(90));
        DateOnly final = s_valuation.AddDays(1000);
        rolledTwice.DiscountFactor(final).Should().BeApproximately(
            original.DiscountFactor(final) / original.DiscountFactor(rollDate.AddDays(90)), 1e-12);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void DiscountCurve_RejectsNonFinitePillars(double value)
    {
        Action act = () => new InterpolatedDiscountCurve(new CurveName("bad"), s_valuation, Currency.USD,
            [new CurvePoint(s_valuation.AddDays(365), value)]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DiscountCurve_RejectsDuplicatePillarDates()
    {
        Action act = () => new InterpolatedDiscountCurve(new CurveName("bad"), s_valuation, Currency.USD,
            [new CurvePoint(s_valuation.AddDays(365), 0.99), new CurvePoint(s_valuation.AddDays(365), 0.98)]);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(InterpolatorKind.LogLinearDiscountFactor)]
    [InlineData(InterpolatorKind.LinearZeroRate)]
    [InlineData(InterpolatorKind.FlatForward)]
    [InlineData(InterpolatorKind.MonotoneCubic)]
    public void Rolling_PreservesNegativeRateSurfaceAndCustomDayCount(InterpolatorKind kind)
    {
        DateOnly step = s_valuation.AddDays(400);
        InterpolatedDiscountCurve original = new(new CurveName("Negative ACT360"), s_valuation, Currency.USD,
            [new(s_valuation.AddDays(365), 1.01), new(s_valuation.AddDays(730), 1.04),
             new(s_valuation.AddDays(1095), 1.09)],
            new InterpolationSettings(kind, "FlatZero", "FlatForward"), Actual360.Instance);
        IDiscountCurve rolled = original.WithValuationDate(step);
        rolled.DayCount.Should().BeSameAs(Actual360.Instance);
        foreach (int offset in new[] { 1, 100, 500, 900 })
        {
            DateOnly target = step.AddDays(offset);
            rolled.DiscountFactor(target).Should().BeApproximately(
                original.DiscountFactor(target) / original.DiscountFactor(step), 1e-12);
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void JumpCurve_RejectsNonFiniteFactors(double factor)
    {
        FlatDiscountCurve curve = new(new CurveName("base"), s_valuation, Currency.USD, 0.03);
        Action act = () => new JumpAdjustedDiscountCurve(curve, [new(s_valuation.AddDays(30), factor)]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("FlatZero")]
    [InlineData("FlatForward")]
    public void SinglePillar_UsesItsFlatZeroFallbackOnBothSides(string extrapolator)
    {
        InterpolatedDiscountCurve curve = new(new CurveName("single"), s_valuation, Currency.USD,
            [new(s_valuation.AddDays(365), Math.Exp(-0.04))],
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, extrapolator, extrapolator));
        foreach (int days in new[] { 30, 180, 365, 730 })
        {
            curve.DiscountFactor(s_valuation.AddDays(days)).Should().BeApproximately(Math.Exp(-0.04 * days / 365d), 1e-12);
        }
    }

    [Fact]
    public void JumpFactors_AreIndependentOfInputEnumerationOrder()
    {
        FlatDiscountCurve inner = new(new CurveName("jumps"), s_valuation, Currency.USD, 0.03);
        CurvePoint[] jumps = [new(s_valuation.AddDays(30), 0.99), new(s_valuation.AddDays(60), 0.995)];
        JumpAdjustedDiscountCurve forward = new(inner, jumps);
        JumpAdjustedDiscountCurve reversed = new(inner, jumps.Reverse());
        double expected = Math.Exp(-0.03) * 0.99 * 0.995;
        forward.DiscountFactor(s_valuation.AddDays(365)).Should().BeApproximately(expected, 1e-12);
        reversed.DiscountFactor(s_valuation.AddDays(365)).Should().BeApproximately(expected, 1e-12);
    }

    [Theory]
    [InlineData(CompoundingConvention.Annual, 1d)]
    [InlineData(CompoundingConvention.SemiAnnual, 2d)]
    [InlineData(CompoundingConvention.Quarterly, 4d)]
    [InlineData(CompoundingConvention.Monthly, 12d)]
    public void PeriodicZeroQuotes_ReproduceKnownExponentialDiscount(CompoundingConvention convention, double frequency)
    {
        FlatDiscountCurve curve = new(new CurveName("periodic"), s_valuation, Currency.USD, 0.04);
        DateOnly target = s_valuation.AddDays(730);
        double quote = curve.ZeroRate(target, convention);
        Math.Pow(1d + quote / frequency, -frequency * 2d).Should().BeApproximately(Math.Exp(-0.08), 1e-12);
    }

    private static InterpolatedDiscountCurve Create(InterpolatorKind kind) => new(
        new CurveName("curved"), s_valuation, Currency.USD,
        [new(s_valuation.AddDays(365), 0.97), new(s_valuation.AddDays(730), 0.89),
         new(s_valuation.AddDays(1095), 0.83), new(s_valuation.AddDays(1460), 0.74)],
        new InterpolationSettings(kind, "FlatZero", "FlatForward"));
}
