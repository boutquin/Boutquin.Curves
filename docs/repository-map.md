# Repository Map

This map is organized by architecture layer so a new contributor can locate where a change belongs before touching code.

## Layer 1 Packages: Domain Contracts and Conventions

- `Boutquin.Curves.Abstractions`
	- Stable contracts, identifiers, value objects, and core infrastructure interfaces used by downstream packages. Includes `IFixingsStore`/`InMemoryFixingsStore` for historical benchmark fixing storage.
	- Start here when defining or extending market-facing interfaces.
- `Boutquin.Curves.Conventions`
	- Day count, schedule roll, business-day adjustment, and instrument-convention semantics. Contains `BusinessScheduleGenerator` for Analytics-specific schedule generation (calendar contracts themselves live in `MarketData.Abstractions.Calendars`).
	- Update here when a product rule changes.
- `Boutquin.Curves.Indices`
	- Benchmark identity and metadata catalog surfaces.
- `Boutquin.Curves.Quotes`
	- Quote identity and normalization primitives used across adapters and bootstrap logic.

## Layer 2 Packages: Curve Construction and Analytics

- `Boutquin.Curves.Core`
	- Query-time curve objects and curve-group composition. Includes spreaded discount curves (`ZeroSpreadedDiscountCurve`, `MultiplicativeSpreadDiscountCurve`) for credit/liquidity overlays.
- `Boutquin.Curves.Interpolation`
	- Interpolation/extrapolation strategy implementations: log-linear discount factor, linear zero rate, flat-forward (piecewise-constant instantaneous forwards), monotone convex (Hagan-West 2006), and monotone cubic (Fritsch-Carlson).
- `Boutquin.Curves.Bootstrap`
	- Calibration definitions, helper seams, solve orchestration, diagnostics, pre-calibration validation (`CurveCalibrationInputValidator`), and root solver infrastructure (`IRootSolver` with bisection, Brent, and Newton-Raphson implementations). Convexity adjustment abstraction (`IConvexityAdjustment`) for correcting futures-implied rates.
- `Boutquin.Curves.Risk`
	- Scenario and sensitivity extension seams aligned with curve abstractions.

When to work in Layer 2:
- adding a new node/helper type,
- changing interpolation or extrapolation behavior,
- extending calibration diagnostics.

## Layer 3 Packages: Integration, Serialization, and Delivery

- `Boutquin.Curves.Recipes`
	- Standard curve recipes and single-call `CurveBuilder` for USD SOFR, CAD CORRA, GBP SONIA, and EUR EUR-STR workflows. Integrates with `Boutquin.MarketData` via `IDataPipeline` for data fetch; `ICurveNodeSpec` creates typed `IDataRequest` objects and extracts rates from canonical records.
- `Boutquin.Curves.Serialization`
	- Schema-oriented JSON mapping for curve group definitions; calibration report export to indented JSON via `CalibrationReportExporter`; and CSV quote ingestion via `CsvQuoteLoader`.
- `Boutquin.Curves.Examples`
	- Executable examples for core construction and usage patterns.

## MarketData Dependencies

Market data ingestion is delegated to external packages:

- **Boutquin.MarketData** -- shared data kernel providing `IDataPipeline`, `IDataRequest`, `IClock`, and canonical record types (`ScalarObservation`, `YieldCurveQuote`, `FuturesSettlement`). Transport, caching, storage, normalization, and orchestration live here.
- **Boutquin.MarketData.Adapter** -- concrete source adapters (NY Fed, US Treasury, Bank of Canada, CME, Bank of England, ECB, and others) live in a separate repository.

## Test Coverage Map

- `tests/*`
	- Unit and integration tests for contracts, conventions, curves, bootstrap behavior, interpolation, diagnostics, risk, serialization, recipes, and property-based tests.

## Practical Navigation Guide

- "I need a new benchmark, convention, or identifier": start in Layer 1.
- "I need a new curve node or calibration behavior": start in Layer 2.
- "I need a new market-data source or adapter": that belongs in `Boutquin.MarketData` or `Boutquin.MarketData.Adapter`, not here.
- "I need a simpler user entry point or JSON format update": start in Layer 3 (Recipes/Serialization).
