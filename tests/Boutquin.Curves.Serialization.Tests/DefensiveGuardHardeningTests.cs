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

// M-1 defensive-guard hardening tests:
// A-8a: CalibrationReportExporter.Export — null result guard

using FluentAssertions;

namespace Boutquin.Curves.Serialization.Tests;

public sealed class CalibrationReportExporterDefensiveGuardTests
{
    // ── A-8a: CalibrationReportExporter.Export — null result ──────────────

    [Fact]
    public void CalibrationReportExporter_Export_NullResult_Throws_ArgumentNullException_NamedResult()
    {
        // AC-G1: null CurveCalibrationResult must throw ArgumentNullException naming "result"
        // Currently throws NRE at result.CurveGroup.Name.Value.
        Action act = () => CalibrationReportExporter.Export(null!, new DateOnly(2026, 6, 30));

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("result");
    }
}
