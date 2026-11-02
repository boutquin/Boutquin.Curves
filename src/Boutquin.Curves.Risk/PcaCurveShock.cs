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
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using Boutquin.Numerics.LinearAlgebra;

namespace Boutquin.Curves.Risk;

/// <summary>
/// Applies a PCA-based curve shock by perturbing zero rates along a principal component eigenvector.
/// </summary>
/// <remarks>
/// <para>
/// The shock is constructed from a <see cref="PcaResult"/> produced by
/// <c>Boutquin.Numerics.LinearAlgebra.PrincipalComponentAnalysis.Decompose</c>. The covariance must describe rate changes in basis points squared. Unit eigenvector columns
/// encode component directions; multiplying by the square root of the selected eigenvalue and
/// <see cref="StandardDeviations"/> gives each tenor's shift in basis points. For a standardized
/// fit, the fitted per-variable scales restore basis-point units.
/// </para>
/// <para>
/// Non-discount curves are returned unchanged. For discount curves, the shocked discount factor
/// at time <em>t</em> is:
/// </para>
/// <code>
/// DF_shocked(t) = DF_base(t) * exp(-shift(t) * t)
/// </code>
/// <para>
/// where <c>shift(t)</c> is the linearly interpolated zero-rate shift (in decimal) at time <em>t</em>.
/// Flat extrapolation is used outside the tenor grid.
/// </para>
/// </remarks>
public sealed record PcaCurveShock : CurveShockScenario
{
    private readonly double[] _tenorYears;
    private readonly double[] _shiftsBps;

    /// <summary>
    /// The principal component applied by this shock.
    /// </summary>
    public PrincipalComponent Component { get; }

    /// <summary>
    /// Number of standard deviations applied along the selected eigenvector.
    /// </summary>
    public decimal StandardDeviations { get; }

    /// <summary>
    /// Initializes a new <see cref="PcaCurveShock"/> from a PCA factor model.
    /// </summary>
    /// <param name="component">Which principal component to apply.</param>
    /// <param name="standardDeviations">Number of standard deviations to shock along that component.</param>
    /// <param name="factorModel">
    /// PCA result from <c>PrincipalComponentAnalysis.Decompose(covarianceMatrix)</c>.
    /// Eigenvectors are columns ordered by descending variance explained.
    /// </param>
    /// <param name="tenorGrid">
    /// Tenor grid aligning the eigenvector rows to market tenors (e.g. "1Y", "2Y", "5Y", "10Y").
    /// Must have the same length as the number of rows in <paramref name="factorModel"/>.Eigenvectors,
    /// and resolve to strictly increasing positive year fractions. Accepted units are D, W, M, Y,
    /// with ON, O/N and 1WK aliases; numeric magnitudes use invariant decimal notation.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="factorModel"/> or <paramref name="tenorGrid"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="tenorGrid"/> is empty, has an invalid or unordered tenor, does not
    /// match the eigenvector row count, or when the selected variance or fitted scales are invalid.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="component"/> is not a defined <see cref="PrincipalComponent"/> value, or
    /// when its index is beyond the number of eigenvector columns in <paramref name="factorModel"/>
    /// (e.g. requesting <see cref="PrincipalComponent.Curvature"/> against a model with fewer than 3 components).
    /// </exception>
    public PcaCurveShock(
        PrincipalComponent component,
        decimal standardDeviations,
        PcaResult factorModel,
        IReadOnlyList<Tenor> tenorGrid)
        : base($"PCA-{component}+{standardDeviations}σ")
    {
        ArgumentNullException.ThrowIfNull(factorModel);
        ArgumentNullException.ThrowIfNull(tenorGrid);

        if (!Enum.IsDefined(component))
        {
            throw new ArgumentOutOfRangeException(
                nameof(component), component, "Undefined principal component value.");
        }

        decimal[,] eigenvectors = factorModel.Eigenvectors;
        decimal[] eigenvalues = factorModel.Eigenvalues;
        decimal[] scales = factorModel.StandardDeviations;
        int n = tenorGrid.Count;
        int eigRows = eigenvectors.GetLength(0);
        if (n == 0 || n != eigRows)
        {
            throw new ArgumentException(
                $"tenorGrid length ({n}) must match eigenvector row count ({eigRows}).",
                nameof(tenorGrid));
        }

        int componentIndex = (int)component;
        int eigCols = eigenvectors.GetLength(1);
        if (componentIndex >= eigCols)
        {
            throw new ArgumentOutOfRangeException(
                nameof(component),
                component,
                $"Principal component index ({componentIndex}) is out of range for the eigenvector column count ({eigCols}).");
        }

        if (componentIndex >= eigenvalues.Length || eigenvalues[componentIndex] < 0m ||
            (scales.Length != 0 && (scales.Length != n || scales.Any(static scale => scale <= 0m))))
        {
            throw new ArgumentException("PCA variance must be non-negative and fitted scales must be positive and match the tenor grid.", nameof(factorModel));
        }

        double standardDeviation = Math.Sqrt((double)eigenvalues[componentIndex]);
        Component = component;
        StandardDeviations = standardDeviations;

        _tenorYears = new double[n];
        _shiftsBps = new double[n];

        for (int i = 0; i < n; i++)
        {
            _tenorYears[i] = TenorToYears(tenorGrid[i]);
            if (i > 0 && _tenorYears[i] <= _tenorYears[i - 1])
            {
                throw new ArgumentException("Tenor grid must be strictly increasing with no equivalent maturities.", nameof(tenorGrid));
            }

            double scale = scales.Length == 0 ? 1d : (double)scales[i];
            _shiftsBps[i] = (double)eigenvectors[i, componentIndex] * (double)standardDeviations * standardDeviation * scale;
        }
    }

