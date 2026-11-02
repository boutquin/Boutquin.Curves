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

using Boutquin.MarketData.Abstractions.Contracts;
using Boutquin.MarketData.Abstractions.Results;
using Boutquin.MarketData.Abstractions.Snapshots;

namespace Boutquin.Curves.Recipes.Testing;

/// <summary>
/// In-memory data pipeline that returns pre-registered envelopes keyed by dataset identifier.
/// Designed for unit tests, integration tests, and deterministic examples where no network
/// access is available or desired.
/// </summary>
/// <remarks>
/// Register envelopes via <see cref="Register{TRecord}"/> before calling
/// <see cref="ExecuteAsync{TRequest, TRecord}"/>. The pipeline matches requests by
/// <see cref="IDataRequest.DatasetKey"/> and returns the first envelope whose key matches,
/// regardless of the request's date range or other parameters. This keeps the fake simple
/// while still exercising the full <see cref="CurveBuilder"/> orchestration path.
/// </remarks>
public sealed class FakeDataPipeline : IDataPipeline
{
    private readonly Dictionary<string, object> _envelopes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers an envelope to be returned when a request with the matching
    /// <paramref name="datasetKey"/> is executed.
    /// </summary>
    /// <typeparam name="TRecord">Canonical record type contained in the envelope.</typeparam>
    /// <param name="datasetKey">Dataset key that incoming requests will match against.</param>
    /// <param name="envelope">Pre-built envelope containing the fixture records.</param>
    /// <returns>The current pipeline instance for fluent chaining.</returns>
    public FakeDataPipeline Register<TRecord>(
        string datasetKey,
        DataEnvelope<IReadOnlyList<TRecord>> envelope)
    {
        _envelopes[datasetKey] = envelope;
        return this;
    }

    /// <summary>Returns the registered fixture for the request's dataset key without I/O.</summary>
    /// <typeparam name="TRequest">Request type supplying the dataset key.</typeparam>
    /// <typeparam name="TRecord">Canonical record type registered under that key.</typeparam>
    /// <param name="request">Request identifying the fixture; range and frequency do not filter it.</param>
    /// <param name="cancellationToken">Accepted for interface compatibility; fixture lookup completes synchronously.</param>
    /// <returns>An already-completed task containing the registered envelope.</returns>
    /// <exception cref="InvalidOperationException">No envelope is registered for the dataset key.</exception>
    /// <exception cref="InvalidCastException">The registered envelope has a different record type.</exception>
    public Task<DataEnvelope<IReadOnlyList<TRecord>>> ExecuteAsync<TRequest, TRecord>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IDataRequest
    {
        if (_envelopes.TryGetValue(request.DatasetKey, out var envelope))
        {
            if (envelope is not DataEnvelope<IReadOnlyList<TRecord>> typed)
            {
                throw new InvalidCastException(
                    $"Fixture '{request.DatasetKey}' was registered with a different record type; expected {typeof(TRecord).Name}.");
            }
            return Task.FromResult(typed);
        }

        throw new InvalidOperationException(
            $"No fixture data registered for dataset key '{request.DatasetKey}'. " +
            $"Call Register<{typeof(TRecord).Name}>(\"{request.DatasetKey}\", ...) before executing.");
    }

    /// <summary>Rejects partitioned reads, which this fixture pipeline does not implement.</summary>
    /// <typeparam name="TRequest">Batch request type.</typeparam>
    /// <typeparam name="TRecord">Canonical record type.</typeparam>
    /// <typeparam name="TPartitionKey">Identity of one batch partition.</typeparam>
    /// <typeparam name="TDatasetKey">Identity of one stored dataset.</typeparam>
    /// <param name="request">Unused batch request.</param>
    /// <param name="splitForPartition">Unused partition projection.</param>
    /// <param name="buildDatasetKey">Unused dataset-key projection.</param>
    /// <param name="cancellationToken">Unused cancellation token.</param>
    /// <returns>No task is returned; the method throws synchronously.</returns>
    /// <exception cref="NotSupportedException">Partitioned reads are unsupported.</exception>
    /// <remarks>
    /// Not implemented: the curve recipes drive every node through the single-request
    /// <see cref="ExecuteAsync{TRequest, TRecord}"/> path, so the fake never needs the
    /// per-partition FX read. Register one envelope per dataset key and use <c>ExecuteAsync</c>.
    /// </remarks>
    public Task<IReadOnlyDictionary<TDatasetKey, DataEnvelope<IReadOnlyList<TRecord>>>>
        ExecutePartitionedAsync<TRequest, TRecord, TPartitionKey, TDatasetKey>(
            TRequest request,
            Func<TRequest, TPartitionKey, TRequest> splitForPartition,
            Func<TRequest, TPartitionKey, TDatasetKey> buildDatasetKey,
            CancellationToken cancellationToken = default)
        where TRequest : IBatchDataRequest<TPartitionKey>
        where TDatasetKey : notnull
        => throw new NotSupportedException(
            "FakeDataPipeline does not implement partitioned reads; register per-dataset envelopes and use ExecuteAsync.");

    /// <summary>Rejects point-in-time read handles, which this fixture pipeline does not implement.</summary>
    /// <param name="cancellationToken">Unused cancellation token.</param>
    /// <returns>No task is returned; the method throws synchronously.</returns>
    /// <exception cref="NotSupportedException">Snapshot reads are unsupported.</exception>
    /// <remarks>Not implemented: the curve recipes do not take point-in-time snapshots.</remarks>
    public Task<IDataSnapshot> AcquireReadSnapshotAsync(CancellationToken cancellationToken = default)
        => throw new NotSupportedException("FakeDataPipeline does not implement snapshot reads.");
}
