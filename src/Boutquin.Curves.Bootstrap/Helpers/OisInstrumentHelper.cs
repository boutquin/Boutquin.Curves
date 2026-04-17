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
/// Solves OIS bootstrap nodes from quoted par rates.
/// </summary>
/// <remarks>
/// Uses continuous-compounding discounting: <c>df = exp(-rate * tau)</c>. Repricing inverts via
/// <c>rate = -ln(df) / tau</c> with a <c>1e-15</c> floor on df to prevent <c>ln(0)</c>.
/// The year fraction is floored at <c>1e-12</c> to prevent division by zero.
/// </remarks>
public sealed class OisInstrumentHelper : AbstractInstrumentHelper
{
    private readonly ResolvedNode _node;

    /// <summary>
    /// Initializes a new instance of the <see cref="OisInstrumentHelper"/> type.
    /// </summary>
    /// <param name="node">Resolved node carrying rate and metadata.</param>
    /// <param name="quoteValue">Observed market quote for the OIS instrument.</param>
    public OisInstrumentHelper(ResolvedNode node, double quoteValue)
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
        return Math.Exp(-QuoteValue * yearFraction);
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
        return -Math.Log(Math.Max(discountFactor.Value, 1e-15)) / yearFraction;
    }
}
