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

namespace Boutquin.Curves.Abstractions.Bootstrap;

/// <summary>
/// Defines a calibration engine that transforms market inputs into calibrated curve outputs.
/// </summary>
/// <typeparam name="TRequest">Request payload type containing market inputs and configuration.</typeparam>
/// <typeparam name="TResult">Result payload type containing calibrated objects and diagnostics.</typeparam>
public interface ICurveCalibrator<in TRequest, out TResult>
{
    /// <summary>
    /// Calibrates curve outputs from the provided request payload.
    /// </summary>
    /// <param name="request">Calibration request containing the required inputs.</param>
    /// <returns>Calibration result produced by the implementation.</returns>
    TResult Calibrate(TRequest request);
}
