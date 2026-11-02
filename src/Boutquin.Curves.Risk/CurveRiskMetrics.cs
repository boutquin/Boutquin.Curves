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
using Boutquin.MarketData.Conventions;

namespace Boutquin.Curves.Risk;

/// <summary>
/// Hull Tier-1 interest-rate risk metrics expressed as extension methods on <see cref="IDiscountCurve"/>.
/// </summary>
/// <remarks>
/// <para>
/// All metrics derive from the present-value function <c>P = Σ CF_i × DF(t_i)</c> where
/// <c>DF(t_i)</c> is the discount factor from <see cref="IDiscountCurve.DiscountFactor"/> and
/// <c>t_i</c> is the year fraction from the valuation date to each payment date
/// computed via <see cref="ICurve.DayCount"/>.
/// </para>
/// <para>
/// Duration and convexity follow Hull, <i>Options, Futures and Other Derivatives</i>, Chapter 4,
/// where Macaulay duration is the PV-weighted average maturity, modified duration converts it to a
/// yield-compounding-adjusted rate sensitivity, and convexity measures the curvature of the
/// price-yield relationship.
/// </para>
/// <para>
/// DV01 and Gamma use finite-difference shocks via <see cref="ParallelZeroRateShock"/>:
/// DV01 is the one-basis-point price change; Gamma is the centered second-order finite difference
/// (positive for instruments with positive convexity). Both remain accurate on non-flat curves
/// because the shock is applied through the curve's own discount-factor machinery.
/// </para>
/// </remarks>
public static class CurveRiskMetrics
{
    /// <summary>
    /// Computes the Macaulay duration — the PV-weighted average time to receipt of cash flows.
    /// </summary>
    /// <param name="curve">Discount curve supplying discount factors and the day-count convention.</param>
    /// <param name="cashFlows">Ordered collection of cash flows to price.</param>
    /// <param name="valuationDate">Date from which year fractions and discount factors are measured.</param>
    /// <returns>Macaulay duration in years.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curve"/> or <paramref name="cashFlows"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cashFlows"/> is empty.</exception>
    /// <remarks>
    /// D_mac = Σ(t_i × CF_i × DF(t_i)) / Σ(CF_i × DF(t_i)) — Hull eq 4.11.
    /// </remarks>
    public static double MacaulayDuration(
        this IDiscountCurve curve,
        IReadOnlyList<CashFlow> cashFlows,
        DateOnly valuationDate)
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(cashFlows);
        if (cashFlows.Count == 0)
        {
            throw new ArgumentException("Cash-flow list must contain at least one cash flow.", nameof(cashFlows));
        }

        double pv = 0d;
        double numerator = 0d;

        foreach (CashFlow cf in cashFlows)
        {
            double t = curve.DayCount.YearFraction(valuationDate, cf.PaymentDate);
            double cfPv = (double)cf.Amount * curve.DiscountFactor(cf.PaymentDate);
            pv += cfPv;
            numerator += t * cfPv;
        }

