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

namespace Boutquin.Curves.Abstractions.Curves;

/// <summary>
/// Classifies the economic role played by a curve in valuation and risk workflows.
/// </summary>
public enum CurveRole
{
    /// <summary>
    /// Primary discounting curve for present-value calculations.
    /// </summary>
    Discount = 0,

    /// <summary>
    /// Forward projection curve used to derive expected floating rates.
    /// </summary>
    Forward = 1,

    /// <summary>
    /// Basis spread curve linking two benchmark projections.
    /// </summary>
    Basis = 2,

    /// <summary>
    /// Collateral remuneration curve, commonly tied to CSA discounting.
    /// </summary>
    Collateral = 3,

    /// <summary>
    /// Borrowing or funding curve for unsecured or internal funding adjustments.
    /// </summary>
    Borrow = 4,

    /// <summary>
    /// Dividend or carry curve for equity-linked discounting and forwards.
    /// </summary>
    Dividend = 5,

    /// <summary>
    /// Inflation-linked curve for real/nominal conversion workflows.
    /// </summary>
    Inflation = 6,

    /// <summary>
    /// Extension point for project-specific curve semantics.
    /// </summary>
    Custom = 7
}
