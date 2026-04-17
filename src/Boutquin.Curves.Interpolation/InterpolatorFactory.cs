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

namespace Boutquin.Curves.Interpolation;

/// <summary>
/// Creates interpolator instances from interpolation settings metadata.
/// </summary>
/// <remarks>
/// Maps <see cref="InterpolatorKind"/> enum values to concrete <see cref="INodalCurveInterpolator"/>
/// instances. This factory is the single point where interpolation algorithm selection is resolved,
/// keeping <c>InterpolatedDiscountCurve</c> decoupled from specific interpolator implementations.
/// Curve definitions store algorithm identifiers while this factory resolves concrete implementations
/// at runtime, making model changes auditable and supporting controlled rollout of new methods.
/// </remarks>
public static class InterpolatorFactory
{
    /// <summary>
    /// Creates an interpolator implementation for the requested interpolation kind.
    /// </summary>
    /// <param name="kind">Interpolation algorithm identifier from curve settings.</param>
    /// <returns>The interpolator instance that implements <paramref name="kind"/>.</returns>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="kind"/> is not mapped to a runtime implementation.</exception>
    /// <remarks>
    /// Each enum value maps to a distinct implementation. Adding a new interpolation algorithm requires
    /// registering a new <see cref="InterpolatorKind"/> value and a corresponding case here.
    /// </remarks>
    public static INodalCurveInterpolator Create(InterpolatorKind kind)
    {
        return kind switch
        {
            InterpolatorKind.LogLinearDiscountFactor => new LogLinearDiscountFactorInterpolator(),
            InterpolatorKind.LinearZeroRate => new LinearZeroRateInterpolator(),
            InterpolatorKind.FlatForward => new FlatForwardInterpolator(),
            InterpolatorKind.MonotoneCubic => new MonotoneCubicInterpolator(),
            InterpolatorKind.MonotoneConvex => new MonotoneConvexInterpolator(),
            _ => throw new NotSupportedException($"Unsupported interpolator kind: {kind}.")
        };
    }
}
