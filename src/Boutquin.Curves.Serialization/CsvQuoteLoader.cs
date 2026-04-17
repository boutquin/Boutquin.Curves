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

using System.Globalization;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Abstractions.Quotes;

namespace Boutquin.Curves.Serialization;

/// <summary>
/// Loads market quotes from CSV text into a <see cref="MarketQuoteSet"/>.
/// </summary>
/// <remarks>
/// Expected CSV format has a header row followed by data rows. Required columns are
/// <c>QuoteId</c>, <c>Value</c>, <c>FieldName</c>, and <c>AsOfDate</c>. Optional columns
/// <c>Source</c>, <c>Unit</c>, and <c>Notes</c> are parsed when present. The set's
/// <see cref="MarketQuoteSet.AsOfDate"/> is taken from the first data row. All values
/// are trimmed of whitespace before parsing.
/// </remarks>
/// <example>
/// <code>
/// string csv = File.ReadAllText("quotes.csv");
/// MarketQuoteSet quotes = CsvQuoteLoader.Load(csv);
/// </code>
/// </example>
public static class CsvQuoteLoader
{
    private static readonly string[] s_requiredColumns = ["QuoteId", "Value", "FieldName", "AsOfDate"];

    /// <summary>
    /// Parses CSV text into a <see cref="MarketQuoteSet"/>.
    /// </summary>
    /// <param name="csv">CSV content with a header row and one or more data rows.</param>
    /// <returns>Market quote set populated from the CSV data.</returns>
    /// <exception cref="FormatException">
    /// Thrown when the CSV is empty, missing required columns, contains duplicate quote IDs,
    /// or has unparseable values.
    /// </exception>
    public static MarketQuoteSet Load(string csv)
    {
        using var reader = new StringReader(csv);
        return LoadCore(reader);
    }

    /// <summary>
    /// Parses a CSV stream into a <see cref="MarketQuoteSet"/>.
    /// </summary>
    /// <param name="stream">Readable stream containing CSV content.</param>
    /// <returns>Market quote set populated from the CSV data.</returns>
    /// <exception cref="FormatException">
    /// Thrown when the CSV is empty, missing required columns, contains duplicate quote IDs,
    /// or has unparseable values.
    /// </exception>
    public static MarketQuoteSet Load(Stream stream)
    {
        using var reader = new StreamReader(stream);
        return LoadCore(reader);
    }

    private static MarketQuoteSet LoadCore(TextReader reader)
    {
        var headerLine = reader.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new FormatException("CSV is empty or missing a header row.");
        }

        var headers = headerLine.Split(',').Select(h => h.Trim()).ToArray();
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headers.Length; i++)
        {
            columnIndex[headers[i]] = i;
        }

        foreach (var required in s_requiredColumns)
        {
            if (!columnIndex.ContainsKey(required))
            {
                throw new FormatException($"CSV header is missing required column '{required}'.");
            }
        }

        var quotes = new Dictionary<QuoteId, MarketQuote>();
        DateOnly? setDate = null;
        int rowNumber = 1;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            rowNumber++;
            var fields = trimmed.Split(',').Select(f => f.Trim()).ToArray();

            var quoteIdValue = fields[columnIndex["QuoteId"]];

            if (!decimal.TryParse(fields[columnIndex["Value"]], NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"Cannot parse Value on row {rowNumber}: '{fields[columnIndex["Value"]]}'.");
            }

            var fieldName = fields[columnIndex["FieldName"]];

            if (!DateOnly.TryParseExact(fields[columnIndex["AsOfDate"]], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var asOfDate))
            {
                throw new FormatException($"Cannot parse AsOfDate on row {rowNumber}: '{fields[columnIndex["AsOfDate"]]}'.");
            }

            setDate ??= asOfDate;

            string? source = GetOptional(fields, columnIndex, "Source");
            string? unit = GetOptional(fields, columnIndex, "Unit");
            string? notes = GetOptional(fields, columnIndex, "Notes");

            var quoteId = new QuoteId(quoteIdValue);
            var quote = new MarketQuote(quoteId, value, fieldName, asOfDate, source, unit, notes);

            if (!quotes.TryAdd(quoteId, quote))
            {
                throw new FormatException($"CSV contains duplicate QuoteId '{quoteIdValue}'.");
            }
        }

        if (setDate is null)
        {
            throw new FormatException("CSV contains no data rows.");
        }

        return new MarketQuoteSet(setDate.Value, quotes);
    }

    private static string? GetOptional(string[] fields, Dictionary<string, int> columnIndex, string columnName)
    {
        if (!columnIndex.TryGetValue(columnName, out var index) || index >= fields.Length)
        {
            return null;
        }

        var value = fields[index];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
