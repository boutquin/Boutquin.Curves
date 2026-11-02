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

using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Abstractions.Tests;

/// <summary>
/// Regression tests for T-C5-1 and T-C5-2:
/// <list type="bullet">
/// <item>T-C5-1: <see cref="FallbackAdjustedBenchmarkFactory.Create"/> must reject
/// null/whitespace <c>name</c>, <c>calendarCode</c>, and <c>dayCountCode</c> with a
/// located <see cref="ArgumentException"/>.</item>
/// <item>T-C5-2: <see cref="BenchmarkCatalog.Add"/> must reject a null benchmark with a
/// located <see cref="ArgumentNullException"/> rather than a bare NullReferenceException.</item>
/// </list>
/// </summary>
public sealed class ArgumentGuardTests
{
    private static readonly Tenor s_someTenor = new("3M");

    // ── T-C5-1: FallbackAdjustedBenchmarkFactory.Create null/whitespace guards ──────

    [Fact]
    public void Create_NullName_Throws_ArgumentException()
    {
        Action act = () => FallbackAdjustedBenchmarkFactory.Create(
            null!, Currency.USD, s_someTenor, "USNY", "ACT/360");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Create_WhitespaceName_Throws_ArgumentException()
    {
        Action act = () => FallbackAdjustedBenchmarkFactory.Create(
            "   ", Currency.USD, s_someTenor, "USNY", "ACT/360");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Create_NullCalendarCode_Throws_ArgumentException()
    {
        Action act = () => FallbackAdjustedBenchmarkFactory.Create(
            "FALLBACK-SOFR", Currency.USD, s_someTenor, null!, "ACT/360");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("calendarCode");
    }

    [Fact]
    public void Create_WhitespaceCalendarCode_Throws_ArgumentException()
    {
        Action act = () => FallbackAdjustedBenchmarkFactory.Create(
            "FALLBACK-SOFR", Currency.USD, s_someTenor, " ", "ACT/360");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("calendarCode");
    }

    [Fact]
    public void Create_NullDayCountCode_Throws_ArgumentException()
    {
        Action act = () => FallbackAdjustedBenchmarkFactory.Create(
            "FALLBACK-SOFR", Currency.USD, s_someTenor, "USNY", null!);
        act.Should().Throw<ArgumentException>()
            .WithParameterName("dayCountCode");
    }

    [Fact]
    public void Create_WhitespaceDayCountCode_Throws_ArgumentException()
    {
        Action act = () => FallbackAdjustedBenchmarkFactory.Create(
            "FALLBACK-SOFR", Currency.USD, s_someTenor, "USNY", "  ");
        act.Should().Throw<ArgumentException>()
            .WithParameterName("dayCountCode");
    }

    // ── T-C5-2: BenchmarkCatalog.Add null-benchmark guard ───────────────────────────

    [Fact]
    public void Add_NullBenchmark_Throws_ArgumentNullException_Not_NullReferenceException()
    {
        var catalog = new BenchmarkCatalog();
        Action act = () => catalog.Add(null!);
        // Must throw ArgumentNullException (not bare NullReferenceException)
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("benchmark");
    }
}
