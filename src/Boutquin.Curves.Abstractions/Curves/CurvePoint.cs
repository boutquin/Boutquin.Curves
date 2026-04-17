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
/// Represents a single nodal observation on a curve at a given date.
/// </summary>
/// <param name="Date">Node date associated with the quoted or solved curve value.</param>
/// <param name="Value">Curve value at <paramref name="Date"/>, typically a discount factor or rate-like ordinate.</param>
public sealed record CurvePoint(DateOnly Date, double Value);
