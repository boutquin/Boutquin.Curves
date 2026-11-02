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

using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

public sealed class TenorParserContractTests
{
    private static readonly DateOnly s_baseDate = new(2026, 4, 9);

    [Theory]
    [InlineData("XM")]   // non-numeric prefix → int.Parse FormatException
    [InlineData("M")]    // empty prefix → int.Parse FormatException
    [InlineData("3.5M")] // decimal prefix → int.Parse FormatException
    public void AddTenor_MalformedNumericPrefix_ThrowsDocumentedNotSupportedException(string tenor)
    {
        Action act = () => TenorParser.AddTenor(s_baseDate, new Tenor(tenor));

        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*{tenor}*");
    }

    [Fact]
    public void AddTenor_NumericPrefixBeyondInt32_ThrowsDocumentedNotSupportedException()
    {
        Action act = () => TenorParser.AddTenor(s_baseDate, new Tenor("99999999999Y"));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*99999999999Y*");
    }

    [Theory]
    [InlineData("3M")]
    [InlineData("2Y")]
    [InlineData("1W")]
    [InlineData("10D")]
    [InlineData("ON")]
    public void AddTenor_WellFormedTenor_DoesNotThrow(string tenor)
    {
        Action act = () => TenorParser.AddTenor(s_baseDate, new Tenor(tenor));

        act.Should().NotThrow();
    }
}
