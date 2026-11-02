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

// M-1 defensive-guard hardening tests:
// A-1: FlatForwardInterpolator.Interpolate — empty points guard
// A-2: LogLinearDiscountFactorInterpolator.Interpolate — empty points guard
// A-3: MonotoneCubicInterpolator.Interpolate — empty points guard

using Boutquin.Curves.Abstractions.Curves;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Interpolation.Tests;

public sealed class DefensiveGuardHardeningTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly DateOnly s_targetDate = new(2026, 10, 9);
    private static readonly IYearFractionCalculator s_dayCount = new Act365DayCount();
    private static readonly CurvePoint[] s_emptyPoints = [];

    // ── A-1: FlatForwardInterpolator empty points ──────────────────────────

    [Fact]
    public void FlatForwardInterpolator_Interpolate_EmptyPoints_Throws_ArgumentException_NamedPoints()
    {
        // AC-G1: FlatForwardInterpolator must throw a located ArgumentException naming "points"
        // when an empty points list is passed — not an opaque kernel exception.
        var sut = new FlatForwardInterpolator();

        Action act = () => sut.Interpolate(s_valuationDate, s_targetDate, s_emptyPoints, s_dayCount);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("points")
            .WithMessage("*points*");
    }

    // ── A-2: LogLinearDiscountFactorInterpolator empty points ──────────────

    [Fact]
    public void LogLinearDiscountFactorInterpolator_Interpolate_EmptyPoints_Throws_ArgumentException_NamedPoints()
    {
        // AC-G1: LogLinearDiscountFactorInterpolator must throw a located ArgumentException
        // naming "points" — not an opaque kernel exception.
        var sut = new LogLinearDiscountFactorInterpolator();

        Action act = () => sut.Interpolate(s_valuationDate, s_targetDate, s_emptyPoints, s_dayCount);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("points")
            .WithMessage("*points*");
    }

    // ── A-3: MonotoneCubicInterpolator empty points ────────────────────────

    [Fact]
    public void MonotoneCubicInterpolator_Interpolate_EmptyPoints_Throws_ArgumentException_NamedPoints()
    {
        // AC-G1: MonotoneCubicInterpolator must throw a located ArgumentException naming "points"
        // for an empty list — not a silent wrong result or NullRef.
        var sut = new MonotoneCubicInterpolator();

        Action act = () => sut.Interpolate(s_valuationDate, s_targetDate, s_emptyPoints, s_dayCount);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("points")
            .WithMessage("*points*");
    }

    private sealed class Act365DayCount : IYearFractionCalculator
    {
        public string Code => "ACT/365F";
        public double YearFraction(DateOnly startDate, DateOnly endDate) =>
            (endDate.DayNumber - startDate.DayNumber) / 365.0;
    }
}
