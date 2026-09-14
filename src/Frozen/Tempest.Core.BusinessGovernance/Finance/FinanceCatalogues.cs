using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessGovernance.Finance;

/// <summary>A deterministic filter over the financial-assumption library.</summary>
public sealed record FinancialAssumptionQuery
{
    /// <summary>Matches any assumption whose reference or statement contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these determination states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<DeterminationState> States { get; init; } = [];

    /// <summary>Matches assumptions applying on this date, and assumptions applying generally. <see langword="null"/> to match any.</summary>
    public DateOnly? ApplyingOn { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The library of financial assumptions.</summary>
public interface IFinancialAssumptionCatalog : IReferenceDataCatalog<FinancialAssumption>
{
    /// <summary>Returns the assumption registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<FinancialAssumption>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered assumption matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<FinancialAssumption>>> SearchAsync(
        FinancialAssumptionQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IFinancialAssumptionCatalog"/> implementation.</summary>
public sealed class FinancialAssumptionCatalog : ReferenceDataCatalog<FinancialAssumption>, IFinancialAssumptionCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every assumption record's own backing document carries.</summary>
    public const string FinancialAssumptionDocumentKind = "BusinessFinancialAssumption";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>assumptionId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessFinancialAssumptions.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each assumption reference to the <c>assumptionId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessFinancialAssumptions.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="FinancialAssumptionCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own assumption records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public FinancialAssumptionCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessFinancialAssumptions";

    /// <inheritdoc />
    public override string DocumentKind => FinancialAssumptionDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<FinancialAssumption>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(FinancialAssumption.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<FinancialAssumption>>> SearchAsync(
        FinancialAssumptionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(FinancialAssumption definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(FinancialAssumption definition) => $"Assumption reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<FinancialAssumption> record, FinancialAssumptionQuery query)
    {
        var assumption = record.Definition;

        if (query.TextContains is { } text
            && !assumption.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !assumption.Statement.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.States.Count > 0 && !query.States.Contains(assumption.State))
            return false;

        if (query.ApplyingOn is { } date && assumption.AppliesOver is { } period && !period.Contains(date))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>A deterministic filter over the financial-scenario library.</summary>
public sealed record FinancialScenarioQuery
{
    /// <summary>Matches any scenario whose reference, name or purpose contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches scenarios stated in this currency. <see langword="null"/> to match any.</summary>
    public CurrencyCode? Currency { get; init; }

    /// <summary>Matches only the approved planning case, or only the others. <see langword="null"/> to match any.</summary>
    public bool? IsPlanningCase { get; init; }

    /// <summary>Matches scenarios covering a period containing this date. <see langword="null"/> to match any.</summary>
    public DateOnly? CoveringDate { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The library of financial scenarios.</summary>
public interface IFinancialScenarioCatalog : IReferenceDataCatalog<FinancialScenario>
{
    /// <summary>Returns the scenario registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<FinancialScenario>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered scenario matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<FinancialScenario>>> SearchAsync(
        FinancialScenarioQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IFinancialScenarioCatalog"/> implementation.</summary>
public sealed class FinancialScenarioCatalog : ReferenceDataCatalog<FinancialScenario>, IFinancialScenarioCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every scenario record's own backing document carries.</summary>
    public const string FinancialScenarioDocumentKind = "BusinessFinancialScenario";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>scenarioId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessFinancialScenarios.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each scenario reference to the <c>scenarioId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessFinancialScenarios.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="FinancialScenarioCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own scenario records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public FinancialScenarioCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => "BusinessFinancialScenarios";

    /// <inheritdoc />
    public override string DocumentKind => FinancialScenarioDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<FinancialScenario>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(FinancialScenario.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<FinancialScenario>>> SearchAsync(
        FinancialScenarioQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(FinancialScenario definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(FinancialScenario definition) => $"Scenario reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<FinancialScenario> record, FinancialScenarioQuery query)
    {
        var scenario = record.Definition;

        if (query.TextContains is { } text
            && !scenario.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !scenario.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !scenario.Purpose.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Currency is { } currency && scenario.Currency != currency)
            return false;

        if (query.IsPlanningCase is { } planning && scenario.IsPlanningCase != planning)
            return false;

        if (query.CoveringDate is { } date && !scenario.Periods.Any(p => p.Contains(date)))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes C5's validation services report.</summary>
public static class FinanceValidationRules
{
    /// <summary>The assumption does not say where it came from.</summary>
    public const string AssumptionHasNoSource = "TEMPEST-BGF-001";

    /// <summary>The assumption states neither a money amount nor a value, so nothing can be computed from it.</summary>
    public const string AssumptionStatesNoValue = "TEMPEST-BGF-002";

    /// <summary>A numeric assumption does not say what it is measured in.</summary>
    public const string AssumptionHasNoUnit = "TEMPEST-BGF-003";

    /// <summary>Two assumptions share one reference.</summary>
    public const string DuplicateAssumptionReference = "TEMPEST-BGF-004";

    /// <summary>The scenario covers no periods.</summary>
    public const string ScenarioMustHavePeriods = "TEMPEST-BGF-005";

    /// <summary>Two periods in one scenario share a label.</summary>
    public const string DuplicatePeriodLabel = "TEMPEST-BGF-006";

    /// <summary>Two periods in one scenario overlap.</summary>
    public const string OverlappingPeriods = "TEMPEST-BGF-007";

    /// <summary>Figures are keyed to a period the scenario does not declare.</summary>
    public const string UndeclaredPeriod = "TEMPEST-BGF-008";

    /// <summary>A figure is stated in a currency other than the scenario's own.</summary>
    public const string CurrencyMustMatchScenario = "TEMPEST-BGF-009";

    /// <summary>A figure does not say whether it is an actual, a forecast or a budget.</summary>
    public const string FigureKindMustBeStated = "TEMPEST-BGF-010";

    /// <summary>A figure recorded as an actual has neither a source nor evidence.</summary>
    public const string ActualIsUnsupported = "TEMPEST-BGF-011";

    /// <summary>A forecast rests on no stated assumption.</summary>
    public const string ForecastHasNoAssumptions = "TEMPEST-BGF-012";

    /// <summary>A figure names an assumption the library does not hold.</summary>
    public const string AssumptionReferenceMustResolve = "TEMPEST-BGF-013";

    /// <summary>A period has closed and still carries forecasts rather than actuals.</summary>
    public const string ClosedPeriodStillForecast = "TEMPEST-BGF-014";

    /// <summary>The scenario is not the approved planning case and nothing says which one is.</summary>
    public const string ScenarioIsNotApproved = "TEMPEST-BGF-015";

    /// <summary>A cost figure is negative, which is more often a sign error than a credit.</summary>
    public const string NegativeCostFigure = "TEMPEST-BGF-016";

    /// <summary>Two scenarios share one reference.</summary>
    public const string DuplicateScenarioReference = "TEMPEST-BGF-017";
}

/// <summary>Governance of financial assumptions themselves.</summary>
public interface IFinancialAssumptionValidationService : IReferenceValidationService<FinancialAssumption>
{
}

/// <summary>The concrete <see cref="IFinancialAssumptionValidationService"/> implementation.</summary>
public sealed class FinancialAssumptionValidationService
    : ReferenceValidationService<FinancialAssumption>, IFinancialAssumptionValidationService
{
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="FinancialAssumptionValidationService"/> class.</summary>
    /// <param name="catalog">The assumption library whose records this service validates.</param>
    /// <param name="timeProvider">The clock review checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public FinancialAssumptionValidationService(IFinancialAssumptionCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        FinancialAssumption definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Financial assumption '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        BusinessGovernanceValidator.Evaluate(subject, definition.Governance, today, errors, warnings, expectEvidence: false);

        if (!definition.HasStatedSource)
            warnings.Add(Diagnostic(
                FinanceValidationRules.AssumptionHasNoSource,
                $"{subject} does not say where it came from, so nobody can tell whether it rests on last year's actuals or on a "
                + "guess."));

        if (definition.AssumedAmount is null && definition.AssumedValue is null)
            warnings.Add(Diagnostic(
                FinanceValidationRules.AssumptionStatesNoValue,
                $"{subject} states no value, so no forecast can be computed from it. That is legitimate for a qualitative "
                + "assumption and is reported because it is usually an omission."));

        if (definition.AssumedValue is not null && string.IsNullOrWhiteSpace(definition.Unit))
            errors.Add(Diagnostic(
                FinanceValidationRules.AssumptionHasNoUnit,
                $"{subject} assumes the value {definition.AssumedValue} without saying what it measures. "
                + "A bare number cannot be used safely in a forecast."));

        return Task.CompletedTask;
    }
}
