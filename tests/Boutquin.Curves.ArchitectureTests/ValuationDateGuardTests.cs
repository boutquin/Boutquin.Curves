// Copyright (c) 2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Core.Discounting;
using Boutquin.Curves.Core.Forwards;
using Boutquin.Curves.Interpolation;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;

namespace Boutquin.Curves.ArchitectureTests;

/// <summary>
/// M-5 — the durable behavioral gate for the valuation-date-boundary guard defect class: pins that
/// every public discount/forward curve query method handles at/before-valuation-date queries with its
/// documented convention rather than leaking an opaque exception, so the class cannot silently
/// regress.
/// </summary>
/// <remarks>
/// NetArchTest performs structural/dependency assertions, not method-body control-flow analysis, so
/// "every query method guards <c>date &lt;= ValuationDate</c> before calling <c>YearFraction</c>"
/// cannot be checked as an IL pattern. This is instead enforced BEHAVIORALLY, and the boundary
/// property differs by curve family because the two query surfaces take a different shape of input:
///
/// <para><b>Discount curves (single-date query).</b> Every public, non-abstract, non-nested class
/// implementing <see cref="IDiscountCurve"/> is queried at/before its own
/// <see cref="ICurve.ValuationDate"/> and must return the family's documented flat-boundary
/// convention value (typically 1.0) rather than letting an opaque
/// <see cref="ArgumentOutOfRangeException"/> (from <c>IYearFractionCalculator.YearFraction</c>
/// rejecting a non-positive span — see M-4's day-count investigation) escape uncaught. This is the
/// original valuation-date-boundary defect class: <c>DiscountFactor(date &lt;= ValuationDate)</c>
/// must yield the flat convention, not a raw day-count exception.</para>
///
/// <para><b>Forward curves (two-date accrual query).</b> <see cref="IForwardCurve.ForwardRate"/>
/// takes an ordered (start, end) accrual interval, so a single-date valuation-boundary query does
/// not apply. Their meaningful boundary property is the <b>ordering contract</b>: a degenerate or
/// inverted interval (<c>endDate &lt;= startDate</c>) must throw the curve's OWN documented
/// <see cref="ArgumentOutOfRangeException"/> located on <c>nameof(endDate)</c> — NOT an opaque
/// day-count-layer exception from some other parameter — while a valid positive interval returns a
/// finite rate. Pinning the located exception is what makes these tests non-vacuous: the earlier
/// form queried a positive span and asserted <c>NotThrow</c>, which passed without ever reaching the
/// rejection path it claimed to guard.</para>
///
/// Two-part completeness idiom: <see cref="AllPublicDiscountAndForwardCurveTypes_MatchTheCoveredTable"/>
/// reflectively counts every type satisfying the shape above and asserts it matches the explicit
/// covered-type list below. It scans ALL loaded <c>Boutquin.Curves.*</c> assemblies (not the shared
/// <c>AllSourceAssemblies</c> list, which omits the Indices/Quotes compat metapackages and the
/// Examples executable), so a public curve type landing in any Boutquin.Curves assembly — including
/// those three — cannot escape the invariant. If a new curve type is added without adding its case to
/// <see cref="s_coveredTypes"/>, that meta-test fails — the gap can't silently regress into an
/// untested query type.
/// </remarks>
public sealed class ValuationDateGuardTests : BaseArchitectureTest
{
    private const Currency Usd = Currency.USD;
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly DateOnly s_beforeValuation = s_valuationDate.AddDays(-5);

    private static readonly Type[] s_coveredTypes =
    [
        typeof(FlatDiscountCurve),
        typeof(InterpolatedDiscountCurve),
        typeof(ZeroSpreadedDiscountCurve),
        typeof(JumpAdjustedDiscountCurve),
        typeof(MultiplicativeSpreadDiscountCurve),
        typeof(FlatForwardCurve),
        typeof(ForwardCurveFromDiscountCurves),
    ];

