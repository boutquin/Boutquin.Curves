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
using Boutquin.Curves.Conventions.Calendars;
using Boutquin.MarketData.Abstractions.Calendars;
using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.Records;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Abstractions.Requests;
using Boutquin.MarketData.Conventions;

namespace Boutquin.Curves.Recipes.Nodes;

/// <summary>
/// Curve node that fetches an overnight benchmark fixing (e.g., SOFR, SONIA, CORRA) and
/// extracts the most recent observation on or before the valuation date.
/// </summary>
/// <remarks>
/// <para>
/// The lookback window guards against weekends and holidays: the request spans
/// the configured number of days ending on the valuation date so that
/// at least one business-day observation is likely to be present. The extraction logic
/// selects the latest observation whose date is at or before the valuation date.
/// </para>
/// <para>
/// When a <see cref="IBusinessCalendar"/> is provided, the lookback counts business days
/// instead of calendar days, ensuring that long weekends and holidays do not cause
/// the window to miss the most recent fixing.
/// </para>
/// <para>
/// When an <see cref="ObservationShiftConvention"/> other than <see cref="ObservationShiftConvention.None"/>
/// is specified together with a calendar, the target observation date is adjusted according
/// to the convention before filtering records.
/// </para>
/// </remarks>
public sealed class OvernightFixingNode : ICurveNodeSpec
{
    private readonly string _benchmarkId;
    private readonly int _lookbackDays;
    private readonly IBusinessCalendar? _calendar;
    private readonly ObservationShiftConvention _shiftConvention;
    private readonly int _shiftDays;

    /// <summary>
    /// Creates a node specification for an overnight benchmark fixing.
    /// </summary>
    /// <param name="label">Human-readable label for this node.</param>
    /// <param name="benchmarkId">Benchmark identifier dispatched to the data pipeline (e.g., "SOFR").</param>
    /// <param name="conventionCode">Convention code for day-count and payment rules.</param>
    /// <param name="targetCurve">The curve this node calibrates into.</param>
    /// <param name="lookbackDays">Days to look back from the valuation date when fetching fixings. Counted as business days when a calendar is provided, otherwise calendar days.</param>
    /// <param name="calendar">Optional business calendar for business-day-aware lookback and observation shifting.</param>
    /// <param name="shiftConvention">Observation shift convention applied when extracting rates. Requires a calendar to take effect.</param>
    /// <param name="shiftDays">Number of business days the observation date is shifted back under the <see cref="ObservationShiftConvention.Shifted"/> and <see cref="ObservationShiftConvention.Lookback"/> conventions. Requires a calendar to take effect.</param>
    public OvernightFixingNode(
        string label,
        string benchmarkId,
        string conventionCode,
        CurveReference targetCurve,
        int lookbackDays = 5,
        IBusinessCalendar? calendar = null,
        ObservationShiftConvention shiftConvention = ObservationShiftConvention.None,
        int shiftDays = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(benchmarkId);
        ArgumentException.ThrowIfNullOrWhiteSpace(conventionCode);
        ArgumentNullException.ThrowIfNull(targetCurve);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lookbackDays);
        ArgumentOutOfRangeException.ThrowIfNegative(shiftDays);

        Label = label;
        _benchmarkId = benchmarkId;
        ConventionCode = conventionCode;
        TargetCurve = targetCurve;
        _lookbackDays = lookbackDays;
        _calendar = calendar;
        _shiftConvention = shiftConvention;
        _shiftDays = shiftDays;
    }

    /// <summary>Human-readable label identifying this fixing anchor.</summary>
    public string Label { get; }

    /// <summary>Overnight calibration tenor.</summary>
    public Tenor Tenor { get; } = new("ON");

    /// <summary>Deposit helper classification for the overnight anchor.</summary>
    public string InstrumentType => "Deposit";

    /// <summary>Instrument convention used to interpret the fixing accrual.</summary>
    public string ConventionCode { get; }

    /// <summary>Curve calibrated by this overnight anchor.</summary>
    public CurveReference TargetCurve { get; }

    /// <summary>Requests the benchmark over the configured lookback ending at the valuation date.</summary>
    /// <param name="valuationDate">As-of date and inclusive request end.</param>
    /// <returns>An overnight-fixing request; the lookback uses business days when a calendar is supplied.</returns>
    public IDataRequest CreateRequest(DateOnly valuationDate) =>
        new OvernightFixingRequest(
            new MarketData.Abstractions.ReferenceData.BenchmarkName(_benchmarkId),
            new DateRange(
                _calendar is not null
                    ? _calendar.Advance(valuationDate, -_lookbackDays)
                    : valuationDate.AddDays(-_lookbackDays),
                valuationDate));

    /// <summary>Selects the latest fixing on or before the shifted observation date.</summary>
    /// <param name="records">Canonical fixing records returned for this benchmark.</param>
    /// <param name="valuationDate">As-of date from which the observation convention is applied.</param>
    /// <returns>The decimal fixing rate, or <see langword="null"/> when no eligible fixing exists.</returns>
    public decimal? ExtractRate(IReadOnlyList<object> records, DateOnly valuationDate)
    {
        var targetDate = _calendar is not null && _shiftConvention != ObservationShiftConvention.None
            ? ObservationShiftHelper.ComputeObservationDate(valuationDate, _shiftConvention, _calendar, _shiftDays)
            : valuationDate;

        return records
            .OfType<ScalarObservation>()
            .Where(o => o.Date <= targetDate)
            .OrderByDescending(o => o.Date)
            .Select(o => (decimal?)o.Value)
            .FirstOrDefault();
    }

    /// <summary>Reports the observation date when fixing selection rolls back before its target date.</summary>
    /// <param name="records">Canonical fixing records returned for this benchmark.</param>
    /// <param name="valuationDate">As-of date from which the observation convention is applied.</param>
    /// <returns>The selected date when earlier than the target, otherwise <see langword="null"/>.</returns>
    public DateOnly? ExtractActualDate(IReadOnlyList<object> records, DateOnly valuationDate)
    {
        var targetDate = _calendar is not null && _shiftConvention != ObservationShiftConvention.None
            ? ObservationShiftHelper.ComputeObservationDate(valuationDate, _shiftConvention, _calendar, _shiftDays)
            : valuationDate;

        return records
            .OfType<ScalarObservation>()
            .Where(o => o.Date <= targetDate)
            .OrderByDescending(o => o.Date)
            .Select(o => (DateOnly?)o.Date)
            .FirstOrDefault() is { } actual && actual != targetDate
                ? actual
                : null;
    }
}
