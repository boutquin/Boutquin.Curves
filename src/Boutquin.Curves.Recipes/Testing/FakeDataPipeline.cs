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

    /// <inheritdoc />
    public Task<DataEnvelope<IReadOnlyList<TRecord>>> ExecuteAsync<TRequest, TRecord>(
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IDataRequest
    {
        if (_envelopes.TryGetValue(request.DatasetKey, out var envelope))
        {
            return Task.FromResult((DataEnvelope<IReadOnlyList<TRecord>>)envelope);
        }

        throw new InvalidOperationException(
            $"No fixture data registered for dataset key '{request.DatasetKey}'. " +
            $"Call Register<{typeof(TRecord).Name}>(\"{request.DatasetKey}\", ...) before executing.");
    }
}
