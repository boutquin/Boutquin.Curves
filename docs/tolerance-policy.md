# Tolerance Policy

Domain-specific tolerance thresholds for numerical tests in Boutquin.Curves.

## Calibration Repricing

| Metric | Tolerance | Context |
|--------|-----------|---------|
| Repricing absolute error | 1e-8 | Bootstrap calibration node repricing vs observed quote |
| Jacobian diagonal element | > 0.0 | Finite-difference Jacobian must detect sensitivity |

Repricing errors above 1e-8 indicate either a convention mismatch or a solver convergence failure. Investigation is required before accepting any curve built with above-tolerance repricing errors.

## Discount Factors

| Property | Tolerance | Rationale |
|----------|-----------|-----------|
| DF at valuation date | Exact 1.0 | By construction |
| DF positivity | > 0 | Arbitrage-free requirement |
| DF monotonicity | DF(T+1) < DF(T) for T > 0 | Positive interest rates (normal market) |
| DF round-trip (zero → DF → zero) | 1e-12 | Numerical precision of exp/log |

## Zero Rates

| Property | Tolerance | Rationale |
|----------|-----------|-----------|
| Continuous zero rate stability | 1e-6 | Interpolation and day-count precision |
| Cross-compounding consistency | 1e-8 | DF → zero rate conversion round-trip |
| Zero-spread identity (0 spread) | 1e-12 | Algebraic identity: base + 0 = base |

## Forward Rates

| Property | Tolerance | Rationale |
|----------|-----------|-----------|
| Forward DF positivity | > 0 | DF(T2)/DF(T1) > 0 for T2 > T1 |
| Forward DF < 1 | < 1.0 | Positive forward rates in normal market |
| Instantaneous forward (finite diff) | 1e-4 | Numerical derivative approximation |

## Convexity Adjustments

| Property | Tolerance | Rationale |
|----------|-----------|-----------|
| Short-dated (< 1Y) adjustment | < 1 bp (1e-4) | Convexity bias is negligible for short contracts |
| Ho-Lee limit convergence | 1e-8 | HW with a→0 must match Ho-Lee formula |
| Monotonicity in maturity | CA(T+1) > CA(T) | Convexity grows with maturity |

## Spreaded Curves

| Property | Tolerance | Rationale |
|----------|-----------|-----------|
| Zero spread identity | 1e-12 | Additive 0 or multiplicative 1.0 = base |
| Positive spread → lower DF | Strict | Directional correctness |
| Forward rate shift | 1e-6 | Additive spread appears in forward rate |

## Market Data Adapters

| Metric | Tolerance | Rationale |
|--------|-----------|-----------|
| Rate normalization | Exact | Percentage to decimal division by 100 |
| Staleness detection | 0 days | Warning emitted when latest < requested |

## Test Categories

- **Golden tests**: Known fixture → expected numerical output. Verify calibration pipeline end-to-end.
- **Property tests**: Statistical invariants (positivity, monotonicity, identity). Verify algebraic contracts.
- **Repricing tests**: Bootstrap node accuracy. Verify solver convergence.
- **Adapter tests**: Deterministic mock HTTP. Verify parsing and normalization.
