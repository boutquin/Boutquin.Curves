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

namespace Boutquin.Curves.Abstractions.Fixings;

/// <summary>
/// In-memory implementation of <see cref="IFixingsStore"/> backed by nested dictionaries.
/// </summary>
/// <remarks>
/// Suitable for unit tests, fixtures, and single-process curve construction workflows. For
/// persistent storage across sessions, implement <see cref="IFixingsStore"/> against a database
/// or file-backed store.
///
/// Thread safety: this implementation is not thread-safe. Concurrent writes require external
/// synchronization.
/// </remarks>
public sealed class InMemoryFixingsStore : IFixingsStore
{
    private readonly Dictionary<string, SortedDictionary<DateOnly, decimal>> _data = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public decimal GetFixing(string fixingKey, DateOnly date)
    {
        if (!TryGetFixing(fixingKey, date, out decimal value))
        {
            throw new KeyNotFoundException($"No fixing found for '{fixingKey}' on {date:yyyy-MM-dd}.");
        }

        return value;
    }

    /// <inheritdoc/>
    public bool TryGetFixing(string fixingKey, DateOnly date, out decimal value)
    {
        if (_data.TryGetValue(fixingKey, out SortedDictionary<DateOnly, decimal>? series)
            && series.TryGetValue(date, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public void AddFixing(string fixingKey, DateOnly date, decimal value)
    {
        if (!_data.TryGetValue(fixingKey, out SortedDictionary<DateOnly, decimal>? series))
        {
            series = [];
            _data[fixingKey] = series;
        }

        series[date] = value;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, decimal> GetFixingsForDate(DateOnly date)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, SortedDictionary<DateOnly, decimal> series) in _data)
        {
            if (series.TryGetValue(date, out decimal value))
            {
                result[key] = value;
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<DateOnly, decimal> GetTimeSeries(string fixingKey)
    {
        if (_data.TryGetValue(fixingKey, out SortedDictionary<DateOnly, decimal>? series))
        {
            return series;
        }

        return new SortedDictionary<DateOnly, decimal>();
    }
}
