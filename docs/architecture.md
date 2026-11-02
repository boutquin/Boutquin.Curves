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

Calendar contracts (`IBusinessCalendar`, `BusinessDayAdjustment`) live in `MarketData.Abstractions.Calendars` as a single source of truth. Calendar implementations (`HolidayCalendar`, `WeekendOnlyCalendar`) come from `MarketData.Calendars`. Curves-specific schedule generation (`ScheduleGenerator`) remains in `Boutquin.Curves.Conventions`.

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

## Boutquin.Numerics Integration

`Boutquin.Curves.Interpolation` depends on [Boutquin.Numerics](https://github.com/boutquin/Boutquin.Numerics) for the interpolation kernel. No other package in this repository references Numerics directly.

The integration surface is the generic `IInterpolator<T>` contract and the five stateless singleton implementations:

| Numerics type | Curves wrapper | Algorithm |
|---|---|---|
| `LinearInterpolator<double>` | `LinearZeroRateInterpolator` | Piecewise-linear in zero-rate space |
| `LogLinearInterpolator<double>` | `LogLinearDiscountFactorInterpolator` | Piecewise-linear in log-DF space |
| `FlatForwardInterpolator<double>` | `FlatForwardInterpolator` | Piecewise-constant instantaneous forwards |
| `MonotoneCubicInterpolator<double>` | `MonotoneCubicInterpolator` | Fritsch-Carlson monotone cubic Hermite |
| `MonotoneConvexInterpolator<double>` | `MonotoneConvexInterpolator` | Hagan-West monotone convex (non-negative forwards) |

Each wrapper converts `DateOnly`/`IReadOnlyList<CurvePoint>` inputs to `ReadOnlySpan<double>` year-fraction arrays, delegates to `XxxInterpolator<double>.Instance.Interpolate(x, xs, ys)`, and applies any domain-level coordinate transform (e.g., log-DF, NCR) before and after the call.

**Why `<double>`:** the curve layer operates in `double` arithmetic throughout — discount factors, year fractions, and zero rates are all `double`. The Numerics `IFloatingPoint<T>` tier allows `decimal`, but the additional precision yields no benefit here and would impose a cast boundary at every interpolation call.

**Why no Numerics solvers:** the bootstrap calibration is entirely analytic. Each `AbstractInstrumentHelper.SolveNodeValue` implementation computes the pillar discount factor algebraically from the market quote — no iterative root finding is needed. If future instruments require iteration (e.g., bonds with complex cashflow structures), `BrentSolver<double>` from Numerics is the natural addition; for now it is not referenced.

**Version:** Curves currently targets `Boutquin.Numerics` 2.x, which uses the generic `IInterpolator<T>` surface. The pre-2.0 non-generic facades (`BNU0001` diagnostic) are not used.

## Dependency Direction

The intended flow is one-way:
1. Layer 1 defines contracts and conventions.
2. Layer 2 calibrates and serves curve analytics.
3. Layer 3 composes these capabilities into user-facing workflows, integrating with `Boutquin.MarketData` for data access.

For most downstream pricing systems, the minimum surface is:
- `Boutquin.Curves.Abstractions`
- `Boutquin.Curves.Core`

This direction keeps the repository extraction-ready and avoids coupling pricers to data-adapter or bootstrap implementation details.
