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
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

public sealed class DiscountCurveCompatibilityTests
{
    [Fact]
    public void FlatDiscountCurve_ShouldReturnExpectedDiscountFactor()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        FlatDiscountCurve curve = new(
            new CurveName("USD-OIS"),
            valuationDate,
            CurrencyCode.USD,
            0.05d);

        double actual = curve.DiscountFactor(valuationDate.AddYears(1));

        actual.Should().BeApproximately(Math.Exp(-0.05d), 1e-12d);
    }

    [Fact]
    public void InterpolatedDiscountCurve_ShouldInterpolateBetweenNodes()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        InterpolatedDiscountCurve curve = new(
            new CurveName("USD-SWAP"),
            valuationDate,
            CurrencyCode.USD,
            [
                new CurvePoint(valuationDate.AddMonths(6), 0.99d),
                new CurvePoint(valuationDate.AddMonths(12), 0.97d)
            ]);

        double actual = curve.DiscountFactor(valuationDate.AddMonths(9));

        actual.Should().BeGreaterThan(0.97d).And.BeLessThan(0.99d);
    }
}
