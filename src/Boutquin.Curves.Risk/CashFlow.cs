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

namespace Boutquin.Curves.Risk;

/// <summary>
/// Represents a single deterministic cash flow: a fixed monetary amount paid on a specific date.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CashFlow"/> is the atomic unit consumed by <see cref="CurveRiskMetrics"/> extension
/// methods. It intentionally carries no pricing logic — it is a pure data carrier composable with
/// schedule generators, bond analytics, swap leg builders, and any other source of dated cash amounts.
/// </para>
/// <para>
/// <see cref="Amount"/> is a decimal to preserve cent-level precision when working with monetary
/// quantities. The risk computations in <see cref="CurveRiskMetrics"/> convert to <c>double</c>
/// internally for numerical stability, so the public API can accept large arrays without accumulating
/// decimal rounding error.
/// </para>
/// </remarks>
/// <param name="PaymentDate">The date on which the cash flow is paid.</param>
/// <param name="Amount">Notional amount of the cash flow in <paramref name="Currency"/>. Positive for receipts, negative for payments.</param>
/// <param name="Currency">Currency of the cash flow. Must match the discount curve's currency for correct present-value computation.</param>
public sealed record CashFlow(DateOnly PaymentDate, decimal Amount, Currency Currency);
