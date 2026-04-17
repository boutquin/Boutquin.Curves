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

namespace Boutquin.Curves.Bootstrap.Definitions;

/// <summary>
/// Identifies the ordinate type solved for each node, such as discount factor or zero rate.
/// </summary>
/// <remarks>
/// Ordinate choice defines the numerical space in which calibration and interpolation are expressed.
/// It influences smoothness behavior, extrapolation interpretation, and transformation risk.
/// </remarks>
public enum CurveValueType
{
    /// <summary>
    /// Node value represents a discount factor at maturity.
    /// </summary>
    /// <remarks>
    /// Preferred for arbitrage-safe interpolation workflows because discount factors are directly tied
    /// to present-value economics and positivity constraints.
    /// </remarks>
    DiscountFactor = 0,

    /// <summary>
    /// Node value represents a zero-coupon continuously-compounded rate.
    /// </summary>
    /// <remarks>
    /// Useful when market workflows and controls are expressed in rate space, but requires careful
    /// conversion back to discount factors for valuation consistency.
    /// </remarks>
    ZeroRate = 1
}
