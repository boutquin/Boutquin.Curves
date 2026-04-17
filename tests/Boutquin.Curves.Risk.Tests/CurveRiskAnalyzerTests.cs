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
using Boutquin.Curves.Core;
using Boutquin.Curves.Core.Discounting;
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Risk.Tests;

public sealed class CurveRiskAnalyzerTests
{
    [Fact]
    public void BuildScenarioRiskReport_ShouldThrow_WhenCurveGroupIsNull()
    {
        CurveRiskAnalyzer analyzer = new();
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference reference = new(CurveRole.Discount, CurrencyCode.USD);

        Action act = () => analyzer.BuildScenarioRiskReport(
            null!,
            reference,
            new ParallelZeroRateShock("Up1bp", 1d),
            new[] { new RiskBucket("1Y", valuationDate.AddYears(1)) });

        act.Should().Throw<ArgumentNullException>().WithParameterName("curveGroup");
    }

    [Fact]
    public void BuildScenarioRiskReport_ShouldThrow_WhenScenarioIsNull()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference reference = new(CurveRole.Discount, CurrencyCode.USD);
        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), valuationDate, CurrencyCode.USD, 0.04d);
        CurveGroup curveGroup = new CurveGroupBuilder(new CurveGroupName("USD"), valuationDate)
            .Add(reference, curve)
            .Build();

        CurveRiskAnalyzer analyzer = new();

        Action act = () => analyzer.BuildScenarioRiskReport(
            curveGroup,
            reference,
            null!,
            new[] { new RiskBucket("1Y", valuationDate.AddYears(1)) });

        act.Should().Throw<ArgumentNullException>().WithParameterName("scenario");
    }

    [Fact]
    public void BuildScenarioRiskReport_ShouldThrow_WhenBucketsIsNull()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference reference = new(CurveRole.Discount, CurrencyCode.USD);
        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), valuationDate, CurrencyCode.USD, 0.04d);
        CurveGroup curveGroup = new CurveGroupBuilder(new CurveGroupName("USD"), valuationDate)
            .Add(reference, curve)
            .Build();

        CurveRiskAnalyzer analyzer = new();

        Action act = () => analyzer.BuildScenarioRiskReport(
            curveGroup,
            reference,
            new ParallelZeroRateShock("Up1bp", 1d),
            null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("buckets");
    }

    [Fact]
    public void BuildPlaceholderBucketedReport_ShouldThrow_WhenCurveGroupIsNull()
    {
        CurveRiskAnalyzer analyzer = new();
        CurveReference reference = new(CurveRole.Discount, CurrencyCode.USD);

        Action act = () => analyzer.BuildPlaceholderBucketedReport(null!, reference);

        act.Should().Throw<ArgumentNullException>().WithParameterName("curveGroup");
    }

    [Fact]
    public void BuildScenarioRiskReport_ShouldUseActualShockedValuationDeltas_ForParallelShock()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference reference = new(CurveRole.Discount, CurrencyCode.USD);
        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), valuationDate, CurrencyCode.USD, 0.04d);
        CurveGroup curveGroup = new CurveGroupBuilder(new CurveGroupName("USD"), valuationDate)
            .Add(reference, curve)
            .Build();

        CurveRiskAnalyzer analyzer = new();
        RiskReport report = analyzer.BuildScenarioRiskReport(
            curveGroup,
            reference,
            new ParallelZeroRateShock("Up10bp", 10d),
            new[]
            {
                new RiskBucket("1Y", valuationDate.AddYears(1)),
                new RiskBucket("2Y", valuationDate.AddYears(2)),
                new RiskBucket("5Y", valuationDate.AddYears(5))
            });

        report.Sensitivities.Should().HaveCount(3);
        report.Sensitivities.Select(s => s.Bucket).Should().Equal("1Y", "2Y", "5Y");
        report.Sensitivities.All(s => s.Delta < 0d).Should().BeTrue();

        // Ensure results are not placeholder fixed weights (0.1/0.2/0.7 style).
        report.Sensitivities[1].Delta.Should().NotBeApproximately(report.Sensitivities[0].Delta * 2d, 1e-12d);
        report.Sensitivities[2].Delta.Should().NotBeApproximately(report.Sensitivities[0].Delta * 7d, 1e-12d);
    }

    [Fact]
    public void BuildScenarioRiskReport_ShouldSupportBucketedShocksAcrossMaturities()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        CurveReference reference = new(CurveRole.Discount, CurrencyCode.USD);
        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), valuationDate, CurrencyCode.USD, 0.04d);
        CurveGroup curveGroup = new CurveGroupBuilder(new CurveGroupName("USD"), valuationDate)
            .Add(reference, curve)
            .Build();

        CurveRiskAnalyzer analyzer = new();
        RiskReport report = analyzer.BuildScenarioRiskReport(
            curveGroup,
            reference,
            new BucketedZeroRateShock(
                "Bucketed",
                new[]
                {
                    new BucketedShockPoint(1d, 5d),
                    new BucketedShockPoint(2d, 20d),
                    new BucketedShockPoint(5d, 50d)
                }),
            new[]
            {
                new RiskBucket("1Y", valuationDate.AddYears(1)),
                new RiskBucket("2Y", valuationDate.AddYears(2)),
                new RiskBucket("5Y", valuationDate.AddYears(5))
            });

        report.Sensitivities.Should().HaveCount(3);
        Math.Abs(report.Sensitivities[0].Delta).Should().BeLessThan(Math.Abs(report.Sensitivities[1].Delta));
        Math.Abs(report.Sensitivities[1].Delta).Should().BeLessThan(Math.Abs(report.Sensitivities[2].Delta));
    }
}
