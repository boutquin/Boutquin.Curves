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

using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.Curves.Bootstrap;
using Boutquin.Curves.Recipes.Nodes;
using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.Diagnostics;
using Boutquin.MarketData.Abstractions.Records;
using Boutquin.MarketData.Abstractions.Requests;
using Boutquin.MarketData.Abstractions.Results;
using Microsoft.Extensions.Logging;

namespace Boutquin.Curves.Recipes;

/// <summary>
/// Orchestrates curve construction by fetching data via <see cref="IDataPipeline"/>,
/// extracting rates via <see cref="ICurveNodeSpec"/>, and delegating calibration
/// to <see cref="PiecewiseBootstrapCalibrator"/>.
/// </summary>
/// <remarks>
/// <para>
/// CurveBuilder bridges the gap between declarative <see cref="CurveGroupRecipe"/> definitions
/// and the calibrator's <see cref="CurveCalibrationInput"/> input format. For each recipe node
/// it fetches market data through the pipeline, extracts rates, constructs
/// <see cref="ResolvedNode"/> entries, and assembles the full calibration input before
/// delegating to the bootstrap calibrator.
/// </para>
/// <para>
/// Data requests are deduplicated across nodes that share the same logical dataset (same key,
/// date range, and frequency) using <see cref="DataRequestEqualityComparer"/>, reducing
/// redundant pipeline calls when multiple nodes draw from the same source.
/// </para>
/// <para>
/// Pipeline failures and missing rates are captured as <see cref="DataIssue"/> entries in the
/// returned <see cref="CurveSnapshot"/> rather than thrown as exceptions, enabling partial
/// curve construction when some nodes cannot be resolved.
/// </para>
/// </remarks>
public sealed class CurveBuilder
{
    private readonly IDataPipeline _pipeline;
    private readonly PiecewiseBootstrapCalibrator _calibrator;
    private readonly IReferenceDataProvider _referenceData;
    private readonly ILogger<CurveBuilder> _logger;

