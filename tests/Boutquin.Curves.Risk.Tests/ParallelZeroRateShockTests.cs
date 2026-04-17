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
using Boutquin.Curves.Core.Discounting;
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Risk.Tests;

public sealed class ParallelZeroRateShockTests
{
    [Fact]
    public void Apply_ShouldReturnDiscountCurve()
    {
        FlatDiscountCurve curve = new(new CurveName("USD-Disc"), new DateOnly(2026, 4, 9), CurrencyCode.USD, 0.04d);
        ParallelZeroRateShock shock = new("Up1bp", 1d);

        var shockedCurve = shock.Apply(curve);

        shockedCurve.Should().NotBeNull();
    }

    [Fact]
    public void Apply_ShouldThrow_WhenCurveIsNull()
    {
        ParallelZeroRateShock shock = new("Up1bp", 1d);

        Action act = () => shock.Apply(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("curve");
    }
}
