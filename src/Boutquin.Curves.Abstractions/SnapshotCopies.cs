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

using System.Collections.ObjectModel;

namespace Boutquin.Curves.Abstractions;

internal static class SnapshotCopies
{
    internal static IReadOnlyList<T> List<T>(IReadOnlyList<T> values, string parameter)
    {
        ArgumentNullException.ThrowIfNull(values, parameter);
        return Array.AsReadOnly(values.ToArray());
    }

    internal static IReadOnlyDictionary<TKey, TValue> Dictionary<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> values, string parameter)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(values, parameter);
        var comparer = values is Dictionary<TKey, TValue> dictionary ? dictionary.Comparer : EqualityComparer<TKey>.Default;
        return new ReadOnlyDictionary<TKey, TValue>(new Dictionary<TKey, TValue>(values, comparer));
    }
}
