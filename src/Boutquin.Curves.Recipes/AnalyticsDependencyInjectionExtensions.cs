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

using Boutquin.Curves.Abstractions.ReferenceData;
using Boutquin.Curves.Bootstrap;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.MarketData.Conventions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Boutquin.Curves.Recipes;

/// <summary>
/// Extension methods for registering Analytics curve construction services
/// into a <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// Registers the core calibration pipeline: <see cref="CurveBuilder"/> for orchestration,
/// <see cref="PiecewiseBootstrapCalibrator"/> for bootstrap solving, and a default
/// <see cref="IReferenceDataProvider"/> backed by <see cref="SimpleReferenceDataProvider"/>
/// with standard calendars, benchmarks, and conventions. All registrations use
/// <c>TryAddSingleton</c> so callers can override individual services before calling
/// this method.
/// </remarks>
public static class AnalyticsDependencyInjectionExtensions
{
    /// <summary>
    /// Registers <see cref="CurveBuilder"/>, <see cref="PiecewiseBootstrapCalibrator"/>,
    /// and a default <see cref="IReferenceDataProvider"/> into the service collection.
    /// </summary>
    /// <param name="services">Service collection to populate.</param>
    /// <returns>The same <paramref name="services"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddAnalytics(this IServiceCollection services)
    {
        services.TryAddSingleton<PiecewiseBootstrapCalibrator>();
        services.TryAddSingleton<IReferenceDataProvider>(_ => CreateDefaultReferenceData());
        services.TryAddSingleton<CurveBuilder>();
        return services;
    }

    private static SimpleReferenceDataProvider CreateDefaultReferenceData()
    {
        var calendars = HolidayCalendarFactory.SupportedCodes.Select(HolidayCalendarFactory.Create).ToArray();

        var benchmarks = new[]
        {
            new RateBenchmark(new BenchmarkName("SOFR"), CurrencyCode.USD, BenchmarkKind.OvernightRiskFree, null, 0, 1, "USNY", "ACT/360", true),
            new RateBenchmark(new BenchmarkName("CORRA"), CurrencyCode.CAD, BenchmarkKind.OvernightRiskFree, null, 0, 1, "CATO", "ACT/365F", true),
            new RateBenchmark(new BenchmarkName("SONIA"), CurrencyCode.GBP, BenchmarkKind.OvernightRiskFree, null, 0, 0, "GBLO", "ACT/365F", true),
            new RateBenchmark(new BenchmarkName("ESTR"), CurrencyCode.EUR, BenchmarkKind.OvernightRiskFree, null, 0, 1, "TARGET", "ACT/360", true),
        };

        var registry = InstrumentConventionRegistry.CreateDefault();
        var conventions = new[]
        {
            registry.GetRequired("USD-SOFR-OIS"),
            registry.GetRequired("CAD-CORRA-OIS"),
            registry.GetRequired("GBP-SONIA-OIS"),
            registry.GetRequired("EUR-ESTR-OIS"),
            registry.GetRequired("USD-FIXED-6M-30-360"),
            registry.GetRequired("GBP-FIXED-1Y-ACT-365F"),
            registry.GetRequired("EUR-FIXED-1Y-ACT-360"),
        };

        return new SimpleReferenceDataProvider(calendars, benchmarks, conventions);
    }
}
