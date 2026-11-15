# Migrating Curves to 3.0

Curves 3.0 targets .NET 10 and .NET 11 and consumes the complete published MarketData and Adapter 3.0.0 family. Its own pricing algorithms are unchanged. Because Curves exposes upstream calendar, record and pipeline contracts, callers must upgrade and recompile the package families together.

Use the same Curves version for all ten packages, including the Indices and Quotes compatibility packages. Retain published Numerics 2.0.0, Storage 2.0.1 and Domain 1.0.1 unless performing a separately qualified upgrade. Restore from NuGet.org with regenerated lockfiles; older MarketData 2.x pins must not remain in a mixed graph.

Price-history consumers use the new explicit price-basis contract. Declare the required basis, preserve adapter provenance and handle unsupported or mismatched bases separately from provider availability. Follow the [MarketData migration guide](https://github.com/boutquin/Boutquin.MarketData/blob/v3.0.0/docs/migration-3.0.md) for request, cache and legacy-data behavior. Do not relabel adjusted historical data as raw.

Curve construction, interpolation and calibration APIs retain their behavior. The package verification workflow executes both target suites and package-only examples. .NET 10 financial baselines remain in force; .NET 11 uses its native decimal conversion precision.
