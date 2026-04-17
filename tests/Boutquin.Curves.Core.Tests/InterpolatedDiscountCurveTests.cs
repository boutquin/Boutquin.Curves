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
using Boutquin.Curves.Interpolation;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

public sealed class InterpolatedDiscountCurveTests
{
    [Fact]
    public void DiscountFactor_ShouldDecreaseAcrossCurve()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        InterpolatedDiscountCurve curve = new(
            new CurveName("USD-Disc"),
            valuationDate,
            CurrencyCode.USD,
            new[]
            {
                new CurvePoint(valuationDate.AddMonths(6), 0.98d),
                new CurvePoint(valuationDate.AddYears(2), 0.92d)
            });

        double sixMonths = curve.DiscountFactor(valuationDate.AddMonths(6));
        double twoYears = curve.DiscountFactor(valuationDate.AddYears(2));

        sixMonths.Should().BeGreaterThan(twoYears);
        curve.ZeroRate(valuationDate.AddYears(2), CompoundingConvention.Continuous).Should().BePositive();
    }

    [Fact]
    public void DiscountFactor_ShouldRespectLinearZeroRateInterpolationSelection()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        InterpolatedDiscountCurve curve = new(
            new CurveName("USD-Disc"),
            valuationDate,
            CurrencyCode.USD,
            new[]
            {
                new CurvePoint(valuationDate.AddYears(1), 0.98d),
                new CurvePoint(valuationDate.AddYears(2), 0.92d)
            },
            new InterpolationSettings(InterpolatorKind.LinearZeroRate, "FlatZero", "FlatForward"));

        double targetDf = curve.DiscountFactor(valuationDate.AddMonths(18));

        double t = curve.DayCount.YearFraction(valuationDate, valuationDate.AddMonths(18));
        double t0 = curve.DayCount.YearFraction(valuationDate, valuationDate.AddYears(1));
        double t1 = curve.DayCount.YearFraction(valuationDate, valuationDate.AddYears(2));
        double z0 = -Math.Log(0.98d) / t0;
        double z1 = -Math.Log(0.92d) / t1;
        double z = z0 + ((z1 - z0) * ((t - t0) / (t1 - t0)));
        double expected = Math.Exp(-z * t);

        targetDf.Should().BeApproximately(expected, 1e-12d);
    }

    [Fact]
    public void DiscountFactor_ShouldRespectFlatForwardRightExtrapolationSelection()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        InterpolatedDiscountCurve curve = new(
            new CurveName("USD-Disc"),
            valuationDate,
            CurrencyCode.USD,
            new[]
            {
                new CurvePoint(valuationDate.AddYears(1), 0.97d),
                new CurvePoint(valuationDate.AddYears(2), 0.91d)
            },
            new InterpolationSettings(InterpolatorKind.LogLinearDiscountFactor, "FlatZero", "FlatForward"));

        double targetDf = curve.DiscountFactor(valuationDate.AddYears(3));

        DateOnly oneYear = valuationDate.AddYears(1);
        DateOnly twoYears = valuationDate.AddYears(2);
        DateOnly threeYears = valuationDate.AddYears(3);
        double t1 = curve.DayCount.YearFraction(valuationDate, oneYear);
        double t2 = curve.DayCount.YearFraction(valuationDate, twoYears);
        double t3 = curve.DayCount.YearFraction(valuationDate, threeYears);
        double forward = -Math.Log(0.91d / 0.97d) / (t2 - t1);
        double expected = 0.91d * Math.Exp(-forward * (t3 - t2));

        targetDf.Should().BeApproximately(expected, 1e-12d);
    }
}