    [Fact]
    public void AllPublicDiscountAndForwardCurveTypes_MatchTheCoveredTable()
    {
        var curveAssemblies = LoadAllCurveAssemblies();

        // Guard against silent under-scanning: if any Boutquin.Curves.* assembly failed to load
        // (e.g. a referenced-but-unused compat metapackage that the JIT never touched), a public
        // curve type living there would evade this invariant. Assert the discovery set explicitly
        // includes the three assemblies the shared AllSourceAssemblies list omits.
        var loadedNames = curveAssemblies
            .Select(assembly => assembly.GetName().Name)
            .ToArray();
        loadedNames.Should().Contain(
            new[] { "Boutquin.Curves.Indices", "Boutquin.Curves.Quotes", "Boutquin.Curves.Examples" },
            because: "the completeness scan must cover the Indices/Quotes compat metapackages and the " +
                     "Examples executable — the assemblies AllSourceAssemblies omits — or a public curve " +
                     "type added to any of them would silently escape the valuation-date-boundary gate");

        var discovered = curveAssemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass
                && type.IsPublic
                && !type.IsAbstract
                && !type.IsNested
                && (typeof(IDiscountCurve).IsAssignableFrom(type) || typeof(IForwardCurve).IsAssignableFrom(type)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        discovered.Should().BeEquivalentTo(s_coveredTypes,
            because: "every public IDiscountCurve/IForwardCurve implementation must be added to " +
                     $"{nameof(s_coveredTypes)} with a corresponding valuation-date-boundary behavioral test — " +
                     "a new curve type landing without one is exactly the regression class this gate exists to catch");
    }

    /// <summary>
    /// Loads every <c>Boutquin.Curves.*</c> assembly present next to this test assembly, forcing the
    /// referenced-but-unused compat metapackages (Indices, Quotes) and the Examples executable to load
    /// so the completeness scan covers them. <see cref="AppDomain.CurrentDomain"/>'s already-loaded set
    /// is unioned in so the scan works regardless of JIT load order.
    /// </summary>
    private static Assembly[] LoadAllCurveAssemblies()
    {
        var testAssemblyLocation = typeof(ValuationDateGuardTests).Assembly.Location;
        var probeDirectory = Path.GetDirectoryName(testAssemblyLocation)!;

        foreach (var dllPath in Directory.EnumerateFiles(probeDirectory, "Boutquin.Curves.*.dll"))
        {
            // LoadFrom is idempotent for an already-loaded assembly (returns the loaded instance),
            // so force-loading here is safe even for assemblies the JIT already touched.
            Assembly.LoadFrom(dllPath);
        }

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("Boutquin.Curves", StringComparison.Ordinal) == true)
            .ToArray();
    }

    [Fact]
    public void FlatDiscountCurve_AtOrBeforeValuationDate_ReturnsFlatConvention_NotOpaqueException()
    {
        IDiscountCurve curve = new FlatDiscountCurve(new CurveName("USD-Disc"), s_valuationDate, Usd, 0.03);
        AssertDiscountCurveHandlesBoundary(curve);
    }

