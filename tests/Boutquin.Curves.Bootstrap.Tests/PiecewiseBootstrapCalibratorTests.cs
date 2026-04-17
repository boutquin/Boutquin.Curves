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

using System.Reflection;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Diagnostics;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Core.Discounting;
using Boutquin.Curves.Indices;
using Boutquin.Curves.Interpolation;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

public sealed class PiecewiseBootstrapCalibratorTests
{
    [Fact]
    public void Calibrate_ShouldProduceCurveGroup()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference curveReference = new(CurveRole.Discount, CurrencyCode.USD);

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });

        CurveCalibrationInput input = new(
            valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveReference,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("1M", new Tenor("1M"), "Ois", "USD-SOFR-OIS", curveReference, 0.05m),
                        new ResolvedNode("1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", curveReference, 0.04m)
                    })
            },
            referenceData);

        PiecewiseBootstrapCalibrator calibrator = new();
        CurveCalibrationResult result = calibrator.Calibrate(input);

        result.CurveGroup.Name.Value.Should().Be("USD-Disc");
        result.Diagnostics.Repricing.Should().HaveCount(2);
    }

    [Fact]
    public void Calibrate_ShouldProduceExpandedDiagnostics_AndRepriceAllSupportedNodeTypes()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference curveReference = new(CurveRole.Discount, CurrencyCode.USD);

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[]
            {
                InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS"),
                InstrumentConventionRegistry.CreateDefault().GetRequired("USD-FRA"),
                InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SR3"),
                InstrumentConventionRegistry.CreateDefault().GetRequired("USD-FIXED-6M-30-360")
            });

        CurveCalibrationInput input = new(
            valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveReference,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("DEP-1M", new Tenor("1M"), "Deposit", "USD-SOFR-OIS", curveReference, 0.048m),
                        new ResolvedNode("OIS-3M", new Tenor("3M"), "Ois", "USD-SOFR-OIS", curveReference, 0.045m),
                        new ResolvedNode("FUT-6M", new Tenor("6M"), "OisFuture", "USD-SR3", curveReference, 0.043m),
                        new ResolvedNode("FRA-9M", new Tenor("9M"), "Fra", "USD-FRA", curveReference, 0.044m),
                        new ResolvedNode("SWAP-1Y", new Tenor("1Y"), "FixedFloatSwap", "USD-FIXED-6M-30-360", curveReference, 0.046m)
                    })
            },
            referenceData);

        PiecewiseBootstrapCalibrator calibrator = new();
        CurveCalibrationResult result = calibrator.Calibrate(input);

        result.Diagnostics.Structural.Should().ContainSingle(d => d.Code == "NODE_COUNT");
        result.Diagnostics.Structural.Should().ContainSingle(d => d.Code == "CURVE_COUNT");
        result.Diagnostics.Structural.Should().NotContain(d => d.Code == "SCAFFOLD");
        result.Diagnostics.Numerical.Should().ContainSingle(d => d.SolverName == "PiecewiseBootstrap");

        result.Diagnostics.Repricing.Should().HaveCount(5);
        result.Diagnostics.Repricing.Select(d => d.InstrumentType).Should().BeEquivalentTo(new[]
        {
            "Deposit",
            "Ois",
            "OisFuture",
            "Fra",
            "FixedFloatSwap"
        });

        result.Diagnostics.Repricing.All(d => d.AbsoluteError < 1e-10).Should().BeTrue();
    }

    [Fact]
    public void Calibrate_ShouldWireCurveInterpolationSettingsIntoBuiltCurve()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference curveReference = new(CurveRole.Discount, CurrencyCode.USD);
        InterpolationSettings expectedSettings = new(InterpolatorKind.LinearZeroRate, "FlatZero", "FlatForward");

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });

        CurveCalibrationInput input = new(
            valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveReference,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    expectedSettings,
                    new[]
                    {
                        new ResolvedNode("1M", new Tenor("1M"), "Ois", "USD-SOFR-OIS", curveReference, 0.05m),
                        new ResolvedNode("1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", curveReference, 0.04m)
                    })
            },
            referenceData);

        PiecewiseBootstrapCalibrator calibrator = new();
        CurveCalibrationResult result = calibrator.Calibrate(input);

        InterpolatedDiscountCurve builtCurve = result.CurveGroup.GetCurve(curveReference).Should().BeOfType<InterpolatedDiscountCurve>().Subject;
        builtCurve.Interpolation.Should().Be(expectedSettings);
    }

    [Fact]
    public void Calibrate_ShouldWrapDiscountCurveWithJumpAdjustments_WhenConfigured()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference curveReference = new(CurveRole.Discount, CurrencyCode.USD);

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });

        CurveCalibrationInput input = new(
            valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveReference,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("1M", new Tenor("1M"), "Ois", "USD-SOFR-OIS", curveReference, 0.05m),
                        new ResolvedNode("1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", curveReference, 0.04m)
                    },
                    new[] { new CurvePoint(valuationDate.AddMonths(18), 0.99d) })
            },
            referenceData);

        PiecewiseBootstrapCalibrator calibrator = new();
        CurveCalibrationResult result = calibrator.Calibrate(input);

        JumpAdjustedDiscountCurve jumpedCurve = result.CurveGroup.GetCurve(curveReference).Should().BeOfType<JumpAdjustedDiscountCurve>().Subject;
        jumpedCurve.DiscountFactor(valuationDate.AddYears(2)).Should().BeLessThan(1d);
    }

    [Fact]
    public void Calibrate_ShouldProduceCalibrationJacobian_WithDeterministicLabelsAndDimensions()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference curveReference = new(CurveRole.Discount, CurrencyCode.USD);

        SimpleReferenceDataProvider referenceData = new(
            new[] { new WeekendOnlyCalendar("USNY") },
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });

        CurveCalibrationInput input = new(
            valuationDate,
            new[]
            {
                new CurveCalibrationSpec(
                    new CurveName("USD-Disc"),
                    curveReference,
                    CurveValueType.DiscountFactor,
                    "ACT/360",
                    new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"),
                    new[]
                    {
                        new ResolvedNode("1M", new Tenor("1M"), "Ois", "USD-SOFR-OIS", curveReference, 0.05m),
                        new ResolvedNode("1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", curveReference, 0.04m)
                    })
            },
            referenceData);

        PiecewiseBootstrapCalibrator calibrator = new();
        CurveCalibrationResult result = calibrator.Calibrate(input);

        result.Jacobian.Should().NotBeNull();
        result.Jacobian!.RowLabels.Should().HaveCount(2);
        result.Jacobian.ColumnLabels.Should().HaveCount(2);
        result.Jacobian.RowLabels.Should().AllSatisfy(label => label.Should().NotBeNullOrWhiteSpace());
        result.Jacobian.ColumnLabels.Should().AllSatisfy(label => label.Should().NotBeNullOrWhiteSpace());
        result.Jacobian.Values.GetLength(0).Should().Be(2);
        result.Jacobian.Values.GetLength(1).Should().Be(2);

        for (int row = 0; row < result.Jacobian.Values.GetLength(0); row++)
        {
            for (int column = 0; column < result.Jacobian.Values.GetLength(1); column++)
            {
                double value = result.Jacobian.Values[row, column];
                double.IsFinite(value).Should().BeTrue();
            }
        }

        result.Diagnostics.Structural.Should().ContainSingle(d => d.Code == "JACOBIAN_DIMENSIONS");
        result.Diagnostics.Structural.Should().ContainSingle(d => d.Code == "JACOBIAN_FINITE");
        result.Diagnostics.Numerical.Should().ContainSingle(d => d.SolverName == "JacobianQuality");
        result.Diagnostics.Numerical.Single(d => d.SolverName == "JacobianQuality").Message.Should().Contain("Condition estimate");
    }

    [Fact]
    public void Calibrate_ShouldThrow_WhenJacobianContainsNonFiniteValues()
    {
        MethodInfo? qualityMethod = typeof(PiecewiseBootstrapCalibrator).GetMethod(
            "AppendJacobianQualityDiagnostics",
            BindingFlags.NonPublic | BindingFlags.Static);
        qualityMethod.Should().NotBeNull();

        CalibrationJacobian jacobian = new(
            new[] { "row-1" },
            new[] { "col-1" },
            new double[,]
            {
                { double.NaN }
            });

        Action act = () => qualityMethod!.Invoke(null, new object[] { BootstrapDiagnostics.Empty, jacobian });

        act.Should()
            .Throw<TargetInvocationException>()
            .Where(ex =>
                ex.InnerException is InvalidOperationException &&
                ex.InnerException.Message.Contains("non-finite", StringComparison.OrdinalIgnoreCase));
    }
}
