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

using Boutquin.Curves.Abstractions.Curves;

namespace Boutquin.Curves.Risk;

/// <summary>
/// Builds bucketed risk reports by resolving a curve from a group, applying a shock scenario,
/// and computing the valuation delta at each configured maturity bucket.
/// </summary>
/// <remarks>
/// Pipeline context: CurveRiskAnalyzer is the terminal consumer in the standard curve pipeline.
/// It takes calibrated curves from the ICurveGroup, applies shock scenarios, and produces risk
/// reports. Upstream: everything from data aggregation through bootstrap calibration.
/// </remarks>
/// <example>
/// <code>
/// var analyzer = new CurveRiskAnalyzer();
/// var scenario = new ParallelZeroRateShock(0.0001); // 1 bp parallel shock
/// var buckets = new[] { new RiskBucket("1Y", date1Y), new RiskBucket("5Y", date5Y) };
/// RiskReport report = analyzer.BuildScenarioRiskReport(curveGroup, curveRef, scenario, buckets);
/// // report.Sensitivities contains delta per bucket
/// </code>
/// </example>
/// <seealso cref="Boutquin.Curves.Abstractions.Curves.IDiscountCurve"/>
/// <seealso cref="Boutquin.Curves.Risk.CurveShockScenario"/>
public sealed class CurveRiskAnalyzer
{
    /// <summary>
    /// Builds a bucketed scenario-risk report by computing shocked-minus-base valuation differences
    /// at each maturity bucket for the specified curve and shock scenario.
    /// </summary>
    /// <param name="curveGroup">Curve snapshot used to resolve the target curve by reference.</param>
    /// <param name="reference">Logical curve key (role + currency) identifying the curve to analyze.</param>
    /// <param name="scenario">Shock scenario that transforms the base curve into a stressed curve.</param>
    /// <param name="buckets">Maturity buckets where shocked-vs-base valuation deltas are measured.</param>
    /// <returns>A risk report containing one <see cref="BucketedSensitivity"/> per bucket with the actual valuation delta.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="curveGroup"/>, <paramref name="scenario"/>, or <paramref name="buckets"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="buckets"/> is empty.
    /// </exception>
    /// <remarks>
    /// Sensitivities are computed as <c>shockedCurve.ValueAt(date) - baseCurve.ValueAt(date)</c>.
    /// For discount curves under a parallel up-shock, all deltas are negative because higher rates
    /// reduce discount factors.
    /// </remarks>
    public RiskReport BuildScenarioRiskReport(
        ICurveGroup curveGroup,
        CurveReference reference,
        CurveShockScenario scenario,
        IReadOnlyList<RiskBucket> buckets)
    {
        ArgumentNullException.ThrowIfNull(curveGroup);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(buckets);

        if (buckets.Count == 0)
        {
            throw new ArgumentException("At least one risk bucket is required.", nameof(buckets));
        }

        ICurve baseCurve = curveGroup.GetCurve(reference);
        ICurve shockedCurve = scenario.Apply(baseCurve);

        IReadOnlyList<BucketedSensitivity> sensitivities = buckets
            .Select(bucket => new BucketedSensitivity(
                bucket.Bucket,
                shockedCurve.ValueAt(bucket.MaturityDate) - baseCurve.ValueAt(bucket.MaturityDate)))
            .ToArray();

        return new RiskReport(
            $"{baseCurve.Name} scenario risk",
            scenario.Name,
            sensitivities);
    }

    /// <summary>
    /// Builds a placeholder bucketed risk report using a 1 basis-point parallel zero-rate shock
    /// at standard 1Y, 2Y, and 5Y maturity buckets.
    /// </summary>
    /// <param name="curveGroup">Curve snapshot used to resolve the curve under analysis.</param>
    /// <param name="reference">Logical curve key (role + currency) identifying the curve to analyze.</param>
    /// <returns>A risk report with three buckets containing actual shocked-vs-base valuation deltas.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="curveGroup"/> is <c>null</c>.
    /// </exception>
    /// <remarks>
    /// Delegates to <see cref="BuildScenarioRiskReport"/> with a <see cref="ParallelZeroRateShock"/>
    /// of 1 bp. Useful for quick sanity checks before configuring full scenario analysis.
    /// </remarks>
    public RiskReport BuildPlaceholderBucketedReport(ICurveGroup curveGroup, CurveReference reference)
    {
        ArgumentNullException.ThrowIfNull(curveGroup);

        ICurve curve = curveGroup.GetCurve(reference);
        return BuildScenarioRiskReport(
            curveGroup,
            reference,
            new ParallelZeroRateShock("ParallelZero", 1d),
            new[]
            {
                new RiskBucket("1Y", curve.ValuationDate.AddYears(1)),
                new RiskBucket("2Y", curve.ValuationDate.AddYears(2)),
                new RiskBucket("5Y", curve.ValuationDate.AddYears(5))
            });
    }
}
