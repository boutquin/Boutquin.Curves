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
/// Provides a compatibility bootstrapper for simple swap projection curve construction.
/// </summary>
/// <remarks>
/// This is a transition helper that collapses a swap quote strip into a flat curve. It keeps old
/// integration points working while richer projection-curve calibration is introduced incrementally.
///
/// Projection curves are needed when the floating leg of a swap or loan references a benchmark rate
/// that differs from the overnight rate used for discounting. In a single-curve world (where LIBOR was
/// both the discount and projection rate), this separation was unnecessary. Post-LIBOR, the discount
/// curve (OIS) and projection curve (term benchmark) are always distinct, and using the wrong curve for
/// the wrong purpose produces material valuation errors. If you discount with the projection curve, you
/// overstate present values because term rates embed a credit/liquidity spread over OIS. If you project
/// with the discount curve, you understate future coupon fixings by that same spread. Both errors
/// compound across the swap tenor and are most visible in long-dated positions.
/// </remarks>
public sealed class SwapProjectionCurveBootstrapper
{
    /// <summary>
    /// Builds a flat discount curve from the terminal swap quote.
    /// </summary>
    /// <param name="input">Bootstrap input values.</param>
    /// <returns>The resulting curve.</returns>
    /// <exception cref="ArgumentException">Thrown when the swap quote set is empty.</exception>
    /// <remarks>
    /// The terminal quote is treated as the single representative rate for all maturities.
    /// This is intentionally simplistic and should not be used for production risk or hedge construction.
    /// </remarks>
    public IDiscountCurve Bootstrap(SwapBootstrapInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Swaps.Count == 0)
        {
            throw new ArgumentException("At least one swap quote is required.", nameof(input));
        }

        double terminalRate = input.Swaps[^1].FixedRate;
        CurrencyCode currency = input.Currency ?? CurrencyCode.USD;

        return new FlatDiscountCurve(
            new CurveName(input.CurveName),
            input.AnchorDate,
            currency,
            terminalRate,
            input.YearFractionCalculator);
    }
}
