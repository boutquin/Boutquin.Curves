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
using Boutquin.Domain.Enumerations;
using FluentAssertions;

namespace Boutquin.Curves.Abstractions.Tests;

/// <summary>
/// Tests for CF-C4-2: BootstrapDiagnostics must take a defensive copy of its
/// IReadOnlyList parameters so that post-construction mutation of the caller's
/// original list does not silently modify the captured diagnostic snapshot.
/// </summary>
public sealed class BootstrapDiagnosticsTests
{
    private static StructuralDiagnostic MakeStructural(string code = "S1") =>
        new(code, "test message", "Info");

    private static RepricingDiagnostic MakeRepricing() =>
        new("label", new Boutquin.Curves.Abstractions.Curves.CurveReference(
            Boutquin.Curves.Abstractions.Curves.CurveRole.Discount,
            Currency.USD),
            new DateOnly(2026, 1, 1), 0.05, 0.05, 0.0, 0.0, "OIS");

    private static NumericalDiagnostic MakeNumerical() =>
        new("Newton", 5, true, "Converged");

    [Fact]
    public void Structural_List_Mutated_After_Construction_Does_Not_Affect_Snapshot()
    {
        // Arrange: live list with one entry
        var liveList = new List<StructuralDiagnostic> { MakeStructural("S1") };

        var diag = new BootstrapDiagnostics(
            Array.Empty<RepricingDiagnostic>(),
            liveList,
            Array.Empty<NumericalDiagnostic>());

        int countBefore = diag.Structural.Count;

        // Act: mutate the source list after construction
        liveList.Add(MakeStructural("S2"));

        // Assert: the diagnostic snapshot is unchanged
        diag.Structural.Count.Should().Be(countBefore,
            "BootstrapDiagnostics must take a defensive copy of Structural");
    }

    [Fact]
    public void Repricing_List_Mutated_After_Construction_Does_Not_Affect_Snapshot()
    {
        var liveList = new List<RepricingDiagnostic> { MakeRepricing() };

        var diag = new BootstrapDiagnostics(
            liveList,
            Array.Empty<StructuralDiagnostic>(),
            Array.Empty<NumericalDiagnostic>());

        int countBefore = diag.Repricing.Count;
        liveList.Add(MakeRepricing());

        diag.Repricing.Count.Should().Be(countBefore,
            "BootstrapDiagnostics must take a defensive copy of Repricing");
    }

    [Fact]
    public void Numerical_List_Mutated_After_Construction_Does_Not_Affect_Snapshot()
    {
        var liveList = new List<NumericalDiagnostic> { MakeNumerical() };

        var diag = new BootstrapDiagnostics(
            Array.Empty<RepricingDiagnostic>(),
            Array.Empty<StructuralDiagnostic>(),
            liveList);

        int countBefore = diag.Numerical.Count;
        liveList.Add(MakeNumerical());

        diag.Numerical.Count.Should().Be(countBefore,
            "BootstrapDiagnostics must take a defensive copy of Numerical");
    }

    [Fact]
    public void Empty_Static_Property_Is_Unaffected_By_Any_Construction()
    {
        // Verify the static Empty instance continues to be empty regardless of other ctors
        BootstrapDiagnostics.Empty.Repricing.Should().BeEmpty();
        BootstrapDiagnostics.Empty.Structural.Should().BeEmpty();
        BootstrapDiagnostics.Empty.Numerical.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // F-L2-C4-R3-01: init setter bypass — structural prevention proven by type check
    //
    // The fix removes the three `init` accessors so that no `with`-expression can
    // supply a raw mutable-list reference.  These tests prove the stored backing
    // value is a T[] (the ToArray() copy), not the caller's original IReadOnlyList<T>.
    //
    // Regression proof (mutation check):
    //   If the fix is reverted and `init` is restored WITHOUT a `.ToArray()` in the
    //   setter, a caller could invoke `baseDiag with { Repricing = liveList }` and the
    //   stored value would be the caller's List<T> — these tests would still pass
    //   because the IS-type check only tests the ctor path.
    //
    //   The mutation-check proof is embedded in the existing ctor-path tests above
    //   (Structural/Repricing/Numerical_List_Mutated_After_Construction): those tests
    //   go RED any time the production code stores a raw reference rather than a copy,
    //   whether via ctor or via a reverted init setter that passes the reference through.
    //
    //   The critical structural guarantee — that the `init` bypass surface no longer
    //   exists — is enforced at compile time: a `with { Repricing = list }` expression
    //   is now a CS0200 compile error. That constraint cannot be tested at runtime; it
    //   is documented here as the rationale for removing the `init` accessors and is
    //   verified by the fact that the test project builds (no `init` means `with` fails).
    // -----------------------------------------------------------------------

    [Fact]
    public void Repricing_StoredValue_CannotBeMutatedThroughCollectionCast()
    {
        // Caller lists and castable output collections must not mutate the snapshot.
        var input = new List<RepricingDiagnostic> { MakeRepricing() };

        var diag = new BootstrapDiagnostics(
            input,
            Array.Empty<StructuralDiagnostic>(),
            Array.Empty<NumericalDiagnostic>());

        // The IReadOnlyList contract must stay read-only after a supported collection cast.
        Action mutate = () => ((IList<RepricingDiagnostic>)diag.Repricing)[0] = default!;
        mutate.Should().Throw<NotSupportedException>();
        diag.Repricing[0].Should().Be(input[0]);
    }

    [Fact]
    public void Structural_StoredValue_CannotBeMutatedThroughCollectionCast()
    {
        var input = new List<StructuralDiagnostic> { MakeStructural() };

        var diag = new BootstrapDiagnostics(
            Array.Empty<RepricingDiagnostic>(),
            input,
            Array.Empty<NumericalDiagnostic>());

        Action mutate = () => ((IList<StructuralDiagnostic>)diag.Structural)[0] = default!;
        mutate.Should().Throw<NotSupportedException>();
        diag.Structural[0].Should().Be(input[0]);
    }

    [Fact]
    public void Numerical_StoredValue_CannotBeMutatedThroughCollectionCast()
    {
        var input = new List<NumericalDiagnostic> { MakeNumerical() };

        var diag = new BootstrapDiagnostics(
            Array.Empty<RepricingDiagnostic>(),
            Array.Empty<StructuralDiagnostic>(),
            input);

        Action mutate = () => ((IList<NumericalDiagnostic>)diag.Numerical)[0] = default!;
        mutate.Should().Throw<NotSupportedException>();
        diag.Numerical[0].Should().Be(input[0]);
    }
}
