using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Finance;

/// <summary>Governance of financial scenarios themselves.</summary>
public interface IFinancialScenarioValidationService : IReferenceValidationService<FinancialScenario>
{
}

/// <summary>The concrete <see cref="IFinancialScenarioValidationService"/> implementation.</summary>
/// <remarks>
/// The checks are about traceability, not about whether the numbers are
/// right. Whether a revenue forecast is achievable is a commercial
/// judgement; whether it says which assumptions it rests on, whether its
/// periods overlap, and whether a closed period is still carrying
/// forecasts are questions of record-keeping.
/// </remarks>
public sealed class FinancialScenarioValidationService
    : ReferenceValidationService<FinancialScenario>, IFinancialScenarioValidationService
{
    private readonly IFinancialAssumptionCatalog? _assumptions;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="FinancialScenarioValidationService"/> class.</summary>
    /// <param name="catalog">The scenario library whose records this service validates.</param>
    /// <param name="assumptions">The assumption library, for confirming that a named assumption exists. Optional.</param>
    /// <param name="timeProvider">The clock period-closure checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public FinancialScenarioValidationService(
        IFinancialScenarioCatalog catalog,
        IFinancialAssumptionCatalog? assumptions = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _assumptions = assumptions;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        FinancialScenario definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Scenario '{definition.Reference}' ({definition.Name})";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings, expectEvidence: false);

        EvaluatePeriods(definition, subject, errors, warnings);
        await EvaluateLinesAsync(definition, subject, today, errors, warnings, cancellationToken).ConfigureAwait(false);

        if (!definition.IsPlanningCase)
            warnings.Add(Diagnostic(
                FinanceValidationRules.ScenarioIsNotApproved,
                $"{subject} is not the approved planning case. That is expected of a conservative or stretch case and is reported "
                + "so that a scenario nobody approved is never mistaken for the one budgets are set against."));
    }

    private void EvaluatePeriods(
        FinancialScenario definition,
        string subject,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (definition.Periods.Count == 0)
        {
            errors.Add(Diagnostic(
                FinanceValidationRules.ScenarioMustHavePeriods,
                $"{subject} covers no periods, so nothing in it can be dated, totalled or compared."));

            return;
        }

        foreach (var duplicate in definition.Periods
                     .GroupBy(p => p.Label, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1)
                     .Select(g => g.Key))
            errors.Add(Diagnostic(
                FinanceValidationRules.DuplicatePeriodLabel,
                $"{subject} declares period '{duplicate}' more than once, so figures keyed to it are ambiguous."));

        var ordered = definition.Periods.OrderBy(p => p.Period.From).ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i - 1].Period.Overlaps(ordered[i].Period))
                errors.Add(Diagnostic(
                    FinanceValidationRules.OverlappingPeriods,
                    $"{subject} has overlapping periods '{ordered[i - 1].Label}' ({ordered[i - 1].Period}) and "
                    + $"'{ordered[i].Label}' ({ordered[i].Period}). A figure falling in both would be counted twice."));
        }

        foreach (var label in definition.LinesByPeriod.Keys.Where(
                     k => !definition.Periods.Any(p => string.Equals(p.Label, k, StringComparison.OrdinalIgnoreCase))))
            errors.Add(Diagnostic(
                FinanceValidationRules.UndeclaredPeriod,
                $"{subject} carries figures for period '{label}', which it does not declare."));
    }

    private async Task EvaluateLinesAsync(
        FinancialScenario definition,
        string subject,
        DateOnly today,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        foreach (var period in definition.Periods)
        {
            var lines = definition.LinesFor(period.Label);

            if (period.HasClosedBy(today) && lines.Any(l => l.Kind == FinancialFigureKind.Forecast))
                warnings.Add(Diagnostic(
                    FinanceValidationRules.ClosedPeriodStillForecast,
                    $"{subject} still carries forecasts for '{period.Label}', which ended on {period.Period.To:O}. A closed "
                    + "period should be carrying actuals."));

            foreach (var line in lines)
            {
                if (line.Amount.Currency != definition.Currency)
                    errors.Add(Diagnostic(
                        FinanceValidationRules.CurrencyMustMatchScenario,
                        $"A '{period.Label}' figure in {subject} is stated in {line.Amount.Currency} on a scenario stated in "
                        + $"{definition.Currency}: {line.Description}"));

                if (line.Kind == FinancialFigureKind.Unspecified)
                    errors.Add(Diagnostic(
                        FinanceValidationRules.FigureKindMustBeStated,
                        $"A '{period.Label}' figure in {subject} does not say whether it is an actual, a forecast or a budget, "
                        + $"so it cannot be compared with anything: {line.Description}"));

                if (line.IsUnsupportedActual)
                    errors.Add(Diagnostic(
                        FinanceValidationRules.ActualIsUnsupported,
                        $"A '{period.Label}' figure in {subject} is recorded as an actual with neither a source nor evidence. "
                        + $"An actual comes from an accounting record; this is a forecast that has been relabelled: {line.Description}"));

                if (line.Kind == FinancialFigureKind.Forecast && line.AssumptionReferences.Count == 0)
                    warnings.Add(Diagnostic(
                        FinanceValidationRules.ForecastHasNoAssumptions,
                        $"A '{period.Label}' forecast in {subject} names no assumption, so if it turns out wrong nobody can say "
                        + $"which input was wrong: {line.Description}"));

                if (!line.IsInflow && line.Amount.IsNegative)
                    warnings.Add(Diagnostic(
                        FinanceValidationRules.NegativeCostFigure,
                        $"A '{period.Label}' {line.Category} figure in {subject} is negative ({line.Amount}). That may be a "
                        + $"credit; it is more often a sign error: {line.Description}"));

                await EvaluateAssumptionReferencesAsync(line, subject, period.Label, warnings, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task EvaluateAssumptionReferencesAsync(
        FinancialLine line,
        string subject,
        string periodLabel,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_assumptions is null)
            return;

        foreach (var reference in line.AssumptionReferences)
        {
            if (await _assumptions.FindByReferenceAsync(reference, cancellationToken).ConfigureAwait(false) is null)
                warnings.Add(Diagnostic(
                    FinanceValidationRules.AssumptionReferenceMustResolve,
                    $"A '{periodLabel}' figure in {subject} rests on assumption '{reference}', which the assumption library does "
                    + "not hold."));
        }
    }
}

/// <summary>
/// How one scenario's expectations compared with what actually happened.
/// </summary>
/// <param name="ScenarioReference">The scenario compared.</param>
/// <param name="ScenarioPin">The exact scenario revision compared.</param>
/// <param name="PeriodLabel">The period compared.</param>
/// <param name="Variances">The variance for each category the period carries both an expectation and an actual for.</param>
/// <param name="CategoriesWithoutActuals">Categories that were forecast or budgeted and have no actual recorded.</param>
/// <param name="CategoriesWithoutExpectation">Categories with an actual and nothing that expected it.</param>
public sealed record VarianceReport(
    string ScenarioReference,
    ReferencePin ScenarioPin,
    string PeriodLabel,
    IReadOnlyList<FinancialVariance> Variances,
    IReadOnlyList<FinancialCategory> CategoriesWithoutActuals,
    IReadOnlyList<FinancialCategory> CategoriesWithoutExpectation)
{
    /// <summary>The variances that are bad for the organisation.</summary>
    public IReadOnlyList<FinancialVariance> AdverseVariances => Variances.Where(v => v.IsAdverse).ToList();

    /// <summary>Whether the period can be compared at all, or is simply incomplete.</summary>
    public bool IsComparable => Variances.Count > 0;
}

/// <summary>
/// Compares what a scenario expected against what was recorded, and
/// reports the difference.
/// </summary>
/// <remarks>
/// <b>This is financial control, not accounting.</b> It compares figures
/// somebody else recorded; it posts nothing, recognises nothing,
/// depreciates nothing, computes no tax, and produces no statement anybody
/// should file. Where a figure depends on an accountant, `P07` keeps the
/// dependency rather than inventing the answer.
/// </remarks>
public interface IFinancialControlService
{
    /// <summary>Compares expectation against actual for one period of one scenario.</summary>
    /// <param name="scenarioReference">The scenario to compare.</param>
    /// <param name="periodLabel">The period to compare.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <exception cref="ArgumentException">A string argument is null, empty, or whitespace, or the scenario does not declare the period.</exception>
    /// <exception cref="ReferenceRecordNotFoundException">No scenario is registered under <paramref name="scenarioReference"/>.</exception>
    Task<VarianceReport> CompareAsync(
        string scenarioReference,
        string periodLabel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares the same period across two scenarios, so a conservative
    /// case and a stretch case can be read side by side.
    /// </summary>
    /// <param name="firstReference">The first scenario.</param>
    /// <param name="secondReference">The second scenario.</param>
    /// <param name="periodLabel">The period both must declare.</param>
    /// <param name="kind">Which kind of figure to compare — usually Forecast.</param>
    /// <param name="cancellationToken">A token to observe while awaiting.</param>
    /// <returns>The total for each category in each scenario, and the difference.</returns>
    /// <exception cref="ArgumentException">A string argument is blank, or a scenario does not declare the period.</exception>
    /// <exception cref="CurrencyMismatchException">The two scenarios are stated in different currencies.</exception>
    /// <exception cref="ReferenceRecordNotFoundException">Either scenario is unregistered.</exception>
    Task<ScenarioComparison> CompareScenariosAsync(
        string firstReference,
        string secondReference,
        string periodLabel,
        FinancialFigureKind kind = FinancialFigureKind.Forecast,
        CancellationToken cancellationToken = default);
}

/// <summary>How two scenarios differ over one period.</summary>
/// <param name="PeriodLabel">The period compared.</param>
/// <param name="FirstReference">The first scenario.</param>
/// <param name="SecondReference">The second scenario.</param>
/// <param name="Kind">Which kind of figure was compared.</param>
/// <param name="Differences">The total for each category in each scenario, and the second less the first.</param>
public sealed record ScenarioComparison(
    string PeriodLabel,
    string FirstReference,
    string SecondReference,
    FinancialFigureKind Kind,
    IReadOnlyList<ScenarioCategoryDifference> Differences);

/// <summary>One category's totals in two scenarios.</summary>
/// <param name="Category">The category compared.</param>
/// <param name="First">The first scenario's total.</param>
/// <param name="Second">The second scenario's total.</param>
/// <param name="Difference">The second less the first.</param>
public sealed record ScenarioCategoryDifference(FinancialCategory Category, Money First, Money Second, Money Difference);

/// <summary>The concrete <see cref="IFinancialControlService"/> implementation.</summary>
public sealed class FinancialControlService : IFinancialControlService
{
    private readonly IFinancialScenarioCatalog _scenarios;

    /// <summary>Initialises a new instance of the <see cref="FinancialControlService"/> class.</summary>
    /// <param name="scenarios">The scenario library.</param>
    /// <exception cref="ArgumentNullException"><paramref name="scenarios"/> is <see langword="null"/>.</exception>
    public FinancialControlService(IFinancialScenarioCatalog scenarios)
    {
        ArgumentNullException.ThrowIfNull(scenarios);

        _scenarios = scenarios;
    }

    /// <inheritdoc />
    public async Task<VarianceReport> CompareAsync(
        string scenarioReference,
        string periodLabel,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(periodLabel);

        var record = await RequireAsync(scenarioReference, cancellationToken).ConfigureAwait(false);
        var scenario = record.Definition;

        RequirePeriod(scenario, periodLabel);

        var lines = scenario.LinesFor(periodLabel);
        var categories = lines.Select(l => l.Category).Distinct().OrderBy(c => c).ToList();

        var variances = new List<FinancialVariance>();
        var withoutActual = new List<FinancialCategory>();
        var withoutExpectation = new List<FinancialCategory>();

        foreach (var category in categories)
        {
            var actual = scenario.Total(periodLabel, category, FinancialFigureKind.Actual);
            var hasActual = lines.Any(l => l.Category == category && l.Kind == FinancialFigureKind.Actual);

            // A budget is the firmer expectation where both exist: it is
            // what somebody committed to, and a forecast is what somebody
            // expected.
            var expectedKind = lines.Any(l => l.Category == category && l.Kind == FinancialFigureKind.Budget)
                ? FinancialFigureKind.Budget
                : lines.Any(l => l.Category == category && l.Kind == FinancialFigureKind.Forecast)
                    ? FinancialFigureKind.Forecast
                    : (FinancialFigureKind?)null;

            if (expectedKind is null)
            {
                if (hasActual)
                    withoutExpectation.Add(category);

                continue;
            }

            if (!hasActual)
            {
                withoutActual.Add(category);

                continue;
            }

            var expected = scenario.Total(periodLabel, category, expectedKind.Value);

            variances.Add(new FinancialVariance(
                periodLabel,
                category,
                expected,
                expectedKind.Value,
                actual,
                actual - expected));
        }

        return new VarianceReport(
            scenario.Reference,
            ReferencePin.For(_scenarios.LibraryName, record),
            periodLabel,
            variances,
            withoutActual,
            withoutExpectation);
    }

    /// <inheritdoc />
    public async Task<ScenarioComparison> CompareScenariosAsync(
        string firstReference,
        string secondReference,
        string periodLabel,
        FinancialFigureKind kind = FinancialFigureKind.Forecast,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(secondReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(periodLabel);

        var first = (await RequireAsync(firstReference, cancellationToken).ConfigureAwait(false)).Definition;
        var second = (await RequireAsync(secondReference, cancellationToken).ConfigureAwait(false)).Definition;

        RequirePeriod(first, periodLabel);
        RequirePeriod(second, periodLabel);

        if (first.Currency != second.Currency)
            throw new CurrencyMismatchException(first.Currency, second.Currency);

        var categories = first.LinesFor(periodLabel).Select(l => l.Category)
            .Concat(second.LinesFor(periodLabel).Select(l => l.Category))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var differences = categories
            .Select(category =>
            {
                var a = first.Total(periodLabel, category, kind);
                var b = second.Total(periodLabel, category, kind);

                return new ScenarioCategoryDifference(category, a, b, b - a);
            })
            .ToList();

        return new ScenarioComparison(periodLabel, first.Reference, second.Reference, kind, differences);
    }

    private async Task<IReferenceRecord<FinancialScenario>> RequireAsync(string reference, CancellationToken cancellationToken) =>
        await _scenarios.FindByReferenceAsync(reference, cancellationToken).ConfigureAwait(false)
        ?? throw new ReferenceRecordNotFoundException(_scenarios.LibraryName, reference);

    private static void RequirePeriod(FinancialScenario scenario, string periodLabel)
    {
        if (!scenario.Periods.Any(p => string.Equals(p.Label, periodLabel, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException(
                $"Scenario '{scenario.Reference}' does not declare period '{periodLabel}'.",
                nameof(periodLabel));
    }
}
