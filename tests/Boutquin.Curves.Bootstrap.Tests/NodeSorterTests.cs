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
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

/// <summary>
/// B-3 (round-1 F-L4-5 C1): <see cref="NodeSorter.Sort"/> had no coverage for nodes that
/// resolve to the same pillar date. <c>Sort</c> delegates to LINQ <c>OrderBy</c>, which is a
/// stable sort — nodes with equal resolved pillar dates keep their original relative (input)
/// order. This test pins that documented-by-implementation behavior so a future swap to an
/// unstable sort (e.g. <c>Array.Sort</c> on the projected key) is caught by a failing test
/// instead of silently reordering same-date nodes.
/// </summary>
public sealed class NodeSorterTests
{
    private static readonly DateOnly s_valuationDate = new(2026, 4, 9);
    private static readonly CurveReference s_curveRef = new(CurveRole.Discount, Currency.USD);

    [Fact]
    public void Sort_DuplicatePillarDate_PreservesInputOrder()
    {
        // Two distinct tenor strings ("1M" and "30D") that both resolve to the same pillar date
        // relative to the valuation date used below is awkward to construct generically, so this
        // test instead uses two nodes with the IDENTICAL tenor string ("3M") — guaranteeing the
        // resolved pillar date collides exactly — distinguished only by label, to observe whether
        // Sort preserves their original relative order (stable sort) or not.
        ResolvedNode first = new("First-3M", new Tenor("3M"), "Ois", "USD-SOFR-OIS", s_curveRef, 0.05m);
        ResolvedNode second = new("Second-3M", new Tenor("3M"), "Ois", "USD-SOFR-OIS", s_curveRef, 0.04m);
        ResolvedNode[] nodes = [first, second];

        IReadOnlyList<ResolvedNode> sorted = NodeSorter.Sort(nodes, s_valuationDate);

        sorted.Should().HaveCount(2);
        sorted[0].Label.Should().Be("First-3M",
            because: "NodeSorter.Sort uses LINQ OrderBy, a stable sort — nodes with an identical " +
                "resolved pillar date must retain their original input order");
        sorted[1].Label.Should().Be("Second-3M");
    }

    [Fact]
    public void Sort_DuplicatePillarDate_ReverseInputOrder_AlsoPreserved()
    {
        // Same collision, but declared in the opposite order, to prove the result tracks INPUT
        // order rather than some other tie-break (e.g. label or rate) that would coincidentally
        // match the previous test's expected order.
        ResolvedNode first = new("Second-3M", new Tenor("3M"), "Ois", "USD-SOFR-OIS", s_curveRef, 0.04m);
        ResolvedNode second = new("First-3M", new Tenor("3M"), "Ois", "USD-SOFR-OIS", s_curveRef, 0.05m);
        ResolvedNode[] nodes = [first, second];

        IReadOnlyList<ResolvedNode> sorted = NodeSorter.Sort(nodes, s_valuationDate);

        sorted.Should().HaveCount(2);
        sorted[0].Label.Should().Be("Second-3M");
        sorted[1].Label.Should().Be("First-3M");
    }
}
