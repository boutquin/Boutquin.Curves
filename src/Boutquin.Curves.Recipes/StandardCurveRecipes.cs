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
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Interpolation;
using Boutquin.Curves.Recipes.Nodes;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.Calendars;
using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Recipes;

/// <summary>
/// Pre-built curve recipes for major interest-rate benchmarks.
/// </summary>
/// <remarks>
/// Each method returns a date-independent <see cref="CurveGroupRecipe"/> that captures the
/// full specification for fetching market data and calibrating a curve group. The recipes
/// encode the standard node layout, instrument conventions, interpolation settings, and
/// data-source identifiers for illustrative rate-curve recipes and explicit proxy inputs. Consumers resolve a recipe
/// into a <see cref="CurveCalibrationInput"/> by supplying a valuation date and fetching
/// the market data described by each node.
/// </remarks>
public static class StandardCurveRecipes
{
    // --- Interpolation presets ---

    private static readonly InterpolationSettings s_discountInterpolation =
        new(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward");

    private static readonly InterpolationSettings s_projectionInterpolation =
        new(InterpolatorKind.FlatForward, "FlatZero", "FlatForward");

    // --- USD SOFR ---

    /// <summary>
    /// USD SOFR discount curve: overnight fixing plus OIS and swap nodes out to 30 years.
    /// </summary>
    /// <returns>A single discount-curve recipe.</returns>
    public static CurveGroupRecipe UsdSofrDiscount(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.USD;
        var benchmark = new BenchmarkName("SOFR");
        var curveRef = new CurveReference(CurveRole.Discount, ccy, benchmark);
        const string oisConv = "USD-SOFR-OIS";
        const string swapConv = "USD-FIXED-6M-30-360";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "SOFR", oisConv, curveRef, calendar: calendar),
            new YieldCurveNode("1M", new Tenor("1M"), "Ois", "CME-USD-SOFR-OIS", "1M", oisConv, curveRef),
            new YieldCurveNode("3M", new Tenor("3M"), "Ois", "CME-USD-SOFR-OIS", "3M", oisConv, curveRef),
            new YieldCurveNode("6M", new Tenor("6M"), "Ois", "CME-USD-SOFR-OIS", "6M", oisConv, curveRef),
            new YieldCurveNode("1Y", new Tenor("1Y"), "Ois", "CME-USD-SOFR-OIS", "1Y", oisConv, curveRef),
            new YieldCurveNode("2Y", new Tenor("2Y"), "FixedFloatSwap", "UST-PAR", "2Y", swapConv, curveRef),
            new YieldCurveNode("3Y", new Tenor("3Y"), "FixedFloatSwap", "UST-PAR", "3Y", swapConv, curveRef),
            new YieldCurveNode("5Y", new Tenor("5Y"), "FixedFloatSwap", "UST-PAR", "5Y", swapConv, curveRef),
            new YieldCurveNode("7Y", new Tenor("7Y"), "FixedFloatSwap", "UST-PAR", "7Y", swapConv, curveRef),
            new YieldCurveNode("10Y", new Tenor("10Y"), "FixedFloatSwap", "UST-PAR", "10Y", swapConv, curveRef),
            new YieldCurveNode("30Y", new Tenor("30Y"), "FixedFloatSwap", "UST-PAR", "30Y", swapConv, curveRef),
        };

        var curve = new CurveRecipe(
            "USD-SOFR-DISC",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_discountInterpolation,
            nodes);

