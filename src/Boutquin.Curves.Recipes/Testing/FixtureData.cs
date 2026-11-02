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

using Boutquin.MarketData.Abstractions.Provenance;
using Boutquin.MarketData.Abstractions.Records;
using Boutquin.MarketData.Abstractions.Results;

namespace Boutquin.Curves.Recipes.Testing;

/// <summary>
/// Deterministic fixture data for all four standard currency curves (USD, CAD, GBP, EUR).
/// Provides pre-built <see cref="ScalarObservation"/> and <see cref="YieldCurveQuote"/>
/// records matching the <see cref="StandardCurveRecipes"/> node layout, enabling complete
/// curve calibration without network access.
/// </summary>
/// <remarks>
/// All quotes are pinned to the <see cref="FixtureDate"/> and are hand-selected to produce
/// well-behaved monotonic curves. Do not use fixture data for production calibration; the
/// rates are static and do not reflect current market conditions.
/// </remarks>
public static class FixtureData
{
    /// <summary>
    /// Pinned valuation date for all fixture observations.
    /// </summary>
    public static readonly DateOnly FixtureDate = new(2026, 4, 9);

    private static readonly DataCoverage s_fullCoverage = new(1, 1, 0, 1.0m);
    private static readonly IReadOnlyList<DataIssue> s_noIssues = [];
    private static readonly IReadOnlyList<DataProvenance> s_fixtureProvenance =
    [
        new DataProvenance(new ProviderCode("Fixture"), "FixtureData", LicenseType.Free, RetrievalMode.Fixture, FreshnessClass.Stale, DateTimeOffset.UtcNow, null),
    ];

    // -------------------------------------------------------------------------
    //  USD SOFR
    // -------------------------------------------------------------------------

    /// <summary>
    /// SOFR overnight fixing observations (lookback window around <see cref="FixtureDate"/>).
    /// </summary>
    public static DataEnvelope<IReadOnlyList<ScalarObservation>> UsdSofrFixings() =>
        Envelope<ScalarObservation>(
        [
            new(new DateOnly(2026, 4, 7), 0.0528m, "rate"),
            new(new DateOnly(2026, 4, 8), 0.0529m, "rate"),
            new(FixtureDate, 0.0529m, "rate"),
        ]);

    /// <summary>
    /// SOFR OIS par rate quotes for the short end of the USD discount curve.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<YieldCurveQuote>> UsdSofrOisQuotes() =>
        Envelope<YieldCurveQuote>(
        [
            new(FixtureDate, "1M", 0.0480m),
            new(FixtureDate, "3M", 0.0460m),
            new(FixtureDate, "6M", 0.0440m),
            new(FixtureDate, "1Y", 0.0420m),
        ]);

    /// <summary>
    /// US Treasury par yield quotes for the long end of the USD discount curve.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<YieldCurveQuote>> UsdTreasuryParQuotes() =>
        Envelope<YieldCurveQuote>(
        [
            new(FixtureDate, "2Y", 0.0347m),
            new(FixtureDate, "3Y", 0.0362m),
            new(FixtureDate, "5Y", 0.0385m),
            new(FixtureDate, "7Y", 0.0402m),
            new(FixtureDate, "10Y", 0.0419m),
            new(FixtureDate, "30Y", 0.0445m),
        ]);

    /// <summary>
    /// SOFR IRS (fixed-for-SOFR) par rate quotes for the long end of the USD SOFR multi-curve
    /// discount curve. Rates are calibrated to be consistent with <see cref="UsdSofrOisQuotes"/>
    /// (the projection curve for the multi-curve recipe) — the SOFR OIS curve declines from
    /// 5.29% overnight to 4.20% at 1Y and is flat-extrapolated thereafter, giving par IRS
    /// rates in the 4.20–4.22% range across 2Y–30Y.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<YieldCurveQuote>> UsdSofrIrsQuotes() =>
        Envelope<YieldCurveQuote>(
        [
            new(FixtureDate, "2Y", 0.0422m),
            new(FixtureDate, "3Y", 0.0421m),
            new(FixtureDate, "5Y", 0.0420m),
            new(FixtureDate, "7Y", 0.0420m),
            new(FixtureDate, "10Y", 0.0420m),
            new(FixtureDate, "30Y", 0.0420m),
        ]);

