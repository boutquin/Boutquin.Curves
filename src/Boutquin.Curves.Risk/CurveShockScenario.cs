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

namespace Boutquin.Curves.Risk;

/// <summary>
/// Defines a curve shock transformation applied to discount or forward curve values.
/// </summary>
/// <remarks>
/// Abstract base for curve transformation rules used in scenario analysis. Concrete implementations
/// define how a base curve is modified to produce a stressed curve (e.g., parallel shift, bucketed
/// bumps, twist). The <see cref="Apply"/> method returns a new curve without mutating the original,
/// ensuring base curves remain reusable across multiple scenarios within the same risk run.
/// </remarks>
public abstract record CurveShockScenario(string Name)
{
    /// <summary>
    /// Applies the shock scenario to a curve.
    /// </summary>
    /// <param name="curve">Curve instance to transform.</param>
    /// <returns>The shocked curve produced by this scenario.</returns>
    public abstract ICurve Apply(ICurve curve);
}
