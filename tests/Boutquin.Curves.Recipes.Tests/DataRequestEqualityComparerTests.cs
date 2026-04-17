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
using Boutquin.MarketData.Abstractions.Requests;
using FluentAssertions;

namespace Boutquin.Curves.Recipes.Tests;

/// <summary>
/// Tests for <see cref="DataRequestEqualityComparer"/>.
/// </summary>
public sealed class DataRequestEqualityComparerTests
{
    private static readonly DateOnly s_baseDate = new(2026, 4, 9);
    private static readonly DataRequestEqualityComparer s_comparer = DataRequestEqualityComparer.Instance;

    [Fact]
    public void Same_Request_Is_Equal()
    {
        // Arrange
        var range = new DateRange(s_baseDate.AddDays(-5), s_baseDate);
        var request = new OvernightFixingRequest(new BenchmarkName("SOFR"), range);

        // Act & Assert
        s_comparer.Equals(request, request).Should().BeTrue();
        s_comparer.GetHashCode(request).Should().Be(s_comparer.GetHashCode(request));
    }

    [Fact]
    public void Equivalent_Requests_Are_Equal()
    {
        // Arrange
        var range = new DateRange(s_baseDate.AddDays(-5), s_baseDate);
        var request1 = new OvernightFixingRequest(new BenchmarkName("SOFR"), range);
        var request2 = new OvernightFixingRequest(new BenchmarkName("SOFR"), range);

        // Act & Assert
        s_comparer.Equals(request1, request2).Should().BeTrue();
        s_comparer.GetHashCode(request1).Should().Be(s_comparer.GetHashCode(request2));
    }

    [Fact]
    public void Different_Dataset_Is_Not_Equal()
    {
        // Arrange
        var range = new DateRange(s_baseDate.AddDays(-5), s_baseDate);
        var request1 = new OvernightFixingRequest(new BenchmarkName("SOFR"), range);
        var request2 = new OvernightFixingRequest(new BenchmarkName("SONIA"), range);

        // Act & Assert
        s_comparer.Equals(request1, request2).Should().BeFalse();
    }

    [Fact]
    public void Different_Range_Is_Not_Equal()
    {
        // Arrange
        var range1 = new DateRange(s_baseDate.AddDays(-5), s_baseDate);
        var range2 = new DateRange(s_baseDate.AddDays(-10), s_baseDate);
        var request1 = new OvernightFixingRequest(new BenchmarkName("SOFR"), range1);
        var request2 = new OvernightFixingRequest(new BenchmarkName("SOFR"), range2);

        // Act & Assert
        s_comparer.Equals(request1, request2).Should().BeFalse();
    }

    [Fact]
    public void Different_Frequency_Is_Not_Equal()
    {
        // Arrange
        var range = new DateRange(s_baseDate.AddDays(-5), s_baseDate);
        var request1 = new OvernightFixingRequest(new BenchmarkName("SOFR"), range, DataFrequency.Daily);
        var request2 = new OvernightFixingRequest(new BenchmarkName("SOFR"), range, DataFrequency.Weekly);

        // Act & Assert
        s_comparer.Equals(request1, request2).Should().BeFalse();
    }

    [Fact]
    public void Null_Requests_Are_Handled()
    {
        // Arrange
        var range = new DateRange(s_baseDate.AddDays(-5), s_baseDate);
        var request = new OvernightFixingRequest(new BenchmarkName("SOFR"), range);

        // Act & Assert
        s_comparer.Equals(null, null).Should().BeTrue();
        s_comparer.Equals(request, null).Should().BeFalse();
        s_comparer.Equals(null, request).Should().BeFalse();
    }
}