    // -------------------------------------------------------------------------
    //  CAD CORRA
    // -------------------------------------------------------------------------

    /// <summary>
    /// CORRA overnight fixing observations.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<ScalarObservation>> CadCorraFixings() =>
        Envelope<ScalarObservation>(
        [
            new(new DateOnly(2026, 4, 7), 0.0226m, "rate"),
            new(new DateOnly(2026, 4, 8), 0.0227m, "rate"),
            new(FixtureDate, 0.0227m, "rate"),
        ]);

    /// <summary>
    /// Bank of Canada zero-curve quotes for the CAD discount curve.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<YieldCurveQuote>> CadBocZeroQuotes() =>
        Envelope<YieldCurveQuote>(
        [
            new(FixtureDate, "3M", 0.0245m),
            new(FixtureDate, "6M", 0.0268m),
            new(FixtureDate, "1Y", 0.0292m),
            new(FixtureDate, "2Y", 0.0310m),
            new(FixtureDate, "3Y", 0.0318m),
            new(FixtureDate, "5Y", 0.0330m),
            new(FixtureDate, "7Y", 0.0338m),
            new(FixtureDate, "10Y", 0.0345m),
            new(FixtureDate, "30Y", 0.0355m),
        ]);

    // -------------------------------------------------------------------------
    //  GBP SONIA
    // -------------------------------------------------------------------------

    /// <summary>
    /// SONIA overnight fixing observations.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<ScalarObservation>> GbpSoniaFixings() =>
        Envelope<ScalarObservation>(
        [
            new(new DateOnly(2026, 4, 7), 0.0415m, "rate"),
            new(new DateOnly(2026, 4, 8), 0.0416m, "rate"),
            new(FixtureDate, 0.0416m, "rate"),
        ]);

    /// <summary>
    /// SONIA OIS par rate quotes spanning the full GBP discount curve.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<YieldCurveQuote>> GbpSoniaOisQuotes() =>
        Envelope<YieldCurveQuote>(
        [
            new(FixtureDate, "1M", 0.0385m),
            new(FixtureDate, "3M", 0.0370m),
            new(FixtureDate, "6M", 0.0358m),
            new(FixtureDate, "1Y", 0.0345m),
            new(FixtureDate, "2Y", 0.0330m),
            new(FixtureDate, "3Y", 0.0325m),
            new(FixtureDate, "5Y", 0.0328m),
            new(FixtureDate, "7Y", 0.0335m),
            new(FixtureDate, "10Y", 0.0340m),
            new(FixtureDate, "30Y", 0.0350m),
        ]);

    // -------------------------------------------------------------------------
    //  EUR ESTR
    // -------------------------------------------------------------------------

    /// <summary>
    /// ESTR overnight fixing observations.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<ScalarObservation>> EurEstrFixings() =>
        Envelope<ScalarObservation>(
        [
            new(new DateOnly(2026, 4, 7), 0.0348m, "rate"),
            new(new DateOnly(2026, 4, 8), 0.0349m, "rate"),
            new(FixtureDate, 0.0349m, "rate"),
        ]);

    /// <summary>
    /// ESTR OIS par rate quotes spanning the full EUR discount curve.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<YieldCurveQuote>> EurEstrOisQuotes() =>
        Envelope<YieldCurveQuote>(
        [
            new(FixtureDate, "1M", 0.0320m),
            new(FixtureDate, "3M", 0.0305m),
            new(FixtureDate, "6M", 0.0290m),
            new(FixtureDate, "1Y", 0.0278m),
            new(FixtureDate, "2Y", 0.0265m),
            new(FixtureDate, "3Y", 0.0260m),
            new(FixtureDate, "5Y", 0.0262m),
            new(FixtureDate, "7Y", 0.0268m),
            new(FixtureDate, "10Y", 0.0275m),
            new(FixtureDate, "30Y", 0.0285m),
        ]);

    // -------------------------------------------------------------------------
    //  CHF SARON
    // -------------------------------------------------------------------------

