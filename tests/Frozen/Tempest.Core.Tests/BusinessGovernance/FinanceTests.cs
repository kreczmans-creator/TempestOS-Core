using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Finance;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.BusinessGovernance;

// C5 must keep actual, budget, forecast and assumption apart, and must
// stay out of accounting.
public class FinanceTests
{
    private static DateOnly Today => BusinessGovernanceFixtures.Today;

    private static Money Gbp(decimal amount) => BusinessGovernanceFixtures.Gbp_(amount);

    private static FinancialLine Line(
        FinancialCategory category,
        FinancialFigureKind kind,
        decimal amount,
        string? source = "Fixture source.",
        IReadOnlyList<string>? assumptions = null) =>
        new(category, kind, Gbp(amount), $"Fixture {category} {kind}.", source, assumptions ?? ["ASM-1"]);

    private static FinancialScenario ScenarioWith(params FinancialLine[] lines)
    {
        var period = BusinessGovernanceFixtures.Period();

        return BusinessGovernanceFixtures.Scenario(periods: period) with
        {
            LinesByPeriod = new Dictionary<string, IReadOnlyList<FinancialLine>>(StringComparer.OrdinalIgnoreCase)
            {
                [period.Label] = lines,
            },
        };
    }

    [Fact]
    public void APeriodWithNoEnd_CannotBeConstructed()
    {
        // An unbounded period cannot be totalled, compared or closed.
        Assert.Throws<ArgumentException>(() =>
            new FinancialPeriod("Forever", new EffectivePeriod(Today, null)));
    }

