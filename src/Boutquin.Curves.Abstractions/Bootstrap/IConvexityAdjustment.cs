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

namespace Boutquin.Curves.Abstractions.Bootstrap;

/// <summary>
/// Defines a convexity adjustment applied to futures-implied rates before curve calibration.
/// </summary>
/// <remarks>
/// Futures contracts settle daily via margin payments, which creates a financing convexity bias
/// relative to the OIS forward rate that forward rate agreements (FRAs) and swaps reference.
/// For short-dated contracts (under 1 year), this bias is typically small (0.1-0.5 bp). For
/// contracts beyond 1 year, the adjustment can reach several basis points and becomes material
/// for calibration accuracy.
/// </remarks>
public interface IConvexityAdjustment
{
    /// <summary>
    /// Computes the convexity adjustment for a futures contract.
    /// </summary>
    /// <param name="timeToExpiry">Year fraction from valuation to contract expiry.</param>
    /// <param name="timeToMaturity">Year fraction from valuation to contract maturity (end of accrual period).</param>
    /// <returns>Convexity adjustment in decimal form (e.g., 0.0003 for 3 bp). Subtract from futures rate to get OIS-equivalent rate.</returns>
    double Adjustment(double timeToExpiry, double timeToMaturity);
}
