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

    /// <summary>
    /// AT-11: Two distinct concrete request types that share the same DatasetKey, date range,
    /// and frequency must NOT be considered equal. Without a GetType() check, the comparer would
    /// collapse them into the same fetch group, causing one request type's records to be dispatched
    /// for the wrong request type.
    ///
    /// Concretely: OvernightFixingRequest("SR3") and FuturesSettlementRequest("SR3") share
    /// DatasetKey = "SR3". If the comparer lacks a type check, they hash and equal the same —
    /// a FuturesSettlementRequest fetch group would absorb an OvernightFixingRequest node,
    /// returning FuturesSettlement records to ExtractRate logic that expects ScalarObservation.
    /// </summary>
    [Fact]
    public void Different_RuntimeTypes_With_Same_Key_Are_Not_Equal()
    {
        // Arrange: same DatasetKey ("SR3"), same range, same (default) frequency,
        // but OvernightFixingRequest vs FuturesSettlementRequest.
        var range = new DateRange(s_baseDate.AddDays(-5), s_baseDate);

        var fixing = new OvernightFixingRequest(new BenchmarkName("SR3"), range);
        var futures = new FuturesSettlementRequest(new FuturesProductCode("SR3"), range);

        // Precondition: same DatasetKey — this is exactly the collision scenario.
        fixing.DatasetKey.Should().Be(futures.DatasetKey,
            because: "the test is only meaningful when both requests share the DatasetKey");

        // Act & Assert — same key, different types → must NOT be equal
        s_comparer.Equals(fixing, futures).Should().BeFalse(
            because: "different runtime types with the same DatasetKey must not collapse into one fetch group");
    }

    /// <summary>
    /// AT-11 complementary: two OvernightFixingRequests with the same key, range, and frequency
    /// must remain equal (regression guard — the type check must not break same-type deduplication).
    /// </summary>
    [Fact]
    public void Same_RuntimeType_With_Same_Key_Are_Equal()
    {
        var range = new DateRange(s_baseDate.AddDays(-5), s_baseDate);
        var r1 = new OvernightFixingRequest(new BenchmarkName("SOFR"), range);
        var r2 = new OvernightFixingRequest(new BenchmarkName("SOFR"), range);

        s_comparer.Equals(r1, r2).Should().BeTrue(
            because: "two OvernightFixingRequests with identical fields must still be equal after the type-check is added");
        s_comparer.GetHashCode(r1).Should().Be(s_comparer.GetHashCode(r2));
    }
}
