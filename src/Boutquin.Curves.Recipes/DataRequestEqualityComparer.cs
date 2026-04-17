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

using Boutquin.MarketData.Abstractions.Contracts;

namespace Boutquin.Curves.Recipes;

/// <summary>
/// Compares <see cref="IDataRequest"/> instances by their logical identity:
/// <see cref="IDataRequest.DatasetKey"/>, date range boundaries, and frequency.
/// </summary>
/// <remarks>
/// Used to deduplicate data requests across multiple curve nodes that may request
/// overlapping datasets. Two requests are considered equal when they target the same
/// dataset key, span the same date range, and use the same observation frequency,
/// regardless of their concrete runtime type.
/// </remarks>
public sealed class DataRequestEqualityComparer : IEqualityComparer<IDataRequest>
{
    /// <summary>
    /// Shared singleton instance.
    /// </summary>
    public static DataRequestEqualityComparer Instance { get; } = new();

    /// <inheritdoc />
    public bool Equals(IDataRequest? x, IDataRequest? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return string.Equals(x.DatasetKey, y.DatasetKey, StringComparison.Ordinal)
            && x.Range.From == y.Range.From
            && x.Range.To == y.Range.To
            && x.Frequency == y.Frequency;
    }

    /// <inheritdoc />
    public int GetHashCode(IDataRequest obj)
    {
        ArgumentNullException.ThrowIfNull(obj);

        return HashCode.Combine(
            obj.DatasetKey,
            obj.Range.From,
            obj.Range.To,
            obj.Frequency);
    }
}
