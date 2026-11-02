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

using Boutquin.Domain.Enumerations;
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
    /// <exception cref="ArgumentNullException"><paramref name="group"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no discount curve exists for <paramref name="currency"/>.</exception>
    /// <exception cref="InvalidCastException">Thrown when the resolved curve does not implement <see cref="IDiscountCurve"/>.</exception>
    public static IDiscountCurve GetDiscountCurve(this ICurveGroup group, Currency currency)
    {
        ArgumentNullException.ThrowIfNull(group);
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
    /// <exception cref="ArgumentNullException"><paramref name="group"/> is <see langword="null"/>.</exception>
    public static bool TryGetDiscountCurve(this ICurveGroup group, Currency currency, out IDiscountCurve? curve)
    {
        ArgumentNullException.ThrowIfNull(group);
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
    /// <param name="benchmark">
    /// Benchmark identity for the forward curve (e.g., <c>SOFR</c>, <c>CORRA</c>).
    /// Unlike <see cref="GetDiscountCurve"/>, which only requires <paramref name="currency"/>
    /// because a group holds at most one discount curve per currency, forward curves are not
    /// unique per currency — a USD group may carry both a SOFR and an EFFR forward curve. The
    /// <paramref name="benchmark"/> parameter disambiguates between them.
    /// </param>
    /// <returns>
    /// The forward curve as <see cref="ICurve"/>. Cast to <see cref="IForwardCurve"/> when
    /// forward-rate calls are needed; the base interface is returned here to avoid an
    /// unconditional type check (not all forward curves implement <see cref="IForwardCurve"/>).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="group"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no forward curve exists for the benchmark.</exception>
    public static ICurve GetForwardCurve(this ICurveGroup group, Currency currency, BenchmarkName benchmark)
    {
        ArgumentNullException.ThrowIfNull(group);
        var reference = new CurveReference(CurveRole.Forward, currency, benchmark);
        return group.GetCurve(reference);
    }

    /// <summary>
    /// Attempts to resolve the forward curve for the specified benchmark without throwing.
    /// </summary>
    /// <param name="group">Curve group to query.</param>
    /// <param name="currency">Currency for forward curve lookup.</param>
    /// <param name="benchmark">
    /// Benchmark identity for the forward curve. See <see cref="GetForwardCurve"/> for the
    /// rationale for requiring this parameter in addition to <paramref name="currency"/>.
    /// </param>
    /// <param name="curve">Resolved forward curve when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a matching forward curve is found; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="group"/> is <see langword="null"/>.</exception>
    public static bool TryGetForwardCurve(this ICurveGroup group, Currency currency, BenchmarkName benchmark, out ICurve? curve)
    {
        ArgumentNullException.ThrowIfNull(group);
        var reference = new CurveReference(CurveRole.Forward, currency, benchmark);
        return group.TryGetCurve(reference, out curve);
    }
}
