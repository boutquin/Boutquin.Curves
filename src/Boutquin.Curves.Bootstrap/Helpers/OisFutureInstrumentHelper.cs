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

using Boutquin.Curves.Abstractions.Bootstrap;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;

namespace Boutquin.Curves.Bootstrap.Helpers;

/// <summary>
/// Solves OIS-future bootstrap nodes and reprices them from the calibrated discount curve.
/// </summary>
/// <remarks>
/// Uses continuous-compounding discounting: <c>df = exp(-rate * tau)</c>. The quote value is
/// expected to be a decimal rate (post-normalization from the IMM 100-minus-rate price convention).
/// The year fraction is floored at <c>1e-12</c> to prevent division by zero.
/// </remarks>
public sealed class OisFutureInstrumentHelper : AbstractInstrumentHelper
{
    private readonly ResolvedNode _node;
    private readonly IConvexityAdjustment? _convexityAdjustment;

    /// <summary>
    /// Initializes a new instance of the <see cref="OisFutureInstrumentHelper"/> type.
    /// </summary>
    /// <param name="node">Resolved node carrying rate and metadata.</param>
    /// <param name="quoteValue">Observed market quote for the OIS-future instrument.</param>
    /// <param name="convexityAdjustment">Optional convexity adjustment applied to futures rates before calibration.</param>
    public OisFutureInstrumentHelper(ResolvedNode node, double quoteValue, IConvexityAdjustment? convexityAdjustment = null)
        : base(node.Label, node.TargetCurve, quoteValue, node.InstrumentType)
    {
        _node = node;
        _convexityAdjustment = convexityAdjustment;
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
        double tau = ComputeYearFraction(valuationDate, pillar, referenceData);
        double adjustedRate = ApplyConvexityAdjustment(QuoteValue, tau, tau);
        return Math.Exp(-adjustedRate * tau);
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

        double tau = ComputeYearFraction(valuationDate, pillar, referenceData);
        return -Math.Log(Math.Max(discountFactor.Value, 1e-15)) / tau;
    }

    private double ApplyConvexityAdjustment(double futuresRate, double timeToExpiry, double timeToMaturity)
    {
        if (_convexityAdjustment is null)
        {
            return futuresRate;
        }

        return futuresRate - _convexityAdjustment.Adjustment(timeToExpiry, timeToMaturity);
    }

    private double ComputeYearFraction(DateOnly valuationDate, DateOnly pillar, IReferenceDataProvider referenceData)
    {
        FutureContractConvention convention = (FutureContractConvention)referenceData.GetConvention(_node.ConventionCode);
        return Math.Max(1e-12, DayCountResolver.Resolve(convention.DayCountCode).YearFraction(valuationDate, pillar));
    }
}
