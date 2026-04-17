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

namespace Boutquin.Curves.Interpolation;

/// <summary>
/// Defines interpolation behavior for evaluating discount factors between nodal curve points.
/// </summary>
/// <remarks>
/// Bootstrappers solve a finite set of pillar values, but pricing engines need values at arbitrary dates.
/// Implementations of this interface provide that bridge. Interpolator choice is a modeling decision:
/// it affects forward-curve smoothness, hedge stability, and sensitivity distribution.
/// </remarks>
public interface INodalCurveInterpolator
{
    /// <summary>
    /// Stable interpolator name used in diagnostics and configuration.
    /// </summary>
    /// <remarks>
    /// Persist this name in snapshots and logs to guarantee reproducibility across calibration runs.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Interpolates a discount factor at the target date from ordered nodal points.
    /// </summary>
    /// <param name="valuationDate">Curve valuation date used as the interpolation anchor.</param>
    /// <param name="targetDate">Date for which a discount factor is requested.</param>
    /// <param name="points">Ordered curve nodes used as interpolation inputs.</param>
    /// <param name="dayCount">Day-count convention used when interpolation requires time fractions.</param>
    /// <returns>Interpolated discount factor for <paramref name="targetDate"/>.</returns>
    /// <remarks>
    /// Callers must supply nodes ordered by increasing date and strictly positive discount factors.
    /// If boundary behavior matters, combine this with explicit left/right extrapolation policy at
    /// the curve level rather than encoding extrapolation assumptions inside interpolation internals.
    /// </remarks>
    double Interpolate(DateOnly valuationDate, DateOnly targetDate, IReadOnlyList<CurvePoint> points, IYearFractionCalculator dayCount);
}
