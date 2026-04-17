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
using Boutquin.MarketData.Conventions;

namespace Boutquin.Curves.Conventions.Calendars;

/// <summary>
/// Computes observation dates adjusted per the overnight fixing observation shift convention.
/// </summary>
/// <remarks>
/// Different RFR markets apply distinct observation date adjustments relative to accrual periods.
/// <see cref="ObservationShiftConvention.Shifted"/> and <see cref="ObservationShiftConvention.Lookback"/>
/// both move the observation date backward by a specified number of business days, while
/// <see cref="ObservationShiftConvention.Lockout"/> freezes the rate at the period boundary rather
/// than shifting individual observation dates.
/// </remarks>
public static class ObservationShiftHelper
{
    /// <summary>
    /// Adjusts an accrual date to its observation date using the given shift convention.
    /// </summary>
    /// <param name="accrualDate">The original accrual date.</param>
    /// <param name="convention">The observation shift convention to apply.</param>
    /// <param name="calendar">The business calendar for date adjustment.</param>
    /// <param name="shiftDays">Number of business days to shift (for Shifted and Lookback conventions).</param>
    /// <returns>The adjusted observation date.</returns>
    public static DateOnly ComputeObservationDate(
        DateOnly accrualDate,
        ObservationShiftConvention convention,
        IBusinessCalendar calendar,
        int shiftDays = 0)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        return convention switch
        {
            ObservationShiftConvention.None => accrualDate,
            ObservationShiftConvention.Shifted => calendar.Advance(accrualDate, -shiftDays),
            ObservationShiftConvention.Lookback => calendar.Advance(accrualDate, -shiftDays),
            ObservationShiftConvention.Lockout => accrualDate, // Lockout freezes at period end -- not a per-date shift
            _ => accrualDate,
        };
    }
}
