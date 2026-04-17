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

namespace Boutquin.Curves.Abstractions.Quotes;

/// <summary>
/// Represents a raw market quote captured from a source feed or fixture.
/// </summary>
/// <remarks>
/// Pipeline context: MarketQuote is the normalized input stage of the curve construction pipeline.
/// Raw data from adapters (NY Fed, Treasury, CME, Bank of Canada) is collected into MarketQuote
/// records keyed by QuoteId, then assembled into a MarketQuoteSet for downstream consumers.
/// </remarks>
/// <param name="Id">Unique quote identifier used for lookup in calibration requests.</param>
/// <param name="Value">Quoted numeric value as published by the source.</param>
/// <param name="FieldName">Field name from the source system (for example LAST, MID, or RATE).</param>
/// <param name="AsOfDate">Market date at which the quote is observed.</param>
/// <param name="Source">Optional source label indicating data origin.</param>
/// <param name="Unit">Optional unit descriptor (for example percent, bps, or price).</param>
/// <param name="Notes">Optional free-form notes attached during ingestion or normalization.</param>
public sealed record MarketQuote(
    QuoteId Id,
    decimal Value,
    string FieldName,
    DateOnly AsOfDate,
    string? Source = null,
    string? Unit = null,
    string? Notes = null);
