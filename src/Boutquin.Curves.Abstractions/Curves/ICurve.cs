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
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Abstractions.Curves;

/// <summary>
/// Defines the minimum contract for a term-structure object in the market layer.
/// </summary>
/// <remarks>
/// A curve maps maturity dates to numerical states used by pricing engines (for example,
/// discount factors, zero rates, or forward rates). In production banking systems, this
/// abstraction is intentionally small so different curve families can be swapped without
/// changing valuation pipelines.
/// </remarks>
public interface ICurve
{
    /// <summary>
    /// Curve identifier used in diagnostics, serialization, and downstream risk reports.
    /// </summary>
    CurveName Name { get; }

    /// <summary>
    /// Market date at which the curve is calibrated.
    /// </summary>
    /// <remarks>
    /// Time-to-maturity is measured from this date. For most implementations,
    /// <c>ValueAt(ValuationDate)</c> represents the anchor state (for example discount factor 1.0).
    /// </remarks>
    DateOnly ValuationDate { get; }

    /// <summary>
    /// Currency of the cash flows this curve is intended to price.
    /// </summary>
    /// <remarks>
    /// Multi-curve setups typically maintain one discount curve per collateral/funding currency,
    /// plus separate projection curves per index. Currency consistency is a first-line model-control check.
    /// </remarks>
    CurrencyCode Currency { get; }

    /// <summary>
    /// Day-count convention used to convert date intervals into year fractions.
    /// </summary>
    /// <remarks>
    /// Day-count choice (ACT/360, ACT/365F, 30/360, and so on) changes accrual factors and therefore
    /// forward-rate and PV outputs. Treat it as part of the product convention, not an implementation detail.
    /// </remarks>
    IYearFractionCalculator DayCount { get; }

    /// <summary>
    /// Returns the curve ordinate at the requested date.
    /// </summary>
    /// <param name="date">Target date for evaluation.</param>
    /// <returns>Curve value at <paramref name="date"/> in the native representation of the concrete curve type.</returns>
    /// <remarks>
    /// Consumers should prefer type-specific methods (for example discount-factor or forward-rate accessors)
    /// when semantics matter. This generic accessor exists for infrastructure workflows that operate on curves
    /// polymorphically (serialization, charting, diagnostics, and scenario engines).
    /// </remarks>
    double ValueAt(DateOnly date);
}
