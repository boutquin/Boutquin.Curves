# Changelog

All notable changes to `Boutquin.Curves` are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Versions are produced by [MinVer](https://github.com/adamralph/minver) from git tags on the public release repository.

## [Unreleased]

### Added

- **`IDiscountCurve.WithValuationDate(DateOnly)` and `IForwardCurve.WithValuationDate(DateOnly)`:** New abstract members that return a curve anchored at a future valuation date under the forwards-realize assumption. Contract: for any date `t >= newValuationDate`, `rolled.DiscountFactor(t) == original.DiscountFactor(t) / original.DiscountFactor(newValuationDate)`. Load-bearing for simulators, backtests, and multi-step pricers that advance time through a market snapshot — plain `with { ValuationDate = ... }` cloning of a curve-containing record silently produces time-stretched implied rates. Rolling past the last calibrated pillar throws; rolling backwards throws. Every concrete curve type implements rolling: `FlatDiscountCurve`, `FlatForwardCurve`, `InterpolatedDiscountCurve`, `ZeroSpreadedDiscountCurve`, `MultiplicativeSpreadDiscountCurve`, `JumpAdjustedDiscountCurve`, `ForwardCurveFromDiscountCurves`, and `BucketedShiftedDiscountCurve` (inside `BucketedZeroRateShock`).
- **`WithValuationDateTests`:** Test coverage in `Boutquin.Curves.Core.Tests` pinning down the forwards-realize identity and edge cases (backwards-in-time, rolling past the last pillar, dropped past jumps).

### Changed

- **Type migration to `Boutquin.MarketData`:** Eliminated 17 duplicate type definitions by migrating shared domain types upstream. `BenchmarkName`, `CurrencyCode` (now an ISO 4217 `enum`), and `Tenor` moved from `Boutquin.Curves.Abstractions.Identifiers` to `Boutquin.MarketData.Abstractions.ReferenceData`. `BenchmarkKind` and `RateBenchmark` moved from `Boutquin.Curves.Abstractions.Benchmarks` to the same namespace. All OIS/FRA/swap/futures/settlement convention record types (`OisConvention`, `FraConvention`, `SwapLegConvention`, `FutureContractConvention`, `SettlementConvention`), compounding/payment/roll/stub convention enums, `IInstrumentConvention`, and `InstrumentConventionRegistry` moved from `Boutquin.Curves.Abstractions.Conventions` / `Boutquin.Curves.Conventions.Records` to `Boutquin.MarketData.Conventions`.
- **`CurrencyCode` breaking change:** `CurrencyCode` is now an `enum` (not a `readonly record struct`). Call sites using `new CurrencyCode("USD")` must use `CurrencyCode.USD`; variable-based construction uses `Enum.Parse<CurrencyCode>(str, ignoreCase: true)`. Serialization uses `.ToString()` instead of `.Value`.
- **`BenchmarkKind` renames:** `.Ibor` → `.InterbankOffered`, `.TermRate` → `.TermRiskFree`, `.FallbackAdjustedRate` → `.FallbackAdjusted`, `.SyntheticRate` → `.Synthetic`.
- **Package version:** `Boutquin.MarketData.*` bumped to `0.16.0-local` to carry the migrated types; `Boutquin.Curves` updated to consume `0.16.0-local`.

## [1.0.0] — 2026-04-17

First stable release. All public APIs stamped as shipped; no breaking changes without a major version bump.

### Highlights

- **Eleven-package layered architecture** across three tiers: domain contracts and conventions (Layer 1), curve construction and analytics (Layer 2), and integration/serialization/delivery (Layer 3).
- **Four-currency proof packs:** USD SOFR, CAD CORRA, GBP SONIA, and EUR €STR — complete end-to-end curve construction from free public EOD data sources via `Boutquin.MarketData.Adapter`.
- **Production-grade data layer integration:** `StandardCurveRecipes` defines node specs against `IDataPipeline`; `CurveBuilder` orchestrates data fetch, calibration, and diagnostic collection in a single `BuildAsync` call.
- **Full bootstrap pipeline:** `PiecewiseBootstrapCalibrator` with three interpolation methods (linear zero-rate, log-linear DF, monotone cubic Fritsch-Carlson), three root solvers (bisection, Brent, Newton-Raphson), convexity adjustments, spreaded curves, and calibration diagnostics.
- **Risk layer:** Scenario-based risk reports with bucketed zero-rate shocks, `CurveRiskAnalyzer`, DV01 stubs, and Jacobian generation.
- **246 tests** across 16 test assemblies — unit, integration, property-based, and golden calibration tests with documented tolerance policy.

### Added

#### Core Contracts (`Boutquin.Curves.Abstractions`)

- **Curve contracts:** `ICurve`, `IDiscountCurve`, `IForwardCurve`, `ICurveGroup`, `CurveReference` (currency + role key), `CurveRole` enum. `CurveGroupExtensions` convenience methods `GetDiscountCurve`, `TryGetDiscountCurve`, `GetForwardCurve`, and `TryGetForwardCurve`.
- **Identifiers and value objects:** `QuoteId` (null/whitespace-rejecting), `CurveGroupId`, `NodeLabel`, `CalibrationResidual`, `CalibrationDiagnostic`.
- **Diagnostics:** `CurveSnapshot` wrapping the calibrated curve group, diagnostic collection, and forwarded `DataIssue` entries from `Boutquin.MarketData`. `DATE_ROLLBACK` node-level issue emitted by `CurveBuilder` when the actual observation date precedes the valuation date; `"Info"` severity for expected publication lags (e.g., SOFR T+1), adapter-level `"Warning"` for unexpected gaps from same-day publishers.
- **`IFixingsStore` / `InMemoryFixingsStore`:** Keyed historical benchmark fixing storage by `BenchmarkName` + `DateOnly`; time-series queries and date-sliced snapshots. Foundation for valuing instruments that reference past fixings (already-accruing swaps).
- **Public API snapshots:** All 11 source packages have fully populated `PublicAPI.Shipped.txt` files enforced by `Microsoft.CodeAnalysis.PublicApiAnalyzers`. Future additions require explicit `PublicAPI.Unshipped.txt` entries before merging.
- **MinVer versioning:** Switched from manual `VersionPrefix`/`VersionSuffix` to tag-driven semantic versioning. Version is derived from the latest `v*` git tag on the public release repository.

#### Conventions and Calendars (`Boutquin.Curves.Conventions`)

- **Day-count, roll, and business-day convention resolution** by stable codes (e.g., `USD-SOFR-OIS`). `InstrumentConventionRegistry.CreateDefault()` ships OIS, FRA, futures, and swap-leg entries for USD, CAD, GBP, and EUR.
- **Calendar unification:** Calendar contracts (`IBusinessCalendar`, `BusinessDayAdjustment`) consumed from `Boutquin.MarketData.Abstractions.Calendars`; implementations (`HolidayCalendar`, `HolidayCalendarFactory`) from `Boutquin.MarketData.Calendars`. No calendar types are defined in Analytics — eliminates the pre-1.0 duplication. Full holiday datasets for USNY, GBLO, TARGET, and CATO covering 2020–2030.
- **`BusinessScheduleGenerator`:** Convention-aware accrual-period generation for OIS, FRA, and swap legs. Pillar dates account for real financial-center holidays.

#### Benchmark Indices (`Boutquin.Curves.Indices`)

- Benchmark identity and catalog surfaces: SOFR, CORRA, SONIA, and €STR as default RFR identities; LIBOR and CDOR modeled as isolated legacy seams, not as primary architecture.

#### Interpolation (`Boutquin.Curves.Interpolation`)

- **`LogLinearDiscountFactorInterpolator`:** Log-linear interpolation in discount-factor space; guaranteed non-negative instantaneous forward rates between pillars.
- **`LinearZeroRateInterpolator`:** Linear interpolation in continuously compounded zero-rate space.
- **`MonotoneCubicInterpolator`:** Fritsch-Carlson monotone-preserving cubic Hermite interpolation in log-DF space. Eliminates forward-rate jumps of log-linear without spurious oscillations of unconstrained splines. Registered in `InterpolatorFactory` for `InterpolatorKind.MonotoneCubic`.
- **`InterpolatorFactory`** + **`InterpolatorKind`** enum for runtime interpolator selection.

#### Curve Objects (`Boutquin.Curves.Core`)

- **Discount curves:** `FlatDiscountCurve`, `InterpolatedDiscountCurve`, `ZeroSpreadedDiscountCurve` (additive spread on continuously compounded zero rates), `MultiplicativeSpreadDiscountCurve` (multiplicative hazard-rate-style spread on discount factors), `JumpAdjustedDiscountCurve`.
- **Forward curves:** `FlatForwardCurve`, `ForwardCurveFromDiscountCurves`.
- All curve types implement `ICurve.ZeroRate(DateOnly)` and `IDiscountCurve.DiscountFactor(DateOnly)`; forward curves additionally implement `IForwardCurve.ForwardRate(DateOnly, DateOnly)`.

#### Bootstrap (`Boutquin.Curves.Bootstrap`)

- **`PiecewiseBootstrapCalibrator`:** Core sequential exact-repricing bootstrap engine. Resolves each pillar discount factor by iterating the root solver until the instrument's model price equals its market quote. Pre-calibration validation via `CurveGroupDefinitionValidator` checks duplicate curve references, duplicate node labels, missing quotes, unresolvable conventions, unsupported interpolators, and invalid extrapolator modes. Returns categorized `ValidationResult` with errors and warnings; invalid definitions throw `InvalidOperationException` with a clear error summary. Opt out via `CurveCalibrationRequest.SkipValidation = true`.
- **Root solvers:** `IBracketedRootSolver` with `BisectionSolver` (guaranteed convergence), `BrentSolver` (superlinear production default), and `NewtonRaphsonSolver` (quadratic with safeguarded fallback); returned `RootSolverResult` carries convergence diagnostics.
- **Instrument helpers:** `DepositInstrumentHelper`, `FraInstrumentHelper`, `OisInstrumentHelper`, `OisFutureInstrumentHelper`, `FixedFloatSwapInstrumentHelper`.
- **Convexity adjustments:** `IConvexityAdjustment` interface; `ConstantConvexityAdjustment` (flat adjustment for short-dated strips) and `HullWhiteConvexityAdjustment` (Hull-White one-factor formula with automatic Ho-Lee fallback when mean reversion → 0). Applied in `OisFutureInstrumentHelper` to reduce futures-implied rates by the model-implied convexity bias before the bootstrap node is solved.
- **`ResolvedNode`:** Node with rate attached — no `QuoteId`/`MarketQuoteSet` indirection; rates travel directly from `ICurveNodeSpec.ExtractRate()` to the calibrator.
- **`CurveCalibrationInput`:** Calibrator input with pre-resolved rates; validated before entering the bootstrap loop.

#### Risk (`Boutquin.Curves.Risk`)

- **`CurveRiskAnalyzer`:** `BuildScenarioRiskReport` for actual shocked-vs-base valuation delta reporting across maturity buckets.
- **`BucketedZeroRateShock`** / **`BucketedShockPoint`:** Maturity-dependent zero-rate shock scenarios with linear interpolation.
- **Jacobian generation:** Finite-difference bumping with deterministic row/column labeling and quality diagnostics (dimension consistency, finite-value checks, condition estimate).
- DV01 and key-rate duration stubs for downstream sensitivity attribution.

#### Recipes and MarketData Integration (`Boutquin.Curves.Recipes`)

- **`CurveBuilder`:** Single-call orchestrator. Fetches market data via `IDataPipeline`, resolves nodes via `ICurveNodeSpec.ExtractRate()`, calibrates via `PiecewiseBootstrapCalibrator`, and returns a `CurveSnapshot` containing the calibrated curve group and all diagnostic and data-issue entries.
- **`ICurveNodeSpec`:** Describes how to fetch and extract a rate for one curve node. Default method `ExtractActualDate` returns the observation date when it differs from the valuation date; `OvernightFixingNode` overrides this for date gap detection on benchmarks with known publication lags (e.g., SOFR T+1).
- **`StandardCurveRecipes`:** Eight pre-built recipes covering USD/CAD/GBP/EUR discount and projection curves. Discount recipes use 10–11 nodes (O/N + short-tenor deposits + long-tenor OIS swaps); projection recipes use 2 nodes (research-grade). See the recipe table in `README.md`.
- **`CurveGroupRecipe`:** Typed curve definition pairing a `CurveGroupId` with an ordered list of `ICurveNodeSpec` entries.
- **`FakeDataPipeline`** / **`FixtureData`** (`Boutquin.Curves.Recipes.Testing`): Deterministic market data for all four currencies. Enables full end-to-end calibration without network access — the foundation for CI golden tests and the Recipes Quick Start example.

#### Serialization (`Boutquin.Curves.Serialization`)

- **`CalibrationReportExporter`:** Static utility for exporting a `CurveCalibrationResult` to an indented JSON calibration report. The report includes `schemaVersion: "1.0"`, valuation date, curve group name, diagnostics (repricing residuals, structural observations, numerical solver metrics), and an optional Jacobian matrix (omitted when `null`).
- **`CsvQuoteLoader`:** Static utility for loading market quotes from CSV text or streams into a `MarketQuoteSet`. Required columns: `QuoteId`, `Value`, `FieldName`, `AsOfDate` (ISO 8601). Optional: `Source`, `Unit`, `Notes`. Throws `FormatException` on empty input, missing required columns, unparseable values, duplicate quote IDs, or no data rows.
- **`CurveGroupDefinitionDto`** with `SchemaVersion = "1.0"` for forward-compatible versioning of persisted curve group definitions.

#### CI / Quality

- **`pr-verify.yml`:** Build, test, coverage, and format checks on all PRs to main; three-stage doc-quality scan enforcing prohibited-phrase, low-signal-phrase, and accessor-verb policies; benchmarks smoke test (`--job dry`).
- **Documentation-quality CI gates** reject prohibited-phrase, low-signal-phrase, and accessor-verb patterns across `src/**/*.cs` before merge.

### Documentation

- [`docs/architecture.md`](docs/architecture.md) — Layer design rationale, pipeline context, and package placement guide.
- [`docs/curve-construction-guide.md`](docs/curve-construction-guide.md) — Practitioner guide: bootstrapping, multi-curve, querying, diagnostics, fixings, and OptionPricing integration.
- [`docs/repository-map.md`](docs/repository-map.md) — Per-package descriptions organized by layer with navigation guidance.
- [`docs/tolerance-policy.md`](docs/tolerance-policy.md) — Numerical tolerance standards for tests and diagnostics.
- `README.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md` — repository scaffolding.

---

_The following entries (0.x.x) document pre-release internal milestones. Package names use the pre-1.0 `Boutquin.Market.*` prefix, which was renamed to `Boutquin.Curves.*` at 1.0.0._

## [0.9.0] — Calibration Report Export and CSV Quote Ingestion

- **`CalibrationReportExporter`:** Added static utility class in `Boutquin.Market.Serialization` for exporting a `CurveCalibrationResult` to an indented JSON calibration report. The report includes a `schemaVersion` field (`"1.0"`) for forward compatibility, the valuation date, the curve group name, diagnostics (repricing residuals, structural observations, and numerical solver metrics), and an optional Jacobian matrix. The Jacobian is omitted when `null`, keeping the report compact for production monitoring workflows where only residuals matter.
- **`CsvQuoteLoader`:** Added static utility class in `Boutquin.Market.Serialization` for loading market quotes from CSV text or streams into a `MarketQuoteSet`. Required columns are `QuoteId`, `Value`, `FieldName`, and `AsOfDate` (ISO 8601 format). Optional columns `Source`, `Unit`, and `Notes` are parsed when present. Throws `FormatException` on empty input, missing required columns, unparseable values, duplicate quote IDs, or no data rows.
- **`CurveGroupDefinitionDto` schema version:** Added `SchemaVersion = "1.0"` as a default positional parameter on `CurveGroupDefinitionDto` in `Boutquin.Market.Serialization.Dto`. Enables forward-compatible versioning of persisted curve group definitions across service boundaries.
- **Serialization test coverage:** Added `Boutquin.Market.Serialization.Tests` project with `CalibrationReportExporterTests`, `CsvQuoteLoaderTests`, and `SchemaVersionTests` covering round-trip export correctness, CSV parsing edge cases, and schema version defaults.

## [0.8.0] — Convexity Adjustments, Spreaded Curves, and EUR/GBP Data Adapters

- **Convexity adjustment abstraction:** Added `IConvexityAdjustment` interface to `Boutquin.Market.Abstractions.Bootstrap`. Two implementations in `Boutquin.Market.Curves.Bootstrap.ConvexityAdjustments`: `ConstantConvexityAdjustment` (flat adjustment suitable for short-dated strips) and `HullWhiteConvexityAdjustment` (Hull-White one-factor formula with automatic Ho-Lee fallback when mean reversion approaches zero). Applied in `OisFutureInstrumentHelper` via an optional `IConvexityAdjustment` parameter — the futures-implied rate is reduced by the model-implied bias before the bootstrap node is solved.
- **Spreaded discount curves:** Added `ZeroSpreadedDiscountCurve` (additive spread on continuously compounded zero rates) and `MultiplicativeSpreadDiscountCurve` (multiplicative hazard-rate-style spread on discount factors) in `Boutquin.Market.Curves.Discounting`. Both implement `IDiscountCurve` and forward discount factor, zero rate, and instantaneous forward queries to an underlying calibrated curve. Use cases include credit/liquidity overlays, fallback spread construction, and scenario analysis.
- **`CurveGroupExtensions`:** Added convenience extension methods `GetDiscountCurve`, `TryGetDiscountCurve`, `GetForwardCurve`, and `TryGetForwardCurve` on `ICurveGroup` in `Boutquin.Market.Abstractions.Curves`. Resolves discount and forward curves by currency and benchmark without manually constructing `CurveReference` instances.
- **Bank of England SONIA adapter:** Added `BankOfEnglandSoniaDataAdapter` in `Boutquin.Market.Data.BankOfEngland`. Fetches SONIA fixings from the Bank of England Statistics API (series IUDSNKY, CSV format) as a Tier 1 official public source. Emits `USED_LAST_AVAILABLE_FIXING` warning on weekends and UK bank holidays when data for the requested date is not yet available.
- **ECB €STR adapter:** Added `EuropeanCentralBankEstrDataAdapter` in `Boutquin.Market.Data.Ecb`. Fetches €STR fixings from the ECB Data Portal SDMX-ML API (series FM.B.U2.EUR.4F.KR.DFR.LEV) as a Tier 1 official public source. Emits `USED_LAST_AVAILABLE_FIXING` warning on weekends and TARGET holidays.
- **`WithPublicAdapters()` expanded to four currencies:** `EodCurveSnapshotBuilder.WithPublicAdapters()` now includes the Bank of England SONIA and ECB €STR adapters alongside NY Fed, US Treasury, Bank of Canada, and CME. Production-grade EOD curve building is now available for USD, CAD, GBP, and EUR entirely from free public sources.
- **Tolerance policy documentation:** Added `docs/tolerance-policy.md` cataloguing domain-specific numerical tolerance thresholds for calibration repricing, discount factors, zero rates, forward rates, convexity adjustments, spreaded curves, and market data adapters — shared reference for all test projects.
- **Test expansion:** Added `ConvexityAdjustmentTests`, `SpreadedDiscountCurveTests`, `CurveGroupExtensionsTests`, `BankOfEnglandSoniaDataAdapterTests`, `EuropeanCentralBankEstrDataAdapterTests`, and `GoldenCurveTests` (end-to-end golden calibration tests across all four currencies). Expanded `DiscountCurvePropertyTests` with spreaded-curve property invariants (zero-spread identity, directional correctness, forward-rate shift).

## [0.7.0] — Multi-Currency and Interpolation

- **GBP SONIA proof pack:** Added `GBP-SONIA-OIS` convention, `GBP-FIXED-1Y-ACT-365F` swap leg convention, `GbpSoniaDiscount()` and `GbpSoniaProjection()` standard curve definitions (11 nodes: O/N + 1M–30Y), fixture data for all GBP tenors, and integration tests proving end-to-end calibration.
- **EUR €STR proof pack:** Added `EUR-ESTR-OIS` convention, `EUR-FIXED-1Y-ACT-360` swap leg convention, `EurEstrDiscount()` and `EurEstrProjection()` standard curve definitions (11 nodes: O/N + 1M–30Y), fixture data for all EUR tenors, and integration tests proving end-to-end calibration.
- **Monotone cubic interpolator:** Added `MonotoneCubicInterpolator` implementing Fritsch-Carlson monotone-preserving cubic Hermite interpolation in log-DF space. Eliminates forward-rate jumps of log-linear interpolation while avoiding spurious oscillations of unconstrained cubic splines. Registered in `InterpolatorFactory` for `InterpolatorKind.MonotoneCubic`. Comparative tests against linear-zero and log-linear on the same node set.
- **Historical fixings store:** Added `IFixingsStore` interface and `InMemoryFixingsStore` implementation for benchmark fixing storage and retrieval keyed by benchmark identifier and observation date. Supports time-series queries, date-sliced snapshots, and case-insensitive key lookup. Foundation for valuing instruments that reference past fixings (already-accruing swaps).
- **Fixture data expansion:** `FixtureMarketDataSourceAdapter` now covers 4 currencies (USD, CAD, GBP, EUR) with deterministic fixture data for all standard curve tenors.
- **Convention registry expansion:** `InstrumentConventionRegistry.CreateDefault()` now includes GBP and EUR swap leg conventions alongside existing USD and CAD entries.

## [0.6.0] — Production Foundations

- **Holiday calendars:** Added embedded holiday datasets for USNY (New York), GBLO (London), TARGET (Eurozone), and CATO (Toronto) covering 2020–2030. `HolidayCalendarFactory` produces named calendars with real holiday data; unrecognized codes fall back to weekend-only logic.
- **Holiday integration:** Updated `EodCurveSnapshotBuilder` and `FixedFloatSwapInstrumentHelper` to use holiday-aware calendars via `HolidayCalendarFactory` instead of weekend-only logic. Pillar dates now account for real financial center holidays.
- **Root solver abstraction:** Added `IRootSolver` interface with `RootSolverResult` diagnostics record. Three solver implementations: `BisectionSolver` (guaranteed convergence), `BrentSolver` (superlinear production default), `NewtonRaphsonSolver` (quadratic with safeguarded fallback). Infrastructure ready for iterative OIS/swap solving in multi-period scenarios.
- **`CurveGroupDefinition` validation:** Added `CurveGroupDefinitionValidator` with pre-calibration checks: duplicate curve references, duplicate node labels, missing quotes, unresolvable conventions, unsupported interpolators, and invalid extrapolator modes. Returns categorized `ValidationResult` with errors and warnings.
- **Calibrator validation gate:** `PiecewiseBootstrapCalibrator.Calibrate()` runs pre-flight validation by default. Invalid definitions produce `InvalidOperationException` with clear error summary. Opt out via `CurveCalibrationRequest.SkipValidation = true`.
- **`SwapLegConvention` calendar:** Added `CalendarCode` property to `SwapLegConvention` (defaults to `"WEEKEND"`) for holiday-aware swap schedule generation.

## [0.5.0] — Recipes Layer

- Added `Boutquin.Market.Recipes` package — thin orchestration layer that wraps bootstrap + data into pre-wired curve definitions and a single-call snapshot builder.
- Added `StandardCurveDefinitions` with factory methods for USD SOFR discount, CAD CORRA discount, USD SOFR projection, and CAD CORRA projection curves.
- Added `EodCurveSnapshotBuilder` with fluent API: `WithFixtureAdapters()`, `WithPublicAdapters()`, `WithAdapter()`, and `BuildAsync()` for end-to-end curve construction.
- Added `EodCurveSnapshot` record combining calibrated curve group, diagnostics, data provenance, and coverage report.
- Expanded `FixtureMarketDataSourceAdapter` to cover all standard curve tenors: 11 USD quote IDs (O/N + 1M-30Y) and 10 CAD quote IDs (O/N + 3M-30Y) with pre-keyed `MarketQuote` output.
- Added `CAD-CORRA-OIS` convention to `InstrumentConventionRegistry.CreateDefault()`.
- Treasury-proxy nodes in USD SOFR definitions produce explicit `TREASURY_PROXY` structural diagnostics.
- Simplified `Examples/Program.cs` from 50+ lines of plumbing to 3-line Recipes usage.
- Fixed missing `using` directives across all data adapter projects (pre-existing build failures when compiled individually).

## [0.4.0] — Data Pipeline

- Added `DataFrequency` enum (`EndOfDay`, `Intraday`, `Snapshot`) and `PrimaryFrequency` on all source descriptors for staleness and completeness decisions.
- Added `CmeEodSettlementDataAdapter` for SOFR futures EOD settlement prices parsed from local CSV files (CME DataMine or website download).
- Added `SnapshotStalenessPolicy` with configurable max-age threshold and staleness warnings.
- Enhanced `MarketDataAggregationService` with cache-hit dedup (skips adapter calls when fresh snapshot exists) and save-after-fetch for snapshot persistence.
- Added `Frequency` field to `MarketDataFetchResult` to tag each fetch with actual data granularity.
- Refreshed golden test datasets for all 7 live adapters (NY Fed SOFR, Treasury bills, Treasury par yields, BoC CORRA, BoC money-market, BoC bond yields, BoC zero-curve).
- Added deterministic CME SOFR settlement sample CSV for parser tests.
- All Tier 1 adapters verified live and producing expected response shapes (API spike 2026-04-10).

## [0.3.0] — Calibration and Risk

- Wired interpolation and extrapolation selection end-to-end: `CurveDefinition.Interpolation` drives runtime interpolator choice via `InterpolatorKind`, with explicit left/right extrapolation modes.
- Added jump support: curve definitions accept optional multiplicative jump points, and the bootstrap pipeline produces `JumpAdjustedDiscountCurve` wrappers.
- Added calibration Jacobian generation via finite-difference bumping, with deterministic row/column labeling and quality diagnostics (dimension consistency, finite-value checks, condition estimate).
- Added `CurveRiskAnalyzer.BuildScenarioRiskReport` for actual shocked-vs-base valuation delta reporting across maturity buckets.
- Added `BucketedZeroRateShock` and `BucketedShockPoint` for maturity-dependent zero-rate shock scenarios with linear interpolation.
- Added `RiskBucket` type for valuation bucket definitions.
- Added `ScenarioName` property to `RiskReport`.
- Added null-argument validation and defensive copies across Risk public API.

## [0.2.0] — Schedule Engine and Bootstrap Diagnostics

- Added a richer schedule engine with convention-aware period generation.
- Added explicit quote normalization rules keyed by bootstrap node type.
- Expanded exact repricing helper coverage across deposits, OIS, futures, FRAs, and swaps.
- Expanded bootstrap diagnostics with structural and numerical metadata for calibration runs.

## [0.1.0-alpha] — Initial Skeleton

- Initial repository skeleton.
- Added market abstractions, conventions, calendars, indices, quotes, curves, interpolation, bootstrap, serialization, risk, examples, tests, and benchmarks scaffolding.
