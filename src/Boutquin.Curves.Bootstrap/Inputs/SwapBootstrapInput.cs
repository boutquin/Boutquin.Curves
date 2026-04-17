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
/// Contains swap par rates for the projection (forward) curve bootstrap, typically covering tenors
/// from 2Y through 30Y. Each <see cref="InterestRateSwapQuote"/> in the <paramref name="Swaps"/>
/// collection provides a par fixed rate at one maturity point; the bootstrapper solves for the
/// forward rates that reproduce those par rates under the applicable day-count and payment
/// frequency conventions. This record preserves a legacy swap-strip ingestion shape while allowing
/// callers to transition toward richer node-definition-based calibration requests.
/// </remarks>
/// <param name="CurveName">Logical curve name used in diagnostics, persistence, and output labeling.</param>
/// <param name="AnchorDate">Valuation date that anchors tenor interpretation and year-fraction calculations.</param>
/// <param name="Swaps">Swap par-rate strip used by compatibility bootstrappers.</param>
/// <param name="YearFractionCalculator">Day-count calculator used in compatibility conversions.</param>
/// <param name="Currency">Optional currency identity associated with the input strip.</param>
public sealed record SwapBootstrapInput(
    string CurveName,
    DateOnly AnchorDate,
    IReadOnlyList<InterestRateSwapQuote> Swaps,
    IYearFractionCalculator YearFractionCalculator,
    CurrencyCode? Currency = null);
