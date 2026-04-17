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

using BenchmarkDotNet.Attributes;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Core.Discounting;
using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Benchmarks;

/// <summary>
/// Measures discount-factor query performance on a representative interpolated curve.
/// </summary>
[MemoryDiagnoser]
public sealed class DiscountCurveQueryBenchmarks
{
    private readonly InterpolatedDiscountCurve _curve;
    private readonly DateOnly _targetDate;

    /// <summary>
    /// Initializes benchmark state with a synthetic USD discount curve and fixed query target.
    /// </summary>
    public DiscountCurveQueryBenchmarks()
    {
        DateOnly valuationDate = new(2026, 4, 9);
        _curve = new InterpolatedDiscountCurve(
            new CurveName("USD-Disc"),
            valuationDate,
            CurrencyCode.USD,
            Enumerable.Range(1, 40)
                .Select(index => new CurvePoint(valuationDate.AddMonths(index * 3), Math.Exp(-0.04d * (index * 0.25d))))
                .ToArray());

        _targetDate = valuationDate.AddYears(7);
    }

    /// <summary>
    /// Queries a discount factor at the configured target date.
    /// </summary>
    /// <returns>Discount factor value returned by the curve implementation.</returns>
    [Benchmark]
    public double QueryDiscountFactor() => _curve.DiscountFactor(_targetDate);
}
