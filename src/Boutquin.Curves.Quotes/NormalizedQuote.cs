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

namespace Boutquin.Curves.Quotes;

/// <summary>
/// Represents a quote after normalization into canonical value type, instrument type, and convention metadata.
/// </summary>
/// <param name="QuoteId">Identifier of the market quote bound to this node.</param>
/// <param name="Value">Numeric quote value.</param>
/// <param name="ValueType">Curve ordinate type solved during calibration.</param>
/// <param name="InstrumentType">Instrument classification metadata.</param>
/// <param name="ConventionCode">Convention code used for schedule and accrual rules.</param>
/// <param name="Label">Node label used in diagnostics and reporting.</param>
public sealed record NormalizedQuote(
    QuoteId QuoteId,
    decimal Value,
    QuoteValueType ValueType,
    MarketInstrumentType InstrumentType,
    string ConventionCode,
    string Label);
