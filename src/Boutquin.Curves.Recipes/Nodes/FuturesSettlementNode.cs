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
using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.Records;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Abstractions.Requests;

namespace Boutquin.Curves.Recipes.Nodes;

/// <summary>
/// Curve node that fetches futures settlement data and converts the settlement price to an implied rate.
/// </summary>
/// <remarks>
/// Requests settlement data for the given product code on the valuation date, filters to the
/// specified contract month, and converts the settlement price to an annualized rate using the
/// standard futures-price-to-rate formula: <c>(100 - price) / 100</c>. This is the standard
/// node type for STIR futures such as 3-month SOFR futures (SR3) or Fed Funds futures (ZQ).
/// </remarks>
public sealed class FuturesSettlementNode : ICurveNodeSpec
{
    private readonly string _productCode;
    private readonly ContractMonth _contractMonth;

    /// <summary>
    /// Creates a node specification for a futures settlement observation.
    /// </summary>
    /// <param name="label">Human-readable label for this node.</param>
    /// <param name="tenor">Calibration tenor for the futures contract (e.g., "3M").</param>
    /// <param name="productCode">Exchange product code dispatched to the data pipeline (e.g., "SR3").</param>
    /// <param name="contractMonth">Delivery month in YYYY-MM format to match against settlement records.</param>
    /// <param name="conventionCode">Convention code for day-count and payment rules.</param>
    /// <param name="targetCurve">The curve this node calibrates into.</param>
    public FuturesSettlementNode(
        string label,
        Tenor tenor,
        string productCode,
        string contractMonth,
        string conventionCode,
        CurveReference targetCurve)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(contractMonth);
        ArgumentException.ThrowIfNullOrWhiteSpace(conventionCode);
        ArgumentNullException.ThrowIfNull(targetCurve);

        // Parse and validate the contract month once, at construction, so a malformed-but-non-
        // whitespace value (e.g. "Jun26") fails fast here rather than throwing inside ExtractRate
        // — which runs outside CurveBuilder's fetch try/catch and would abort the whole BuildAsync.
        try
        {
            _contractMonth = new ContractMonth(contractMonth);
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException(ex.Message, nameof(contractMonth), ex);
        }

        Label = label;
        Tenor = tenor;
        _productCode = productCode;
        ConventionCode = conventionCode;
        TargetCurve = targetCurve;
    }

    /// <summary>Human-readable label identifying this futures calibration node.</summary>
    public string Label { get; }

    /// <summary>Calibration tenor identifying the futures pillar.</summary>
    public Tenor Tenor { get; }

    /// <summary>Overnight-index futures helper classification.</summary>
    public string InstrumentType => "OisFuture";

    /// <summary>Convention for futures accrual and pricing.</summary>
    public string ConventionCode { get; }

    /// <summary>Curve calibrated by this futures node.</summary>
    public CurveReference TargetCurve { get; }

    /// <summary>Requests product settlements on the valuation date.</summary>
    /// <param name="valuationDate">Settlement observation date.</param>
    /// <returns>A request for every contract month of the configured product on that date.</returns>
    public IDataRequest CreateRequest(DateOnly valuationDate) =>
        new FuturesSettlementRequest(
            new FuturesProductCode(_productCode),
            new DateRange(valuationDate, valuationDate));

    /// <summary>Selects the contract month's canonical implied rate or converts its settlement price.</summary>
    /// <param name="records">Canonical settlement records returned for this product.</param>
    /// <param name="valuationDate">As-of date of the associated request; records are filtered by contract month.</param>
    /// <returns>The supplied implied rate, or <c>(100 - price) / 100</c>; <see langword="null"/> when the contract is absent.</returns>
    public decimal? ExtractRate(IReadOnlyList<object> records, DateOnly valuationDate) =>
        records
            .OfType<FuturesSettlement>()
            .Where(s => s.ContractMonth == _contractMonth)
            .Select(s => (decimal?)(s.ImpliedRate ?? (100m - s.SettlePrice) / 100m))
            .FirstOrDefault();
}
