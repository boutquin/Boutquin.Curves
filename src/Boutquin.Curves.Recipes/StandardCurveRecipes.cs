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
/// data-source identifiers for production-grade rate curves. Consumers resolve a recipe
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
    /// <returns>A two-curve group recipe containing the discount curve specification.</returns>
    public static CurveGroupRecipe UsdSofrDiscount(IBusinessCalendar? calendar = null)
    {
        var ccy = CurrencyCode.USD;
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
        var ccy = CurrencyCode.USD;
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

    // --- CAD CORRA ---

    /// <summary>
    /// CAD CORRA discount curve: overnight fixing plus Bank of Canada zero-curve points out to 30 years.
    /// </summary>
    /// <returns>A curve group recipe containing the discount curve specification.</returns>
    public static CurveGroupRecipe CadCorraDiscount(IBusinessCalendar? calendar = null)
    {
        var ccy = CurrencyCode.CAD;
        var benchmark = new BenchmarkName("CORRA");
        var curveRef = new CurveReference(CurveRole.Discount, ccy, benchmark);
        const string oisConv = "CAD-CORRA-OIS";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "CORRA", oisConv, curveRef, calendar: calendar),
            new YieldCurveNode("3M", new Tenor("3M"), "Deposit", "BOC-ZERO", "3M", oisConv, curveRef),
            new YieldCurveNode("6M", new Tenor("6M"), "Deposit", "BOC-ZERO", "6M", oisConv, curveRef),
            new YieldCurveNode("1Y", new Tenor("1Y"), "Deposit", "BOC-ZERO", "1Y", oisConv, curveRef),
            new YieldCurveNode("2Y", new Tenor("2Y"), "Deposit", "BOC-ZERO", "2Y", oisConv, curveRef),
            new YieldCurveNode("3Y", new Tenor("3Y"), "Deposit", "BOC-ZERO", "3Y", oisConv, curveRef),
            new YieldCurveNode("5Y", new Tenor("5Y"), "Deposit", "BOC-ZERO", "5Y", oisConv, curveRef),
            new YieldCurveNode("7Y", new Tenor("7Y"), "Deposit", "BOC-ZERO", "7Y", oisConv, curveRef),
            new YieldCurveNode("10Y", new Tenor("10Y"), "Deposit", "BOC-ZERO", "10Y", oisConv, curveRef),
            new YieldCurveNode("30Y", new Tenor("30Y"), "Deposit", "BOC-ZERO", "30Y", oisConv, curveRef),
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
    /// CAD CORRA projection curve: overnight fixing and 30-year zero-curve endpoint.
    /// </summary>
    /// <returns>A curve group recipe containing the forward projection curve specification.</returns>
    public static CurveGroupRecipe CadCorraProjection(IBusinessCalendar? calendar = null)
    {
        var ccy = CurrencyCode.CAD;
        var benchmark = new BenchmarkName("CORRA");
        var curveRef = new CurveReference(CurveRole.Forward, ccy, benchmark);
        const string oisConv = "CAD-CORRA-OIS";

        var nodes = new ICurveNodeSpec[]
        {
            new OvernightFixingNode("O/N", "CORRA", oisConv, curveRef, calendar: calendar),
            new YieldCurveNode("30Y", new Tenor("30Y"), "Deposit", "BOC-ZERO", "30Y", oisConv, curveRef),
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
        var ccy = CurrencyCode.GBP;
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
        var ccy = CurrencyCode.GBP;
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
        var ccy = CurrencyCode.EUR;
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
        var ccy = CurrencyCode.EUR;
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
}