    /// <summary>
    /// SARON overnight fixing observations (lookback window around <see cref="FixtureDate"/>).
    /// </summary>
    public static DataEnvelope<IReadOnlyList<ScalarObservation>> ChfSaronFixings() =>
        Envelope<ScalarObservation>(
        [
            new(new DateOnly(2026, 4, 7), 0.0163m, "rate"),
            new(new DateOnly(2026, 4, 8), 0.0163m, "rate"),
            new(FixtureDate, 0.0163m, "rate"),
        ]);

    /// <summary>
    /// SIX CHF SARON zero-curve quotes for the CHF discount curve.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<YieldCurveQuote>> ChfSaronZeroQuotes() =>
        Envelope<YieldCurveQuote>(
        [
            new(FixtureDate, "3M", 0.0155m),
            new(FixtureDate, "6M", 0.0152m),
            new(FixtureDate, "1Y", 0.0150m),
            new(FixtureDate, "2Y", 0.0148m),
            new(FixtureDate, "3Y", 0.0145m),
            new(FixtureDate, "5Y", 0.0141m),
            new(FixtureDate, "7Y", 0.0138m),
            new(FixtureDate, "10Y", 0.0135m),
            new(FixtureDate, "30Y", 0.0130m),
        ]);

    // -------------------------------------------------------------------------
    //  JPY TONA
    // -------------------------------------------------------------------------

    /// <summary>
    /// TONA overnight fixing observations (lookback window around <see cref="FixtureDate"/>).
    /// </summary>
    public static DataEnvelope<IReadOnlyList<ScalarObservation>> JpyTonaFixings() =>
        Envelope<ScalarObservation>(
        [
            new(new DateOnly(2026, 4, 7), 0.0023m, "rate"),
            new(new DateOnly(2026, 4, 8), 0.0023m, "rate"),
            new(FixtureDate, 0.0023m, "rate"),
        ]);

    /// <summary>
    /// BOJ JPY TONA zero-curve quotes for the JPY discount curve.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<YieldCurveQuote>> JpyTonaZeroQuotes() =>
        Envelope<YieldCurveQuote>(
        [
            new(FixtureDate, "3M", 0.0022m),
            new(FixtureDate, "6M", 0.0025m),
            new(FixtureDate, "1Y", 0.0032m),
            new(FixtureDate, "2Y", 0.0045m),
            new(FixtureDate, "3Y", 0.0058m),
            new(FixtureDate, "5Y", 0.0075m),
            new(FixtureDate, "7Y", 0.0090m),
            new(FixtureDate, "10Y", 0.0105m),
            new(FixtureDate, "30Y", 0.0120m),
        ]);

    // -------------------------------------------------------------------------
    //  Pipeline factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a <see cref="FakeDataPipeline"/> pre-loaded with fixture data for all four
    /// standard currency discount curves (USD SOFR, CAD CORRA, GBP SONIA, EUR ESTR).
    /// </summary>
    /// <returns>A fully populated fake pipeline ready for <see cref="CurveBuilder"/>.</returns>
    public static FakeDataPipeline CreatePipeline()
    {
        return new FakeDataPipeline()
            // USD
            .Register("SOFR", UsdSofrFixings())
            .Register("CME-USD-SOFR-OIS", UsdSofrOisQuotes())
            .Register("UST-PAR", UsdTreasuryParQuotes())
            .Register("CME-USD-SOFR-IRS", UsdSofrIrsQuotes())
            // CAD
            .Register("CORRA", CadCorraFixings())
            .Register("BOC-ZERO", CadBocZeroQuotes())
            // GBP
            .Register("SONIA", GbpSoniaFixings())
            .Register("ICE-GBP-SONIA-OIS", GbpSoniaOisQuotes())
            // EUR
            .Register("ESTR", EurEstrFixings())
            .Register("CME-EUR-ESTR-OIS", EurEstrOisQuotes())
            // CHF
            .Register("SARON", ChfSaronFixings())
            .Register("SIX-CHF-SARON-OIS", ChfSaronZeroQuotes())
            // JPY
            .Register("TONA", JpyTonaFixings())
            .Register("BOJ-JPY-TONA-OIS", JpyTonaZeroQuotes());
    }

    private static DataEnvelope<IReadOnlyList<T>> Envelope<T>(IReadOnlyList<T> payload) =>
        new(payload, s_fullCoverage, s_noIssues, s_fixtureProvenance);
}
