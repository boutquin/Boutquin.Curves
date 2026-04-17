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
/// Classifies instrument families used for quote normalization and diagnostics grouping.
/// </summary>
public enum MarketInstrumentType
{
    /// <summary>
    /// Overnight benchmark fixing such as SOFR or CORRA.
    /// </summary>
    OvernightFixing = 0,

    /// <summary>
    /// Money-market deposit quote over a defined short tenor.
    /// </summary>
    Deposit = 1,

    /// <summary>
    /// Overnight indexed swap quote.
    /// </summary>
    Ois = 2,

    /// <summary>
    /// Future on an overnight indexed swap contract.
    /// </summary>
    OisFuture = 3,

    /// <summary>
    /// Short-term interest-rate futures quote.
    /// </summary>
    StirFuture = 4,

    /// <summary>
    /// Forward rate agreement quote.
    /// </summary>
    Fra = 5,

    /// <summary>
    /// Fixed-versus-floating vanilla swap quote.
    /// </summary>
    FixedFloatSwap = 6,

    /// <summary>
    /// Basis swap quote between two floating benchmarks.
    /// </summary>
    BasisSwap = 7,

    /// <summary>
    /// Extension point for project-specific instrument families.
    /// </summary>
    Custom = 8
}
