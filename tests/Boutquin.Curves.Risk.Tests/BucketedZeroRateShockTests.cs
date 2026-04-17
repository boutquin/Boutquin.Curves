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

public sealed class BucketedZeroRateShockTests
{
    [Fact]
    public void Apply_ShouldThrow_WhenCurveIsNull()
    {
        BucketedZeroRateShock shock = new(
            "Bucketed",
            new[] { new BucketedShockPoint(1d, 5d) });

        Action act = () => shock.Apply(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("curve");
    }

    [Fact]
    public void Constructor_ShouldDefensivelyCopyShockPoints()
    {
        List<BucketedShockPoint> mutableList = new()
        {
            new BucketedShockPoint(1d, 5d),
            new BucketedShockPoint(2d, 10d)
        };

        BucketedZeroRateShock shock = new("Bucketed", mutableList);

        // Mutate the original list after construction.
        mutableList.Add(new BucketedShockPoint(5d, 50d));

        // The record should be unaffected by the mutation.
        shock.ShockPoints.Should().HaveCount(2);
    }
}
