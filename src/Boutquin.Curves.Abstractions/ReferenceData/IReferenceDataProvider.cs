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

using Boutquin.MarketData.Abstractions.Calendars;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;

namespace Boutquin.Curves.Abstractions.ReferenceData;

/// <summary>
/// Defines access to reference-data objects required by calibration and pricing workflows.
/// </summary>
public interface IReferenceDataProvider
{
    /// <summary>
    /// Resolves a business calendar by code.
    /// </summary>
    /// <param name="code">Calendar code.</param>
    /// <returns>Calendar matching <paramref name="code"/>.</returns>
    IBusinessCalendar GetCalendar(string code);

    /// <summary>
    /// Resolves benchmark metadata by benchmark name.
    /// </summary>
    /// <param name="name">Benchmark identifier.</param>
    /// <returns>Benchmark metadata for <paramref name="name"/>.</returns>
    RateBenchmark GetBenchmark(BenchmarkName name);

    /// <summary>
    /// Resolves instrument convention metadata by convention code.
    /// </summary>
    /// <param name="code">Convention code.</param>
    /// <returns>Convention matching <paramref name="code"/>.</returns>
    IInstrumentConvention GetConvention(string code);
}
