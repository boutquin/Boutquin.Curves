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
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Risk.Tests;

public sealed class BucketedZeroRateShockTests
{
    // F-32: constructor must reject null shockPoints to prevent a NullReferenceException inside
    // Apply() or ShockPoints property access. Without the guard, the defensive .ToArray() call
    // in the constructor would throw a NullReferenceException (not ArgumentNullException) at an
    // unexpected call site.
    [Fact]
    public void Constructor_ShouldThrow_WhenShockPointsIsNull()
    {
        Action act = () => _ = new BucketedZeroRateShock("test", null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("shockPoints");
    }

    [Fact]
    public void Apply_ShouldThrow_WhenCurveIsNull()
    {
        BucketedZeroRateShock shock = new(
            "Bucketed",
            new[] { new BucketedShockPoint(1d, 5d) });

        Action act = () => shock.Apply(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("curve");
    }

    [Fact]
    public void Constructor_ShouldDefensivelyCopyShockPoints()
    {
        List<BucketedShockPoint> mutableList = new()
        {
            new BucketedShockPoint(1d, 5d),
            new BucketedShockPoint(2d, 10d)
        };

        BucketedZeroRateShock shock = new("Bucketed", mutableList);

        // Mutate the original list after construction.
        mutableList.Add(new BucketedShockPoint(5d, 50d));

        // The record should be unaffected by the mutation.
        shock.ShockPoints.Should().HaveCount(2);
    }

    // ── F-CENSUS-A-06 ──────────────────────────────────────────────────────────

    /// <summary>
    /// F-CENSUS-A-06: <c>BucketedShiftedDiscountCurve.DiscountFactor</c> must return
    /// the inner curve's discount factor (1.0 for valuation-date-or-past) instead of
    /// throwing <see cref="ArgumentOutOfRangeException"/> when the queried date
    /// is strictly before the valuation date.
    /// Also verifies <c>ValueAt</c> which delegates to <c>DiscountFactor</c>.
    /// </summary>
    [Fact]
    public void BucketedShifted_DiscountFactor_PastDate_ReturnsInnerValue_NotThrows()
    {
        // Arrange
        var valuationDate = new DateOnly(2026, 6, 14);
        var pastDate = valuationDate.AddDays(-10);
        FlatDiscountCurve baseFlat = new(new CurveName("USD-Disc"), valuationDate, Currency.USD, 0.04d);
        BucketedZeroRateShock shock = new(
            "KRD-1Y",
            new[] { new BucketedShockPoint(1d, 50d), new BucketedShockPoint(5d, 50d) });

        var shockedCurve = shock.Apply(baseFlat) as IDiscountCurve;
        shockedCurve.Should().NotBeNull();

        // Act & Assert — must NOT throw; must return the inner's past-date value (1.0)
        double df = shockedCurve!.DiscountFactor(pastDate);
        df.Should().Be(1d, "past-date discount factor must equal 1.0 per family convention (FlatDiscountCurve reference)");

        double vat = shockedCurve.ValueAt(pastDate);
        vat.Should().Be(1d, "ValueAt delegates to DiscountFactor — must also return 1.0");
    }

    // ── F-CENSUS-A-07 ──────────────────────────────────────────────────────────

    /// <summary>
    /// F-CENSUS-A-07: <c>BucketedShiftedDiscountCurve.ZeroRate</c> must return 0.0
    /// instead of throwing <see cref="ArgumentOutOfRangeException"/> when
    /// the queried date is strictly before the valuation date.
    /// Distinct from the enum-default finding (registry row 40, <c>bsdc-enum</c>):
    /// this is a boundary-guard defect at the front of the method, not the switch arm.
    /// </summary>
    [Fact]
    public void BucketedShifted_ZeroRate_PastDate_ReturnsZero_NotThrows()
    {
        // Arrange
        var valuationDate = new DateOnly(2026, 6, 14);
        var pastDate = valuationDate.AddDays(-10);
        FlatDiscountCurve baseFlat = new(new CurveName("USD-Disc"), valuationDate, Currency.USD, 0.04d);
        BucketedZeroRateShock shock = new(
            "KRD-1Y",
            new[] { new BucketedShockPoint(1d, 50d), new BucketedShockPoint(5d, 50d) });

        var shockedCurve = shock.Apply(baseFlat) as IDiscountCurve;
        shockedCurve.Should().NotBeNull();

        // Act & Assert — must NOT throw; must return 0.0 per family convention
        double zero = shockedCurve!.ZeroRate(pastDate, CompoundingConvention.Continuous);
        zero.Should().Be(0d, "past-date zero rate must equal 0.0 per family convention (FlatDiscountCurve reference)");
    }

    /// <summary>
    /// CF-C6-4: BucketedShiftedDiscountCurve.ZeroRate default arm must throw
    /// ArgumentOutOfRangeException for an unknown CompoundingConvention, matching
    /// the sibling CurveMath.ZeroRateFromDiscountFactor contract.
    /// The old default arm silently fell through to Continuous (-Math.Log(df)/t),
    /// yielding a silently-wrong rate for any future enum member.
    /// </summary>
    [Fact]
    public void ZeroRate_UnknownCompoundingConvention_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var valuationDate = new DateOnly(2026, 1, 1);
        FlatDiscountCurve baseFlat = new(
            new CurveName("USD-Test"),
            valuationDate,
            Currency.USD,
            0.04d);

        BucketedZeroRateShock shock = new(
            "Test",
            new[] { new BucketedShockPoint(1d, 1d) });

        var shocked = shock.Apply(baseFlat) as IDiscountCurve;
        shocked.Should().NotBeNull();

        // Act — use an out-of-range enum cast to trigger the default arm.
        const CompoundingConvention unknownConvention = (CompoundingConvention)99;

        Action act = () => shocked!.ZeroRate(valuationDate.AddYears(1), unknownConvention);

        // Assert — must throw, not silently return a wrong value.
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── AT-18: InstantaneousForwardRate front-boundary guard ──────────────────

    /// <summary>
    /// AT-18: <c>BucketedShiftedDiscountCurve.InstantaneousForwardRate</c> must NOT throw
    /// <see cref="ArgumentOutOfRangeException"/> when called at the valuation date (or within
    /// one day of it), because the centered finite-difference requires a left-date that is
    /// before the valuation date, causing <c>YearFraction(startDate, endDate)</c> to throw
    /// when <c>endDate &lt; startDate</c>.
    ///
    /// The fix must mirror the sibling <c>InterpolatedDiscountCurve.InstantaneousForwardRate</c>:
    /// use a one-sided right difference at or within one day of the valuation date.
    /// </summary>
    [Fact]
    public void BucketedShifted_InstantaneousForwardRate_AtValuationDate_DoesNotThrow()
    {
        // Arrange
        var valuationDate = new DateOnly(2026, 6, 14);
        FlatDiscountCurve baseFlat = new(new CurveName("USD-Disc"), valuationDate, Currency.USD, 0.05d);
        BucketedZeroRateShock shock = new(
            "KRD-1Y",
            new[] { new BucketedShockPoint(1d, 50d), new BucketedShockPoint(5d, 50d) });

        var shockedCurve = shock.Apply(baseFlat) as IDiscountCurve;
        shockedCurve.Should().NotBeNull();

        // Act — at valuation date: centered diff would need leftDate < ValuationDate → throws
        Action actAtValuation = () => _ = shockedCurve!.InstantaneousForwardRate(valuationDate);
        // Act — one day after valuation date: leftDate == ValuationDate → still OK after fix
        Action actOneDayAfter = () => _ = shockedCurve!.InstantaneousForwardRate(valuationDate.AddDays(1));

        // Assert — neither must throw after the front-boundary guard is added
        actAtValuation.Should().NotThrow("InstantaneousForwardRate at the valuation date must use a one-sided right difference");
        actOneDayAfter.Should().NotThrow("InstantaneousForwardRate one day after valuation date must also be guarded");
        shockedCurve!.InstantaneousForwardRate(valuationDate).Should().BeApproximately(0.055d, 1e-12);
        shockedCurve.InstantaneousForwardRate(valuationDate.AddDays(1)).Should().BeApproximately(0.055d, 1e-12);
    }

    /// <summary>
    /// AT-18 regression: verify the returned value is finite and positive for a flat
    /// shocked curve at the valuation date — a NaN or Infinity signals implementation error.
    /// </summary>
    [Fact]
    public void BucketedShifted_InstantaneousForwardRate_AtValuationDate_ReturnsFinitePositiveValue()
    {
        // Arrange
        var valuationDate = new DateOnly(2026, 6, 14);
        const double baseRate = 0.05d;
        FlatDiscountCurve baseFlat = new(new CurveName("USD-Disc"), valuationDate, Currency.USD, baseRate);
        BucketedZeroRateShock shock = new(
            "KRD-1Y",
            new[] { new BucketedShockPoint(1d, 50d) }); // +50 bp shock at 1Y

        var shockedCurve = shock.Apply(baseFlat) as IDiscountCurve;
        shockedCurve.Should().NotBeNull();

        // Act
        double fwd = shockedCurve!.InstantaneousForwardRate(valuationDate);

        // A flat 5% curve plus a 50bp zero shock is exactly 5.5% in continuous-forward units.
        fwd.Should().BeApproximately(0.055d, 1e-12);
    }
}
