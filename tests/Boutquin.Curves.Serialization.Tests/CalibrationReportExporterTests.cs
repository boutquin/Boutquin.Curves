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

using System.Text.Json;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Diagnostics;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Bootstrap;
using Boutquin.MarketData.Abstractions.ReferenceData;
using FluentAssertions;

namespace Boutquin.Curves.Serialization.Tests;

/// <summary>
/// Verifies calibration report export to JSON with schema versioning.
/// </summary>
public sealed class CalibrationReportExporterTests
{
    private static CurveCalibrationResult CreateSampleResult()
    {
        var repricing = new List<RepricingDiagnostic>
        {
            new(
                "USD-SOFR-O/N",
                new CurveReference(CurveRole.Discount, CurrencyCode.USD, new BenchmarkName("SOFR")),
                new DateOnly(2024, 1, 16),
                0.0530,
                0.053000001,
                1e-9,
                1e-9,
                "Deposit",
                ["proxy"]),
        };

        var structural = new List<StructuralDiagnostic>
        {
            new("PROXY_USED", "Treasury par yield used as SOFR proxy", "warning", "USD-SOFR-OIS-2Y"),
        };

        var numerical = new List<NumericalDiagnostic>
        {
            new("Brent", 5, true, "Converged within tolerance", 1e-12),
        };

        var diagnostics = new BootstrapDiagnostics(repricing, structural, numerical);

        var jacobian = new CalibrationJacobian(
            ["USD-SOFR-O/N"],
            ["node-0"],
            new double[,] { { 1.0 } });

        // Use a stub curve group — we only test serialization, not curve logic.
        return new CurveCalibrationResult(
            StubCurveGroup.Instance,
            diagnostics,
            jacobian);
    }

    [Fact]
    public void Export_ContainsSchemaVersion()
    {
        var result = CreateSampleResult();

        string json = CalibrationReportExporter.Export(result, new DateOnly(2024, 1, 15));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("schemaVersion").GetString().Should().Be("1.0");
    }

    [Fact]
    public void Export_ContainsValuationDate()
    {
        var result = CreateSampleResult();

        string json = CalibrationReportExporter.Export(result, new DateOnly(2024, 1, 15));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("valuationDate").GetString().Should().Be("2024-01-15");
    }

    [Fact]
    public void Export_ContainsRepricingDiagnostics()
    {
        var result = CreateSampleResult();

        string json = CalibrationReportExporter.Export(result, new DateOnly(2024, 1, 15));

        using var doc = JsonDocument.Parse(json);
        var repricing = doc.RootElement.GetProperty("diagnostics").GetProperty("repricing");
        repricing.GetArrayLength().Should().Be(1);

        var entry = repricing[0];
        entry.GetProperty("label").GetString().Should().Be("USD-SOFR-O/N");
        entry.GetProperty("pillarDate").GetString().Should().Be("2024-01-16");
        entry.GetProperty("marketQuote").GetDouble().Should().Be(0.0530);
        entry.GetProperty("absoluteError").GetDouble().Should().BeLessThan(1e-8);
        entry.GetProperty("instrumentType").GetString().Should().Be("Deposit");
        entry.GetProperty("warningFlags").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void Export_ContainsStructuralDiagnostics()
    {
        var result = CreateSampleResult();

        string json = CalibrationReportExporter.Export(result, new DateOnly(2024, 1, 15));

        using var doc = JsonDocument.Parse(json);
        var structural = doc.RootElement.GetProperty("diagnostics").GetProperty("structural");
        structural.GetArrayLength().Should().Be(1);

        var entry = structural[0];
        entry.GetProperty("code").GetString().Should().Be("PROXY_USED");
        entry.GetProperty("severity").GetString().Should().Be("warning");
    }

    [Fact]
    public void Export_ContainsNumericalDiagnostics()
    {
        var result = CreateSampleResult();

        string json = CalibrationReportExporter.Export(result, new DateOnly(2024, 1, 15));

        using var doc = JsonDocument.Parse(json);
        var numerical = doc.RootElement.GetProperty("diagnostics").GetProperty("numerical");
        numerical.GetArrayLength().Should().Be(1);

        var entry = numerical[0];
        entry.GetProperty("solverName").GetString().Should().Be("Brent");
        entry.GetProperty("iterations").GetInt32().Should().Be(5);
        entry.GetProperty("converged").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Export_ContainsJacobian()
    {
        var result = CreateSampleResult();

        string json = CalibrationReportExporter.Export(result, new DateOnly(2024, 1, 15));

        using var doc = JsonDocument.Parse(json);
        var jacobian = doc.RootElement.GetProperty("jacobian");
        jacobian.GetProperty("rowLabels").GetArrayLength().Should().Be(1);
        jacobian.GetProperty("columnLabels").GetArrayLength().Should().Be(1);
        jacobian.GetProperty("values")[0][0].GetDouble().Should().Be(1.0);
    }

    [Fact]
    public void Export_NullJacobian_OmitsJacobianField()
    {
        var result = new CurveCalibrationResult(
            StubCurveGroup.Instance,
            BootstrapDiagnostics.Empty);

        string json = CalibrationReportExporter.Export(result, new DateOnly(2024, 1, 15));

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("jacobian", out _).Should().BeFalse();
    }

    [Fact]
    public void Export_EmptyDiagnostics_ProducesEmptyArrays()
    {
        var result = new CurveCalibrationResult(
            StubCurveGroup.Instance,
            BootstrapDiagnostics.Empty);

        string json = CalibrationReportExporter.Export(result, new DateOnly(2024, 1, 15));

        using var doc = JsonDocument.Parse(json);
        var diag = doc.RootElement.GetProperty("diagnostics");
        diag.GetProperty("repricing").GetArrayLength().Should().Be(0);
        diag.GetProperty("structural").GetArrayLength().Should().Be(0);
        diag.GetProperty("numerical").GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// Minimal stub implementing ICurveGroup for serialization tests.
    /// </summary>
    private sealed class StubCurveGroup : ICurveGroup
    {
        public static readonly StubCurveGroup Instance = new();

        public CurveGroupName Name => new("test-group");

        public DateOnly ValuationDate => new(2024, 1, 15);

        public ICurve GetCurve(CurveReference reference) =>
            throw new NotSupportedException("Stub — not intended for curve access.");

        public bool TryGetCurve(CurveReference reference, out ICurve? curve)
        {
            curve = null;
            return false;
        }
    }
}
