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

namespace Boutquin.Curves.Conventions.Calendars.Schedule;

/// <summary>
/// Represents a single schedule interval with adjusted accrual and payment dates.
/// </summary>
/// <remarks>
/// One accrual period in a payment schedule, carrying adjusted start and end dates along
/// with the payment date and an optional fixing date for floating-rate coupons. In contexts
/// where both adjusted and unadjusted dates are needed (e.g., year-fraction calculation vs.
/// payment timing), see the <c>Boutquin.Curves.Conventions.SchedulePeriod</c> record which
/// preserves both date pairs explicitly.
/// </remarks>
/// <param name="StartDate">Accrual start date for the period.</param>
/// <param name="EndDate">Accrual end date for the period.</param>
/// <param name="PaymentDate">Payment date associated with the period cashflow.</param>
/// <param name="FixingDate">Optional fixing date for floating-rate periods.</param>
public sealed record SchedulePeriod(
    DateOnly StartDate,
    DateOnly EndDate,
    DateOnly PaymentDate,
    DateOnly? FixingDate = null);
