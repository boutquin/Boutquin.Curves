# Curve Construction Guide

A practitioner-oriented walkthrough of modern yield-curve construction using `Boutquin.Curves`. This guide explains the concepts behind the code — what the library does, why each step matters, and how to interpret results. It assumes a strong engineering background but limited experience with fixed-income markets.

If you are looking for API reference, see the in-code XML documentation. If you are looking for package descriptions and quick-start snippets, see the [README](../README.md).

## Contents

1. [How Bootstrapping Works](#1-how-bootstrapping-works)
2. [Multi-Curve: Discount vs Projection](#2-multi-curve-discount-vs-projection)
3. [Querying Curves: Discount Factors, Zero Rates, Forwards](#3-querying-curves-discount-factors-zero-rates-forwards)
4. [Reading Diagnostics](#4-reading-diagnostics)
5. [Historical Fixings and Forward Projections](#5-historical-fixings-and-forward-projections)
6. [Handing the Market to OptionPricing](#6-handing-the-market-to-optionpricing)

---

## 1. How Bootstrapping Works

### The problem

A yield curve is a function that tells you the time value of money: given a future date, what is a dollar promised on that date worth today? Markets do not publish this function directly. Instead, they publish prices of traded instruments — overnight fixings, deposit rates, futures settlements, and swap par rates. Bootstrapping is the process of extracting a discount curve from those observable prices.

### What the bootstrap engine solves for

Each instrument in the curve definition contributes one observable market quote and introduces one unknown: the discount factor at that instrument's maturity (called a **pillar date** or **node**). The bootstrap engine solves for each discount factor such that the instrument exactly reprices to its observed market quote.

For a deposit maturing at time $T$ with rate $r$ and year fraction $\tau$:

$$P(T) = \frac{1}{1 + r \cdot \tau}$$

For an OIS swap with par rate $c$ and payment dates $T_1, \ldots, T_n$, the unknown discount factor $P(T_n)$ is extracted from the known earlier discount factors:

$$P(T_n) = \frac{1 - c \sum_{i=1}^{n-1} \tau_i \, P(T_i)}{1 + c \cdot \tau_n}$$

This is why the engine works **sequentially from short to long maturities** — each new node depends on the discount factors already solved at shorter tenors.

### Why pillar ordering matters

If you solve a 10-year swap before the 5-year swap, the engine has no value for $P(5Y)$ and cannot compute the fixed-leg present value. The `ICurveNodeSpec` list must be ordered by ascending pillar date. The library validates this before calibration and rejects unordered definitions.

### The overnight anchor

Every OIS discount curve starts with the overnight rate. For a USD SOFR curve, the first node is today's published SOFR fixing, which gives the discount factor for the next business day:

$$P(T_{ON}) = \frac{1}{1 + r_{SOFR} \cdot \tau_{ON}}$$

This single-day discount factor anchors the entire curve. All subsequent nodes build on it.

### How the calibration pipeline flows

```
Market quotes (adapter output)
    → ICurveNodeSpec list (ordered by pillar date)
        → PiecewiseBootstrapCalibrator (solves each node sequentially)
            → InterpolatedDiscountCurve (query-ready curve object)
                + BootstrapDiagnostics (repricing errors, warnings)
```

In code, `CurveBuilder` orchestrates this entire pipeline:

```csharp
var definition = StandardCurveRecipes.UsdSofrDiscount(valuationDate);
var snapshot = await new CurveBuilder()
    .WithFixtureAdapters()
    .BuildAsync(definition, valuationDate);
```

### What "exact repricing" means

After calibration, the engine feeds each solved discount factor back through the instrument's pricing formula. The difference between the instrument's market quote and its model-implied quote is the **repricing error**. A well-calibrated curve has repricing errors below $10^{-8}$ — effectively machine precision. If any node has a larger error, something is wrong: a convention mismatch, a missing fixing, or a solver convergence failure.

### Key types

| Type | Role |
|------|------|
| `ICurveNodeSpec` | Binds one market quote to one pillar date and one instrument type |
| `PiecewiseBootstrapCalibrator` | Solves nodes sequentially, produces calibrated curve + diagnostics |
| `InterpolatedDiscountCurve` | The resulting curve object, queryable for DFs and rates |
| `DepositInstrumentHelper` | Solves the deposit formula for the overnight and short-end nodes |
| `OisInstrumentHelper` | Solves the OIS swap formula for medium- and long-term nodes |
| `CurveBuilder` | Top-level orchestrator: adapters → quotes → bootstrap → snapshot |

---

## 2. Multi-Curve: Discount vs Projection

### Why two curves?

Before the 2008 financial crisis, a single LIBOR curve served as both the discount curve and the projection curve. The crisis revealed that LIBOR embedded significant credit risk, making it unsuitable for discounting collateralized derivatives. The industry moved to **OIS discounting** (using overnight rates like SOFR) for present-value calculations while keeping a separate curve for projecting future floating-rate payments.

This is the **multi-curve framework**: one curve for discounting, another for projection. They share the same valuation date and often the same benchmark identity, but serve different purposes.

### Discount curve

The discount curve answers: "What is a future cash flow worth today?" It provides discount factors used to present-value any stream of payments. In a collateralized trading environment, this curve is built from overnight index swap (OIS) rates because the collateral earns the overnight rate.

`StandardCurveRecipes.UsdSofrDiscount` builds an 11-node curve (O/N + 1M–30Y) suitable for production discounting.

### Projection curve

The projection curve answers: "What floating rate will reset on a future date?" It provides forward rates used to estimate future coupon payments on floating-rate instruments. For SOFR-based instruments, the forward SOFR rate between two dates $t_1$ and $t_2$ is:

$$F(t_1, t_2) = \frac{1}{\tau} \left( \frac{P_{proj}(t_1)}{P_{proj}(t_2)} - 1 \right)$$

`StandardCurveRecipes.UsdSofrProjection` builds a 2-node curve (O/N + 30Y) — sufficient for research-grade forward projections but not for full-tenor trading accuracy.

### When is a 2-node projection curve enough?

For overnight-rate instruments where SOFR is both the discount rate and the projection rate, a simple projection curve works. The forward rate is implied directly from the discount curve's term structure. The 2-node research-grade curve serves this purpose.

For instruments that reference a different tenor (e.g., 3-month Term SOFR), you would need a dedicated projection curve with enough nodes to capture tenor-basis risk. This requires commercial OIS/swap quotes — the library models this gap as an explicit seam.

### Curve groups

A `CurveGroup` holds multiple curves and resolves them by `CurveReference`:

```csharp
// Resolve the discount curve for USD
var discountRef = new CurveReference(CurveRole.Discount, new CurrencyCode("USD"));
var discountCurve = (IDiscountCurve)curveGroup.GetCurve(discountRef);

// Resolve the forward/projection curve for USD SOFR
var forwardRef = new CurveReference(CurveRole.Forward, new CurrencyCode("USD"),
    new BenchmarkName("SOFR"));
var forwardCurve = (IForwardCurve)curveGroup.GetCurve(forwardRef);
```

`CurveRole` distinguishes the purpose: `Discount`, `Forward`, `Basis`, `Collateral`, `Borrow`, `Dividend`, `Inflation`, or `Custom`. The combination of role + currency + optional benchmark name uniquely identifies a curve within a group.

---

## 3. Querying Curves: Discount Factors, Zero Rates, Forwards

Once you have a calibrated curve, three related quantities are available. They are mathematically equivalent — you can derive any one from the others — but each is natural for different tasks.

### Discount factor

The discount factor $P(T)$ gives the present value of one unit of currency received at time $T$. It is always between 0 and 1 in a positive-rate environment, and exactly 1.0 at the valuation date.

**When to use:** Discounting cash flows. Multiply each future payment by its discount factor and sum.

```csharp
double df = discountCurve.DiscountFactor(maturityDate);
double pv = notional * couponRate * yearFraction * df;
```

### Zero rate

The zero rate $z(T)$ is the constant rate that, compounded over the period from today to $T$, produces the discount factor. Under continuous compounding:

$$P(T) = e^{-z(T) \cdot T}$$

or equivalently:

$$z(T) = -\frac{\ln P(T)}{T}$$

The library supports multiple compounding conventions:

```csharp
double zeroCont = discountCurve.ZeroRate(date, CompoundingConvention.Continuous);
double zeroAnnual = discountCurve.ZeroRate(date, CompoundingConvention.Annual);
double zeroSimple = discountCurve.ZeroRate(date, CompoundingConvention.Simple);
```

**When to use:** Quoting rates, comparing across instruments, and as the interpolation space for curve construction (linear-zero-rate interpolation operates in this domain).

### Forward rate

The instantaneous forward rate $f(T)$ is the rate earned over an infinitesimally short period starting at $T$:

$$f(T) = -\frac{d \ln P(T)}{dT}$$

In practice, the library computes this as a finite-difference approximation:

$$f(T) \approx -\frac{\ln P(T + \epsilon) - \ln P(T - \epsilon)}{2\epsilon}$$

```csharp
double fwd = discountCurve.InstantaneousForwardRate(date);
```

For discrete forward rates between two dates (e.g., the 3-month rate starting in 1 year), use `IForwardCurve.ForwardRate`:

```csharp
double fwd3m = forwardCurve.ForwardRate(startDate, endDate);
```

**When to use:** Pricing floating-rate legs, estimating future resets, and diagnosing curve smoothness. Spiky forward rates indicate interpolation problems.

### Practical example

```csharp
var definition = StandardCurveRecipes.UsdSofrDiscount(valuationDate);
var snapshot = await new CurveBuilder()
    .WithFixtureAdapters()
    .BuildAsync(definition, valuationDate);

var discountRef = new CurveReference(CurveRole.Discount, new CurrencyCode("USD"));
var curve = (IDiscountCurve)snapshot.CurveGroup.GetCurve(discountRef);

DateOnly fiveYear = valuationDate.AddYears(5);

double df    = curve.DiscountFactor(fiveYear);
double zero  = curve.ZeroRate(fiveYear, CompoundingConvention.Continuous);
double fwd   = curve.InstantaneousForwardRate(fiveYear);

// These are consistent: df = exp(-zero * yearFraction)
```

### Common pitfall: compounding mismatch

If you extract a continuous zero rate and use it in an annual-compounding formula (or vice versa), the resulting present value will be wrong. The error grows with maturity. Always match the compounding convention between the rate you extract and the formula you apply.

---

## 4. Reading Diagnostics

Every `EodCurveSnapshot` includes a `BootstrapDiagnostics` object with three categories of diagnostic information. These are not optional metadata — they are essential for determining whether the calibrated curve is trustworthy.

### Repricing diagnostics

After calibration, each node's instrument is repriced using the solved discount factors. The difference between the market quote and the model-implied quote is reported:

```csharp
foreach (var repricing in snapshot.Diagnostics.Repricing)
{
    Console.WriteLine($"{repricing.Label}: " +
        $"market={repricing.MarketQuote:F6} " +
        $"implied={repricing.ImpliedQuote:F6} " +
        $"error={repricing.AbsoluteError:E2}");
}
```

**How to interpret:**

| Absolute error | Meaning |
|---------------|---------|
| $< 10^{-8}$ | Clean calibration. The node reprices to machine precision. |
| $10^{-8}$ to $10^{-4}$ | Investigate. Possible convention mismatch or solver near its iteration limit. |
| $> 10^{-4}$ | Calibration failure for this node. Do not use the curve for pricing without understanding the cause. |

Each `RepricingDiagnostic` also carries the `InstrumentType` (e.g., "Deposit", "OIS"), the `PillarDate`, and optional `WarningFlags` that provide additional context.

### Structural diagnostics

Structural diagnostics flag non-numerical issues with the curve construction:

```csharp
foreach (var structural in snapshot.Diagnostics.Structural)
{
    Console.WriteLine($"[{structural.Severity}] {structural.Code}: {structural.Message}");
}
```

The most common structural diagnostic is `TREASURY_PROXY`, which indicates that a node was built from Treasury par yields rather than actual OIS quotes. This is expected for the long end of free EOD curves (where OIS swap quotes require commercial data), but it introduces basis risk. The `Context` field provides details about which tenor was affected.

Another common diagnostic is `DATE_ROLLBACK`, which indicates that the data used for a node was observed on a date earlier than the valuation date. The severity distinguishes expected from unexpected gaps:

| Severity | Meaning | Example |
|----------|---------|---------|
| Info | Expected publication lag | SOFR publishes T+1; building a curve on Monday uses Friday's fixing |
| Warning | Unexpected date gap | A yield curve adapter that normally publishes same-day returned stale data |

`DATE_ROLLBACK` issues appear in `CurveSnapshot.DataIssues` alongside adapter-level issues (e.g., transport warnings). Info-level rollbacks are normal for overnight fixings and do not indicate a data problem.

### Numerical diagnostics

Numerical diagnostics report solver behavior:

- `SolverName`: which root-finding algorithm was used (Bisection, Brent, or Newton-Raphson)
- `Iterations`: how many iterations the solver needed
- `Converged`: whether the solver found a solution within tolerance
- `Residual`: the final residual value

A solver that does not converge signals that the instrument pricing function has no root in the expected range — typically caused by inconsistent input data or a convention error.

### Decision tree

```
All repricing errors < 1e-8 AND no structural warnings?
    → Clean calibration. Curve is production-ready.

All repricing errors < 1e-8 BUT structural warnings present?
    → Read the warnings. TREASURY_PROXY is expected for free EOD data.
      Curve is usable with documented basis risk.

Any repricing error > 1e-4?
    → Calibration failure. Check:
      1. Are input quotes in the correct format (decimal, not percent)?
      2. Are day-count and settlement conventions consistent?
      3. Is the node ordering correct (ascending by pillar date)?

Any solver did not converge?
    → Data problem. The quote does not produce a valid discount factor.
      Check the raw adapter output for missing or stale data.
```

---

## 5. Historical Fixings and Forward Projections

### What is a fixing?

A **fixing** is the official published value of a benchmark rate on a specific date. SOFR is published each business day by the New York Fed. SONIA is published by the Bank of England. These are backward-looking observations — they represent what the overnight rate actually was on that date.

### Why bootstrapping needs fixings

When bootstrapping a curve on any given valuation date, some instruments in the curve may reference periods that have already begun. For example, a 1-month OIS swap starting two weeks ago has an accrued portion where the daily SOFR fixings are already known. The bootstrap engine needs those historical fixings to correctly compute the accrued interest and solve for the remaining unknown discount factor.

Without historical fixings, the engine would have to assume a rate for the already-elapsed period, introducing error into the calibrated curve.

### Using the fixings store

`IFixingsStore` is the interface for supplying historical fixings to the bootstrap engine. `InMemoryFixingsStore` provides a simple in-memory implementation:

```csharp
var fixings = new InMemoryFixingsStore();

// Populate with recent SOFR fixings
fixings.AddFixing("SOFR", new DateOnly(2026, 4, 8), 0.0430m);
fixings.AddFixing("SOFR", new DateOnly(2026, 4, 7), 0.0432m);
fixings.AddFixing("SOFR", new DateOnly(2026, 4, 4), 0.0429m);

// Retrieve a specific fixing
decimal sofrRate = fixings.GetFixing("SOFR", new DateOnly(2026, 4, 8));

// Retrieve the full time series
IReadOnlyDictionary<DateOnly, decimal> series = fixings.GetTimeSeries("SOFR");
```

The data adapters (`NewYorkFedSofrDataAdapter`, `BankOfEnglandSoniaDataAdapter`, etc.) fetch these fixings from their official sources and return them as `SourceSeries` objects that can populate a fixings store.

### How fixings connect to forward projections

A calibrated curve encodes both known history and market-implied expectations:

- **Past dates**: the curve is consistent with published fixings (the "known" part)
- **Future dates**: the curve implies forward rates based on current market pricing (the "projected" part)

The boundary between known and projected is the valuation date. Forward rates extracted from the curve for future dates represent the market's current expectation of where rates will be — not a prediction, but a no-arbitrage-consistent projection from today's traded instruments.

This is why `IForwardCurve.ForwardRate(startDate, endDate)` works for both historical and future periods: for past periods, the rate should match published fixings; for future periods, it reflects market-implied expectations.

---

## 6. Handing the Market to OptionPricing

`Boutquin.OptionPricing` depends on `Boutquin.Curves.Abstractions` — it consumes `IDiscountCurve`, `IForwardCurve`, `ICurveGroup`, `MarketEnvironment`, `IFixingsStore`, and `IBusinessCalendar` without knowing how they were calibrated. All market dependencies are encapsulated in `OptionMarketData`.

Four construction patterns are available, from simplest to most integrated:

### Pattern A: Flat curves for testing and exploration

```csharp
var discountCurve = new FlatDiscountCurve(
    new CurveName("USD-OIS"), valuationDate,
    new CurrencyCode("USD"), riskFreeRate: 0.05);

var dividendCurve = new FlatDiscountCurve(
    new CurveName("DIVIDEND"), valuationDate,
    new CurrencyCode("USD"), rate: 0.02);

var market = new OptionMarketData(
    valuationDate, spot: 100m, flatVolatility: 0.20,
    discountCurve, dividendCurve);
```

### Pattern B: Bootstrapped curves with manual extraction

```csharp
var definition = StandardCurveRecipes.UsdSofrDiscount(valuationDate);
var snapshot = await new CurveBuilder()
    .WithFixtureAdapters()
    .BuildAsync(definition, valuationDate);

var discountRef = new CurveReference(CurveRole.Discount, new CurrencyCode("USD"));
var discountCurve = (IDiscountCurve)snapshot.CurveGroup.GetCurve(discountRef);

var market = new OptionMarketData(
    valuationDate, spot: 100m, flatVolatility: 0.20,
    discountCurve);
```

### Pattern C: FromCurveGroup — automatic curve resolution

Instead of extracting individual curves, pass the entire `ICurveGroup` and let `OptionMarketData` resolve discount, dividend, borrow, and forward curves by `CurveRole`:

```csharp
var group = new CurveGroupBuilder(new CurveGroupName("USD-EQUITY"), valuationDate)
    .Add(new CurveReference(CurveRole.Discount, usd), discountCurve)
    .Add(new CurveReference(CurveRole.Dividend, usd), dividendCurve)
    .Add(new CurveReference(CurveRole.Borrow, usd), borrowCurve)
    .Build();

var market = OptionMarketData.FromCurveGroup(group, usd, spot: 100m, flatVolatility: 0.20);
```

The factory looks up curves by role and currency — `Discount` is required, `Dividend`, `Borrow`, and `Forward` are optional. This works directly with `CurveBuilder` output:

```csharp
var snapshot = await new CurveBuilder()
    .WithFixtureAdapters()
    .BuildAsync(definition, valuationDate);

var market = OptionMarketData.FromCurveGroup(
    snapshot.CurveGroup, new CurrencyCode("USD"),
    spot: 100m, flatVolatility: 0.20);
```

### Pattern D: FromMarketEnvironment — curves, benchmarks, and fixings

When working with a full `MarketEnvironment` (curves + benchmarks + fixings), the factory resolves curves and populates an `IFixingsStore` for path-dependent pricers:

```csharp
var market = OptionMarketData.FromMarketEnvironment(
    env, new CurrencyCode("USD"),
    spot: 100m, flatVolatility: 0.20);

// Fixings are available for path-dependent pricing
IFixingsStore? fixings = market.FixingsStore;
```

Environment fixings (keyed by benchmark name, e.g. `"USD-SOFR"`) are mapped into an `InMemoryFixingsStore` at the valuation date. This is the entry point for Asian options, lookbacks, and other instruments that need historical rate series.

### Diagnostic-gated pricing

Before pricing, inspect the bootstrap diagnostics to ensure the curve calibrated cleanly:

```csharp
double maxError = snapshot.Diagnostics.Repricing
    .Max(r => r.AbsoluteError);

if (maxError > 1e-6)
    throw new InvalidOperationException(
        $"Curve failed quality gate: max repricing error {maxError:E2}");

var market = OptionMarketData.FromCurveGroup(snapshot.CurveGroup, usd, 100m, 0.20);
```

### Settlement calendars

Option contracts now accept an optional `IBusinessCalendar` for settlement date adjustment:

```csharp
var option = new EuropeanOption(
    CallPut.Call, strike: 100m, expiryDate,
    settlementCalendar: usnyCalendar,
    settlementAdjustment: BusinessDayAdjustment.Following,
    settlementLagDays: 1);

DateOnly settlement = option.SettlementDate; // business-day adjusted + lag
```

When no calendar is provided, `SettlementDate` equals `ExpiryDate` — existing code is unaffected.

### TreeModelInput convenience factory

For binomial tree pricing, `TreeModelInput.FromMarketData` eliminates manual parameter duplication:

```csharp
var input = TreeModelInput.FromMarketData(market, strike: 100m, expiryDate, steps: 200);
var tree = new CoxRossRubinsteinTreeBuilder().Build(input);
```

### Summary

All patterns produce an `OptionMarketData` that any pricer (`BlackScholesPricer`, `EuropeanTreePricer`, `AmericanTreePricer`, `BarrierTreePricer`) can consume. The pricing engine never touches bootstrap internals — it only calls `DiscountFactor()`, `ZeroRate()`, and `DayCount` on the curve interface.

| Pattern | Use case | Resolves curves | Includes fixings |
|---|---|---|---|
| A — Manual flat | Unit tests, exploration | Manual | No |
| B — Manual extraction | Production, fine control | Manual | No |
| C — `FromCurveGroup` | Standard production | By `CurveRole` | No |
| D — `FromMarketEnvironment` | Full environment | By `CurveRole` | Yes |

For the full integration story, including Greeks, smile-aware pricing, and tree construction, see the `Boutquin.OptionPricing` documentation and its `Examples` project.
