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

// M-1 defensive-guard hardening tests:
// A-6: SimpleReferenceDataProvider..ctor — null IEnumerable params
// A-7: OisDiscountCurveBootstrapper / SwapProjectionCurveBootstrapper — null collection members
// A-9: ConstantConvexityAdjustment..ctor — double.IsFinite guard
// A-10: BootstrapDiagnosticsFactory.CreateDiagnostics — null repricing guard

using Boutquin.Curves.Bootstrap.ConvexityAdjustments;
using Boutquin.Curves.Bootstrap.Diagnostics;
using Boutquin.Curves.Bootstrap.Inputs;
using Boutquin.Curves.Bootstrap.Instruments;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.MarketData.Abstractions.Calendars;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

public sealed class DefensiveGuardHardeningTests
{
    // ── A-6: SimpleReferenceDataProvider — null IEnumerable params ─────────

    [Fact]
    public void SimpleReferenceDataProvider_NullCalendars_Throws_ArgumentNullException_NamedCalendars()
    {
        // AC-G1: null calendars must throw ArgumentNullException naming "calendars"
        Action act = () => new SimpleReferenceDataProvider(
            null!,
            Array.Empty<RateBenchmark>(),
            Array.Empty<IInstrumentConvention>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("calendars");
    }

    [Fact]
    public void SimpleReferenceDataProvider_NullBenchmarks_Throws_ArgumentNullException_NamedBenchmarks()
    {
        // AC-G1: null benchmarks must throw ArgumentNullException naming "benchmarks"
        Action act = () => new SimpleReferenceDataProvider(
            Array.Empty<IBusinessCalendar>(),
            null!,
            Array.Empty<IInstrumentConvention>());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("benchmarks");
    }

    [Fact]
    public void SimpleReferenceDataProvider_NullConventions_Throws_ArgumentNullException_NamedConventions()
    {
        // AC-G1: null conventions must throw ArgumentNullException naming "conventions"
        Action act = () => new SimpleReferenceDataProvider(
            Array.Empty<IBusinessCalendar>(),
            Array.Empty<RateBenchmark>(),
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("conventions");
    }

    // ── A-7: OisDiscountCurveBootstrapper — null Deposits collection ───────

    [Fact]
    public void OisDiscountCurveBootstrapper_NullDeposits_Throws_ArgumentNullException_NamedDeposits()
    {
        // AC-G1: null Deposits must throw ArgumentNullException (not NRE) naming "deposits" or the param.
        // The spec says guard collections BEFORE first access so it throws ArgumentException/ArgumentNullException
        // not NRE.
        Action act = () =>
        {
            _ = new OisBootstrapInput(
                "USD-OIS",
                new DateOnly(2026, 6, 30),
                null!,
                Array.Empty<OisSwapQuote>(),
                Actual365Fixed.Instance);
        };

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OisDiscountCurveBootstrapper_NullOisSwaps_Throws_ArgumentNullException_NamedOisSwaps()
    {
        // AC-G1: null OisSwaps must throw ArgumentNullException (not NRE).
        Action act = () =>
        {
            _ = new OisBootstrapInput(
                "USD-OIS",
                new DateOnly(2026, 6, 30),
                new[] { new DepositQuote(1, 0.05) },
                null!,
                Actual365Fixed.Instance);
        };

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SwapProjectionCurveBootstrapper_NullSwaps_Throws_ArgumentNullException_NamedSwaps()
    {
        // AC-G1: null Swaps must throw ArgumentNullException (not NRE).
        Action act = () =>
        {
            _ = new SwapBootstrapInput(
                "USD-LIBOR",
                new DateOnly(2026, 6, 30),
                null!,
                Actual365Fixed.Instance);
        };

        act.Should().Throw<ArgumentNullException>();
    }

    // ── A-9: ConstantConvexityAdjustment — NaN/∞ guard ────────────────────

    [Fact]
    public void ConstantConvexityAdjustment_NaN_Throws_ArgumentException()
    {
        // AC-G1: NaN adjustment must throw ArgumentException or ArgumentOutOfRangeException
        Action act = () => new ConstantConvexityAdjustment(double.NaN);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConstantConvexityAdjustment_PositiveInfinity_Throws_ArgumentException()
    {
        // AC-G1: +Infinity adjustment must throw ArgumentException or ArgumentOutOfRangeException
        Action act = () => new ConstantConvexityAdjustment(double.PositiveInfinity);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ConstantConvexityAdjustment_NegativeInfinity_Throws_ArgumentException()
    {
        // AC-G1: -Infinity adjustment must throw ArgumentException or ArgumentOutOfRangeException
        Action act = () => new ConstantConvexityAdjustment(double.NegativeInfinity);

        act.Should().Throw<ArgumentException>();
    }

    // ── A-10: BootstrapDiagnosticsFactory.CreateDiagnostics — null repricing ─

    [Fact]
    public void BootstrapDiagnosticsFactory_CreateDiagnostics_NullRepricing_Throws_ArgumentNullException()
    {
        // AC-G1: null repricing must throw ArgumentNullException naming "repricing"
        Action act = () => BootstrapDiagnosticsFactory.CreateDiagnostics(null!, 1, 1);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("repricing");
    }
}
