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
/// Curve node that fetches yield-curve quotes and extracts the rate for a specific tenor.
/// </summary>
/// <remarks>
/// Requests the full curve snapshot for the valuation date and filters to the single
/// <see cref="YieldCurveQuote"/> whose <c>Tenor</c> matches the configured target tenor string.
/// This is the standard node type for par-yield or zero-rate instruments such as Treasury
/// par yields, SOFR OIS par rates, or swap rates quoted by tenor.
/// </remarks>
public sealed class YieldCurveNode : ICurveNodeSpec
{
    private readonly string _curveId;
    private readonly string _targetTenor;

    /// <summary>
    /// Creates a node specification for a yield-curve quote tenor.
    /// </summary>
    /// <param name="label">Human-readable label for this node.</param>
    /// <param name="tenor">Calibration tenor (e.g., "3M", "1Y", "10Y").</param>
    /// <param name="instrumentType">Instrument type for the calibrator (e.g., "Swap", "Bond").</param>
    /// <param name="curveId">Curve identifier dispatched to the data pipeline (e.g., "USD-TREASURY").</param>
    /// <param name="targetTenor">Tenor string to match against <see cref="YieldCurveQuote.Tenor"/>.</param>
    /// <param name="conventionCode">Convention code for day-count and payment rules.</param>
    /// <param name="targetCurve">The curve this node calibrates into.</param>
    public YieldCurveNode(
        string label,
        Tenor tenor,
        string instrumentType,
        string curveId,
        string targetTenor,
        string conventionCode,
        CurveReference targetCurve)
    {
        Label = label;
        Tenor = tenor;
        InstrumentType = instrumentType;
        _curveId = curveId;
        _targetTenor = targetTenor;
        ConventionCode = conventionCode;
        TargetCurve = targetCurve;
    }

    /// <inheritdoc />
    public string Label { get; }

    /// <inheritdoc />
    public Tenor Tenor { get; }

    /// <inheritdoc />
    public string InstrumentType { get; }

    /// <inheritdoc />
    public string ConventionCode { get; }

    /// <inheritdoc />
    public CurveReference TargetCurve { get; }

    /// <inheritdoc />
    public IDataRequest CreateRequest(DateOnly valuationDate) =>
        new YieldCurveQuoteRequest(new YieldCurveId(_curveId), valuationDate);

    /// <inheritdoc />
    public decimal? ExtractRate(IReadOnlyList<object> records, DateOnly valuationDate) =>
        records
            .OfType<YieldCurveQuote>()
            .Where(q => string.Equals(q.Tenor, _targetTenor, StringComparison.OrdinalIgnoreCase))
            .Select(q => (decimal?)q.Rate)
            .FirstOrDefault();
}
