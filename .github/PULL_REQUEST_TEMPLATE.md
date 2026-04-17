## Summary

Brief description of what this PR does.

## Changes

- ...

## Related Issues

Closes #

## Checklist

- [ ] Code compiles with zero warnings (`TreatWarningsAsErrors` enabled)
- [ ] All existing tests pass (unit, integration, property-based, golden calibration, architecture)
- [ ] New tests added for new functionality — unit tests under `tests/Boutquin.Curves.<Package>.Tests/`; golden calibration tests using `FixtureData` when bootstrap behavior changes; architecture tests when new assemblies or external dependencies are introduced
- [ ] `PublicAPI.Unshipped.txt` updated for any new or changed public API
- [ ] `dotnet format --verify-no-changes` produces no changes
- [ ] XML doc comments are complete (no banned phrases; semantic description; `<remarks>` required on core contracts and key implementations — see [CONTRIBUTING.md](../CONTRIBUTING.md#documentation-style-guide))
- [ ] `CHANGELOG.md` updated under `[Unreleased]` (if user-visible change)
- [ ] No new cross-layer dependency violations: Boutquin.Curves must not be referenced by Boutquin.MarketData; Boutquin.OptionPricing depends on Boutquin.Curves, not the reverse; calendar types must not be defined in Boutquin.Curves (architecture tests enforce this)
