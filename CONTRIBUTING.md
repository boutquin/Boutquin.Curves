# Contributing to Boutquin.Curves

Thank you for considering contributing to Boutquin.Curves! Whether it's reporting a bug, proposing a feature, or submitting a pull request, your input is welcome.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [How to Contribute](#how-to-contribute)
  - [Reporting Bugs](#reporting-bugs)
  - [Suggesting Enhancements](#suggesting-enhancements)
  - [Contributing Code](#contributing-code)
- [Style Guides](#style-guides)
  - [Git Commit Messages](#git-commit-messages)
  - [C# Style Guide](#c-style-guide)
  - [Documentation Style Guide](#documentation-style-guide)
  - [Numerical and Market Correctness](#numerical-and-market-correctness)
- [Pull Request Process](#pull-request-process)
- [License](#license)
- [Community](#community)

## Code of Conduct

This project adheres to the Contributor Covenant [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Report unacceptable behavior through [GitHub Issues](https://github.com/boutquin/Boutquin.Curves/issues).

## How to Contribute

### Reporting Bugs

Open an issue on the [Issues](https://github.com/boutquin/Boutquin.Curves/issues) page with:

- A clear and descriptive title.
- Steps to reproduce the issue (ideally a minimal failing code snippet).
- Expected and actual output, including field values and any relevant `CurveSnapshot` diagnostics or `DataIssue` entries.
- Reference values (from ISDA specifications, central bank publications, or a reference implementation) when asserting calibration or rate correctness.
- Environment: OS, .NET runtime version, package version, data source, and valuation date.

### Suggesting Enhancements

Open an issue describing:

- The curve type, interpolation method, instrument convention, data adapter, or recipe you would like to see added.
- A primary reference (ISDA specification, ARRC/IOSCO/CME documentation, central bank publication) with the canonical form.
- The intended consumer (pricing system, risk engine, backtesting framework, etc.).
- Any trade-offs: numerical stability, data source licensing, convention ambiguity, or scope relative to `Boutquin.MarketData`.

### Contributing Code

1. **Fork the repository** and clone your fork locally.
   ```bash
   git clone https://github.com/your-username/Boutquin.Curves.git
   cd Boutquin.Curves
   ```

2. **Create a feature branch**:
   ```bash
   git checkout -b feature-or-bugfix-name
   ```

3. **Implement the change** following the style guides below.

4. **Add tests** covering the new behavior. For Boutquin.Curves code, prefer:
   - xUnit + FluentAssertions unit tests under `tests/Boutquin.Curves.<Package>.Tests/` with deterministic inputs and explicit field assertions.
   - Property-based tests (FsCheck) for interpolation invariants (monotonicity, continuity, boundary behavior).
   - Golden calibration tests using `FixtureData` and pinned expected discount factors when the change affects the bootstrap calibrator.
   - Architecture assertions under `tests/Boutquin.Curves.ArchitectureTests/` when the change introduces new assemblies, new external dependencies, or new cross-layer references.

5. **Record the public API surface** in `src/<Package>/PublicAPI.Unshipped.txt`. The `PublicAPI` analyzer enforces this at build time.

6. **Update `CHANGELOG.md`** under the `[Unreleased]` section.

7. **Run the full gate** before opening a PR:
   ```bash
   dotnet build Boutquin.Curves.slnx --configuration Release
   dotnet test Boutquin.Curves.slnx --configuration Release
   dotnet format Boutquin.Curves.slnx --verify-no-changes
   ```

8. **Push and open a pull request**.

## Style Guides

### Git Commit Messages

- Use the present tense ("Add monotone convex interpolator" not "Added monotone convex interpolator").
- Use the imperative mood ("Move helper to Bootstrap" not "Moves helper to Bootstrap").
- Limit the first line to 72 characters.
- Reference issues and pull requests where applicable.

### C# Style Guide

- Follow the conventions documented in `CLAUDE.md` and `.editorconfig` at the repository root.
- Public types are `sealed` unless they are interfaces, abstract base classes, or records (enforced by architecture tests where applicable).
- **Boutquin.Curves depends on Boutquin.MarketData for data access — the dependency never flows in reverse.** Do not add a reference from MarketData to Curves. `Boutquin.OptionPricing` depends on Boutquin.Curves, not the reverse.
- **Concrete data source adapters live in the separate `Boutquin.MarketData.Adapter` repository**, not here. The kernel ships contracts and infrastructure only.
- **Calendar contracts (`IBusinessCalendar`, `BusinessDayAdjustment`) and implementations live in `Boutquin.MarketData`.** Do not define calendar types in Boutquin.Curves — consume them from MarketData.
- Litmus test for placement: "Does this belong in the data layer (MarketData), the curve construction layer (Curves), or the pricing layer (OptionPricing)?" If it carries data-access semantics, it belongs in MarketData. If it carries option-specific math, it belongs in OptionPricing.

### Documentation Style Guide

All public API additions must satisfy the in-code documentation bar:

- `<summary>` on every public type, constructor, method, property, and enum member.
- `<param>`, `<returns>`, and `<remarks>` per the required-elements checklist in the [Documentation Style Guide](#documentation-style-guide) section above.
- No banned boilerplate phrases ("Provides the ... functionality", "Executes the ... operation", "Gets or sets the ... for this instance", "Input value for ...", "/// Executes ...", "Operation result.").
- Domain vocabulary is mandatory: valuation date, benchmark, accrual period, day count, fixing, pillar date, interpolation, calibration residual, discount factor, zero rate, forward rate.
- Contracts and invariants must be explicit — rejection conditions, null behaviour, calendar/day-count edge cases, and extrapolation behaviour belong in `<remarks>`, not left implicit.

Validation commands (must return zero matches before opening a PR):

```bash
# Enforce banned-phrase policy
grep -rn \
  -e 'Provides the .* functionality and related domain behavior' \
  -e 'Executes the .* operation for this component' \
  -e 'The .* input value for the operation' \
  -e 'Gets or sets the .* for this instance' \
  --include='*.cs' src/

# Enforce low-signal phrase policy
grep -rn \
  -e 'Input value for <paramref name=' \
  -e '/// Executes ' \
  -e 'Operation result\.' \
  --include='*.cs' src/

# Enforce accessor-verb property doc policy
grep -rn '/// Gets \w' --include='*.cs' src/
```

### Numerical and Market Correctness

- State accepted inputs explicitly in docs and tests. Reject out-of-contract inputs at construction time with `ArgumentException` or `ArgumentOutOfRangeException` — do not silently produce wrong results.
- Document interpolation edge cases: extrapolation beyond the last pillar, degenerate node sets (fewer than 2 pillars), valuation date at or after the curve horizon.
- When asserting calibration output, document the instrument repricing identity being enforced (e.g., "deposit rate equals the implied zero rate over the deposit period") and the tolerance policy (see `docs/tolerance-policy.md`).
- When adding a new instrument helper, document the exact formula used for the model price and the quote convention (e.g., "OIS fair value is the floating leg PV minus the fixed leg PV, quoted as an annual rate with day count ACT/360").
- Calibration diagnostics (`CurveSnapshot.DataIssues`) must document which `DataIssue` codes a new node type can emit — callers cannot handle what they cannot anticipate.

## Pull Request Process

1. **Ensure the full gate passes**: build (warnings-as-errors), all tests (unit, integration, property-based, golden calibration, architecture), and `dotnet format --verify-no-changes`.
2. **Describe your changes** in the PR body: reference the issue, summarise the feature or fix, cite the relevant specification or formula, and note any `PublicAPI.Unshipped.txt` entries added.
3. **Review process**: maintainers will review for correctness, numerical stability, style, and architectural fit. You may be asked to tighten test coverage, add tolerance documentation, or clarify convention choices.
4. **Merge**: once approved, a maintainer merges the PR. Releases are cut separately via the dual-repo squash workflow on the public repository.

## License

By contributing to Boutquin.Curves, you agree that your contributions are licensed under the Apache 2.0 License.

## Community

Join the [GitHub Discussions](https://github.com/boutquin/Boutquin.Curves/discussions) to ask questions, propose new curve types or adapters, and share usage patterns.

---

Thank you for contributing!
