# Architecture

`Boutquin.Curves` is organized as a layered market stack with explicit boundaries between contracts, curve construction, and consumer-facing integration. Market data ingestion is delegated to the external `Boutquin.MarketData` kernel and `Boutquin.MarketData.Adapter` packages.

## Layer 1: Domain Contracts and Conventions

Purpose: define stable market semantics that pricing engines can consume without taking dependencies on calibration internals.

Primary package:
- `Boutquin.Curves.Abstractions`

Supporting packages:
- `Boutquin.Curves.Conventions`
- `Boutquin.Curves.Indices`
- `Boutquin.Curves.Quotes`

What this layer guarantees:
- type-level contracts for curves, quotes, identifiers, and benchmark metadata,
- explicit convention handling instead of hidden utility defaults,
- normalized quote/value representations suitable for deterministic downstream use.

Calendar contracts (`IBusinessCalendar`, `BusinessDayAdjustment`) live in `MarketData.Abstractions.Calendars` as a single source of truth. Calendar implementations (`HolidayCalendar`, `WeekendOnlyCalendar`) come from `MarketData.Calendars`. Analytics-specific schedule generation (`BusinessScheduleGenerator`) remains in `Analytics.Conventions`.

Common mistake:
- Treating conventions as "just metadata". Day count, business-day adjustments, and settlement rules directly change cash-flow schedules and therefore repricing outcomes.

## Layer 2: Curve Construction and Analytics

Purpose: build and query calibrated curves while making numerical behavior auditable.

Packages:
- `Boutquin.Curves.Core`
- `Boutquin.Curves.Interpolation`
- `Boutquin.Curves.Bootstrap`
- `Boutquin.Curves.Risk`

What this layer guarantees:
- immutable, query-time curve objects for discount factors, zero rates, and forwards,
- explicit interpolation and extrapolation choices including monotone cubic (Fritsch-Carlson) for smooth forward rates,
- convexity adjustment abstraction (`IConvexityAdjustment`) with `ConstantConvexityAdjustment` and `HullWhiteConvexityAdjustment` implementations for correcting futures-implied rates before bootstrap,
- spreaded discount curves (`ZeroSpreadedDiscountCurve`, `MultiplicativeSpreadDiscountCurve`) for credit/liquidity overlays and scenario analysis without re-bootstrapping,
- `CurveGroupExtensions` convenience methods for resolving discount and forward curves from a group by currency and benchmark,
- calibration diagnostics (repricing, structure, numerical quality),
- pre-calibration validation of curve group definitions (duplicate references, missing quotes, unresolvable conventions),
- root solver abstraction (`IRootSolver`) with bisection, Brent, and Newton-Raphson implementations for iterative instrument solving,
- historical fixings store (`IFixingsStore`) for instruments referencing past benchmark observations,
- risk seams for scenario and sensitivity workflows.

Pipeline context:
- Layer 2 consumes normalized quotes and conventions from Layer 1,
- Layer 2 is the boundary where market data becomes calibrated curve state.

Common mistake:
- Mixing projection and discounting assumptions. Use the correct curve reference and convention set for each instrument leg.

## Layer 3: Integration, Serialization, and Delivery

Purpose: package the lower layers into consumer-ready workflows for applications, examples, and tests.

Packages:
- `Boutquin.Curves.Recipes` for pre-wired standard curve construction and MarketData pipeline integration,
- `Boutquin.Curves.Serialization` for DTO and JSON mapping of curve definitions, calibration report export (`CalibrationReportExporter`), and CSV quote ingestion (`CsvQuoteLoader`),
- `Boutquin.Curves.Examples` for executable reference workflows.

Why this layer exists:
- consumers can build standard curves in a few lines via `CurveBuilder` and `StandardCurveRecipes`,
- serialized definitions remain stable and reviewable,
- examples demonstrate correct operational wiring.

## MarketData Integration

Market data ingestion is handled by the `Boutquin.MarketData` shared kernel, which owns transport, caching, storage, normalization, provenance, and orchestration. Concrete source adapters (NY Fed, US Treasury, Bank of Canada, CME, Bank of England, ECB, and others) live in the separate `Boutquin.MarketData.Adapter` repository.

The integration surface in Analytics is:
- `Boutquin.MarketData.Abstractions` provides `IDataPipeline`, `IDataRequest`, `IClock`, and canonical records (`ScalarObservation`, `YieldCurveQuote`, `FuturesSettlement`).
- `Boutquin.MarketData.Orchestration` provides the pipeline implementation.
- `ICurveNodeSpec.CreateRequest()` produces typed `IDataRequest` objects; `IDataPipeline.ExecuteAsync()` fetches and caches the data; `ICurveNodeSpec.ExtractRate()` converts canonical records into `ResolvedNode` instances.
- `ICurveNodeSpec.ExtractActualDate()` is a default interface method that returns the actual observation date when it differs from the valuation date. `OvernightFixingNode` overrides this for date gap detection.
- `CurveBuilder` forwards adapter-level `DataIssue` entries and emits node-level `DATE_ROLLBACK` issues. The severity model distinguishes expected from unexpected gaps:
  - **Info**: expected publication lag (overnight fixings such as SOFR that publish T+1).
  - **Warning**: unexpected date gap (yield curve adapters and same-day publishers where a gap signals stale data).
- Both adapter and node-level issues are collected in `CurveSnapshot.DataIssues`.

## Dependency Direction

The intended flow is one-way:
1. Layer 1 defines contracts and conventions.
2. Layer 2 calibrates and serves curve analytics.
3. Layer 3 composes these capabilities into user-facing workflows, integrating with `Boutquin.MarketData` for data access.

For most downstream pricing systems, the minimum surface is:
- `Boutquin.Curves.Abstractions`
- `Boutquin.Curves.Core`

This direction keeps the repository extraction-ready and avoids coupling pricers to data-adapter or bootstrap implementation details.
