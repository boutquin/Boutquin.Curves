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
            new("1M", 0.0480m),
            new("3M", 0.0460m),
            new("6M", 0.0440m),
            new("1Y", 0.0420m),
        ]);

    /// <summary>
    /// US Treasury par yield quotes for the long end of the USD discount curve.
    /// </summary>
    public static DataEnvelope<IReadOnlyList<YieldCurveQuote>> UsdTreasuryParQuotes() =>
        Envelope<YieldCurveQuote>(
        [
            new("2Y", 0.0347m),
            new("3Y", 0.0362m),
            new("5Y", 0.0385m),
            new("7Y", 0.0402m),
            new("10Y", 0.0419m),
            new("30Y", 0.0445m),
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
            new("3M", 0.0245m),
            new("6M", 0.0268m),
            new("1Y", 0.0292m),
            new("2Y", 0.0310m),
            new("3Y", 0.0318m),
            new("5Y", 0.0330m),
            new("7Y", 0.0338m),
            new("10Y", 0.0345m),
            new("30Y", 0.0355m),
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
            new("1M", 0.0385m),
            new("3M", 0.0370m),
            new("6M", 0.0358m),
            new("1Y", 0.0345m),
            new("2Y", 0.0330m),
            new("3Y", 0.0325m),
            new("5Y", 0.0328m),
            new("7Y", 0.0335m),
            new("10Y", 0.0340m),
            new("30Y", 0.0350m),
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
            new("1M", 0.0320m),
            new("3M", 0.0305m),
            new("6M", 0.0290m),
            new("1Y", 0.0278m),
            new("2Y", 0.0265m),
            new("3Y", 0.0260m),
            new("5Y", 0.0262m),
            new("7Y", 0.0268m),
            new("10Y", 0.0275m),
            new("30Y", 0.0285m),
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
            // CAD
            .Register("CORRA", CadCorraFixings())
            .Register("BOC-ZERO", CadBocZeroQuotes())
            // GBP
            .Register("SONIA", GbpSoniaFixings())
            .Register("ICE-GBP-SONIA-OIS", GbpSoniaOisQuotes())
            // EUR
            .Register("ESTR", EurEstrFixings())
            .Register("CME-EUR-ESTR-OIS", EurEstrOisQuotes());
    }

    private static DataEnvelope<IReadOnlyList<T>> Envelope<T>(IReadOnlyList<T> payload) =>
        new(payload, s_fullCoverage, s_noIssues, s_fixtureProvenance);
}
