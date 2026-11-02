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
/// Tests for <see cref="CurveGroupBuilder"/>.
/// Covers F-34 (valuation-date consistency in Build).
/// </summary>
public sealed class CurveGroupBuilderTests
{
    private static readonly DateOnly s_date = new(2026, 4, 9);

    private static InterpolatedDiscountCurve MakeCurve(DateOnly valuationDate, Currency currency = Currency.USD)
    {
        return new InterpolatedDiscountCurve(
            new CurveName("TEST"),
            valuationDate,
            currency,
            new[] { new CurvePoint(valuationDate.AddYears(1), 0.96d) });
    }

    // ── F-34: Valuation-date consistency validation in Build ──────────────────

    [Fact]
    public void Build_ThrowsInvalidOperationException_WhenCurveHasDifferentValuationDate()
    {
        CurveReference reference = new(CurveRole.Discount, Currency.USD);
        DateOnly differentDate = s_date.AddDays(1);

        CurveGroupBuilder builder = new(new CurveGroupName("TEST"), s_date);
        builder.Add(reference, MakeCurve(differentDate)); // curve has tomorrow's valuation date

        Action act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*valuation date*");
    }

    [Fact]
    public void Build_Succeeds_WhenAllCurvesShareGroupValuationDate()
    {
        CurveReference discRef = new(CurveRole.Discount, Currency.USD);
        CurveReference fwdRef = new(CurveRole.Forward, Currency.USD);

        CurveGroupBuilder builder = new(new CurveGroupName("TEST"), s_date);
        builder.Add(discRef, MakeCurve(s_date));
        builder.Add(fwdRef, MakeCurve(s_date));

        CurveGroup group = builder.Build();

        group.ValuationDate.Should().Be(s_date);
        group.TryGetCurve(discRef, out _).Should().BeTrue();
        group.TryGetCurve(fwdRef, out _).Should().BeTrue();
    }

    [Fact]
    public void Build_Succeeds_WhenBuilderIsEmpty()
    {
        CurveGroupBuilder builder = new(new CurveGroupName("EMPTY"), s_date);
        CurveGroup group = builder.Build();
        group.ValuationDate.Should().Be(s_date);
    }

    // ── AT-21: Add must reject null curve immediately, not defer to Build ─────

    /// <summary>
    /// AT-21: <c>CurveGroupBuilder.Add</c> must throw <see cref="ArgumentNullException"/>
    /// immediately when the <c>curve</c> argument is null, rather than silently storing the null
    /// reference and deferring the failure to <c>Build()</c> with a cryptic NullReferenceException.
    /// </summary>
    [Fact]
    public void Add_NullCurve_ThrowsArgumentNullExceptionImmediately()
    {
        CurveReference reference = new(CurveRole.Discount, Currency.USD);
        CurveGroupBuilder builder = new(new CurveGroupName("TEST"), s_date);

        // Act — must throw at Add, not at Build
        Action act = () => builder.Add(reference, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("curve");
    }

    /// <summary>
    /// AT-21 regression: Add must also reject null reference key immediately.
    /// </summary>
    [Fact]
    public void Add_NullReference_ThrowsArgumentNullExceptionImmediately()
    {
        CurveGroupBuilder builder = new(new CurveGroupName("TEST"), s_date);
        ICurve curve = MakeCurve(s_date);

        Action act = () => builder.Add(null!, curve);

        act.Should().Throw<ArgumentNullException>().WithParameterName("reference");
    }
}