    [Fact]
    public void TotallingIsExactAndDeterministic()
    {
        var scenario = ScenarioWith(
            Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 0.1m),
            Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 0.2m));

        var total = scenario.Total("FY26 Q1", FinancialCategory.Revenue, FinancialFigureKind.Forecast);

        Assert.Equal(0.3m, total.Amount);
        Assert.Equal(total, scenario.Total("FY26 Q1", FinancialCategory.Revenue, FinancialFigureKind.Forecast));
    }

    [Fact]
    public void AForecastTotalDoesNotIncludeActuals()
    {
        var scenario = ScenarioWith(
            Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 100m),
            Line(FinancialCategory.Revenue, FinancialFigureKind.Actual, 90m));

        Assert.Equal(Gbp(100m), scenario.Total("FY26 Q1", FinancialCategory.Revenue, FinancialFigureKind.Forecast));
        Assert.Equal(Gbp(90m), scenario.Total("FY26 Q1", FinancialCategory.Revenue, FinancialFigureKind.Actual));
    }

    [Fact]
    public void IndicativeMarginIsRevenueLessEveryCostCategory()
    {
        var scenario = ScenarioWith(
            Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 100_000m),
            Line(FinancialCategory.StaffCost, FinancialFigureKind.Forecast, 60_000m),
            Line(FinancialCategory.Overhead, FinancialFigureKind.Forecast, 15_000m));

        Assert.Equal(Gbp(25_000m), scenario.IndicativeMargin("FY26 Q1", FinancialFigureKind.Forecast));
    }

    [Fact]
    public void AnActualWithNoSourceAndNoEvidence_IsAForecastRelabelled()
    {
        var line = Line(FinancialCategory.Revenue, FinancialFigureKind.Actual, 100m, source: null);

        Assert.True(line.IsUnsupportedActual);
    }

    [Fact]
    public async Task AnUnsupportedActual_IsAnError()
    {
        var result = await ValidateAsync(ScenarioWith(
            Line(FinancialCategory.Revenue, FinancialFigureKind.Actual, 100m, source: null)));

        Assert.Contains(FinanceValidationRules.ActualIsUnsupported, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AForecastRestingOnNoAssumption_IsReported()
    {
        var result = await ValidateAsync(ScenarioWith(
            Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 100m, assumptions: [])));

        Assert.Contains(FinanceValidationRules.ForecastHasNoAssumptions, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AFigureWithNoStatedKind_IsAnError()
    {
        var result = await ValidateAsync(ScenarioWith(
            Line(FinancialCategory.Revenue, FinancialFigureKind.Unspecified, 100m)));

        Assert.Contains(FinanceValidationRules.FigureKindMustBeStated, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AFigureInAnotherCurrency_IsAnError()
    {
        var period = BusinessGovernanceFixtures.Period();
        var scenario = BusinessGovernanceFixtures.Scenario(periods: period) with
        {
            LinesByPeriod = new Dictionary<string, IReadOnlyList<FinancialLine>>(StringComparer.OrdinalIgnoreCase)
            {
                [period.Label] =
                [
                    new FinancialLine(
                        FinancialCategory.Revenue, FinancialFigureKind.Forecast,
                        new Money(100m, new CurrencyCode("EUR")), "Fixture euro line.", "Fixture source.", ["ASM-1"]),
                ],
            },
        };

        var result = await ValidateAsync(scenario);

        Assert.Contains(FinanceValidationRules.CurrencyMustMatchScenario, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task OverlappingPeriods_AreAnError()
    {
        var scenario = BusinessGovernanceFixtures.Scenario(
            periods:
            [
                new FinancialPeriod("Q1", new EffectivePeriod(Today, Today.AddMonths(3))),
                new FinancialPeriod("Q2", new EffectivePeriod(Today.AddMonths(2), Today.AddMonths(5))),
            ]);

        var result = await ValidateAsync(scenario);

        Assert.Contains(FinanceValidationRules.OverlappingPeriods, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task FiguresKeyedToAnUndeclaredPeriod_AreAnError()
    {
        var scenario = BusinessGovernanceFixtures.Scenario(periods: BusinessGovernanceFixtures.Period()) with
        {
            LinesByPeriod = new Dictionary<string, IReadOnlyList<FinancialLine>>(StringComparer.OrdinalIgnoreCase)
            {
                ["FY99 Q4"] = [Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 100m)],
            },
        };

        var result = await ValidateAsync(scenario);

        Assert.Contains(FinanceValidationRules.UndeclaredPeriod, result.Errors.Select(d => d.Code));
    }

    [Fact]
    public async Task AClosedPeriodStillCarryingForecasts_IsReported()
    {
        var closed = new FinancialPeriod("FY25 Q4", new EffectivePeriod(Today.AddMonths(-6), Today.AddMonths(-3)));
        var scenario = BusinessGovernanceFixtures.Scenario(periods: closed) with
        {
            LinesByPeriod = new Dictionary<string, IReadOnlyList<FinancialLine>>(StringComparer.OrdinalIgnoreCase)
            {
                [closed.Label] = [Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 100m)],
            },
        };

        var result = await ValidateAsync(scenario);

        Assert.Contains(FinanceValidationRules.ClosedPeriodStillForecast, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task ANegativeCostFigure_IsReportedAsALikelySignError()
    {
        var result = await ValidateAsync(ScenarioWith(
            Line(FinancialCategory.StaffCost, FinancialFigureKind.Forecast, -1000m)));

        Assert.Contains(FinanceValidationRules.NegativeCostFigure, result.Warnings.Select(d => d.Code));
    }

    [Fact]
    public async Task AnAssumptionWithAValueAndNoUnit_IsAnError()
    {
        var catalog = BusinessGovernanceFixtures.BuildAssumptionCatalog();
        var service = new FinancialAssumptionValidationService(catalog, BusinessGovernanceFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            BusinessGovernanceFixtures.Assumption() with { Unit = null },
            BusinessGovernanceFixtures.Verified());

        Assert.Contains(FinanceValidationRules.AssumptionHasNoUnit, result.Errors.Select(d => d.Code));
    }

    private static async Task<IValidationResult> ValidateAsync(FinancialScenario scenario)
    {
        var scenarios = BusinessGovernanceFixtures.BuildScenarioCatalog();
        var service = new FinancialScenarioValidationService(scenarios, null, BusinessGovernanceFixtures.Clock());

        return await service.ValidateDefinitionAsync(scenario, BusinessGovernanceFixtures.Verified());
    }

    private static async Task<(FinancialScenarioCatalog Catalog, FinancialControlService Service)> BuildAsync(
        params (string Reference, FinancialScenario Scenario)[] scenarios)
    {
        var catalog = BusinessGovernanceFixtures.BuildScenarioCatalog();

        foreach (var (reference, scenario) in scenarios)
            await catalog.RegisterAsync(reference, scenario, BusinessGovernanceFixtures.Verified());

        return (catalog, new FinancialControlService(catalog));
    }

    [Fact]
    public async Task AVarianceComparesActualAgainstTheFirmestExpectation()
    {
        // A budget is what somebody committed to; a forecast is what
        // somebody expected. Where both exist, the budget is the
        // comparison.
        var scenario = ScenarioWith(
            Line(FinancialCategory.Revenue, FinancialFigureKind.Budget, 100_000m),
            Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 90_000m),
            Line(FinancialCategory.Revenue, FinancialFigureKind.Actual, 80_000m));

        var (_, service) = await BuildAsync(("scn-1", scenario));

        var report = await service.CompareAsync("SCN-1", "FY26 Q1");
        var variance = Assert.Single(report.Variances);

        Assert.Equal(FinancialFigureKind.Budget, variance.ExpectedKind);
        Assert.Equal(Gbp(-20_000m), variance.Variance);
        Assert.True(variance.IsAdverse);
    }

    [Fact]
    public async Task LessCostThanExpected_IsNotAdverse()
    {
        // Treating every negative number as bad reports an under-spend as
        // a problem.
        var scenario = ScenarioWith(
            Line(FinancialCategory.Overhead, FinancialFigureKind.Budget, 50_000m),
            Line(FinancialCategory.Overhead, FinancialFigureKind.Actual, 40_000m));

        var (_, service) = await BuildAsync(("scn-1", scenario));

        var variance = Assert.Single((await service.CompareAsync("SCN-1", "FY26 Q1")).Variances);

        Assert.False(variance.IsAdverse);
    }

    [Fact]
    public async Task ACategoryWithNoActual_IsReportedRatherThanComparedAgainstZero()
    {
        var scenario = ScenarioWith(Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 100_000m));

        var (_, service) = await BuildAsync(("scn-1", scenario));

        var report = await service.CompareAsync("SCN-1", "FY26 Q1");

        Assert.Empty(report.Variances);
        Assert.Contains(FinancialCategory.Revenue, report.CategoriesWithoutActuals);
        Assert.False(report.IsComparable);
    }

    [Fact]
    public async Task AnActualNobodyExpected_IsReported()
    {
        var scenario = ScenarioWith(Line(FinancialCategory.Expenses, FinancialFigureKind.Actual, 5_000m));

        var (_, service) = await BuildAsync(("scn-1", scenario));

        var report = await service.CompareAsync("SCN-1", "FY26 Q1");

        Assert.Contains(FinancialCategory.Expenses, report.CategoriesWithoutExpectation);
    }

    [Fact]
    public void AVarianceAgainstAZeroExpectation_IsRefusedRatherThanReportedAsInfinite()
    {
        var variance = new FinancialVariance(
            "FY26 Q1", FinancialCategory.Revenue, Gbp(0m), FinancialFigureKind.Forecast, Gbp(100m), Gbp(100m));

        Assert.Null(variance.VarianceProportion);
        Assert.False(variance.ExceedsProportion(0.1m));
    }

    [Fact]
    public async Task TwoScenariosCanBeComparedSideBySide()
    {
        var conservative = ScenarioWith(Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 200_000m));
        var stretch = ScenarioWith(Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 350_000m)) with
        {
            Reference = "SCN-2",
            Name = "Fixture stretch case",
        };

        var (_, service) = await BuildAsync(("scn-1", conservative), ("scn-2", stretch));

        var comparison = await service.CompareScenariosAsync("SCN-1", "SCN-2", "FY26 Q1");
        var difference = Assert.Single(comparison.Differences);

        Assert.Equal(Gbp(150_000m), difference.Difference);
    }

    [Fact]
    public async Task ComparingScenariosInDifferentCurrencies_IsRefused()
    {
        var sterling = ScenarioWith(Line(FinancialCategory.Revenue, FinancialFigureKind.Forecast, 100m));
        var euro = BusinessGovernanceFixtures.Scenario("SCN-2", periods: BusinessGovernanceFixtures.Period()) with
        {
            Currency = new CurrencyCode("EUR"),
        };

        var (_, service) = await BuildAsync(("scn-1", sterling), ("scn-2", euro));

        await Assert.ThrowsAsync<CurrencyMismatchException>(
            () => service.CompareScenariosAsync("SCN-1", "SCN-2", "FY26 Q1"));
    }

    [Fact]
    public async Task ComparingAPeriodAScenarioDoesNotDeclare_IsRefused()
    {
        var (_, service) = await BuildAsync(("scn-1", ScenarioWith()));

        await Assert.ThrowsAsync<ArgumentException>(() => service.CompareAsync("SCN-1", "FY99 Q4"));
    }

    [Fact]
    public async Task AVarianceReportPinsTheScenarioRevisionItRead()
    {
        var (_, service) = await BuildAsync(("scn-1", ScenarioWith(
            Line(FinancialCategory.Revenue, FinancialFigureKind.Budget, 100m),
            Line(FinancialCategory.Revenue, FinancialFigureKind.Actual, 100m))));

        var report = await service.CompareAsync("SCN-1", "FY26 Q1");

        Assert.Equal("BusinessFinancialScenarios", report.ScenarioPin.Library);
        Assert.Equal(1, report.ScenarioPin.RevisionNumber);
    }

    [Fact]
    public void NothingInTheControlServiceIsAnAccountingOperation()
    {
        var methods = typeof(IFinancialControlService).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain(methods, name =>
            name.Contains("Post", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Recognise", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Tax", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Depreciat", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Invoice", StringComparison.OrdinalIgnoreCase));
    }
}