    [Fact]
    public void InterpolatedDiscountCurve_AtOrBeforeValuationDate_ReturnsFlatConvention_NotOpaqueException()
    {
        IDiscountCurve curve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            s_valuationDate,
            Usd,
            new[] { new CurvePoint(s_valuationDate.AddYears(2), 0.92d) },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));
        AssertDiscountCurveHandlesBoundary(curve);
    }

    [Fact]
    public void ZeroSpreadedDiscountCurve_AtOrBeforeValuationDate_ReturnsFlatConvention_NotOpaqueException()
    {
        IDiscountCurve underlying = new FlatDiscountCurve(new CurveName("USD-Disc"), s_valuationDate, Usd, 0.03);
        IDiscountCurve curve = new ZeroSpreadedDiscountCurve(new CurveName("USD-Disc-Spread"), underlying, 0.0010);
        AssertDiscountCurveHandlesBoundary(curve);
    }

    [Fact]
    public void JumpAdjustedDiscountCurve_AtOrBeforeValuationDate_ReturnsFlatConvention_NotOpaqueException()
    {
        IDiscountCurve underlying = new FlatDiscountCurve(new CurveName("USD-Disc"), s_valuationDate, Usd, 0.03);
        IDiscountCurve curve = new JumpAdjustedDiscountCurve(
            underlying,
            new[] { new CurvePoint(s_valuationDate.AddYears(1), 0.995d) });
        AssertDiscountCurveHandlesBoundary(curve);
    }

    [Fact]
    public void MultiplicativeSpreadDiscountCurve_AtOrBeforeValuationDate_ReturnsFlatConvention_NotOpaqueException()
    {
        IDiscountCurve underlying = new FlatDiscountCurve(new CurveName("USD-Disc"), s_valuationDate, Usd, 0.03);
        IDiscountCurve curve = new MultiplicativeSpreadDiscountCurve(new CurveName("USD-Disc-Mult"), underlying, 1.001);
        AssertDiscountCurveHandlesBoundary(curve);
    }

    [Fact]
    public void FlatForwardCurve_DegenerateInterval_ThrowsOwnOrderingGuard_NotOpaqueException()
    {
        IForwardCurve curve = new FlatForwardCurve(
            new CurveName("USD-SOFR-FWD"), s_valuationDate, Usd, new BenchmarkName("SOFR"), 0.03);
        AssertForwardCurveBoundaryContract(curve);
    }

    [Fact]
    public void ForwardCurveFromDiscountCurves_DegenerateInterval_ThrowsOwnOrderingGuard_NotOpaqueException()
    {
        IDiscountCurve discount = new FlatDiscountCurve(new CurveName("USD-Disc"), s_valuationDate, Usd, 0.03);
        IDiscountCurve projection = new FlatDiscountCurve(new CurveName("USD-Proj"), s_valuationDate, Usd, 0.032);
        IForwardCurve curve = new ForwardCurveFromDiscountCurves(
            new CurveName("USD-SOFR-FWD"), new BenchmarkName("SOFR"), discount, projection);
        AssertForwardCurveBoundaryContract(curve);
    }

    private static void AssertDiscountCurveHandlesBoundary(IDiscountCurve curve)
    {
        Action atValuation = () => curve.DiscountFactor(curve.ValuationDate);
        Action beforeValuation = () => curve.DiscountFactor(s_beforeValuation);
        atValuation.Should().NotThrow(because: "DiscountFactor(ValuationDate) must return the flat boundary convention (typically 1.0), not throw");
        beforeValuation.Should().NotThrow(because: "DiscountFactor before ValuationDate must return the flat boundary convention, not throw");

        curve.DiscountFactor(curve.ValuationDate).Should().Be(curve.DiscountFactor(s_beforeValuation),
            because: "the flat pre-valuation-date convention must be uniform for any date at or before ValuationDate");

        Action zeroRateAtValuation = () => curve.ZeroRate(curve.ValuationDate, CompoundingConvention.Continuous);
        zeroRateAtValuation.Should().NotThrow(because: "ZeroRate(ValuationDate) must not throw an opaque exception");
    }

    private static void AssertForwardCurveBoundaryContract(IForwardCurve curve)
    {
        // Meaningful boundary property #1 — the ordering contract. A degenerate interval (end == start)
        // must throw the curve's OWN documented ArgumentOutOfRangeException located on nameof(endDate),
        // NOT an opaque day-count-layer exception on some other parameter. Pinning WithParameterName is
        // what makes this non-vacuous: the earlier form queried a positive span and asserted NotThrow,
        // which never reached this rejection path. Remove the `if (endDate <= startDate) throw` guard in
        // either forward curve and this assertion goes RED (verified via guard-removal mutation).
        Action degenerate = () => curve.ForwardRate(curve.ValuationDate, curve.ValuationDate);
        degenerate.Should().ThrowExactly<ArgumentOutOfRangeException>(
                because: "a degenerate accrual interval (endDate <= startDate) must throw the curve's own located ordering guard, not an opaque exception")
            .WithParameterName("endDate");

        // Meaningful boundary property #2 — the happy path still works. A valid positive interval
        // strictly after the valuation date returns a finite forward rate without throwing.
        DateOnly start = curve.ValuationDate.AddDays(1);
        DateOnly end = curve.ValuationDate.AddYears(1);
        double rate = 0d;
        Action positive = () => rate = curve.ForwardRate(start, end);
        positive.Should().NotThrow(because: "a valid positive accrual interval must return a forward rate");
        double.IsFinite(rate).Should().BeTrue(because: "the forward rate for a valid interval must be a finite number");
    }
}
