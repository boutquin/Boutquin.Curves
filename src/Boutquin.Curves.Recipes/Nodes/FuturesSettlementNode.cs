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
    private readonly string _contractMonth;

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
        Label = label;
        Tenor = tenor;
        _productCode = productCode;
        _contractMonth = contractMonth;
        ConventionCode = conventionCode;
        TargetCurve = targetCurve;
    }

    /// <inheritdoc />
    public string Label { get; }

    /// <inheritdoc />
    public Tenor Tenor { get; }

    /// <inheritdoc />
    public string InstrumentType => "OisFuture";

    /// <inheritdoc />
    public string ConventionCode { get; }

    /// <inheritdoc />
    public CurveReference TargetCurve { get; }

    /// <inheritdoc />
    public IDataRequest CreateRequest(DateOnly valuationDate) =>
        new FuturesSettlementRequest(
            new FuturesProductCode(_productCode),
            new DateRange(valuationDate, valuationDate));

    /// <inheritdoc />
    public decimal? ExtractRate(IReadOnlyList<object> records, DateOnly valuationDate) =>
        records
            .OfType<FuturesSettlement>()
            .Where(s => s.ContractMonth == new ContractMonth(_contractMonth))
            .Select(s => (decimal?)((100m - s.SettlePrice) / 100m))
            .FirstOrDefault();
}
