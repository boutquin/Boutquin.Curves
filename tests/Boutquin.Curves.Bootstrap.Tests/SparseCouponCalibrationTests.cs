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

using Boutquin.Curves.Abstractions;
using Boutquin.Curves.Abstractions.Curves;
using Boutquin.Curves.Abstractions.Identifiers;
using Boutquin.Curves.Bootstrap.Definitions;
using Boutquin.Curves.Bootstrap.ReferenceData;
using Boutquin.Curves.Conventions;
using Boutquin.Curves.Interpolation;
using Boutquin.Domain.Enumerations;
using Boutquin.MarketData.Abstractions.ReferenceData;
using Boutquin.MarketData.Calendars.Holidays;
using Boutquin.MarketData.Conventions;
using Boutquin.MarketData.DayCount;
using FluentAssertions;

namespace Boutquin.Curves.Bootstrap.Tests;

public sealed class SparseCouponCalibrationTests
{
    [Theory]
    [InlineData(InterpolatorKind.MonotoneCubic)]
    public void Calibrate_WhenLaterInterpolationChangesEarlierCoupons_RejectsTheCompletedCurve(InterpolatorKind kind)
    {
        DateOnly valuation = new(2026, 4, 9);
        var curveRef = new CurveReference(CurveRole.Discount, Currency.USD);
        var references = new SimpleReferenceDataProvider(
            HolidayCalendarFactory.SupportedCodes.Select(HolidayCalendarFactory.Create).ToArray(),
            BenchmarkCatalog.CreateDefault().All(),
            new[] { InstrumentConventionRegistry.CreateDefault().GetRequired("USD-SOFR-OIS") });
        var nodes = new[]
        {
            new ResolvedNode("1Y", new Tenor("1Y"), "Ois", "USD-SOFR-OIS", curveRef, 0.02m),
            new ResolvedNode("3Y", new Tenor("3Y"), "Ois", "USD-SOFR-OIS", curveRef, 0.05m),
            new ResolvedNode("5Y", new Tenor("5Y"), "Ois", "USD-SOFR-OIS", curveRef, 0.03m)
        };
        var spec = new CurveCalibrationSpec(new CurveName("Nonlocal"), curveRef, CurveValueType.DiscountFactor,
            "ACT/365F", new InterpolationSettings(kind, "FlatZero", "FlatForward"), nodes);

        Action calibrate = () => new PiecewiseBootstrapCalibrator().Calibrate(new CurveCalibrationInput(valuation, new[] { spec }, references));

        calibrate.Should().Throw<InvalidOperationException>().WithMessage("*completed curve*simultaneous calibrator*");
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (string instrument in new[] { "Ois", "FixedFloatSwap", "BasisSwap" })
        {
            foreach (InterpolatorKind kind in new[] { InterpolatorKind.LogLinearDiscountFactor, InterpolatorKind.LinearZeroRate, InterpolatorKind.FlatForward })
            {
                foreach (bool negativeRates in new[] { false, true })
                {
                    yield return new object[] { instrument, kind, negativeRates };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Calibrate_RecoversIndependentSparsePillars_AndAllCouponParRates(
        string instrument, InterpolatorKind kind, bool negativeRates)
    {
        DateOnly valuation = new(2026, 4, 9);
        DateOnly first = valuation.AddYears(1);
        DateOnly terminal = valuation.AddYears(5);
        double firstDf = negativeRates ? 1.02 : 0.96;
        double terminalDf = negativeRates ? 1.13 : 0.75;
        var curveRef = new CurveReference(CurveRole.Discount, Currency.USD);
        string code = instrument == "Ois" ? "USD-SOFR-OIS" : "USD-FIXED-6M-30-360";
        var convention = InstrumentConventionRegistry.CreateDefault().GetRequired(code);
        var references = new SimpleReferenceDataProvider(
            HolidayCalendarFactory.SupportedCodes.Select(HolidayCalendarFactory.Create).ToArray(),
            BenchmarkCatalog.CreateDefault().All(), new[] { convention });

        // Quotes come from explicit interpolation identities and the cash-flow par equation,
        // without any production interpolator, discount curve, helper, or root solver.
        double ExpectedDf(DateOnly date)
        {
            double t = date.DayNumber - valuation.DayNumber;
            double t1 = first.DayNumber - valuation.DayNumber;
            double t5 = terminal.DayNumber - valuation.DayNumber;
            if (date <= first)
            {
                return Math.Pow(firstDf, t / t1);
            }

            double weight = (t - t1) / (t5 - t1);
            return kind == InterpolatorKind.LinearZeroRate
                ? Math.Exp(t * ((1 - weight) * Math.Log(firstDf) / t1 + weight * Math.Log(terminalDf) / t5))
                : Math.Pow(firstDf, 1 - weight) * Math.Pow(terminalDf, weight);
        }

        IReadOnlyList<SchedulePeriod> Schedule(DateOnly end) => convention switch
        {
            OisConvention ois => ScheduleGenerator.Generate(valuation, end, ois.PaymentFrequency,
                HolidayCalendarFactory.Create(ois.CalendarCode), ois.BusinessDayAdjustment,
                DayCountResolver.Resolve(ois.DayCountCode), ois.PaymentLagBusinessDays),
            SwapLegConvention swap => ScheduleGenerator.Generate(valuation, end, swap.PaymentFrequency,
                HolidayCalendarFactory.Create(swap.CalendarCode), swap.BusinessDayAdjustment,
                DayCountResolver.Resolve(swap.DayCountCode), swap.PaymentLagBusinessDays, swap.EndOfMonth),
            _ => throw new InvalidOperationException("Unexpected test convention.")
        };

        double ParRate(DateOnly end)
        {
            var schedule = Schedule(end);
            double annuity = schedule.Take(schedule.Count - 1).Sum(period => period.YearFraction * ExpectedDf(period.PeriodEndDate));
            annuity += schedule[^1].YearFraction * ExpectedDf(end);
            return (1 - ExpectedDf(end)) / annuity;
        }

        var nodes = new[]
        {
            new ResolvedNode("1Y", new Tenor("1Y"), instrument, code, curveRef, (decimal)ParRate(first)),
            new ResolvedNode("5Y", new Tenor("5Y"), instrument, code, curveRef, (decimal)ParRate(terminal))
        };
        var spec = new CurveCalibrationSpec(new CurveName("Sparse"), curveRef, CurveValueType.DiscountFactor,
            "ACT/365F", new InterpolationSettings(kind, "FlatZero", "FlatForward"), nodes);
        var result = new PiecewiseBootstrapCalibrator().Calibrate(new CurveCalibrationInput(valuation, new[] { spec }, references));
        var curve = (IDiscountCurve)result.CurveGroup.GetCurve(curveRef);

        curve.DiscountFactor(first).Should().BeApproximately(firstDf, 1e-11);
        curve.DiscountFactor(terminal).Should().BeApproximately(terminalDf, 1e-11);
        result.Diagnostics.Repricing.Should().AllSatisfy(item => item.AbsoluteError.Should().BeLessThan(1e-10));
        foreach (DateOnly end in new[] { first, terminal })
        {
            var schedule = Schedule(end);
            double annuity = schedule.Take(schedule.Count - 1).Sum(period => period.YearFraction * curve.DiscountFactor(period.PeriodEndDate));
            annuity += schedule[^1].YearFraction * curve.DiscountFactor(end);
            double actualPar = (1 - curve.DiscountFactor(end)) / annuity;
            actualPar.Should().BeApproximately(ParRate(end), 1e-11);
        }
    }
}