        return new CurveGroupRecipe("USD-SOFR", new[] { curve });
    }

    /// <summary>
    /// USD SOFR projection curve: overnight fixing and 30-year swap endpoint.
    /// </summary>
    /// <returns>A curve group recipe containing the forward projection curve specification.</returns>
    public static CurveGroupRecipe UsdSofrProjection(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.USD;
        var benchmark = new BenchmarkName("SOFR");
        var curveRef = new CurveReference(CurveRole.Forward, ccy, benchmark);
        const string swapConv = "USD-FIXED-6M-30-360";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "SOFR", "USD-SOFR-OIS", curveRef, calendar: calendar),
            new YieldCurveNode("30Y", new Tenor("30Y"), "FixedFloatSwap", "UST-PAR", "30Y", swapConv, curveRef),
        };

        var curve = new CurveRecipe(
            "USD-SOFR-FWD",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_projectionInterpolation,
            nodes);

        return new CurveGroupRecipe("USD-SOFR-FWD", new[] { curve });
    }

    /// <summary>
    /// USD SOFR multi-curve group: a SOFR projection (forward) curve plus a SOFR discount curve
    /// whose swap nodes name the projection curve as their forward source.
    /// </summary>
    /// <remarks>
    /// The two curves are ordered projection-first so the discount curve's swap nodes can resolve
    /// their <see cref="ICurveNodeSpec.ProjectionCurves"/> reference against a prior specification in
    /// the same calibration batch. Each swap's <see cref="Boutquin.Curves.Bootstrap.ResolvedNode"/>
    /// carries that projection reference so its instrument helper sources floating-leg forwards
    /// from the calibrated projection curve.
    /// </remarks>
    /// <returns>A two-curve group recipe: a SOFR forward curve followed by a SOFR discount curve.</returns>
    public static CurveGroupRecipe UsdSofrMultiCurve(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.USD;
        var benchmark = new BenchmarkName("SOFR");
        var projectionRef = new CurveReference(CurveRole.Forward, ccy, benchmark);
        var discountRef = new CurveReference(CurveRole.Discount, ccy, benchmark);
        const string oisConv = "USD-SOFR-OIS";
        const string swapConv = "USD-FIXED-6M-30-360";

        IReadOnlyList<CurveReference> projectionLink = new[] { projectionRef };

        // Projection curve: overnight fixing plus OIS deposit/OIS nodes.
        var projectionNodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "SOFR", oisConv, projectionRef, calendar: calendar),
            new YieldCurveNode("1M", new Tenor("1M"), "Ois", "CME-USD-SOFR-OIS", "1M", oisConv, projectionRef),
            new YieldCurveNode("3M", new Tenor("3M"), "Ois", "CME-USD-SOFR-OIS", "3M", oisConv, projectionRef),
            new YieldCurveNode("6M", new Tenor("6M"), "Ois", "CME-USD-SOFR-OIS", "6M", oisConv, projectionRef),
            new YieldCurveNode("1Y", new Tenor("1Y"), "Ois", "CME-USD-SOFR-OIS", "1Y", oisConv, projectionRef),
        };

        var projectionCurve = new CurveRecipe(
            "USD-SOFR-PROJ",
            projectionRef,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_projectionInterpolation,
            projectionNodes);

        // Discount curve: overnight fixing plus swap nodes that project off the forward curve.
        // Uses SOFR IRS par rates (consistent with the SOFR OIS projection curve) rather than
        // UST par rates, which would create a ~70bp+ spread between R and the SOFR forwards and
        // cause catastrophic cancellation in the multi-curve par-annuity formula.
        var discountNodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "SOFR", oisConv, discountRef, calendar: calendar),
            new YieldCurveNode("2Y", new Tenor("2Y"), "FixedFloatSwap", "CME-USD-SOFR-IRS", "2Y", swapConv, discountRef, projectionLink),
            new YieldCurveNode("3Y", new Tenor("3Y"), "FixedFloatSwap", "CME-USD-SOFR-IRS", "3Y", swapConv, discountRef, projectionLink),
            new YieldCurveNode("5Y", new Tenor("5Y"), "FixedFloatSwap", "CME-USD-SOFR-IRS", "5Y", swapConv, discountRef, projectionLink),
            new YieldCurveNode("7Y", new Tenor("7Y"), "FixedFloatSwap", "CME-USD-SOFR-IRS", "7Y", swapConv, discountRef, projectionLink),
            new YieldCurveNode("10Y", new Tenor("10Y"), "FixedFloatSwap", "CME-USD-SOFR-IRS", "10Y", swapConv, discountRef, projectionLink),
            new YieldCurveNode("30Y", new Tenor("30Y"), "FixedFloatSwap", "CME-USD-SOFR-IRS", "30Y", swapConv, discountRef, projectionLink),
        };

        var discountCurve = new CurveRecipe(
            "USD-SOFR-MC-DISC",
            discountRef,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_discountInterpolation,
            discountNodes);

        return new CurveGroupRecipe("USD-SOFR-MC", new[] { projectionCurve, discountCurve });
    }

    // --- CAD CORRA ---

    /// <summary>
    /// CAD discount proxy combining an overnight CORRA fixing with Bank of Canada government zero-coupon yields.
    /// </summary>
    /// <returns>A government-yield proxy recipe, not a market CORRA OIS calibration.</returns>
    /// <remarks>
    /// Government points use continuous zero discounting, <c>DF = exp(-z * T)</c>, on the Bank's nominal
    /// tenor grid (3M = 0.25 years). Government funding and CORRA OIS curves have different economics;
    /// replace these proxy points with actual OIS quotes before using this recipe for OIS valuation.
    /// </remarks>
    public static CurveGroupRecipe CadCorraDiscount(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.CAD;
        var benchmark = new BenchmarkName("CORRA");
        var curveRef = new CurveReference(CurveRole.Discount, ccy, benchmark);
        const string oisConv = "CAD-CORRA-OIS";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "CORRA", oisConv, curveRef, calendar: calendar),
            new YieldCurveNode("3M", new Tenor("3M"), "ContinuousZero", "BOC-ZERO", "3M", oisConv, curveRef),
            new YieldCurveNode("6M", new Tenor("6M"), "ContinuousZero", "BOC-ZERO", "6M", oisConv, curveRef),
            new YieldCurveNode("1Y", new Tenor("1Y"), "ContinuousZero", "BOC-ZERO", "1Y", oisConv, curveRef),
            new YieldCurveNode("2Y", new Tenor("2Y"), "ContinuousZero", "BOC-ZERO", "2Y", oisConv, curveRef),
            new YieldCurveNode("3Y", new Tenor("3Y"), "ContinuousZero", "BOC-ZERO", "3Y", oisConv, curveRef),
            new YieldCurveNode("5Y", new Tenor("5Y"), "ContinuousZero", "BOC-ZERO", "5Y", oisConv, curveRef),
            new YieldCurveNode("7Y", new Tenor("7Y"), "ContinuousZero", "BOC-ZERO", "7Y", oisConv, curveRef),
            new YieldCurveNode("10Y", new Tenor("10Y"), "ContinuousZero", "BOC-ZERO", "10Y", oisConv, curveRef),
            new YieldCurveNode("30Y", new Tenor("30Y"), "ContinuousZero", "BOC-ZERO", "30Y", oisConv, curveRef),
        };

        var curve = new CurveRecipe(
            "CAD-CORRA-DISC",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/365F",
            s_discountInterpolation,
            nodes);

        return new CurveGroupRecipe("CAD-CORRA", new[] { curve });
    }

    /// <summary>
    /// CAD projection proxy combining an overnight CORRA fixing with a government zero-coupon endpoint.
    /// </summary>
    /// <returns>A government-yield projection proxy, not a market CORRA forward curve.</returns>
    /// <remarks>The government endpoint uses continuous zero discounting on its nominal tenor; it is an illustrative proxy.</remarks>
    public static CurveGroupRecipe CadCorraProjection(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.CAD;
        var benchmark = new BenchmarkName("CORRA");
        var curveRef = new CurveReference(CurveRole.Forward, ccy, benchmark);
        const string oisConv = "CAD-CORRA-OIS";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "CORRA", oisConv, curveRef, calendar: calendar),
            new YieldCurveNode("30Y", new Tenor("30Y"), "ContinuousZero", "BOC-ZERO", "30Y", oisConv, curveRef),
        };

        var curve = new CurveRecipe(
            "CAD-CORRA-FWD",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/365F",
            s_projectionInterpolation,
            nodes);

        return new CurveGroupRecipe("CAD-CORRA-FWD", new[] { curve });
    }

    // --- GBP SONIA ---

    /// <summary>
    /// GBP SONIA discount curve: overnight fixing plus OIS and swap nodes out to 30 years.
    /// </summary>
    /// <returns>A curve group recipe containing the discount curve specification.</returns>
    public static CurveGroupRecipe GbpSoniaDiscount(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.GBP;
        var benchmark = new BenchmarkName("SONIA");
        var curveRef = new CurveReference(CurveRole.Discount, ccy, benchmark);
        const string oisConv = "GBP-SONIA-OIS";
        const string swapConv = "GBP-FIXED-1Y-ACT-365F";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "SONIA", oisConv, curveRef, calendar: calendar),
            new YieldCurveNode("1M", new Tenor("1M"), "Ois", "ICE-GBP-SONIA-OIS", "1M", oisConv, curveRef),
            new YieldCurveNode("3M", new Tenor("3M"), "Ois", "ICE-GBP-SONIA-OIS", "3M", oisConv, curveRef),
            new YieldCurveNode("6M", new Tenor("6M"), "Ois", "ICE-GBP-SONIA-OIS", "6M", oisConv, curveRef),
            new YieldCurveNode("1Y", new Tenor("1Y"), "Ois", "ICE-GBP-SONIA-OIS", "1Y", oisConv, curveRef),
            new YieldCurveNode("2Y", new Tenor("2Y"), "FixedFloatSwap", "ICE-GBP-SONIA-OIS", "2Y", swapConv, curveRef),
            new YieldCurveNode("3Y", new Tenor("3Y"), "FixedFloatSwap", "ICE-GBP-SONIA-OIS", "3Y", swapConv, curveRef),
            new YieldCurveNode("5Y", new Tenor("5Y"), "FixedFloatSwap", "ICE-GBP-SONIA-OIS", "5Y", swapConv, curveRef),
            new YieldCurveNode("7Y", new Tenor("7Y"), "FixedFloatSwap", "ICE-GBP-SONIA-OIS", "7Y", swapConv, curveRef),
            new YieldCurveNode("10Y", new Tenor("10Y"), "FixedFloatSwap", "ICE-GBP-SONIA-OIS", "10Y", swapConv, curveRef),
            new YieldCurveNode("30Y", new Tenor("30Y"), "FixedFloatSwap", "ICE-GBP-SONIA-OIS", "30Y", swapConv, curveRef),
        };

        var curve = new CurveRecipe(
            "GBP-SONIA-DISC",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/365F",
            s_discountInterpolation,
            nodes);

        return new CurveGroupRecipe("GBP-SONIA", new[] { curve });
    }

    /// <summary>
    /// GBP SONIA projection curve: overnight fixing and 30-year swap endpoint.
    /// </summary>
    /// <returns>A curve group recipe containing the forward projection curve specification.</returns>
    public static CurveGroupRecipe GbpSoniaProjection(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.GBP;
        var benchmark = new BenchmarkName("SONIA");
        var curveRef = new CurveReference(CurveRole.Forward, ccy, benchmark);
        const string swapConv = "GBP-FIXED-1Y-ACT-365F";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "SONIA", "GBP-SONIA-OIS", curveRef, calendar: calendar),
            new YieldCurveNode("30Y", new Tenor("30Y"), "FixedFloatSwap", "ICE-GBP-SONIA-OIS", "30Y", swapConv, curveRef),
        };

        var curve = new CurveRecipe(
            "GBP-SONIA-FWD",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/365F",
            s_projectionInterpolation,
            nodes);

        return new CurveGroupRecipe("GBP-SONIA-FWD", new[] { curve });
    }

    // --- EUR ESTR ---

    /// <summary>
    /// EUR ESTR discount curve: overnight fixing plus OIS and swap nodes out to 30 years.
    /// </summary>
    /// <returns>A curve group recipe containing the discount curve specification.</returns>
    public static CurveGroupRecipe EurEstrDiscount(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.EUR;
        var benchmark = new BenchmarkName("ESTR");
        var curveRef = new CurveReference(CurveRole.Discount, ccy, benchmark);
        const string oisConv = "EUR-ESTR-OIS";
        const string swapConv = "EUR-FIXED-1Y-ACT-360";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "ESTR", oisConv, curveRef, calendar: calendar),
            new YieldCurveNode("1M", new Tenor("1M"), "Ois", "CME-EUR-ESTR-OIS", "1M", oisConv, curveRef),
            new YieldCurveNode("3M", new Tenor("3M"), "Ois", "CME-EUR-ESTR-OIS", "3M", oisConv, curveRef),
            new YieldCurveNode("6M", new Tenor("6M"), "Ois", "CME-EUR-ESTR-OIS", "6M", oisConv, curveRef),
            new YieldCurveNode("1Y", new Tenor("1Y"), "Ois", "CME-EUR-ESTR-OIS", "1Y", oisConv, curveRef),
            new YieldCurveNode("2Y", new Tenor("2Y"), "FixedFloatSwap", "CME-EUR-ESTR-OIS", "2Y", swapConv, curveRef),
            new YieldCurveNode("3Y", new Tenor("3Y"), "FixedFloatSwap", "CME-EUR-ESTR-OIS", "3Y", swapConv, curveRef),
            new YieldCurveNode("5Y", new Tenor("5Y"), "FixedFloatSwap", "CME-EUR-ESTR-OIS", "5Y", swapConv, curveRef),
            new YieldCurveNode("7Y", new Tenor("7Y"), "FixedFloatSwap", "CME-EUR-ESTR-OIS", "7Y", swapConv, curveRef),
            new YieldCurveNode("10Y", new Tenor("10Y"), "FixedFloatSwap", "CME-EUR-ESTR-OIS", "10Y", swapConv, curveRef),
            new YieldCurveNode("30Y", new Tenor("30Y"), "FixedFloatSwap", "CME-EUR-ESTR-OIS", "30Y", swapConv, curveRef),
        };

        var curve = new CurveRecipe(
            "EUR-ESTR-DISC",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_discountInterpolation,
            nodes);

        return new CurveGroupRecipe("EUR-ESTR", new[] { curve });
    }

    /// <summary>
    /// EUR ESTR projection curve: overnight fixing and 30-year swap endpoint.
    /// </summary>
    /// <returns>A curve group recipe containing the forward projection curve specification.</returns>
    public static CurveGroupRecipe EurEstrProjection(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.EUR;
        var benchmark = new BenchmarkName("ESTR");
        var curveRef = new CurveReference(CurveRole.Forward, ccy, benchmark);
        const string swapConv = "EUR-FIXED-1Y-ACT-360";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "ESTR", "EUR-ESTR-OIS", curveRef, calendar: calendar),
            new YieldCurveNode("30Y", new Tenor("30Y"), "FixedFloatSwap", "CME-EUR-ESTR-OIS", "30Y", swapConv, curveRef),
        };

        var curve = new CurveRecipe(
            "EUR-ESTR-FWD",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_projectionInterpolation,
            nodes);

        return new CurveGroupRecipe("EUR-ESTR-FWD", new[] { curve });
    }

    // --- CHF SARON ---

    /// <summary>
    /// CHF SARON discount curve: overnight fixing plus SNB zero-curve points out to 30 years.
    /// </summary>
    /// <returns>A curve group recipe containing the discount curve specification.</returns>
    public static CurveGroupRecipe ChfSaronDiscount(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.CHF;
        var benchmark = new BenchmarkName("SARON");
        var curveRef = new CurveReference(CurveRole.Discount, ccy, benchmark);
        const string oisConv = "CHF-SARON-OIS";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "SARON", oisConv, curveRef, calendar: calendar),
            new YieldCurveNode("3M", new Tenor("3M"), "Deposit", "SIX-CHF-SARON-OIS", "3M", oisConv, curveRef),
            new YieldCurveNode("6M", new Tenor("6M"), "Deposit", "SIX-CHF-SARON-OIS", "6M", oisConv, curveRef),
            new YieldCurveNode("1Y", new Tenor("1Y"), "Deposit", "SIX-CHF-SARON-OIS", "1Y", oisConv, curveRef),
            new YieldCurveNode("2Y", new Tenor("2Y"), "Deposit", "SIX-CHF-SARON-OIS", "2Y", oisConv, curveRef),
            new YieldCurveNode("3Y", new Tenor("3Y"), "Deposit", "SIX-CHF-SARON-OIS", "3Y", oisConv, curveRef),
            new YieldCurveNode("5Y", new Tenor("5Y"), "Deposit", "SIX-CHF-SARON-OIS", "5Y", oisConv, curveRef),
            new YieldCurveNode("7Y", new Tenor("7Y"), "Deposit", "SIX-CHF-SARON-OIS", "7Y", oisConv, curveRef),
            new YieldCurveNode("10Y", new Tenor("10Y"), "Deposit", "SIX-CHF-SARON-OIS", "10Y", oisConv, curveRef),
            new YieldCurveNode("30Y", new Tenor("30Y"), "Deposit", "SIX-CHF-SARON-OIS", "30Y", oisConv, curveRef),
        };

        var curve = new CurveRecipe(
            "CHF-SARON-DISC",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_discountInterpolation,
            nodes);

        return new CurveGroupRecipe("CHF-SARON", new[] { curve });
    }

    /// <summary>
    /// CHF SARON projection curve: overnight fixing and 30-year zero-curve endpoint.
    /// </summary>
    /// <returns>A curve group recipe containing the forward projection curve specification.</returns>
    public static CurveGroupRecipe ChfSaronProjection(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.CHF;
        var benchmark = new BenchmarkName("SARON");
        var curveRef = new CurveReference(CurveRole.Forward, ccy, benchmark);
        const string oisConv = "CHF-SARON-OIS";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "SARON", oisConv, curveRef, calendar: calendar),
            new YieldCurveNode("30Y", new Tenor("30Y"), "Deposit", "SIX-CHF-SARON-OIS", "30Y", oisConv, curveRef),
        };

        var curve = new CurveRecipe(
            "CHF-SARON-FWD",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/360",
            s_projectionInterpolation,
            nodes);

        return new CurveGroupRecipe("CHF-SARON-FWD", new[] { curve });
    }

    // --- JPY TONA ---

    /// <summary>
    /// JPY TONA discount curve: overnight fixing plus BOJ zero-curve points out to 30 years.
    /// </summary>
    /// <returns>A curve group recipe containing the discount curve specification.</returns>
    public static CurveGroupRecipe JpyTonaDiscount(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.JPY;
        var benchmark = new BenchmarkName("TONA");
        var curveRef = new CurveReference(CurveRole.Discount, ccy, benchmark);
        const string oisConv = "JPY-TONA-OIS";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "TONA", oisConv, curveRef, calendar: calendar),
            new YieldCurveNode("3M", new Tenor("3M"), "Deposit", "BOJ-JPY-TONA-OIS", "3M", oisConv, curveRef),
            new YieldCurveNode("6M", new Tenor("6M"), "Deposit", "BOJ-JPY-TONA-OIS", "6M", oisConv, curveRef),
            new YieldCurveNode("1Y", new Tenor("1Y"), "Deposit", "BOJ-JPY-TONA-OIS", "1Y", oisConv, curveRef),
            new YieldCurveNode("2Y", new Tenor("2Y"), "Deposit", "BOJ-JPY-TONA-OIS", "2Y", oisConv, curveRef),
            new YieldCurveNode("3Y", new Tenor("3Y"), "Deposit", "BOJ-JPY-TONA-OIS", "3Y", oisConv, curveRef),
            new YieldCurveNode("5Y", new Tenor("5Y"), "Deposit", "BOJ-JPY-TONA-OIS", "5Y", oisConv, curveRef),
            new YieldCurveNode("7Y", new Tenor("7Y"), "Deposit", "BOJ-JPY-TONA-OIS", "7Y", oisConv, curveRef),
            new YieldCurveNode("10Y", new Tenor("10Y"), "Deposit", "BOJ-JPY-TONA-OIS", "10Y", oisConv, curveRef),
            new YieldCurveNode("30Y", new Tenor("30Y"), "Deposit", "BOJ-JPY-TONA-OIS", "30Y", oisConv, curveRef),
        };

        var curve = new CurveRecipe(
            "JPY-TONA-DISC",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/365F",
            s_discountInterpolation,
            nodes);

        return new CurveGroupRecipe("JPY-TONA", new[] { curve });
    }

    /// <summary>
    /// JPY TONA projection curve: overnight fixing and 30-year zero-curve endpoint.
    /// </summary>
    /// <returns>A curve group recipe containing the forward projection curve specification.</returns>
    public static CurveGroupRecipe JpyTonaProjection(IBusinessCalendar? calendar = null)
    {
        var ccy = Currency.JPY;
        var benchmark = new BenchmarkName("TONA");
        var curveRef = new CurveReference(CurveRole.Forward, ccy, benchmark);
        const string oisConv = "JPY-TONA-OIS";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "TONA", oisConv, curveRef, calendar: calendar),
            new YieldCurveNode("30Y", new Tenor("30Y"), "Deposit", "BOJ-JPY-TONA-OIS", "30Y", oisConv, curveRef),
        };

        var curve = new CurveRecipe(
            "JPY-TONA-FWD",
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/365F",
            s_projectionInterpolation,
            nodes);

        return new CurveGroupRecipe("JPY-TONA-FWD", new[] { curve });
    }
}
