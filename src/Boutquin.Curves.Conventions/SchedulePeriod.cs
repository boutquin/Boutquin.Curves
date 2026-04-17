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

namespace Boutquin.Curves.Conventions;

/// <summary>
/// Represents one generated accrual/payment period in an instrument schedule.
/// </summary>
/// <param name="AccrualStartDate">Unadjusted accrual period start date used as the year-fraction anchor.</param>
/// <param name="AccrualEndDate">Unadjusted accrual period end date; may differ from <paramref name="PeriodEndDate"/> due to business-day adjustment.</param>
/// <param name="PeriodStartDate">Business-day adjusted start date used for cash-flow timing.</param>
/// <param name="PeriodEndDate">Business-day adjusted end date used for cash-flow timing.</param>
/// <param name="PaymentDate">Business-day adjusted payment date, incorporating any configured payment lag.</param>
/// <param name="YearFraction">Accrual year fraction computed over unadjusted dates, not adjusted dates.</param>
/// <remarks>
/// Both unadjusted and adjusted date pairs are preserved so that downstream pricing can choose
/// the appropriate pair: unadjusted for year-fraction computation, adjusted for discounting and payment.
/// </remarks>
public sealed record SchedulePeriod(
    DateOnly AccrualStartDate,
    DateOnly AccrualEndDate,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    DateOnly PaymentDate,
    double YearFraction);
