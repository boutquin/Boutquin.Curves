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
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Bootstrap.Helpers;

/// <summary>
/// Solves deposit bootstrap nodes by converting market quotes into discount factors.
/// </summary>
/// <remarks>
/// Uses simple-rate discounting: <c>df = 1 / (1 + rate * tau)</c>. The year fraction is floored
/// at <c>1e-12</c> to prevent division by zero for same-day or very short tenors. Expects an
/// <see cref="OisConvention"/> from reference data; an <see cref="InvalidCastException"/> results
/// if the convention code resolves to a different convention type.
/// Deposit helpers usually calibrate the short-end anchor where precision in day-count treatment matters
/// most for front-end risk and cash-product repricing.
/// </remarks>
public sealed class DepositInstrumentHelper : AbstractInstrumentHelper
{
    private readonly ResolvedNode _node;

    /// <summary>
    /// Initializes a new instance of the <see cref="DepositInstrumentHelper"/> type.
    /// </summary>
    /// <param name="node">Resolved node carrying rate and metadata.</param>
    /// <param name="quoteValue">Observed market quote for the deposit instrument.</param>
    public DepositInstrumentHelper(ResolvedNode node, double quoteValue)
        : base(node.Label, node.TargetCurve, quoteValue, node.InstrumentType)
    {
        _node = node;
    }

    /// <inheritdoc/>
    public override DateOnly PillarDate(DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        return TenorParser.AddTenor(valuationDate, _node.Tenor);
    }

    /// <inheritdoc/>
    public override double SolveNodeValue(ICurveGroup partialCurveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        DateOnly pillar = PillarDate(valuationDate, referenceData);
        OisConvention convention = (OisConvention)referenceData.GetConvention(_node.ConventionCode);
        double yearFraction = Math.Max(1e-12, DayCountResolver.Resolve(convention.DayCountCode).YearFraction(valuationDate, pillar));
        return 1d / (1d + (QuoteValue * yearFraction));
    }

    /// <inheritdoc/>
    public override double ImpliedQuote(ICurveGroup curveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        DateOnly pillar = PillarDate(valuationDate, referenceData);
        double? discountFactor = TryGetDiscountFactor(curveGroup, pillar);
        if (!discountFactor.HasValue)
        {
            return QuoteValue;
        }

        OisConvention convention = (OisConvention)referenceData.GetConvention(_node.ConventionCode);
        double yearFraction = Math.Max(1e-12, DayCountResolver.Resolve(convention.DayCountCode).YearFraction(valuationDate, pillar));
        return (1d / discountFactor.Value - 1d) / yearFraction;
    }
}
