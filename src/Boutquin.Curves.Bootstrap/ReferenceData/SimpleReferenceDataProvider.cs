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

using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.MarketData.Abstractions.Calendars;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;

namespace Boutquin.Curves.Bootstrap.ReferenceData;

/// <summary>
/// Provides in-memory lookup of calendars, benchmarks, and conventions for calibration workflows.
/// </summary>
public sealed class SimpleReferenceDataProvider : IReferenceDataProvider
{
    private readonly Dictionary<string, IBusinessCalendar> _calendars;
    private readonly Dictionary<BenchmarkName, RateBenchmark> _benchmarks;
    private readonly Dictionary<string, IInstrumentConvention> _conventions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleReferenceDataProvider"/> type.
    /// </summary>
    /// <param name="calendars">Calendar definitions keyed by calendar code.</param>
    /// <param name="benchmarks">Benchmark metadata keyed by benchmark name.</param>
    /// <param name="conventions">Instrument conventions keyed by convention code.</param>
    public SimpleReferenceDataProvider(
        IEnumerable<IBusinessCalendar> calendars,
        IEnumerable<RateBenchmark> benchmarks,
        IEnumerable<IInstrumentConvention> conventions)
    {
        _calendars = calendars.ToDictionary(static calendar => calendar.Code, StringComparer.OrdinalIgnoreCase);
        _benchmarks = benchmarks.ToDictionary(static benchmark => benchmark.Name);
        _conventions = conventions.ToDictionary(static convention => convention.Code, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves a calendar by code.
    /// </summary>
    /// <param name="code">Calendar code to resolve.</param>
    /// <returns>The matching business calendar.</returns>
    public IBusinessCalendar GetCalendar(string code)
    {
        if (!_calendars.TryGetValue(code, out IBusinessCalendar? calendar))
        {
            throw new KeyNotFoundException($"Calendar '{code}' was not found.");
        }

        return calendar;
    }

    /// <summary>
    /// Resolves a benchmark by name.
    /// </summary>
    /// <param name="name">Benchmark name to resolve.</param>
    /// <returns>The matching benchmark definition.</returns>
    public RateBenchmark GetBenchmark(BenchmarkName name)
    {
        if (!_benchmarks.TryGetValue(name, out RateBenchmark? benchmark))
        {
            throw new KeyNotFoundException($"Benchmark '{name}' was not found.");
        }

        return benchmark;
    }

    /// <summary>
    /// Resolves an instrument convention by code.
    /// </summary>
    /// <param name="code">Convention code to resolve.</param>
    /// <returns>The matching instrument convention.</returns>
    public IInstrumentConvention GetConvention(string code)
    {
        if (!_conventions.TryGetValue(code, out IInstrumentConvention? convention))
        {
            throw new KeyNotFoundException($"Convention '{code}' was not found.");
        }

        return convention;
    }
}
