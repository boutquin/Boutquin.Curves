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
using Boutquin.Curves.Abstractions.Quotes;
using FluentAssertions;

namespace Boutquin.Curves.Serialization.Tests;

/// <summary>
/// Verifies CSV quote loading into <see cref="MarketQuoteSet"/>.
/// </summary>
public sealed class CsvQuoteLoaderTests
{
    [Fact]
    public void Load_ValidCsv_ReturnsMarketQuoteSet()
    {
        // Arrange
        const string csv = """
            QuoteId,Value,FieldName,AsOfDate
            NYFED:SOFR,0.0530,RATE,2024-01-15
            CME:SOFR-1M,95.25,SETTLE,2024-01-15
            UST:PAR-10Y,0.0415,MID,2024-01-15
            """;

        // Act
        var result = CsvQuoteLoader.Load(csv);

        // Assert
        result.AsOfDate.Should().Be(new DateOnly(2024, 1, 15));
        result.Quotes.Should().HaveCount(3);

        var sofr = result.GetRequired(new QuoteId("NYFED:SOFR"));
        sofr.Value.Should().Be(0.0530m);
        sofr.FieldName.Should().Be("RATE");
        sofr.AsOfDate.Should().Be(new DateOnly(2024, 1, 15));
    }

    [Fact]
    public void Load_MultipleDates_UsesFirstRowDate()
    {
        // The loader uses the first data row's AsOfDate as the set's AsOfDate.
        const string csv = """
            QuoteId,Value,FieldName,AsOfDate
            NYFED:SOFR,0.0530,RATE,2024-01-15
            CME:SOFR-1M,95.25,SETTLE,2024-01-16
            """;

        var result = CsvQuoteLoader.Load(csv);

        result.AsOfDate.Should().Be(new DateOnly(2024, 1, 15));
        result.Quotes.Should().HaveCount(2);
    }

    [Fact]
    public void Load_WithOptionalSourceColumn_ParsesSource()
    {
        const string csv = """
            QuoteId,Value,FieldName,AsOfDate,Source
            NYFED:SOFR,0.0530,RATE,2024-01-15,NY Fed API
            """;

        var result = CsvQuoteLoader.Load(csv);

        var quote = result.GetRequired(new QuoteId("NYFED:SOFR"));
        quote.Source.Should().Be("NY Fed API");
    }

    [Fact]
    public void Load_EmptyCsv_ThrowsFormatException()
    {
        const string csv = "";

        var act = () => CsvQuoteLoader.Load(csv);

        act.Should().Throw<FormatException>()
            .WithMessage("*header*");
    }

    [Fact]
    public void Load_HeaderOnly_ThrowsFormatException()
    {
        const string csv = "QuoteId,Value,FieldName,AsOfDate";

        var act = () => CsvQuoteLoader.Load(csv);

        act.Should().Throw<FormatException>()
            .WithMessage("*no data rows*");
    }

    [Fact]
    public void Load_MissingRequiredColumn_ThrowsFormatException()
    {
        const string csv = """
            QuoteId,Value,FieldName
            NYFED:SOFR,0.0530,RATE
            """;

        var act = () => CsvQuoteLoader.Load(csv);

        act.Should().Throw<FormatException>()
            .WithMessage("*AsOfDate*");
    }

    [Fact]
    public void Load_DuplicateQuoteId_ThrowsFormatException()
    {
        const string csv = """
            QuoteId,Value,FieldName,AsOfDate
            NYFED:SOFR,0.0530,RATE,2024-01-15
            NYFED:SOFR,0.0531,RATE,2024-01-15
            """;

        var act = () => CsvQuoteLoader.Load(csv);

        act.Should().Throw<FormatException>()
            .WithMessage("*duplicate*NYFED:SOFR*");
    }

    [Fact]
    public void Load_InvalidDecimalValue_ThrowsFormatException()
    {
        const string csv = """
            QuoteId,Value,FieldName,AsOfDate
            NYFED:SOFR,not_a_number,RATE,2024-01-15
            """;

        var act = () => CsvQuoteLoader.Load(csv);

        act.Should().Throw<FormatException>()
            .WithMessage("*Value*row 2*");
    }

    [Fact]
    public void Load_InvalidDate_ThrowsFormatException()
    {
        const string csv = """
            QuoteId,Value,FieldName,AsOfDate
            NYFED:SOFR,0.0530,RATE,not-a-date
            """;

        var act = () => CsvQuoteLoader.Load(csv);

        act.Should().Throw<FormatException>()
            .WithMessage("*AsOfDate*row 2*");
    }

    [Fact]
    public void Load_FromStream_ReturnsMarketQuoteSet()
    {
        const string csv = """
            QuoteId,Value,FieldName,AsOfDate
            NYFED:SOFR,0.0530,RATE,2024-01-15
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        var result = CsvQuoteLoader.Load(stream);

        result.Quotes.Should().HaveCount(1);
        result.AsOfDate.Should().Be(new DateOnly(2024, 1, 15));
    }

    [Fact]
    public void Load_WithUnitAndNotesColumns_ParsesAll()
    {
        const string csv = """
            QuoteId,Value,FieldName,AsOfDate,Source,Unit,Notes
            NYFED:SOFR,0.0530,RATE,2024-01-15,NY Fed API,percent,daily fixing
            """;

        var result = CsvQuoteLoader.Load(csv);

        var quote = result.GetRequired(new QuoteId("NYFED:SOFR"));
        quote.Unit.Should().Be("percent");
        quote.Notes.Should().Be("daily fixing");
    }

    [Fact]
    public void Load_WhitespaceAroundValues_Trims()
    {
        const string csv = """
            QuoteId,Value,FieldName,AsOfDate
             NYFED:SOFR , 0.0530 , RATE , 2024-01-15
            """;

        var result = CsvQuoteLoader.Load(csv);

        result.Quotes.Should().ContainKey(new QuoteId("NYFED:SOFR"));
    }
}
