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

using Boutquin.Curves.Abstractions.Identifiers;
using FluentAssertions;

namespace Boutquin.Curves.Abstractions.Tests;

/// <summary>
/// Tests for CF-C4-1: identifier structs must guard null/empty Value in constructor and
/// produce a non-null sentinel from ToString() on default(T) instances.
/// </summary>
public sealed class IdentifierStructTests
{
    // ── CurveName ──────────────────────────────────────────────────────────

    [Fact]
    public void CurveName_Constructor_NullValue_Throws()
    {
        Action act = () => _ = new CurveName(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CurveName_Constructor_EmptyValue_Throws()
    {
        Action act = () => _ = new CurveName(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CurveName_Constructor_WhitespaceValue_Throws()
    {
        Action act = () => _ = new CurveName("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CurveName_Default_ToString_Returns_Sentinel_Not_Null()
    {
        CurveName d = default;
        string result = d.ToString();
        result.Should().NotBeNull();
        result.Should().Be("<uninitialized>");
    }

    [Fact]
    public void CurveName_ValidValue_ToString_Returns_Value()
    {
        CurveName cn = new("USD-SOFR-Disc");
        cn.ToString().Should().Be("USD-SOFR-Disc");
    }

    // ── QuoteId ────────────────────────────────────────────────────────────

    [Fact]
    public void QuoteId_Constructor_NullValue_Throws()
    {
        Action act = () => _ = new QuoteId(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void QuoteId_Constructor_EmptyValue_Throws()
    {
        Action act = () => _ = new QuoteId(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void QuoteId_Constructor_WhitespaceValue_Throws()
    {
        Action act = () => _ = new QuoteId("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void QuoteId_Default_ToString_Returns_Sentinel_Not_Null()
    {
        QuoteId d = default;
        string result = d.ToString();
        result.Should().NotBeNull();
        result.Should().Be("<uninitialized>");
    }

    [Fact]
    public void QuoteId_ValidValue_ToString_Returns_Value()
    {
        QuoteId qi = new("NYFED:SOFR");
        qi.ToString().Should().Be("NYFED:SOFR");
    }

    // ── CurveGroupName ─────────────────────────────────────────────────────

    [Fact]
    public void CurveGroupName_Constructor_NullValue_Throws()
    {
        Action act = () => _ = new CurveGroupName(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CurveGroupName_Constructor_EmptyValue_Throws()
    {
        Action act = () => _ = new CurveGroupName(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CurveGroupName_Constructor_WhitespaceValue_Throws()
    {
        Action act = () => _ = new CurveGroupName("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CurveGroupName_Default_ToString_Returns_Sentinel_Not_Null()
    {
        CurveGroupName d = default;
        string result = d.ToString();
        result.Should().NotBeNull();
        result.Should().Be("<uninitialized>");
    }

    [Fact]
    public void CurveGroupName_ValidValue_ToString_Returns_Value()
    {
        CurveGroupName cgn = new("USD-SOFR-Discount");
        cgn.ToString().Should().Be("USD-SOFR-Discount");
    }
}
