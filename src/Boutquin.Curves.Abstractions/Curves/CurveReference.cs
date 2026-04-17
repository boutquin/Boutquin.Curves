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

using Boutquin.MarketData.Abstractions.ReferenceData;

namespace Boutquin.Curves.Abstractions.Curves;

/// <summary>
/// Identifies a curve within a market environment by role, currency, and optional benchmark identity.
/// </summary>
/// <param name="Role">Economic role of the curve, such as discounting, forwarding, or basis projection.</param>
/// <param name="Currency">Currency for which the curve is defined.</param>
/// <param name="Benchmark">Optional benchmark identity when the curve role is benchmark-specific.</param>
public sealed record CurveReference(
    CurveRole Role,
    CurrencyCode Currency,
    BenchmarkName? Benchmark = null);
