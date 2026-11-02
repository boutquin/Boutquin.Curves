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
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.ReferenceData;

namespace Boutquin.Curves.Bootstrap.Helpers;

/// <summary>Converts continuously compounded zero yields on a nominal maturity grid into discount factors.</summary>
/// <remarks>
/// A month tenor denotes its integer month count divided by twelve; a year tenor denotes its
/// integer year count. Calendar pillar dates are resolved separately. This preserves the nominal
/// maturity of published zero yields across leap years and calendar-month lengths.
/// Quotes are annualized decimal rates; finite negative rates are supported.
/// </remarks>
internal sealed class ContinuousZeroInstrumentHelper : AbstractInstrumentHelper
{
    private readonly ResolvedNode _node;
    private readonly double _maturityYears;

    /// <summary>Captures a continuous zero quote and its positive month or year maturity.</summary>
    /// <param name="node">Node whose tenor specifies the nominal quote maturity.</param>
    /// <param name="quoteValue">Annualized continuously compounded zero yield in decimal units.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The quote is non-finite or the tenor is non-positive.</exception>
    /// <exception cref="NotSupportedException">The tenor is malformed or uses a unit other than months or years.</exception>
    public ContinuousZeroInstrumentHelper(ResolvedNode node, double quoteValue)
        : base(RequireNode(node).Label, node.TargetCurve, quoteValue, node.InstrumentType)
    {
        if (!double.IsFinite(quoteValue))
        {
            throw new ArgumentOutOfRangeException(nameof(quoteValue), "Continuous zero yield must be finite.");
        }

        _node = node;
        _maturityYears = NominalMaturityYears(node);
    }

    /// <summary>Resolves the unadjusted calendar pillar corresponding to the quoted maturity.</summary>
    /// <param name="valuationDate">Date from which the tenor is measured.</param>
    /// <param name="referenceData">Reference data retained for the common helper contract.</param>
    /// <returns>The calendar date obtained by adding the tenor to the valuation date.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The tenor moves beyond the supported date range.</exception>
    public override DateOnly PillarDate(DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        return TenorParser.AddTenor(valuationDate, _node.Tenor);
    }

    /// <summary>Converts the quoted continuous zero yield into its terminal discount factor.</summary>
    /// <param name="partialCurveGroup">Previously calibrated state, unused for a direct zero quote.</param>
    /// <param name="valuationDate">Calibration valuation date used to validate the calendar pillar.</param>
    /// <param name="referenceData">Reference data retained for the common helper contract.</param>
    /// <returns><c>exp(-quote * nominalMaturityYears)</c>, including values above one for negative yields.</returns>
    /// <exception cref="InvalidOperationException">The exponential overflows or underflows to a non-positive value.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tenor moves beyond the supported date range.</exception>
    public override double SolveNodeValue(ICurveGroup partialCurveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        _ = PillarDate(valuationDate, referenceData);
        double discountFactor = Math.Exp(-QuoteValue * _maturityYears);
        if (!double.IsFinite(discountFactor) || discountFactor <= 0d)
        {
            throw new InvalidOperationException("Continuous zero yield does not produce a finite positive discount factor.");
        }

        return discountFactor;
    }

    /// <summary>Recovers the continuous zero yield from the target curve's actual pillar discount factor.</summary>
    /// <param name="curveGroup">Curve group supplying the target discount curve.</param>
    /// <param name="valuationDate">Date from which the pillar tenor is measured.</param>
    /// <param name="referenceData">Reference data retained for the common helper contract.</param>
    /// <returns><c>-log(discountFactor) / nominalMaturityYears</c>, or the original quote when the target discount curve is unavailable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="curveGroup"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The curve supplies a non-finite or non-positive discount factor.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tenor moves beyond the supported date range.</exception>
    public override double ImpliedQuote(ICurveGroup curveGroup, DateOnly valuationDate, IReferenceDataProvider referenceData)
    {
        double? discountFactor = TryGetDiscountFactor(curveGroup, PillarDate(valuationDate, referenceData));
        if (!discountFactor.HasValue)
        {
            return QuoteValue;
        }

        if (!double.IsFinite(discountFactor.Value) || discountFactor.Value <= 0d)
        {
            throw new InvalidOperationException("Continuous zero repricing requires a finite positive discount factor.");
        }

        return -Math.Log(discountFactor.Value) / _maturityYears;
    }

    private static double NominalMaturityYears(ResolvedNode node)
    {
        string? tenor = node.Tenor.Value?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(tenor) || tenor.Length < 2 || (tenor[^1] != 'M' && tenor[^1] != 'Y')
            || !int.TryParse(tenor[..^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int magnitude))
        {
            throw new NotSupportedException("Continuous zero quotes require an integer month or year tenor.");
        }

        if (magnitude <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(node), "Continuous zero maturity must be positive.");
        }

        return tenor[^1] == 'M' ? magnitude / 12d : magnitude;
    }
}
