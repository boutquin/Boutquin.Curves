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
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Interpolation.Tests;

public sealed class RcInterpolationContractTests
{
    [Theory]
    [InlineData(InterpolatorKind.LogLinearDiscountFactor)]
    [InlineData(InterpolatorKind.LinearZeroRate)]
    [InlineData(InterpolatorKind.FlatForward)]
    [InlineData(InterpolatorKind.MonotoneCubic)]
    [InlineData(InterpolatorKind.MonotoneConvex)]
    public void AllQueries_ValidatePillarsBeforeBoundaryShortcuts(InterpolatorKind kind)
    {
        DateOnly valuation = new(2026, 1, 2);
        INodalCurveInterpolator interpolator = InterpolatorFactory.Create(kind);
        CurvePoint[][] invalid =
        [
            [new(valuation.AddDays(365), 0.99), new(valuation.AddDays(180), 0.98)],
            [new(valuation.AddDays(365), double.NaN)],
            [new(valuation.AddDays(365), double.PositiveInfinity)],
            [new(valuation.AddDays(365), 0d)],
            [new(valuation.AddDays(365), -0.1)],
            [new(valuation.AddDays(365), 0.99), new(valuation.AddDays(365), 0.98)]
        ];
        foreach (CurvePoint[] points in invalid)
        {
            foreach (DateOnly target in new[] { valuation, valuation.AddDays(10), valuation.AddDays(900) })
            {
                Action act = () => interpolator.Interpolate(valuation, target, points, Actual365Fixed.Instance);
                act.Should().Throw<ArgumentException>($"{kind} must reject invalid pillars even at a boundary");
            }
        }
    }

    [Theory]
    [InlineData(InterpolatorKind.LogLinearDiscountFactor)]
    [InlineData(InterpolatorKind.FlatForward)]
    [InlineData(InterpolatorKind.MonotoneCubic)]
    [InlineData(InterpolatorKind.MonotoneConvex)]
    [InlineData(InterpolatorKind.LinearZeroRate)]
    public void ConstantForwardSurface_ReproducesIndependentExponentialOracleAcrossAllocationBoundary(InterpolatorKind kind)
    {
        DateOnly valuation = new(2026, 1, 2);
        foreach (int count in new[] { 2, 127, 128, 129 })
        {
            CurvePoint[] points = Enumerable.Range(1, count)
                .Select(i => new CurvePoint(valuation.AddDays(i * 30), Math.Exp(-0.04d * i * 30d / 365d))).ToArray();
            foreach (CurvePoint point in points)
            {
                InterpolatorFactory.Create(kind).Interpolate(valuation, point.Date, points, Actual365Fixed.Instance)
                    .Should().BeApproximately(point.Value, 1e-12, $"{kind} must reproduce every pillar");
            }

            DateOnly target = valuation.AddDays(45);
            double actual = InterpolatorFactory.Create(kind).Interpolate(valuation, target, points, Actual365Fixed.Instance);
            actual.Should().BeApproximately(Math.Exp(-0.04d * 45d / 365d), 1e-12,
                $"{kind} should reproduce a constant forward under both stack and heap paths");
        }
    }
}
