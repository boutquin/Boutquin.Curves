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

using FluentAssertions;

namespace Boutquin.Curves.Risk.Tests;

public sealed class RiskReportTests
{
    [Fact]
    public void Constructor_ShouldDefensivelyCopySensitivities()
    {
        List<BucketedSensitivity> mutableList = new()
        {
            new BucketedSensitivity("1Y", -0.001),
            new BucketedSensitivity("2Y", -0.003)
        };

        RiskReport report = new("Test", "Scenario", mutableList);

        // Mutate the original list after construction.
        mutableList.Add(new BucketedSensitivity("5Y", -0.010));

        // The record should be unaffected by the mutation.
        report.Sensitivities.Should().HaveCount(2);
    }
}
