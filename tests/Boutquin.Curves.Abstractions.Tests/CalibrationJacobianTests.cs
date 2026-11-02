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

using Boutquin.Curves.Abstractions.Diagnostics;
using FluentAssertions;

namespace Boutquin.Curves.Abstractions.Tests;

public sealed class CalibrationJacobianTests
{
    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 2)]
    [InlineData(0, 1)]
    public void Constructor_MismatchedLabelDimensions_Rejects(int rows, int columns)
    {
        Action act = () => new CalibrationJacobian(["row"], ["column"], new double[rows, columns]);
        act.Should().Throw<ArgumentException>().WithParameterName("Values");
    }

    [Theory]
    [InlineData("RowLabels")]
    [InlineData("ColumnLabels")]
    [InlineData("Values")]
    public void Constructor_NullStructuralInput_Rejects(string parameter)
    {
        Action act = () => new CalibrationJacobian(parameter == "RowLabels" ? null! : ["row"],
            parameter == "ColumnLabels" ? null! : ["column"], parameter == "Values" ? null! : new double[1, 1]);
        act.Should().Throw<ArgumentNullException>().WithParameterName(parameter);
    }

    [Fact]
    public void Constructor_ValidEmptyShape_IsPreserved()
    {
        var jacobian = new CalibrationJacobian([], [], new double[0, 0]);
        jacobian.Values.Length.Should().Be(0);
    }
    [Fact]
    public void ConstructorAndValues_DoNotExposeCallerOrInternalMutableState()
    {
        string[] rows = ["row"];
        string[] columns = ["column"];
        double[,] values = { { 1d } };
        var jacobian = new CalibrationJacobian(rows, columns, values);
        rows[0] = "changed";
        columns[0] = "changed";
        values[0, 0] = 9d;
        jacobian.RowLabels[0].Should().Be("row");
        jacobian.ColumnLabels[0].Should().Be("column");
        jacobian.Values[0, 0].Should().Be(1d);
        double[,] exposed = jacobian.Values;
        exposed[0, 0] = 8d;
        jacobian.Values[0, 0].Should().Be(1d);
    }

}
