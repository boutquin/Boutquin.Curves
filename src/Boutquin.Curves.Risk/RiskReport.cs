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

namespace Boutquin.Curves.Risk;

/// <summary>
/// Immutable risk report containing bucketed valuation sensitivities produced by a single shock scenario.
/// </summary>
/// <remarks>
/// Immutable output of a scenario risk analysis run. The <see cref="Label"/> identifies the analysis
/// (e.g., "USD-SOFR +1bp"), <see cref="ScenarioName"/> identifies the shock type, and
/// <see cref="Sensitivities"/> contain the actual valuation deltas at each maturity bucket. Risk
/// reports are designed for downstream consumption by regulatory reporting, P&amp;L attribution, and
/// limit monitoring. Sensitivities are defensively copied at construction time; external mutation
/// of the original collection does not affect the report.
/// </remarks>
public sealed record RiskReport
{
    /// <summary>Descriptive label identifying the curve and analysis type (e.g. "USD-Disc scenario risk").</summary>
    public string Label { get; }

    /// <summary>Name of the shock scenario that produced this report (e.g. "Up10bp", "Bucketed").</summary>
    public string ScenarioName { get; }

    /// <summary>Ordered sensitivity buckets, each containing a maturity label and the shocked-vs-base valuation delta.</summary>
    public IReadOnlyList<BucketedSensitivity> Sensitivities { get; }

    /// <summary>
    /// Creates a risk report, storing a defensive copy of the sensitivities collection.
    /// </summary>
    /// <param name="label">Descriptive label identifying the curve and analysis type.</param>
    /// <param name="scenarioName">Name of the shock scenario that produced this report.</param>
    /// <param name="sensitivities">Bucketed valuation deltas; copied at construction to ensure immutability.</param>
    public RiskReport(string label, string scenarioName, IReadOnlyList<BucketedSensitivity> sensitivities)
    {
        Label = label;
        ScenarioName = scenarioName;
        Sensitivities = sensitivities.ToArray();
    }
}

/// <summary>
/// Defines a maturity bucket at which shocked-vs-base valuation deltas are extracted.
/// </summary>
/// <param name="Bucket">Tenor label identifying the bucket (e.g. "1Y", "2Y", "5Y").</param>
/// <param name="MaturityDate">Absolute date at which the valuation delta is measured on the curve.</param>
public sealed record RiskBucket(
    string Bucket,
    DateOnly MaturityDate);

/// <summary>
/// Valuation sensitivity for a single maturity bucket, computed as the shocked-minus-base curve value.
/// </summary>
/// <param name="Bucket">Tenor label identifying the bucket (e.g. "1Y", "2Y", "5Y").</param>
/// <param name="Delta">Shocked-minus-base valuation difference; negative for rate-up shocks on discount curves.</param>
public sealed record BucketedSensitivity(
    string Bucket,
    double Delta);
