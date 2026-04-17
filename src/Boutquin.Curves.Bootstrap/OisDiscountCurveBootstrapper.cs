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

using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Bootstrap.Inputs;
using Boutquin.Curves.Core.Discounting;
using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Bootstrap;

/// <summary>
/// Provides a compatibility bootstrapper for simple OIS discount curve construction.
/// </summary>
/// <remarks>
/// This component intentionally produces a flat curve from the terminal quote and is kept for migration
/// and pedagogy. It allows legacy loaders to remain functional while teams adopt full node-by-node
/// calibration via <see cref="PiecewiseBootstrapCalibrator"/>.
///
/// OIS discount curves are the standard post-LIBOR collateral discounting framework. If you only need
/// present-value calculations for collateralized derivatives, the OIS discount curve alone is sufficient.
/// You need a separate projection curve only when pricing floating coupons that reference a specific
/// benchmark — the discount curve prices the cash flows, the projection curve forecasts them. Confusing
/// the two is the most common calibration error for engineers new to multi-curve: using the OIS discount
/// curve to project SOFR term rates overstates coupon fixings by the OIS-term basis, which compounds
/// across long-tenor swaps into material valuation differences.
/// </remarks>
public sealed class OisDiscountCurveBootstrapper
{
    /// <summary>
    /// Builds a flat discount curve from the terminal OIS/deposit quote.
    /// </summary>
    /// <param name="input">Bootstrap input values.</param>
    /// <returns>The resulting discount curve.</returns>
    /// <exception cref="ArgumentException">Thrown when no deposit and no OIS swap quotes are provided.</exception>
    /// <remarks>
    /// Quote selection is a simple terminal-point heuristic, not a market-standard bootstrap algorithm.
    /// Use this only for compatibility paths, demos, or coarse sanity checks.
    /// </remarks>
    public IDiscountCurve Bootstrap(OisBootstrapInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Deposits.Count == 0 && input.OisSwaps.Count == 0)
        {
            throw new ArgumentException("At least one OIS market quote is required.", nameof(input));
        }

        double terminalRate = input.OisSwaps.Count > 0
            ? input.OisSwaps[^1].FixedRate
            : input.Deposits[^1].Rate;

        CurrencyCode currency = input.Currency ?? CurrencyCode.USD;

        return new FlatDiscountCurve(
            new CurveName(input.CurveName),
            input.AnchorDate,
            currency,
            terminalRate,
            input.YearFractionCalculator);
    }
}
