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

using BenchmarkDotNet.Running;

namespace Boutquin.Curves.Benchmarks;

/// <summary>
/// Benchmark entry point for the Boutquin.Curves benchmark suite.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the benchmark set. Pass <c>--job dry</c> as arguments for CI smoke verification.
    /// </summary>
    /// <param name="args">Command-line arguments forwarded to BenchmarkSwitcher for job/filter selection.</param>
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
