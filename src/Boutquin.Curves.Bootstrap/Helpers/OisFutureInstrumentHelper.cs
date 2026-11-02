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
/// Uses continuous-compounding discounting: <c>df = exp(-rate * tau)</c>, where <c>rate</c> is the
/// futures rate corrected by the (optional) convexity adjustment. The quote value is expected to be a
/// decimal rate (post-normalization from the IMM 100-minus-rate price convention). The convexity
/// adjustment receives distinct T1 (time to futures expiry, the pillar) and T2 (time to the underlying
/// rate's accrual end, three months later for the STIR family) — passing T1 == T2 would degenerate the
/// Hull-White adjustment to identically zero. The year fraction is floored at <c>1e-12</c> to prevent
/// division by zero; a negative year fraction (pillar before valuation date) is rejected with
/// <see cref="ArgumentOutOfRangeException"/> rather than silently floored.
/// </remarks>
public sealed class OisFutureInstrumentHelper : AbstractInstrumentHelper
{
    /// <summary>
    /// Underlying accrual length of a standard STIR futures contract (3-month SOFR/Fed-Funds family),
    /// used to derive the convexity-adjustment accrual end T2 from the futures expiry T1.
    /// </summary>
    private const int UnderlyingAccrualMonths = 3;

    private readonly ResolvedNode _node;
    private readonly IConvexityAdjustment? _convexityAdjustment;

    /// <summary>
    /// Initializes a new instance of the <see cref="OisFutureInstrumentHelper"/> type.
    /// </summary>
    /// <param name="node">Resolved node carrying rate and metadata.</param>
    /// <param name="quoteValue">Observed market quote for the OIS-future instrument.</param>
    /// <param name="convexityAdjustment">Optional convexity adjustment applied to futures rates before calibration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    public OisFutureInstrumentHelper(ResolvedNode node, double quoteValue, IConvexityAdjustment? convexityAdjustment = null)
        : base(RequireNode(node).Label, node.TargetCurve, quoteValue, node.InstrumentType)
    {
        _node = node;
        _convexityAdjustment = convexityAdjustment;
    }

    /// <summary>Resolves the instrument pillar by applying its tenor to the valuation date.</summary>
    /// <param name="valuationDate">Date from which the node tenor is measured.</param>
    /// <param name="referenceData">Reference-data provider retained for the helper contract.</param>
    /// <returns>The unadjusted contractual pillar date.</returns>
    /// <exception cref="NotSupportedException">The node tenor format is unsupported.</exception>
    public override DateOnly PillarDate(DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        return TenorParser.AddTenor(valuationDate, _node.Tenor);
    }

    /// <summary>Estimates the terminal discount factor from this instrument's quote and available prior curve state.</summary>
    /// <param name="partialCurveGroup">Previously calibrated curves and pillars.</param>
    /// <param name="valuationDate">Calibration valuation date.</param>
    /// <param name="referenceData">Instrument conventions used to resolve accruals and schedules.</param>
    /// <returns>The discount factor implied by the adjusted rate, using the configured futures convention's day count from valuation to the pillar.</returns>
    public override double SolveNodeValue(ICurveGroup partialCurveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        DateOnly pillar = PillarDate(valuationDate, referenceData);
        double tau = ComputeYearFraction(valuationDate, pillar, referenceData);
        (double timeToExpiry, double timeToAccrualEnd) = ConvexitySpan(valuationDate, pillar, tau, referenceData);

        double adjustedRate = ApplyConvexityAdjustment(QuoteValue, timeToExpiry, timeToAccrualEnd);
        return Math.Exp(-adjustedRate * tau);
    }

