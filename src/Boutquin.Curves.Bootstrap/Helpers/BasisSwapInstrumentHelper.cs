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

namespace Boutquin.Curves.Bootstrap.Helpers;

/// <summary>
/// Solves basis-swap bootstrap nodes and reprices them from the calibrated discount curve.
/// </summary>
/// <remarks>
/// Uses continuous-compounding discounting with an approximate ACT/365 year fraction:
/// <c>df = exp(-rate * dayCount/365)</c>. The year fraction is floored at <c>1e-12</c> to
/// prevent division by zero.
/// </remarks>
public sealed class BasisSwapInstrumentHelper : AbstractInstrumentHelper
{
    private readonly ResolvedNode _node;

    /// <summary>
    /// Initializes a new instance of the <see cref="BasisSwapInstrumentHelper"/> type.
    /// </summary>
    /// <param name="node">Resolved node carrying rate and metadata.</param>
    /// <param name="quoteValue">Observed market quote for the basis-swap instrument.</param>
    public BasisSwapInstrumentHelper(ResolvedNode node, double quoteValue)
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
        double tau = Math.Max(1e-12, (pillar.DayNumber - valuationDate.DayNumber) / 365d);
        return Math.Exp(-QuoteValue * tau);
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

        double tau = Math.Max(1e-12, (pillar.DayNumber - valuationDate.DayNumber) / 365d);
        return -Math.Log(Math.Max(discountFactor.Value, 1e-15)) / tau;
    }
}