        return numerator / pv;
    }

    /// <summary>
    /// Computes the modified duration — the fractional price sensitivity per unit change in yield.
    /// </summary>
    /// <param name="curve">Discount curve supplying discount factors, zero rates, and day-count convention.</param>
    /// <param name="cashFlows">Ordered collection of cash flows to price.</param>
    /// <param name="valuationDate">Date from which year fractions and discount factors are measured.</param>
    /// <param name="compoundingFrequency">Number of compounding periods per year (e.g., 2 for semi-annual).</param>
    /// <returns>Modified duration in years.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curve"/> or <paramref name="cashFlows"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cashFlows"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="compoundingFrequency"/> is not strictly positive.</exception>
    /// <remarks>
    /// D* = D_mac / (1 + y/m) — Hull eq 4.13, where y is the continuously compounded zero rate
    /// at the longest payment date and y/m = exp(y/m) − 1 converts it to the <paramref name="compoundingFrequency"/>-period equivalent.
    /// </remarks>
    public static double ModifiedDuration(
        this IDiscountCurve curve,
        IReadOnlyList<CashFlow> cashFlows,
        DateOnly valuationDate,
        int compoundingFrequency)
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(cashFlows);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(compoundingFrequency);

        // MacaulayDuration enforces the non-empty cash-flow contract; calling it first
        // keeps the empty-list failure mode consistent across the metric family.
        double macD = MacaulayDuration(curve, cashFlows, valuationDate);

        // Use the longest payment maturity independently of input enumeration order.
        // For a non-flat curve this is a representative approximation; no bond yield is solved.
        DateOnly refDate = cashFlows.Max(static cashFlow => cashFlow.PaymentDate);
        double continuousRate = curve.ZeroRate(refDate, CompoundingConvention.Continuous);

        // Convert continuous rate to the periodic yield-per-period equivalent.
        double yOverM = Math.Exp(continuousRate / compoundingFrequency) - 1.0;

        return macD / (1.0 + yOverM);
    }

    /// <summary>
    /// Computes the dollar duration — the signed present-value change per unit parallel rise in yield.
    /// </summary>
    /// <param name="curve">Discount curve.</param>
    /// <param name="cashFlows">Ordered collection of cash flows to price.</param>
    /// <param name="valuationDate">Valuation date.</param>
    /// <param name="compoundingFrequency">Compounding frequency used for modified duration conversion.</param>
    /// <returns>
    /// Dollar duration in currency units (negative for a long fixed-rate position, because price falls when yield rises).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curve"/> or <paramref name="cashFlows"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cashFlows"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="compoundingFrequency"/> is not strictly positive.</exception>
    /// <remarks>DD = −D* × PV — Hull Chapter 4 notation.</remarks>
    public static decimal DollarDuration(
        this IDiscountCurve curve,
        IReadOnlyList<CashFlow> cashFlows,
        DateOnly valuationDate,
        int compoundingFrequency)
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(cashFlows);

        double modD = ModifiedDuration(curve, cashFlows, valuationDate, compoundingFrequency);
        decimal pv = PresentValue(curve, cashFlows);
        return -(decimal)modD * pv;
    }

    /// <summary>
    /// Computes DV01 — the present-value change for a one-basis-point parallel upward shift in zero rates.
    /// </summary>
    /// <param name="curve">Discount curve.</param>
    /// <param name="cashFlows">Ordered collection of cash flows to price.</param>
    /// <param name="valuationDate">Valuation date (not used in the finite-difference computation but accepted for API symmetry).</param>
    /// <returns>
    /// DV01 in currency units per basis point (negative for a long fixed-rate position).
    /// </returns>
    /// <remarks>
    /// DV01 = PV(+1 bp) − PV(base). The shift is applied through <see cref="ParallelZeroRateShock"/>
    /// so that the computation is curve-implementation-agnostic.
    /// </remarks>
    public static decimal Dv01(
        this IDiscountCurve curve,
        IReadOnlyList<CashFlow> cashFlows,
        DateOnly valuationDate)
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(cashFlows);

        var shockedCurve = (IDiscountCurve)new ParallelZeroRateShock("DV01+1bp", 1.0).Apply(curve);
        return PresentValue(shockedCurve, cashFlows) - PresentValue(curve, cashFlows);
    }

    /// <summary>
    /// Computes convexity — the PV-weighted average of squared maturities (continuous measure).
    /// </summary>
    /// <param name="curve">Discount curve.</param>
    /// <param name="cashFlows">Ordered collection of cash flows to price.</param>
    /// <param name="valuationDate">Date from which year fractions are measured.</param>
    /// <returns>Convexity in years squared.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curve"/> or <paramref name="cashFlows"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cashFlows"/> is empty.</exception>
    /// <remarks>
    /// C = Σ(t_i² × CF_i × DF(t_i)) / PV — Hull eq 4.14.
    /// Positive for any long fixed-rate instrument (positive convexity).
    /// </remarks>
    public static double Convexity(
        this IDiscountCurve curve,
        IReadOnlyList<CashFlow> cashFlows,
        DateOnly valuationDate)
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(cashFlows);
        if (cashFlows.Count == 0)
        {
            throw new ArgumentException("Cash-flow list must contain at least one cash flow.", nameof(cashFlows));
        }

        double pv = 0d;
        double numerator = 0d;

        foreach (CashFlow cf in cashFlows)
        {
            double t = curve.DayCount.YearFraction(valuationDate, cf.PaymentDate);
            double cfPv = (double)cf.Amount * curve.DiscountFactor(cf.PaymentDate);
            pv += cfPv;
            numerator += t * t * cfPv;
        }

        return numerator / pv;
    }

    /// <summary>
    /// Computes Gamma — the centered second-order finite-difference change in present value per basis-point move.
    /// </summary>
    /// <param name="curve">Discount curve.</param>
    /// <param name="cashFlows">Ordered collection of cash flows to price.</param>
    /// <param name="valuationDate">Valuation date (accepted for API symmetry with other metrics).</param>
    /// <returns>
    /// Gamma = PV(+1 bp) + PV(−1 bp) − 2 × PV(base), in currency units.
    /// Positive for instruments with positive convexity (all long fixed-rate positions).
    /// </returns>
    /// <remarks>
    /// Gamma captures the second-order price sensitivity that DV01 misses. For a bond,
    /// Gamma is positive because price gains from a rate decline exceed price losses from an
    /// equivalent rise — the definition of positive convexity.
    /// </remarks>
    public static decimal Gamma(
        this IDiscountCurve curve,
        IReadOnlyList<CashFlow> cashFlows,
        DateOnly valuationDate)
    {
        ArgumentNullException.ThrowIfNull(curve);
        ArgumentNullException.ThrowIfNull(cashFlows);

        var upCurve = (IDiscountCurve)new ParallelZeroRateShock("Gamma+1bp", 1.0).Apply(curve);
        var downCurve = (IDiscountCurve)new ParallelZeroRateShock("Gamma-1bp", -1.0).Apply(curve);

        decimal pvUp = PresentValue(upCurve, cashFlows);
        decimal pvBase = PresentValue(curve, cashFlows);
        decimal pvDown = PresentValue(downCurve, cashFlows);

        return pvUp + pvDown - 2m * pvBase;
    }

    // ─── Private helpers ────────────────────────────────────────────────────────

    private static decimal PresentValue(IDiscountCurve curve, IReadOnlyList<CashFlow> cashFlows)
    {
        decimal pv = 0m;
        foreach (CashFlow cf in cashFlows)
        {
            pv += cf.Amount * (decimal)curve.DiscountFactor(cf.PaymentDate);
        }
        return pv;
    }
}
