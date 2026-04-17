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

using System.Globalization;
using System.Text.Json;
using Boutquin.Curves.Bootstrap;

namespace Boutquin.Curves.Serialization;

/// <summary>
/// Exports a <see cref="CurveCalibrationResult"/> to a JSON calibration report.
/// </summary>
/// <remarks>
/// The report includes repricing diagnostics, structural observations, numerical solver
/// metrics, and an optional Jacobian matrix. Every report carries a <c>schemaVersion</c>
/// field for forward compatibility. The Jacobian is omitted when <see langword="null"/>,
/// keeping the report compact for production monitoring where only residuals matter.
/// </remarks>
/// <example>
/// <code>
/// CurveCalibrationResult result = calibrator.Calibrate(request);
/// string json = CalibrationReportExporter.Export(result, valuationDate);
/// File.WriteAllText("report.json", json);
/// </code>
/// </example>
public static class CalibrationReportExporter
{
    private static readonly JsonWriterOptions s_writerOptions = new()
    {
        Indented = true
    };

    /// <summary>
    /// Serializes a calibration result to an indented JSON report.
    /// </summary>
    /// <param name="result">Calibration result containing curves, diagnostics, and optional Jacobian.</param>
    /// <param name="valuationDate">Valuation date included in the report header.</param>
    /// <returns>JSON text representing the calibration report.</returns>
    public static string Export(CurveCalibrationResult result, DateOnly valuationDate)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, s_writerOptions))
        {
            writer.WriteStartObject();

            writer.WriteString("schemaVersion", "1.0");
            writer.WriteString("valuationDate", valuationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            writer.WriteString("curveGroup", result.CurveGroup.Name.Value);

            WriteDiagnostics(writer, result);

            if (result.Jacobian is not null)
            {
                WriteJacobian(writer, result);
            }

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteDiagnostics(Utf8JsonWriter writer, CurveCalibrationResult result)
    {
        writer.WriteStartObject("diagnostics");

        // Repricing
        writer.WriteStartArray("repricing");
        foreach (var r in result.Diagnostics.Repricing)
        {
            writer.WriteStartObject();
            writer.WriteString("label", r.Label);
            writer.WriteString("targetCurve", $"{r.TargetCurve.Role}:{r.TargetCurve.Currency}:{r.TargetCurve.Benchmark?.Value ?? ""}");
            writer.WriteString("pillarDate", r.PillarDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            writer.WriteNumber("marketQuote", r.MarketQuote);
            writer.WriteNumber("impliedQuote", r.ImpliedQuote);
            writer.WriteNumber("absoluteError", r.AbsoluteError);
            writer.WriteNumber("signedError", r.SignedError);
            writer.WriteString("instrumentType", r.InstrumentType);

            if (r.WarningFlags is { Count: > 0 })
            {
                writer.WriteStartArray("warningFlags");
                foreach (var flag in r.WarningFlags)
                {
                    writer.WriteStringValue(flag);
                }
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteStartArray("warningFlags");
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        // Structural
        writer.WriteStartArray("structural");
        foreach (var s in result.Diagnostics.Structural)
        {
            writer.WriteStartObject();
            writer.WriteString("code", s.Code);
            writer.WriteString("message", s.Message);
            writer.WriteString("severity", s.Severity);
            if (s.Context is not null)
            {
                writer.WriteString("context", s.Context);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        // Numerical
        writer.WriteStartArray("numerical");
        foreach (var n in result.Diagnostics.Numerical)
        {
            writer.WriteStartObject();
            writer.WriteString("solverName", n.SolverName);
            writer.WriteNumber("iterations", n.Iterations);
            writer.WriteBoolean("converged", n.Converged);
            writer.WriteString("message", n.Message);
            if (n.Residual.HasValue)
            {
                writer.WriteNumber("residual", n.Residual.Value);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private static void WriteJacobian(Utf8JsonWriter writer, CurveCalibrationResult result)
    {
        var j = result.Jacobian!;

        writer.WriteStartObject("jacobian");

        writer.WriteStartArray("rowLabels");
        foreach (var label in j.RowLabels)
        {
            writer.WriteStringValue(label);
        }
        writer.WriteEndArray();

        writer.WriteStartArray("columnLabels");
        foreach (var label in j.ColumnLabels)
        {
            writer.WriteStringValue(label);
        }
        writer.WriteEndArray();

        writer.WriteStartArray("values");
        int rows = j.Values.GetLength(0);
        int cols = j.Values.GetLength(1);
        for (int r = 0; r < rows; r++)
        {
            writer.WriteStartArray();
            for (int c = 0; c < cols; c++)
            {
                writer.WriteNumberValue(j.Values[r, c]);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }
}
