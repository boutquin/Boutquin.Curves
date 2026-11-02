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

internal static class BootstrapInputSnapshot
{
    internal static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(source, parameterName);
        T[] copy = source.ToArray();
        if (copy.Any(static item => item is null))
        {
            throw new ArgumentException("Snapshot collections cannot contain null elements.", parameterName);
        }

        return Array.AsReadOnly(copy);
    }

    internal static IReadOnlyList<T>? CopyOptional<T>(IReadOnlyList<T>? source) =>
        source is null ? null : Copy(source, nameof(source));
}
