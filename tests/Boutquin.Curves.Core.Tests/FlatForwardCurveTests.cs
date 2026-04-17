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

using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Core.Forwards;
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Core.Tests;

public sealed class FlatForwardCurveTests
{
    [Fact]
    public void ForwardRate_ShouldReturnConstantRate()
    {
        FlatForwardCurve curve = new(
            new CurveName("USD-FWD"),
            new DateOnly(2026, 4, 10),
            CurrencyCode.USD,
            new BenchmarkName("USD-LIBOR-3M"),
            0.0375d);

        double value = curve.ForwardRate(new DateOnly(2026, 7, 10), new DateOnly(2026, 10, 10));

        value.Should().BeApproximately(0.0375d, 1e-12d);
    }

    [Fact]
    public void ValueAt_ShouldDecreaseForPositiveRates()
    {
        DateOnly valuationDate = new(2026, 4, 10);
        FlatForwardCurve curve = new(
            new CurveName("USD-FWD"),
            valuationDate,
            CurrencyCode.USD,
            new BenchmarkName("USD-LIBOR-3M"),
            0.05d);

        double near = curve.ValueAt(valuationDate.AddMonths(3));
        double far = curve.ValueAt(valuationDate.AddYears(2));

        near.Should().BeGreaterThan(far);
    }

    [Fact]
    public void ForwardRate_ShouldThrow_WhenEndDateIsNotAfterStartDate()
    {
        FlatForwardCurve curve = new(
            new CurveName("USD-FWD"),
            new DateOnly(2026, 4, 10),
            CurrencyCode.USD,
            new BenchmarkName("USD-LIBOR-3M"),
            0.02d);

        Action act = () => curve.ForwardRate(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Forward end date must be after the start date.*");
    }
}