    /// <summary>Reprices this instrument using the supplied curve group.</summary>
    /// <param name="curveGroup">Curves used to compute the implied quote.</param>
    /// <param name="valuationDate">Calibration valuation date.</param>
    /// <param name="referenceData">Instrument conventions used for accruals and schedules.</param>
    /// <returns>The implied quote, or the original quote when the target discount curve is unavailable.</returns>
    public override double ImpliedQuote(ICurveGroup curveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        DateOnly pillar = PillarDate(valuationDate, referenceData);
        double? discountFactor = TryGetDiscountFactor(curveGroup, pillar);
        if (!discountFactor.HasValue)
        {
            return QuoteValue;
        }

        double tau = ComputeYearFraction(valuationDate, pillar, referenceData);
        if (discountFactor.Value <= 0d)
        {
            throw new InvalidOperationException(
                $"Non-positive discount factor {discountFactor.Value:G6} at {pillar:yyyy-MM-dd}: calibration has diverged.");
        }

        // SolveNodeValue embeds adjustedRate = QuoteValue - CA into the DF, so -log(df)/tau recovers
        // the ADJUSTED rate, not the original futures quote. Re-apply the convexity adjustment with
        // the SAME T1/T2 the solve used so the solve -> reprice round-trip is exact (AbsoluteError ≈ 0)
        // when convexity is active; with no adjustment this adds 0 and reduces to -log(df)/tau.
        (double timeToExpiry, double timeToAccrualEnd) = ConvexitySpan(valuationDate, pillar, tau, referenceData);
        double impliedAdjustedRate = -Math.Log(discountFactor.Value) / tau;
        return ReapplyConvexityAdjustment(impliedAdjustedRate, timeToExpiry, timeToAccrualEnd);
    }

    /// <summary>
    /// Derives the convexity-adjustment span (T1 = time to futures expiry, T2 = time to the
    /// underlying rate's accrual end) shared by <see cref="SolveNodeValue"/> and
    /// <see cref="ImpliedQuote"/>, so the solve and the reprice use identical T1/T2 inputs.
    /// </summary>
    private (double TimeToExpiry, double TimeToAccrualEnd) ConvexitySpan(
        DateOnly valuationDate, DateOnly pillar, double tau, IReferenceDataProvider referenceData)
    {
        // The convexity adjustment requires distinct T1 (futures expiry) and T2 (accrual end);
        // passing T1 == T2 degenerates the Hull-White adjustment to identically zero (B(T1,T1) = 0).
        // T1 = time-to-expiry (the pillar). T2 = T1 plus the STIR contract's underlying accrual
        // period (three months for SOFR/Fed-Funds futures), measured under the same day-count
        // convention so the span is convention-driven rather than a hard-coded year fraction.
        DateOnly accrualEnd = pillar.AddMonths(UnderlyingAccrualMonths);
        double timeToAccrualEnd = ComputeYearFraction(valuationDate, accrualEnd, referenceData);
        return (tau, timeToAccrualEnd);
    }

    private double ReapplyConvexityAdjustment(double adjustedRate, double timeToExpiry, double timeToMaturity)
    {
        if (_convexityAdjustment is null)
        {
            return adjustedRate;
        }

        double adjustment = _convexityAdjustment.Adjustment(timeToExpiry, timeToMaturity);
        if (!double.IsFinite(adjustment))
        {
            throw new InvalidOperationException(
                $"Convexity adjustment is non-finite ({adjustment:G6}) for {Label}: calibration would produce a corrupt discount factor.");
        }

        return adjustedRate + adjustment;
    }

    private double ApplyConvexityAdjustment(double futuresRate, double timeToExpiry, double timeToMaturity)
    {
        if (_convexityAdjustment is null)
        {
            return futuresRate;
        }

        double adjustment = _convexityAdjustment.Adjustment(timeToExpiry, timeToMaturity);

        // A non-finite adjustment (NaN/±∞) would yield adjustedRate = NaN/±∞ → Math.Exp(...) = NaN
        // or 0, and a NaN discount factor escapes the downstream InterpolatedDiscountCurve
        // positivity guard (NaN <= 0d is false), silently poisoning the calibrated curve. Reject it
        // at the application seam so it fails loud regardless of which IConvexityAdjustment produced it.
        if (!double.IsFinite(adjustment))
        {
            throw new InvalidOperationException(
                $"Convexity adjustment is non-finite ({adjustment:G6}) for {Label}: calibration would produce a corrupt discount factor.");
        }

        return futuresRate - adjustment;
    }

    private double ComputeYearFraction(DateOnly valuationDate, DateOnly pillar, IReferenceDataProvider referenceData)
    {
        FutureContractConvention convention = (FutureContractConvention)referenceData.GetConvention(_node.ConventionCode);
        return FloorYearFraction(DayCountResolver.Resolve(convention.DayCountCode).YearFraction(valuationDate, pillar));
    }
}