    /// <summary>
    /// Creates a new CurveBuilder instance.
    /// </summary>
    /// <param name="pipeline">Data pipeline for fetching market data records.</param>
    /// <param name="calibrator">Bootstrap calibrator that solves curve node values.</param>
    /// <param name="referenceData">Provider for holiday calendars, conventions, and static data.</param>
    /// <param name="logger">Logger for diagnostic output during curve construction.</param>
    public CurveBuilder(
        IDataPipeline pipeline,
        PiecewiseBootstrapCalibrator calibrator,
        IReferenceDataProvider referenceData,
        ILogger<CurveBuilder> logger)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _calibrator = calibrator ?? throw new ArgumentNullException(nameof(calibrator));
        _referenceData = referenceData ?? throw new ArgumentNullException(nameof(referenceData));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Builds a calibrated curve snapshot from a recipe by fetching data, resolving nodes, and running the bootstrap.
    /// </summary>
    /// <param name="recipe">Declarative curve group recipe defining what to build.</param>
    /// <param name="valuationDate">As-of date for the calibration run.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="CurveSnapshot"/> containing the calibrated curves, diagnostics, and any data issues.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no curves could be resolved from the recipe.</exception>
    public async Task<CurveSnapshot> BuildAsync(
        CurveGroupRecipe recipe,
        DateOnly valuationDate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        List<DataIssue> issues = [];
        List<DataProvenance> provenance = [];
        List<CurveCalibrationSpec> curveSpecs = [];

        foreach (CurveRecipe curve in recipe.Curves)
        {
            List<ResolvedNode> nodes = [];

            // Group nodes by request for deduplication
            IEnumerable<IGrouping<IDataRequest, ICurveNodeSpec>> groups = curve.Nodes.GroupBy(
                n => n.CreateRequest(valuationDate),
                DataRequestEqualityComparer.Instance);

            foreach (IGrouping<IDataRequest, ICurveNodeSpec> group in groups)
            {
                // Fetch data from pipeline
                IReadOnlyList<object>? records = null;
                try
                {
                    (records, IReadOnlyList<DataProvenance> fetchProvenance, IReadOnlyList<DataIssue> fetchIssues) =
                        await FetchRecordsAsync(group.Key, ct).ConfigureAwait(false);
                    provenance.AddRange(fetchProvenance);
                    issues.AddRange(fetchIssues);

                    // Surface calendar-aware gap issues from adapters at Warning level.
                    var unexpectedGaps = fetchIssues.Where(i =>
                        i.Code == IssueCode.UnexpectedGap);
                    foreach (var gap in unexpectedGaps)
                    {
                        _logger.LogWarning("Data gap for {DatasetKey}: {Message}",
                            group.Key.DatasetKey, gap.Message);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Pipeline fetch failed for {DatasetKey}", group.Key.DatasetKey);
                    issues.Add(new DataIssue(new IssueCode("FETCH_FAILED"), IssueSeverity.Error,
                        $"No data for {group.Key.DatasetKey}: {ex.Message}"));
                }

                foreach (ICurveNodeSpec nodeSpec in group)
                {
                    if (records is null)
                    {
                        issues.Add(new DataIssue(new IssueCode("NODE_MISSING"), IssueSeverity.Warning,
                            $"No data available for {nodeSpec.Label}"));
                        continue;
                    }

                    decimal? rate = nodeSpec.ExtractRate(records, valuationDate);
                    if (rate is null)
                    {
                        var hasUnexpectedGap = issues.Any(i =>
                            i.Code == IssueCode.UnexpectedGap);
                        issues.Add(new DataIssue(
                            new IssueCode("RATE_MISSING"),
                            hasUnexpectedGap ? IssueSeverity.Error : IssueSeverity.Warning,
                            $"No rate extracted for {nodeSpec.Label} from {group.Key.DatasetKey}"));
                        continue;
                    }

                    DateOnly? actualDate = nodeSpec.ExtractActualDate(records, valuationDate);
                    if (actualDate.HasValue)
                    {
                        // Node-level date gaps are expected publication lags (e.g., SOFR T+1).
                        issues.Add(new DataIssue(new IssueCode("DATE_ROLLBACK"), IssueSeverity.Warning,
                            $"{nodeSpec.Label}: using {actualDate.Value:yyyy-MM-dd} data (requested {valuationDate:yyyy-MM-dd}, expected publication lag)."));
                    }

                    nodes.Add(new ResolvedNode(
                        nodeSpec.Label,
                        nodeSpec.Tenor,
                        nodeSpec.InstrumentType,
                        nodeSpec.ConventionCode,
                        nodeSpec.TargetCurve,
                        rate.Value));
                }
            }

            if (nodes.Count == 0)
            {
                _logger.LogWarning("No resolved nodes for curve {CurveId}", curve.CurveId);
                continue;
            }

            curveSpecs.Add(new CurveCalibrationSpec(
                new CurveName(curve.CurveId),
                curve.CurveReference,
                curve.ValueType,
                curve.DayCountCode,
                curve.Interpolation,
                nodes));
        }

        if (curveSpecs.Count == 0)
        {
            throw new InvalidOperationException(
                $"No curves resolved for group '{recipe.GroupName}'. Check data pipeline and node specs.");
        }

        CurveCalibrationInput input = new(valuationDate, curveSpecs, _referenceData);
        CurveCalibrationResult result = _calibrator.Calibrate(input);

        return new CurveSnapshot(
            recipe.GroupName,
            valuationDate,
            result.CurveGroup,
            result.Diagnostics,
            issues,
            provenance,
            result.Jacobian);
    }

    private async Task<(IReadOnlyList<object> Records, IReadOnlyList<DataProvenance> Provenance, IReadOnlyList<DataIssue> Issues)> FetchRecordsAsync(
        IDataRequest request, CancellationToken ct)
    {
        return request switch
        {
            OvernightFixingRequest ofr => Extract(
                await _pipeline.ExecuteAsync<OvernightFixingRequest, ScalarObservation>(ofr, ct).ConfigureAwait(false)),

            YieldCurveQuoteRequest ycr => Extract(
                await _pipeline.ExecuteAsync<YieldCurveQuoteRequest, YieldCurveQuote>(ycr, ct).ConfigureAwait(false)),

            FuturesSettlementRequest fsr => Extract(
                await _pipeline.ExecuteAsync<FuturesSettlementRequest, FuturesSettlement>(fsr, ct).ConfigureAwait(false)),

            _ => throw new NotSupportedException($"Unsupported request type: {request.GetType().Name}")
        };

        static (IReadOnlyList<object>, IReadOnlyList<DataProvenance>, IReadOnlyList<DataIssue>) Extract<T>(DataEnvelope<IReadOnlyList<T>> envelope)
            => (envelope.Payload.Cast<object>().ToList(), envelope.Provenance, envelope.Issues);
    }
}
