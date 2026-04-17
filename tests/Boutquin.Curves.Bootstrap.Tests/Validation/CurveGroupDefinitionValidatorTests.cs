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
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Bootstrap.Validation;
using Boutquin.Curves.Indices;
using Boutquin.Curves.Interpolation;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests.Validation;

public sealed class CurveGroupDefinitionValidatorTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private const CurrencyCode Usd = CurrencyCode.USD;

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

    [Fact]
    public void Validate_UnknownExtrapolator_ReturnsWarning()
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

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Code == "UNKNOWN_LEFT_EXTRAPOLATOR");
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
