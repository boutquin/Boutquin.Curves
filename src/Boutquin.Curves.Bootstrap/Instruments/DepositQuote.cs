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

namespace Boutquin.Curves.Bootstrap.Instruments;

/// <summary>
/// Represents compatibility bootstrap input used to bridge legacy quote loaders into the current model.
/// </summary>
/// <remarks>
/// Compatibility bridge record for short-end cash deposit quotes. The <paramref name="TenorInMonths"/>
/// field specifies maturity as an integer month count from spot; the <paramref name="Rate"/> field is an
/// annualized deposit rate in decimal form (e.g., 0.0475 for 4.75%). This record is used by legacy
/// integration paths; modern flows use <c>CurveNodeDefinition</c> with explicit <c>QuoteId</c> resolution.
/// </remarks>
/// <param name="TenorInMonths">Deposit maturity tenor in months from anchor date.</param>
/// <param name="Rate">Quoted annualized deposit rate in decimal form (for example 0.0475 for 4.75%).</param>
public sealed record DepositQuote(int TenorInMonths, double Rate);
