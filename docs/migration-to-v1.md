# Migration Guide: Boutquin.Curves v0.x → v1.0

## Breaking Changes

### Package restructure
- `Boutquin.Curves.Indices` → deprecated. Types moved to `Boutquin.Curves.Abstractions`.
  - `BenchmarkCatalog` → `Boutquin.Curves.Abstractions.BenchmarkCatalog`
  - `FallbackAdjustedBenchmarkFactory` → `Boutquin.Curves.Abstractions.FallbackAdjustedBenchmarkFactory`
- `Boutquin.Curves.Quotes` → deprecated. Types moved to `Boutquin.Curves.Core`.
  - `NormalizedQuote` → `Boutquin.Curves.Core.NormalizedQuote`
  - `QuoteNormalizer` → `Boutquin.Curves.Core.QuoteNormalizer`
  - `QuoteValueType` → `Boutquin.Curves.Core.QuoteValueType`
  - `MarketInstrumentType` → `Boutquin.Curves.Core.MarketInstrumentType`

Both `Boutquin.Curves.Indices` and `Boutquin.Curves.Quotes` ship in v1.0 as empty compat
metapackages that forward to their new homes (Abstractions and Core respectively), so existing
package references continue to resolve. They will be **removed in v1.1** — migrate your `using`
directives now.

### Removed types (Phase A shims no longer available)
- `Boutquin.Curves.Abstractions.IFixingsStore` → use `Boutquin.MarketData.Abstractions.IFixingsStore`
- `Boutquin.Curves.Abstractions.InMemoryFixingsStore` → use `Boutquin.MarketData.Storage.InMemoryFixingsStore`
- `Boutquin.Curves.Indices.StandardBenchmarks` → use `Boutquin.MarketData.Abstractions.ReferenceData.StandardBenchmarks`

### New capabilities
- `CurveRiskMetrics` — DV01, Duration, Convexity, Gamma extension methods
- `BondYieldConvexityAdjustment` — Hull eq 30.1 model-free convexity adjustment
- `CompoundedInArrearsRate` — Hull Ch 4 p.100 RFR compounding formula
- `PcaCurveShock` — PCA-based level/slope/curvature curve shocks

## Upgrade steps
1. Update `Boutquin.Curves.*` package references to `1.0.0`
2. Replace `using Boutquin.Curves.Indices;` with `using Boutquin.Curves.Abstractions;`
3. Replace `using Boutquin.Curves.Quotes;` with `using Boutquin.Curves.Core;`
4. Replace `Boutquin.Curves.Abstractions.IFixingsStore` with `Boutquin.MarketData.Abstractions.IFixingsStore`
5. Add `Boutquin.MarketData` references where needed for IFixingsStore/InMemoryFixingsStore
