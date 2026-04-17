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
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

/// <summary>
/// Tests for <see cref="CurveGroupExtensions"/> convenience methods.
/// </summary>
public sealed class CurveGroupExtensionsTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 11);
    private const CurrencyCode Usd = CurrencyCode.USD;

    [Fact]
    public void GetDiscountCurve_Returns_DiscountCurve_For_Currency()
    {
        var discountCurve = new FlatDiscountCurve(new CurveName("USD-Disc"), s_valuationDate, Usd, 0.05);
        var reference = new CurveReference(CurveRole.Discount, Usd);
        var group = new CurveGroupBuilder(new CurveGroupName("Test"), s_valuationDate)
            .Add(reference, discountCurve)
            .Build();

        var result = group.GetDiscountCurve(Usd);

        result.Should().BeSameAs(discountCurve);
    }

    [Fact]
    public void GetDiscountCurve_Throws_KeyNotFound_For_Missing_Currency()
    {
        var group = new CurveGroupBuilder(new CurveGroupName("Test"), s_valuationDate).Build();

        var act = () => group.GetDiscountCurve(Usd);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void TryGetDiscountCurve_Returns_True_When_Found()
    {
        var discountCurve = new FlatDiscountCurve(new CurveName("USD-Disc"), s_valuationDate, Usd, 0.05);
        var reference = new CurveReference(CurveRole.Discount, Usd);
        var group = new CurveGroupBuilder(new CurveGroupName("Test"), s_valuationDate)
            .Add(reference, discountCurve)
            .Build();

        var found = group.TryGetDiscountCurve(Usd, out var result);

        found.Should().BeTrue();
        result.Should().BeSameAs(discountCurve);
    }

    [Fact]
    public void TryGetDiscountCurve_Returns_False_When_Missing()
    {
        var group = new CurveGroupBuilder(new CurveGroupName("Test"), s_valuationDate).Build();

        var found = group.TryGetDiscountCurve(Usd, out var result);

        found.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void GetForwardCurve_Returns_Curve_For_Benchmark()
    {
        var benchmark = new BenchmarkName("SOFR");
        var forwardCurve = new FlatDiscountCurve(new CurveName("USD-SOFR-Fwd"), s_valuationDate, Usd, 0.05);
        var reference = new CurveReference(CurveRole.Forward, Usd, benchmark);
        var group = new CurveGroupBuilder(new CurveGroupName("Test"), s_valuationDate)
            .Add(reference, forwardCurve)
            .Build();

        var result = group.GetForwardCurve(Usd, benchmark);

        result.Should().BeSameAs(forwardCurve);
    }

    [Fact]
    public void GetForwardCurve_Throws_KeyNotFound_For_Missing_Benchmark()
    {
        var group = new CurveGroupBuilder(new CurveGroupName("Test"), s_valuationDate).Build();

        var act = () => group.GetForwardCurve(Usd, new BenchmarkName("SOFR"));

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void TryGetForwardCurve_Returns_True_When_Found()
    {
        var benchmark = new BenchmarkName("SOFR");
        var forwardCurve = new FlatDiscountCurve(new CurveName("USD-SOFR-Fwd"), s_valuationDate, Usd, 0.05);
        var reference = new CurveReference(CurveRole.Forward, Usd, benchmark);
        var group = new CurveGroupBuilder(new CurveGroupName("Test"), s_valuationDate)
            .Add(reference, forwardCurve)
            .Build();

        var found = group.TryGetForwardCurve(Usd, benchmark, out var result);

        found.Should().BeTrue();
        result.Should().BeSameAs(forwardCurve);
    }

    [Fact]
    public void TryGetForwardCurve_Returns_False_When_Missing()
    {
        var group = new CurveGroupBuilder(new CurveGroupName("Test"), s_valuationDate).Build();

        var found = group.TryGetForwardCurve(Usd, new BenchmarkName("SOFR"), out var result);

        found.Should().BeFalse();
        result.Should().BeNull();
    }
}
