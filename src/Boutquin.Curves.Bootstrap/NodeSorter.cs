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

namespace Boutquin.Curves.Bootstrap;

/// <summary>
/// Orders resolved nodes by pillar date to produce deterministic bootstrap sequencing.
/// </summary>
/// <remarks>
/// Sorts bootstrap nodes by their resolved pillar date (tenor applied to the valuation date)
/// to ensure deterministic sequential calibration. Correct ordering is essential because later
/// nodes may depend on discount factors solved from earlier nodes -- reordering the same set
/// of nodes can produce materially different calibrated curves and risk outputs.
/// </remarks>
public static class NodeSorter
{
    /// <summary>
    /// Sorts resolved nodes by pillar date to guarantee deterministic calibration order.
    /// </summary>
    /// <param name="nodes">Nodes to order.</param>
    /// <param name="valuationDate">Curve valuation date used to resolve each node's tenor to a pillar date.</param>
    /// <returns>Nodes ordered by increasing resolved pillar date.</returns>
    /// <remarks>
    /// Pillar date is computed by applying each node's tenor to the valuation date using
    /// <see cref="TenorParser.AddTenor"/>. Ordering by resolved date (not input declaration
    /// order) avoids hidden sequencing bugs.
    /// </remarks>
    public static IReadOnlyList<ResolvedNode> Sort(
        IEnumerable<ResolvedNode> nodes,
        DateOnly valuationDate)
    {
        return nodes.OrderBy(node => TenorParser.AddTenor(valuationDate, node.Tenor)).ToArray();
    }
}
