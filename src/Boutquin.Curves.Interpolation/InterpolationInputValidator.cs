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

internal static class InterpolationInputValidator
{
    internal static void Validate(IReadOnlyList<CurvePoint> points, IYearFractionCalculator dayCount)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(dayCount);
        if (points.Count == 0)
        {
            throw new ArgumentException("At least one node is required to interpolate a discount factor.", nameof(points));
        }

        for (int i = 0; i < points.Count; i++)
        {
            CurvePoint point = points[i];
            if (point is null || !double.IsFinite(point.Value) || point.Value <= 0d)
            {
                throw new ArgumentException("Node discount factors must be finite and strictly positive.", nameof(points));
            }

            if (i > 0 && point.Date <= points[i - 1].Date)
            {
                throw new ArgumentException($"Node dates must be strictly ascending; node {i} is not after its predecessor.", nameof(points));
            }
        }
    }
}
