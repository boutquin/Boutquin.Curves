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

using Boutquin.Curves.Bootstrap.Instruments;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Bootstrap.Inputs;

/// <summary>
/// Represents compatibility bootstrap input used to bridge legacy quote loaders into the current model.
/// </summary>
/// <remarks>
/// Pre-assembled input set for OIS discount curve bootstrapping. Contains the deposit quotes that
/// anchor the short end of the curve (overnight through a few months) and OIS swap quotes that
/// define the medium-to-long end. Together, these instruments provide enough market information
/// to solve for a complete discount factor curve. This DTO exists for backward-compatible ingestion
/// paths; modern calibration flows assemble inputs through <see cref="CurveCalibrationInput"/>
/// with explicit node definitions and quote-ID resolution.
/// </remarks>
/// <param name="CurveName">Logical output curve name used in diagnostics and downstream reporting.</param>
/// <param name="AnchorDate">Valuation date from which tenors and year fractions are measured.</param>
/// <param name="Deposits">Short-end money-market deposit quotes, typically used to anchor front maturities.</param>
/// <param name="OisSwaps">OIS par-rate strip used to shape medium and long maturities.</param>
/// <param name="YearFractionCalculator">Day-count calculator applied when converting date intervals to accrual fractions.</param>
/// <param name="Currency">Optional currency identity; defaults may be applied by compatibility bootstrappers when omitted.</param>
public sealed record OisBootstrapInput(
    string CurveName,
    DateOnly AnchorDate,
    IReadOnlyList<DepositQuote> Deposits,
    IReadOnlyList<OisSwapQuote> OisSwaps,
    IYearFractionCalculator YearFractionCalculator,
    CurrencyCode? Currency = null);
