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

using Boutquin.Curves.Abstractions;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Bootstrap.Validation;
using Boutquin.Curves.Interpolation;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests.Validation;

public sealed class CurveGroupDefinitionValidatorTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private const Currency Usd = Currency.USD;

    [Fact]
    public void Validate_ValidInput_ReturnsValid()
    {
        CurveCalibrationInput input = CreateValidInput();

        ValidationResult result = CurveCalibrationInputValidator.Validate(input);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DuplicateCurveReference_ReturnsError()
    {
        CurveReference duplicateRef = new(CurveRole.Discount, Usd);
        CurveCalibrationInput input = new(
            s_valuationDate,
            new[]
            {
                CreateCurveSpec("Curve1", duplicateRef, new[] { ("N1", "1M") }),
                CreateCurveSpec("Curve2", duplicateRef, new[] { ("N2", "3M") }),
            },
            CreateDefaultRefData());

        ValidationResult result = CurveCalibrationInputValidator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "DUPLICATE_CURVE_REF");
    }

    [Fact]
    public void Validate_DuplicateNodeLabel_ReturnsError()
    {
        CurveReference curveRef = new(CurveRole.Discount, Usd);
        CurveCalibrationInput input = new(
            s_valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveRef,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("DUP", new Tenor("1M"), "Ois", "USD-SOFR-OIS", curveRef, 0.05m),
                        new ResolvedNode("DUP", new Tenor("3M"), "Ois", "USD-SOFR-OIS", curveRef, 0.04m),
                    })
            },
            CreateDefaultRefData());

        ValidationResult result = CurveCalibrationInputValidator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "DUPLICATE_NODE_LABEL");
    }

    [Fact]
    public void Validate_UnresolvableConvention_ReturnsError()
    {
        CurveReference curveRef = new(CurveRole.Discount, Usd);
        CurveCalibrationInput input = new(
            s_valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveRef,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("1M", new Tenor("1M"), "Ois", "NONEXISTENT-CONV", curveRef, 0.05m),
                    })
            },
            CreateDefaultRefData());

        ValidationResult result = CurveCalibrationInputValidator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "UNRESOLVABLE_CONVENTION");
    }

    // An unrecognized extrapolator mode is a BLOCKING error (the runtime InterpolatedDiscountCurve
    // ctor rejects anything outside {FlatZero, FlatForward}); this previously asserted a non-blocking
    // warning with IsValid==true, which enshrined the defective contract — the validator signed off on
    // an input the calibrator would then fail with a raw ArgumentException. Corrected to assert the
    // categorized blocking error.
    [Fact]
    public void Validate_UnknownExtrapolator_ReturnsError()
    {
        CurveReference curveRef = new(CurveRole.Discount, Usd);
        CurveCalibrationInput input = new(
            s_valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveRef,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "BadExtrap", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("1M", new Tenor("1M"), "Ois", "USD-SOFR-OIS", curveRef, 0.05m),
                    })
            },
            CreateDefaultRefData());

        ValidationResult result = CurveCalibrationInputValidator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "UNKNOWN_LEFT_EXTRAPOLATOR");
    }

    // B-1 (round-2 V-C2-1): the round-1 fix flipped BOTH UNKNOWN_LEFT_EXTRAPOLATOR and
    // UNKNOWN_RIGHT_EXTRAPOLATOR to blocking errors, but the existing regression coverage
    // (Validate_UnknownExtrapolator_ReturnsError, Validate_RuntimeRejectedExtrapolator_IsFlagged)
    // pins only the LEFT path. A regression reverting UNKNOWN_RIGHT_EXTRAPOLATOR back to
    // warnings.Add would leave IsValid==true and go undetected by every existing test in this
    // file. This test pins the RIGHT path the same way the left path is already pinned.
    [Fact]
    public void Validate_UnknownRightExtrapolator_ReturnsError()
    {
        CurveReference curveRef = new(CurveRole.Discount, Usd);
        CurveCalibrationInput input = new(
            s_valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveRef,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "BadExtrap"),
                    new[]
                    {
                        new ResolvedNode("1M", new Tenor("1M"), "Ois", "USD-SOFR-OIS", curveRef, 0.05m),
                    })
            },
            CreateDefaultRefData());

        ValidationResult result = CurveCalibrationInputValidator.Validate(input);

        // The runtime InterpolatedDiscountCurve constructor only accepts FlatZero / FlatForward for
        // the right extrapolator just as it does for the left, so an unrecognized right-extrapolator
        // mode must be a BLOCKING error (IsValid == false), not a non-blocking warning.
        result.IsValid.Should().BeFalse(
            because: "an unrecognized right extrapolator is rejected at curve construction, so the pre-flight validator must block it");
        result.Errors.Should().ContainSingle(e => e.Code == "UNKNOWN_RIGHT_EXTRAPOLATOR",
            because: "an unrecognized right extrapolator must be flagged as an error, not a warning");
        result.Warnings.Should().NotContain(e => e.Code == "UNKNOWN_RIGHT_EXTRAPOLATOR",
            because: "the right-extrapolator severity must be error, not warning");
    }

    /// <summary>
    /// Guard DEEP-API-07: Validate(null) must throw ArgumentNullException, not
    /// NullReferenceException from the input.Curves dereference in ValidateNonEmptyCurves.
    /// Validate is a shipped public entry point on the same surface as
    /// PiecewiseBootstrapCalibrator.Calibrate (which guards request); the two siblings must agree.
    /// </summary>
    [Fact]
    public void Validate_NullInput_ThrowsArgumentNullException()
    {
        Action act = () => CurveCalibrationInputValidator.Validate(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("input");
    }

    [Fact]
    public void Validate_EmptyCurves_ReturnsError()
    {
        CurveCalibrationInput input = new(
            s_valuationDate,
            Array.Empty<CurveCalibrationSpec>(),
            CreateDefaultRefData());

        ValidationResult result = CurveCalibrationInputValidator.Validate(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == "EMPTY_CURVES");
    }

    [Fact]
    public void Calibrator_EmptyCurves_ThrowsCategorizedValidationException()
    {
        CurveCalibrationInput input = new(
            s_valuationDate,
            Array.Empty<CurveCalibrationSpec>(),
            CreateDefaultRefData());

        PiecewiseBootstrapCalibrator calibrator = new();
        Action act = () => calibrator.Calibrate(input);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*validation failed*")
            .WithMessage("*EMPTY_CURVES*");
    }

    [Fact]
    public void Calibrator_ShouldRejectInvalidInput()
    {
        CurveReference curveRef = new(CurveRole.Discount, Usd);
        CurveCalibrationInput input = new(
            s_valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveRef,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("1M", new Tenor("1M"), "Ois", "NONEXISTENT-CONV", curveRef, 0.05m),
                    })
            },
            CreateDefaultRefData());

        PiecewiseBootstrapCalibrator calibrator = new();
        Action act = () => calibrator.Calibrate(input);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*validation failed*");
    }

    [Fact]
    public void Calibrator_ShouldSkipValidation_WhenRequested()
    {
        CurveCalibrationInput input = CreateValidInput() with { SkipValidation = true };

        PiecewiseBootstrapCalibrator calibrator = new();
        CurveCalibrationResult result = calibrator.Calibrate(input);

        result.CurveGroup.Should().NotBeNull();
    }

    /// <summary>
    /// Guard DEEP-ERR-04: SkipValidation=true with an empty Curves list must throw
    /// InvalidOperationException("[EMPTY_CURVES]…"), not ArgumentOutOfRangeException
    /// from the unconditional Curves[0] access at line 98 of CalibrateInternal.
    /// </summary>
    [Fact]
    public void Calibrator_SkipValidation_EmptyCurves_ShouldThrowInvalidOperationException()
    {
        CurveCalibrationInput input = new(
            s_valuationDate,
            Array.Empty<CurveCalibrationSpec>(),
            CreateDefaultRefData(),
            SkipValidation: true);

        PiecewiseBootstrapCalibrator calibrator = new();
        Action act = () => calibrator.Calibrate(input);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*[EMPTY_CURVES]*");
    }

    /// <summary>
    /// Guard DEEP-API-07: Calibrate(null) must throw ArgumentNullException, not
    /// NullReferenceException from the deferred request.SkipValidation dereference at CalibrateInternal:88.
    /// Both sibling entry points (OisDiscountCurveBootstrapper.Bootstrap, SwapProjectionCurveBootstrapper.Bootstrap)
    /// carry ArgumentNullException.ThrowIfNull at their top; Calibrate must be consistent.
    /// </summary>
    [Fact]
    public void Calibrate_NullInput_ThrowsArgumentNullException()
    {
        PiecewiseBootstrapCalibrator calibrator = new();
        Action act = () => calibrator.Calibrate(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("request");
    }

    /// <summary>
    /// Guard DEEP-ERR-04: a CurveCalibrationSpec with zero nodes must produce a structured
    /// InvalidOperationException("[EMPTY_NODES]…") from the calibrator, not an ArgumentException
    /// leaking from InterpolatedDiscountCurve's ctor ("At least one curve point is required.").
    /// The validator must detect the empty-nodes spec and report EMPTY_NODES before the solve loop
    /// attempts to call BuildCurve with an empty points list.
    /// Fires whether SkipValidation is false (validator path) or true (must also be caught).
    /// </summary>
    [Fact]
    public void Calibrate_ZeroNodeSpec_ThrowsInvalidOperationWithEmptyNodesCode()
    {
        CurveReference curveRef = new(CurveRole.Discount, Usd);
        // A curve spec with no resolved nodes at all.
        CurveCalibrationInput input = new(
            s_valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveRef,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    Array.Empty<ResolvedNode>())
            },
            CreateDefaultRefData());

        PiecewiseBootstrapCalibrator calibrator = new();
        Action act = () => calibrator.Calibrate(input);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*validation failed*")
            .WithMessage("*EMPTY_NODES*");
    }

    // AT-19: the validator's accepted-extrapolator set must match the set
    // InterpolatedDiscountCurve actually accepts at construction ({FlatZero, FlatForward}).
    // "Linear" and "None" were previously waved through by the validator yet rejected by the
    // runtime curve constructor with ArgumentException — the pre-flight validator must catch them.
    // The flag must be a BLOCKING error (IsValid == false), not a non-blocking warning: a warning
    // leaves IsValid true and the calibrator proceeds to throw a raw ArgumentException in curve
    // construction, defeating the pre-flight contract. This previously asserted only a warning.
    [Theory]
    [InlineData("Linear")]
    [InlineData("None")]
    public void Validate_RuntimeRejectedExtrapolator_IsFlagged(string extrapolator)
    {
        CurveReference curveRef = new(CurveRole.Discount, Usd);
        CurveCalibrationInput input = new(
            s_valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveRef,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, extrapolator, "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("1M", new Tenor("1M"), "Ois", "USD-SOFR-OIS", curveRef, 0.05m),
                    })
            },
            CreateDefaultRefData());

        ValidationResult result = CurveCalibrationInputValidator.Validate(input);

        // The runtime InterpolatedDiscountCurve constructor only accepts FlatZero / FlatForward, so a
        // validator that promises "extrapolator mode names are valid" must not silently accept these —
        // and must reject them as a blocking error so IsValid is false and the calibrator never proceeds.
        result.IsValid.Should().BeFalse(
            because: "{0} is rejected at curve construction, so the pre-flight validator must block it", extrapolator);
        result.Errors.Should().Contain(e => e.Code == "UNKNOWN_LEFT_EXTRAPOLATOR",
            because: "{0} is rejected at curve construction, so the pre-flight validator must flag it as an error", extrapolator);
    }

    private static CurveCalibrationInput CreateValidInput()
    {
        CurveReference curveRef = new(CurveRole.Discount, Usd);
        return new CurveCalibrationInput(
            s_valuationDate,
            new[]
            {
                CreateCurveSpec("USD-Disc", curveRef, new[] { ("1M", "1M") })
            },
            CreateDefaultRefData());
    }

    private static CurveCalibrationSpec CreateCurveSpec(string name, CurveReference curveRef, (string Label, string Tenor)[] nodes)
    {
        return new CurveCalibrationSpec(
            new CurveName(name),
            curveRef,
            CurveValueType.DiscountFactor,
            "ACT/360",
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
            nodes.Select(n => new ResolvedNode(n.Label, new Tenor(n.Tenor), "Ois", "USD-SOFR-OIS", curveRef, 0.05m)).ToArray());
    }

    private static SimpleReferenceDataProvider CreateDefaultRefData()
    {
        return new SimpleReferenceDataProvider(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });
    }
}
