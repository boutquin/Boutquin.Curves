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
// A-8b: RiskReport..ctor — null sensitivities guard

using FluentAssertions;

namespace Boutquin.Curves.Risk.Tests;

public sealed class RiskReportDefensiveGuardTests
{
    // ── A-8b: RiskReport..ctor — null sensitivities ───────────────────────

    [Fact]
    public void RiskReport_Constructor_NullSensitivities_Throws_ArgumentNullException_NamedSensitivities()
    {
        // AC-G1: null sensitivities must throw ArgumentNullException naming "sensitivities"
        // Currently throws NRE at sensitivities.ToArray() inside the constructor.
        Action act = () => new RiskReport("Label", "Scenario", null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sensitivities");
    }
}
