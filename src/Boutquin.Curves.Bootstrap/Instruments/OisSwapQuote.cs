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
/// Represents the fixed rate of an overnight index swap (OIS) at a given tenor. In an OIS, one
/// counterparty pays a fixed rate while the other pays the compounded overnight reference rate
/// (e.g., SOFR or ESTR) over the swap period. The <paramref name="FixedRate"/> is the par rate at
/// which the swap has zero present value at inception, making it the primary calibration input for
/// the OIS discount curve. This compatibility record is used in legacy integration paths; modern
/// flows resolve OIS quotes through <c>CurveNodeDefinition</c> with explicit <c>QuoteId</c> bindings.
/// </remarks>
/// <param name="TenorInMonths">OIS maturity tenor in months from anchor date.</param>
/// <param name="FixedRate">Quoted OIS fixed leg par rate in decimal form.</param>
public sealed record OisSwapQuote(int TenorInMonths, double FixedRate);
