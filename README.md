# Boutquin.Curves

[![NuGet](https://img.shields.io/nuget/v/Boutquin.Curves.Abstractions.svg)](https://www.nuget.org/packages/Boutquin.Curves.Abstractions)
[![License](https://img.shields.io/github/license/boutquin/Boutquin.Curves)](https://github.com/boutquin/Boutquin.Curves/blob/main/LICENSE.txt)
[![Build](https://github.com/boutquin/Boutquin.Curves/actions/workflows/pr-verify.yml/badge.svg)](https://github.com/boutquin/Boutquin.Curves/actions/workflows/pr-verify.yml)

A curve construction library for .NET 10. Post-LIBOR, RFR-first, multi-curve architecture.

SOFR, CORRA, SONIA, and EUR-STR are the default benchmark identities, convention keys, and data source targets. Legacy rates (LIBOR, CDOR) are modeled as isolated seams rather than as the primary architecture.

## Data Access

`Boutquin.Curves` consumes [Boutquin.MarketData](https://github.com/boutquin/Boutquin.MarketData) as its shared data kernel for transport, caching, and canonical record types. Concrete source adapters (NY Fed, US Treasury, Bank of Canada, CME, Bank of England, ECB, and others) live in the separate [Boutquin.MarketData.Adapter](https://github.com/boutquin/Boutquin.MarketData.Adapter) repository.

The data flow through Analytics is: `StandardCurveRecipes` defines node specs, each `ICurveNodeSpec` creates a typed `IDataRequest`, `IDataPipeline` executes the request and returns canonical records, and `ICurveNodeSpec.ExtractRate()` produces a `ResolvedNode` that feeds the bootstrap calibrator. `ICurveNodeSpec` also exposes a default method `ExtractActualDate` that returns the actual observation date when it differs from the valuation date; `OvernightFixingNode` overrides this for date gap detection on benchmarks with known publication lags.

`CurveBuilder` forwards adapter-level `DataIssue` entries (e.g., stale-data warnings from the transport layer) and emits node-level `DATE_ROLLBACK` issues when the actual observation date precedes the valuation date. Expected publication lags (e.g., SOFR T+1) produce `DATE_ROLLBACK` with severity `"Info"`; unexpected date gaps from same-day publishers produce adapter-level `"Warning"`. Both adapter and node-level issues are collected in the `CurveSnapshot.DataIssues` list.

## Package Layering

```
Boutquin.Curves.Abstractions          (core contracts, identifiers, diagnostics)
  ^
Conventions / Curves.Interpolation       (instrument conventions, calendars, interpolation)
  ^
Indices / Quotes / Curves                (curve implementations, benchmarks, quotes)
  ^
Curves.Bootstrap                         (piecewise calibrator, instrument helpers)
  ^
Recipes                                  (CurveBuilder, StandardCurveRecipes, MarketData integration)
  ^
Risk / Serialization / Examples          (risk analysis, JSON export, usage examples)
```

**Layer 1 -- Domain Contracts and Conventions**

| Package | Description |
|---------|-------------|
| `Boutquin.Curves.Abstractions` | Contracts, value objects, identifiers, and `IFixingsStore`/`InMemoryFixingsStore` for historical benchmark fixing storage |
| `Boutquin.Curves.Conventions` | Day-count, roll, and business-day convention resolution by stable codes (e.g. `USD-SOFR-OIS`) |
| `Boutquin.Curves.Indices` | Benchmark identity and catalog surfaces (SOFR, CORRA, and legacy rates) |
| `Boutquin.Curves.Quotes` | Normalized quote primitives used across adapters and bootstrap logic |

**Layer 2 -- Curve Construction and Analytics**

| Package | Description |
|---------|-------------|
| `Boutquin.Curves.Core` | Query-time discount and projection curve objects and curve-group composition |
| `Boutquin.Curves.Interpolation` | Interpolation/extrapolation strategies: log-linear DF, linear zero-rate, flat-forward, monotone convex (Hagan-West), monotone cubic (Fritsch-Carlson) |
| `Boutquin.Curves.Bootstrap` | Piecewise exact-repricing bootstrap engine with diagnostics, pre-calibration validation, and root solver abstraction (bisection, Brent, Newton-Raphson) |
| `Boutquin.Curves.Risk` | Scenario and sensitivity seams: bucketed zero-rate shocks, `CurveRiskAnalyzer`, DV01 and key-rate duration stubs |

**Layer 3 -- Integration, Serialization, and Delivery**

| Package | Description |
|---------|-------------|
| `Boutquin.Curves.Recipes` | Standard curve recipes, single-call `CurveBuilder`, and MarketData pipeline integration |
| `Boutquin.Curves.Serialization` | JSON calibration report export (`CalibrationReportExporter`), CSV quote ingestion (`CsvQuoteLoader`), and DTO mapping |
| `Boutquin.Curves.Examples` | Executable examples for core construction workflows |

## Quick Start

### Installation

```sh
dotnet add package Boutquin.Curves.Abstractions
dotnet add package Boutquin.Curves.Core
dotnet add package Boutquin.Curves.Bootstrap
dotnet add package Boutquin.Curves.Recipes
```

### Build a Curve with Fixture Data (No Network)

```csharp
// Using fixture data (no network calls)
using Boutquin.Curves.Recipes;
using Boutquin.Curves.Recipes.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddAnalytics();
services.AddSingleton<IDataPipeline>(FixtureData.CreatePipeline());
services.AddSingleton<IClock>(new FixedClock(DateTimeOffset.UtcNow));
services.AddLogging();
var sp = services.BuildServiceProvider();

var builder = sp.GetRequiredService<CurveBuilder>();
var recipe = StandardCurveRecipes.UsdSofrDiscount();
var snapshot = await builder.BuildAsync(recipe, new DateOnly(2026, 4, 9));

var discountRef = new CurveReference(CurveRole.Discount, new CurrencyCode("USD"));
var curve = (IDiscountCurve)snapshot.CurveGroup.GetCurve(discountRef);
Console.WriteLine($"2Y DF: {curve.DiscountFactor(new DateOnly(2028, 4, 9)):F8}");
```

### Build a Curve with Real Market Data (Network Required)

```csharp
// Using real market data from public sources (requires network)
// Adapters from Boutquin.MarketData.Adapter provide live data:
// - NY Fed SOFR fixings
// - US Treasury par yields
// - Bank of Canada zero curves + CORRA
// - Bank of England SONIA
// - ECB EUR-STR
// - CME SOFR futures settlements
using Boutquin.Curves.Recipes;
using Boutquin.MarketData.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddAnalytics();
services.AddMarketDataKernel();           // registers IDataPipeline, cache, etc.
// Register source adapters from Boutquin.MarketData.Adapter.*
// services.AddNewYorkFedAdapter();
// services.AddUsTreasuryAdapter();
// etc.
services.AddLogging();
var sp = services.BuildServiceProvider();

var builder = sp.GetRequiredService<CurveBuilder>();
var snapshot = await builder.BuildAsync(
    StandardCurveRecipes.UsdSofrDiscount(),
    DateOnly.FromDateTime(DateTime.Today));
```

### Available Standard Recipes

| Method | Currency | Nodes | Day Count | Use Case |
|--------|----------|-------|-----------|----------|
| `UsdSofrDiscount` | USD | 11 (O/N + 1M-30Y) | ACT/360 | Collateralized cash-flow discounting |
| `CadCorraDiscount` | CAD | 10 (O/N + 3M-30Y) | ACT/365F | CAD discounting |
| `GbpSoniaDiscount` | GBP | 11 (O/N + 1M-30Y) | ACT/365F | GBP discounting (T+0 settlement) |
| `EurEstrDiscount` | EUR | 11 (O/N + 1M-30Y) | ACT/360 | EUR discounting |
| `UsdSofrProjection` | USD | 2 (O/N + 30Y) | ACT/360 | Research-grade forward projection |
| `CadCorraProjection` | CAD | 2 (O/N + 30Y) | ACT/365F | Research-grade forward projection |
| `GbpSoniaProjection` | GBP | 2 (O/N + 30Y) | ACT/365F | Research-grade forward projection |
| `EurEstrProjection` | EUR | 2 (O/N + 30Y) | ACT/360 | Research-grade forward projection |

### Build a Flat Curve Directly

For exploring the lower-level API without the data layer:

```csharp
var flatCurve = new FlatDiscountCurve(
    valuationDate: DateOnly.FromDateTime(DateTime.Today),
    rate: 0.05m);

var df = flatCurve.DiscountFactor(
    DateOnly.FromDateTime(DateTime.Today.AddYears(1)));
```

## Key Types

| Type | Package | Purpose |
|------|---------|---------|
| `CurveBuilder` | Recipes | Orchestrates data fetch + calibration |
| `StandardCurveRecipes` | Recipes | Pre-built recipes for USD/CAD/GBP/EUR |
| `ICurveNodeSpec` | Recipes | Describes how to fetch and extract a rate for one curve node; `ExtractActualDate` default method returns the observation date when it differs from the valuation date |
| `CurveGroupRecipe` | Recipes | Typed curve definition with node specs |
| `ResolvedNode` | Curves.Bootstrap | Node with rate attached (no QuoteId lookup) |
| `CurveCalibrationInput` | Curves.Bootstrap | Calibrator input with pre-resolved rates |
| `PiecewiseBootstrapCalibrator` | Curves.Bootstrap | Core sequential bootstrap solver |
| `FakeDataPipeline` | Recipes.Testing | Test double for `IDataPipeline` |
| `FixtureData` | Recipes.Testing | Deterministic market data for all 4 currencies |

## Architecture

Dependency flows in one direction: Recipes --> Curves.Bootstrap --> Curves/Indices/Quotes --> Conventions/Interpolation --> Abstractions.

- **Layer 1** defines stable market semantics: type-level contracts for curves, quotes, identifiers, benchmark metadata, day-count conventions, and calendars.
- **Layer 2** calibrates curves and serves analytics: piecewise bootstrap, query-time curve objects, interpolation/extrapolation, diagnostics, and risk seams.
- **Layer 3** composes the lower layers into consumer-ready workflows with pre-wired recipes, serialization, and examples.

Market data ingestion is delegated to `Boutquin.MarketData` (shared kernel) and `Boutquin.MarketData.Adapter` (concrete source adapters). Calendar contracts (`IBusinessCalendar`, `BusinessDayAdjustment`) live in `MarketData.Abstractions.Calendars` as a single source of truth; Analytics-specific schedule generation (`BusinessScheduleGenerator`) remains in `Analytics.Conventions`.

For downstream pricing systems, the minimum dependency surface is `Boutquin.Curves.Abstractions` + `Boutquin.Curves.Core`.

See [docs/architecture.md](docs/architecture.md) for design rationale and package placement guidance.

## Design Principles

- `Boutquin.OptionPricing` depends on `Boutquin.Curves`, not the reverse
- `Boutquin.Curves` depends on `Boutquin.MarketData` for data access; the dependency never flows in reverse
- Bootstrapping layer is separate from curve query objects
- Diagnostics are first-class output
- Risk layer uses actual shocked-vs-base valuation deltas
- Conventions, calendars, and benchmarks have explicit seams
- Rates travel directly on `ResolvedNode`: no QuoteId/MarketQuoteSet indirection
- EOD-first data strategy: EOD settlement prices and daily benchmark fixings are free and production-grade

## Directory Structure

```
Boutquin.Curves/
+-- src/                        # Source projects (11)
|   +-- Abstractions/           # Core interfaces and value objects
|   +-- Conventions/            # Day-count, business-day, roll conventions
|   +-- Indices/                # Benchmark index definitions
|   +-- Quotes/                 # Normalized quote records
|   +-- Curves/                 # Discount curves and curve groups
|   +-- Curves.Interpolation/   # Interpolation methods
|   +-- Curves.Bootstrap/       # Piecewise bootstrap engine
|   +-- Risk/                   # Scenario and sensitivity
|   +-- Recipes/                # Standard curve recipes + MarketData integration
|   +-- Serialization/          # JSON serialization and CSV ingestion
|   +-- Examples/               # Executable examples
+-- tests/                      # Test projects (12)
+-- benchmarks/
|   +-- Benchmarks/             # BenchmarkDotNet suite
+-- docs/                       # Documentation
+-- Resources/                  # Shared assets (icon)
+-- scripts/                    # Utility scripts
+-- .github/                    # CI/CD workflows
```

## Documentation

| Document | Description |
|----------|-------------|
| [docs/architecture.md](docs/architecture.md) | Layer design rationale, pipeline context, and package placement guide |
| [docs/curve-construction-guide.md](docs/curve-construction-guide.md) | Practitioner guide: bootstrapping, multi-curve, querying, diagnostics, fixings, and OptionPricing integration |
| [docs/repository-map.md](docs/repository-map.md) | Per-package descriptions organized by layer with navigation guidance |
| [docs/tolerance-policy.md](docs/tolerance-policy.md) | Numerical tolerance standards for tests and diagnostics |

## Contributing

Contributions are welcome! Please read the [contributing guidelines](CONTRIBUTING.md) and [code of conduct](CODE_OF_CONDUCT.md) first.

### Reporting Bugs

If you find a bug, please report it by opening an issue on the [Issues](https://github.com/boutquin/Boutquin.Curves/issues) page with:

- A clear and descriptive title
- Steps to reproduce the issue
- Expected and actual behavior
- Screenshots or code snippets, if applicable

### Contributing Code

1. Fork the repository and clone locally
2. Create a feature branch: `git checkout -b feature-name`
3. Make your changes following the [style guides](CONTRIBUTING.md)
4. Commit with clear messages: `git commit -m "Add feature X"`
5. Push and open a pull request

## Disclaimer

Boutquin.Curves is open-source software provided under the Apache 2.0 License. It is a general-purpose library intended for educational and research purposes.

**This software does not constitute financial advice.** The curve construction, market data, and risk analysis tools are provided as-is for research and development. Before using any financial calculations in production, consult with qualified professionals who understand your specific requirements and regulatory obligations.

## License

Licensed under the [Apache License, Version 2.0](LICENSE.txt).

Copyright (c) 2026 Pierre G. Boutquin. All rights reserved.

## Contact

For inquiries, please open an issue or reach out via [GitHub Discussions](https://github.com/boutquin/Boutquin.Curves/discussions).
