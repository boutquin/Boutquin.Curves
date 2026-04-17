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

namespace Boutquin.Curves.Quotes;

/// <summary>
/// Classifies how numeric quote values should be interpreted during normalization and calibration.
/// </summary>
public enum QuoteValueType
{
    /// <summary>
    /// Quoted value is a rate, typically in decimal form.
    /// </summary>
    Rate = 0,

    /// <summary>
    /// Quoted value is a tradable price.
    /// </summary>
    Price = 1,

    /// <summary>
    /// Quoted value is a spread versus a reference curve or index.
    /// </summary>
    Spread = 2,

    /// <summary>
    /// Quoted value is a discount factor.
    /// </summary>
    DiscountFactor = 3
}
