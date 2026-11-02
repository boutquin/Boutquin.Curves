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
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

/// <summary>
/// TDD tests for code-arena run 2026-06-29-0935-e026fe round-1 cluster C3-core findings.
/// </summary>
public sealed class ArenaC3FixTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 2, 1);
    private const Currency Usd = Currency.USD;

    private static FlatDiscountCurve CreateBaseCurve(double rate = 0.05)
    {
        return new FlatDiscountCurve(new CurveName("USD-Base"), s_valuationDate, Usd, rate);
    }

    // --- T-C3-2: JumpAdjustedDiscountCurve.DiscountFactor past-date guard ---

    [Fact]
    public void JumpAdjusted_DiscountFactor_AtPastDate_WithPastJump_ReturnsOne()
    {
        // A jump dated before a past query date is wrongly included absent the boundary guard.
        CurvePoint[] jumps =
        {
            new(new DateOnly(2026, 1, 15), 0.999d), // jump date precedes both query date and valuation date
        };
        var jumpCurve = new JumpAdjustedDiscountCurve(CreateBaseCurve(), jumps);

        // DF at a past query date must equal 1.0 by the family convention DF(date <= ValuationDate) == 1.0.
        jumpCurve.DiscountFactor(new DateOnly(2026, 1, 20)).Should().Be(1d);
    }

    [Fact]
    public void JumpAdjusted_DiscountFactor_AtValuationDate_WithJumpOnValuationDate_ReturnsOne()
    {
        // A jump dated exactly on the valuation date breaks DF(ValuationDate) == 1.0 absent the guard.
        CurvePoint[] jumps =
        {
            new(s_valuationDate, 0.997d),
        };
        var jumpCurve = new JumpAdjustedDiscountCurve(CreateBaseCurve(), jumps);

        jumpCurve.DiscountFactor(s_valuationDate).Should().Be(1d);
    }

    [Fact]
    public void JumpAdjusted_DiscountFactor_FutureJump_StillApplies()
    {
        // Guard must not suppress jumps for future query dates — overlay still applies.
        CurvePoint[] jumps =
        {
            new(new DateOnly(2026, 3, 1), 0.99d),
        };
        var jumpCurve = new JumpAdjustedDiscountCurve(CreateBaseCurve(), jumps);

        double futureDate = jumpCurve.DiscountFactor(new DateOnly(2027, 2, 1));
        double baseDf = CreateBaseCurve().DiscountFactor(new DateOnly(2027, 2, 1));

        futureDate.Should().BeApproximately(baseDf * 0.99d, 1e-12);
    }

    // --- T-C3-3: ZeroSpreadedDiscountCurve rejects non-finite spread ---

    [Fact]
    public void ZeroSpreaded_RejectsNaNSpread()
    {
        var act = () => new ZeroSpreadedDiscountCurve(new CurveName("Z"), CreateBaseCurve(), double.NaN);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("spread");
    }

    [Fact]
    public void ZeroSpreaded_RejectsPositiveInfinitySpread()
    {
        var act = () => new ZeroSpreadedDiscountCurve(new CurveName("Z"), CreateBaseCurve(), double.PositiveInfinity);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("spread");
    }

    [Fact]
    public void ZeroSpreaded_RejectsNegativeInfinitySpread()
    {
        var act = () => new ZeroSpreadedDiscountCurve(new CurveName("Z"), CreateBaseCurve(), double.NegativeInfinity);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("spread");
    }

    [Fact]
    public void ZeroSpreaded_AcceptsNegativeFiniteSpread()
    {
        // A negative additive spread is intentionally permitted (tightening overlay).
        var act = () => new ZeroSpreadedDiscountCurve(new CurveName("Z"), CreateBaseCurve(), -0.0050d);

        act.Should().NotThrow();
    }

    // --- T-C3-4: MultiplicativeSpreadDiscountCurve rejects NaN spread factor ---

    [Fact]
    public void Multiplicative_RejectsNaNSpreadFactor()
    {
        var act = () => new MultiplicativeSpreadDiscountCurve(new CurveName("M"), CreateBaseCurve(), double.NaN);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("spreadFactor");
    }

    [Fact]
    public void Multiplicative_RejectsPositiveInfinitySpreadFactor()
    {
        var act = () => new MultiplicativeSpreadDiscountCurve(new CurveName("M"), CreateBaseCurve(), double.PositiveInfinity);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("spreadFactor");
    }

    [Fact]
    public void Multiplicative_RejectsNegativeInfinitySpreadFactor()
    {
        var act = () => new MultiplicativeSpreadDiscountCurve(new CurveName("M"), CreateBaseCurve(), double.NegativeInfinity);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("spreadFactor");
    }

    [Fact]
    public void Multiplicative_RejectsNonPositiveSpreadFactor()
    {
        // The non-positive branch predates the finite guard; pin it so a future edit cannot drop it silently.
        var actZero = () => new MultiplicativeSpreadDiscountCurve(new CurveName("M"), CreateBaseCurve(), 0d);
        var actNegative = () => new MultiplicativeSpreadDiscountCurve(new CurveName("M"), CreateBaseCurve(), -1.0d);

        actZero.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("spreadFactor");
        actNegative.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("spreadFactor");
    }
}