    /// <summary>
    /// Applies the PCA shock to a curve.
    /// </summary>
    /// <param name="curve">Curve to shock.</param>
    /// <returns>
    /// A shocked <see cref="IDiscountCurve"/> when the input is a discount curve;
    /// otherwise the original curve unchanged.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="curve"/> is <c>null</c>.</exception>
    public override ICurve Apply(ICurve curve)
    {
        ArgumentNullException.ThrowIfNull(curve);

        if (curve is IDiscountCurve discountCurve)
        {
            return new PcaShiftedDiscountCurve(discountCurve, _tenorYears, _shiftsBps);
        }

        return curve;
    }

    /// <summary>
    /// Converts a <see cref="Tenor"/> to a year fraction using a simple market-convention heuristic.
    /// </summary>
    private static double TenorToYears(Tenor tenor)
    {
        string code = tenor.Value.Trim().ToUpperInvariant();

        if (code is "ON" or "O/N")
        {
            return 1.0 / 365.0;
        }

        if (code == "1W" || code == "1WK")
        {
            return 7.0 / 365.0;
        }

        if (code.Length < 2 ||
            !double.TryParse(code[..^1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double num) ||
            !double.IsFinite(num) || num <= 0d)
        {
            throw new ArgumentException($"Unsupported positive tenor: '{tenor.Value}'.", nameof(tenor));
        }

        return code[^1] switch
        {
            'D' => num / 365.0,
            'W' => num * 7.0 / 365.0,
            'M' => num / 12.0,
            'Y' => num,
            _ => throw new ArgumentException($"Unsupported tenor unit: '{tenor.Value}'.", nameof(tenor))
        };
    }

    /// <summary>
    /// Wraps a discount curve and applies per-tenor zero-rate shifts from a PCA eigenvector.
    /// </summary>
    private sealed class PcaShiftedDiscountCurve : IDiscountCurve
    {
        private readonly IDiscountCurve _inner;
        private readonly double[] _tenorYears;
        private readonly double[] _shiftsBps;

        public PcaShiftedDiscountCurve(
            IDiscountCurve inner,
            double[] tenorYears,
            double[] shiftsBps)
        {
            _inner = inner;
            _tenorYears = tenorYears;
            _shiftsBps = shiftsBps;
        }

        public CurveName Name => _inner.Name;

        public DateOnly ValuationDate => _inner.ValuationDate;

        public Currency Currency => _inner.Currency;

        public IYearFractionCalculator DayCount => _inner.DayCount;

        public double ValueAt(DateOnly date) => DiscountFactor(date);

        public double DiscountFactor(DateOnly date)
        {
            if (date <= ValuationDate)
            {
                return _inner.DiscountFactor(date);
            }

            double t = DayCount.YearFraction(ValuationDate, date);
            // Interpolate shift at this maturity; eigenvector values are in bps
            double shiftDecimal = Interpolate(t) / 10_000.0;

            return _inner.DiscountFactor(date) * Math.Exp(-shiftDecimal * t);
        }

        public double ZeroRate(DateOnly date, CompoundingConvention compounding)
        {
            if (date <= ValuationDate)
            {
                return 0d;
            }

            double t = Math.Max(1e-12d, DayCount.YearFraction(ValuationDate, date));
            double df = DiscountFactor(date);

            return compounding switch
            {
                CompoundingConvention.Continuous => -Math.Log(df) / t,
                CompoundingConvention.Simple => (1d / df - 1d) / t,
                CompoundingConvention.Annual => Math.Pow(1d / df, 1d / t) - 1d,
                CompoundingConvention.SemiAnnual => 2d * (Math.Pow(1d / df, 1d / (2d * t)) - 1d),
                CompoundingConvention.Quarterly => 4d * (Math.Pow(1d / df, 1d / (4d * t)) - 1d),
                CompoundingConvention.Monthly => 12d * (Math.Pow(1d / df, 1d / (12d * t)) - 1d),
                _ => throw new ArgumentOutOfRangeException(nameof(compounding), compounding,
                    $"Unsupported compounding convention: {compounding}."),
            };
        }

        public double InstantaneousForwardRate(DateOnly date)
        {
            if (date <= ValuationDate)
            {
                return _inner.InstantaneousForwardRate(date);
            }

            double t = DayCount.YearFraction(ValuationDate, date);
            double shiftDecimal = Interpolate(t) / 10_000.0;

            // -d log(P_base(t) exp(-s(t)t))/dt = f_base(t) + s(t) + t s'(t).
            // At a tenor knot use the right-hand derivative; flat extrapolation has zero slope.
            double slopeBps = 0d;
            for (int i = 1; i < _tenorYears.Length; i++)
            {
                if (t >= _tenorYears[i - 1] && t < _tenorYears[i])
                {
                    slopeBps = (_shiftsBps[i] - _shiftsBps[i - 1]) / (_tenorYears[i] - _tenorYears[i - 1]);
                    break;
                }
            }

            return _inner.InstantaneousForwardRate(date) + shiftDecimal + t * slopeBps / 10_000d;
        }

        public IDiscountCurve WithValuationDate(DateOnly newValuationDate)
        {
            return new PcaShiftedDiscountCurve(
                _inner.WithValuationDate(newValuationDate),
                _tenorYears,
                _shiftsBps);
        }

        /// <summary>
        /// Linearly interpolates the shift at year fraction <paramref name="t"/>,
        /// with flat extrapolation outside the tenor grid.
        /// </summary>
        private double Interpolate(double t)
        {
            int n = _tenorYears.Length;

            if (n == 0)
            {
                return 0.0;
            }

            if (n == 1)
            {
                return _shiftsBps[0];
            }

            // Flat extrapolation below first grid point
            if (t <= _tenorYears[0])
            {
                return _shiftsBps[0];
            }

            // Flat extrapolation above last grid point
            if (t >= _tenorYears[n - 1])
            {
                return _shiftsBps[n - 1];
            }

            // Binary search for segment
            int lo = 0;
            int hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (_tenorYears[mid] <= t)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            double t0 = _tenorYears[lo];
            double t1 = _tenorYears[hi];
            double alpha = (t - t0) / (t1 - t0);
            return _shiftsBps[lo] + alpha * (_shiftsBps[hi] - _shiftsBps[lo]);
        }
    }
}
