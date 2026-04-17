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

using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Abstractions.Curves;

/// <summary>
/// Convenience methods for resolving discount and forward curves from a curve group
/// without manually constructing <see cref="CurveReference"/> instances.
/// </summary>
public static class CurveGroupExtensions
{
    /// <summary>
    /// Returns the discount curve for the specified currency.
    /// </summary>
    /// <param name="group">Curve group to query.</param>
    /// <param name="currency">Currency whose discount curve is requested.</param>
    /// <returns>The discount curve cast to <see cref="IDiscountCurve"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no discount curve exists for <paramref name="currency"/>.</exception>
    /// <exception cref="InvalidCastException">Thrown when the resolved curve does not implement <see cref="IDiscountCurve"/>.</exception>
    public static IDiscountCurve GetDiscountCurve(this ICurveGroup group, CurrencyCode currency)
    {
        var reference = new CurveReference(CurveRole.Discount, currency);
        var curve = group.GetCurve(reference);
        return curve as IDiscountCurve
            ?? throw new InvalidCastException($"Curve '{reference}' in group '{group.Name}' does not implement IDiscountCurve.");
    }

    /// <summary>
    /// Attempts to resolve the discount curve for the specified currency without throwing.
    /// </summary>
    /// <param name="group">Curve group to query.</param>
    /// <param name="currency">Currency whose discount curve is requested.</param>
    /// <param name="curve">Resolved discount curve when found and castable; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a matching discount curve is found; otherwise <see langword="false"/>.</returns>
    public static bool TryGetDiscountCurve(this ICurveGroup group, CurrencyCode currency, out IDiscountCurve? curve)
    {
        curve = null;
        var reference = new CurveReference(CurveRole.Discount, currency);
        if (!group.TryGetCurve(reference, out var raw))
        {
            return false;
        }

        if (raw is not IDiscountCurve discount)
        {
            return false;
        }

        curve = discount;
        return true;
    }

    /// <summary>
    /// Returns the forward curve for the specified benchmark.
    /// </summary>
    /// <param name="group">Curve group to query.</param>
    /// <param name="currency">Currency for forward curve lookup.</param>
    /// <param name="benchmark">Benchmark identity for the forward curve.</param>
    /// <returns>The forward curve matching the benchmark.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no forward curve exists for the benchmark.</exception>
    public static ICurve GetForwardCurve(this ICurveGroup group, CurrencyCode currency, BenchmarkName benchmark)
    {
        var reference = new CurveReference(CurveRole.Forward, currency, benchmark);
        return group.GetCurve(reference);
    }

    /// <summary>
    /// Attempts to resolve the forward curve for the specified benchmark without throwing.
    /// </summary>
    /// <param name="group">Curve group to query.</param>
    /// <param name="currency">Currency for forward curve lookup.</param>
    /// <param name="benchmark">Benchmark identity for the forward curve.</param>
    /// <param name="curve">Resolved forward curve when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a matching forward curve is found; otherwise <see langword="false"/>.</returns>
    public static bool TryGetForwardCurve(this ICurveGroup group, CurrencyCode currency, BenchmarkName benchmark, out ICurve? curve)
    {
        var reference = new CurveReference(CurveRole.Forward, currency, benchmark);
        return group.TryGetCurve(reference, out curve);
    }
}
