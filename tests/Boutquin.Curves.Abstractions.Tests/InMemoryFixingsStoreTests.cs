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

using Boutquin.Curves.Abstractions.Fixings;
using FluentAssertions;

namespace Boutquin.Curves.Abstractions.Tests;

public sealed class InMemoryFixingsStoreTests
{
    private static readonly DateOnly s_date1 = new(2026, 4, 8);
    private static readonly DateOnly s_date2 = new(2026, 4, 9);

    [Fact]
    public void AddFixing_Then_GetFixing_Returns_Value()
    {
        var store = new InMemoryFixingsStore();
        store.AddFixing("USD-SOFR", s_date1, 0.0529m);

        decimal value = store.GetFixing("USD-SOFR", s_date1);
        value.Should().Be(0.0529m);
    }

    [Fact]
    public void GetFixing_Missing_Throws_KeyNotFoundException()
    {
        var store = new InMemoryFixingsStore();

        Action act = () => store.GetFixing("USD-SOFR", s_date1);

        act.Should().Throw<KeyNotFoundException>()
            .WithMessage("*USD-SOFR*2026-04-08*");
    }

    [Fact]
    public void TryGetFixing_Existing_Returns_True()
    {
        var store = new InMemoryFixingsStore();
        store.AddFixing("GBP-SONIA", s_date1, 0.0415m);

        bool found = store.TryGetFixing("GBP-SONIA", s_date1, out decimal value);

        found.Should().BeTrue();
        value.Should().Be(0.0415m);
    }

    [Fact]
    public void TryGetFixing_Missing_Returns_False()
    {
        var store = new InMemoryFixingsStore();

        bool found = store.TryGetFixing("GBP-SONIA", s_date1, out _);

        found.Should().BeFalse();
    }

    [Fact]
    public void AddFixing_SameKeyDate_Overwrites()
    {
        var store = new InMemoryFixingsStore();
        store.AddFixing("USD-SOFR", s_date1, 0.0529m);
        store.AddFixing("USD-SOFR", s_date1, 0.0530m);

        store.GetFixing("USD-SOFR", s_date1).Should().Be(0.0530m);
    }

    [Fact]
    public void GetFixingsForDate_Returns_All_Benchmarks_On_Date()
    {
        var store = new InMemoryFixingsStore();
        store.AddFixing("USD-SOFR", s_date1, 0.0529m);
        store.AddFixing("GBP-SONIA", s_date1, 0.0415m);
        store.AddFixing("EUR-ESTR", s_date1, 0.0348m);
        store.AddFixing("USD-SOFR", s_date2, 0.0530m);

        var fixings = store.GetFixingsForDate(s_date1);

        fixings.Should().HaveCount(3);
        fixings["USD-SOFR"].Should().Be(0.0529m);
        fixings["GBP-SONIA"].Should().Be(0.0415m);
        fixings["EUR-ESTR"].Should().Be(0.0348m);
    }

    [Fact]
    public void GetFixingsForDate_Empty_Returns_Empty()
    {
        var store = new InMemoryFixingsStore();

        var fixings = store.GetFixingsForDate(s_date1);

        fixings.Should().BeEmpty();
    }

    [Fact]
    public void GetTimeSeries_Returns_Sorted_History()
    {
        var store = new InMemoryFixingsStore();
        store.AddFixing("USD-SOFR", s_date2, 0.0530m);
        store.AddFixing("USD-SOFR", s_date1, 0.0529m);

        var series = store.GetTimeSeries("USD-SOFR");

        series.Should().HaveCount(2);
        series.Keys.Should().BeInAscendingOrder();
        series[s_date1].Should().Be(0.0529m);
        series[s_date2].Should().Be(0.0530m);
    }

    [Fact]
    public void GetTimeSeries_UnknownKey_Returns_Empty()
    {
        var store = new InMemoryFixingsStore();

        var series = store.GetTimeSeries("UNKNOWN");

        series.Should().BeEmpty();
    }

    [Fact]
    public void CaseInsensitive_Key_Lookup()
    {
        var store = new InMemoryFixingsStore();
        store.AddFixing("USD-SOFR", s_date1, 0.0529m);

        bool found = store.TryGetFixing("usd-sofr", s_date1, out decimal value);

        found.Should().BeTrue();
        value.Should().Be(0.0529m);
    }
}
