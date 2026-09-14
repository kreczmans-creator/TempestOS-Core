using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Knowledge.WorkedExamples;

/// <summary>A deterministic filter over the worked-example library.</summary>
public sealed record WorkedExampleQuery
{
    /// <summary>Matches any example whose reference, title or problem contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches examples applying to this enquiry. <see langword="null"/> to leave every dimension open.</summary>
    public KnowledgeEnquiry? Enquiry { get; init; }

    /// <summary>Matches only examples that teach rather than merely demonstrate. <see langword="null"/> to match any.</summary>
    public bool? IsInstructive { get; init; }

    /// <summary>Matches only examples whose answer was checked independently. <see langword="null"/> to match any.</summary>
    public bool? IsVerified { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The worked-example library.</summary>
public interface IWorkedExampleCatalog : IReferenceDataCatalog<WorkedExample>
{
    /// <summary>Returns the example registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<WorkedExample>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered example matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<WorkedExample>>> SearchAsync(WorkedExampleQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// The examples worth putting in front of a learner, most
    /// instructive first.
    /// </summary>
    /// <remarks>
    /// Reviewed and instructive examples ahead of merely complete ones,
    /// because an unreviewed worked example is somebody's homework and a
    /// complete-but-unexplained one is an answer sheet.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="enquiry"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<WorkedExample>>> FindTeachableAsync(
        KnowledgeEnquiry enquiry,
        CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IWorkedExampleCatalog"/> implementation.</summary>
public sealed class WorkedExampleCatalog : ReferenceDataCatalog<WorkedExample>, IWorkedExampleCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every example's own backing document carries.</summary>
    public const string WorkedExampleDocumentKind = "KnowledgeWorkedExample";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string WorkedExampleLibraryName = "KnowledgeWorkedExamples";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>exampleId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "KnowledgeWorkedExamples.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each example reference to the <c>exampleId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "KnowledgeWorkedExamples.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="WorkedExampleCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own examples are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public WorkedExampleCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => WorkedExampleLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => WorkedExampleDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<WorkedExample>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(WorkedExample.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<WorkedExample>>> SearchAsync(
        WorkedExampleQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<WorkedExample>>> FindTeachableAsync(
        KnowledgeEnquiry enquiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(enquiry);

        var teachable = await FilterAsync(
            record => record.Definition.IsComplete
                      && record.Definition.Provenance.IsCurrent
                      && record.Definition.Applicability.AppliesTo(enquiry),
            cancellationToken).ConfigureAwait(false);

        return teachable
            .OrderByDescending(r => r.Definition.IsInstructive)
            .ThenByDescending(r => r.Definition.Provenance.IsReviewed)
            .ThenByDescending(r => r.Definition.IsVerified)
            .ThenBy(r => r.Definition.Reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(WorkedExample definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(WorkedExample definition) => $"Worked example reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<WorkedExample> record, WorkedExampleQuery query)
    {
        var example = record.Definition;

        if (query.TextContains is { } text
            && !example.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !example.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !example.ProblemStatement.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Enquiry is { } enquiry && !example.Applicability.AppliesTo(enquiry))
            return false;

        if (query.IsInstructive is { } instructive && example.IsInstructive != instructive)
            return false;

        if (query.IsVerified is { } verified && example.IsVerified != verified)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes F5's validation service reports.</summary>
public static class WorkedExampleValidationRules
{
    /// <summary>The example shows no working.</summary>
    public const string ExampleHasNoSteps = "TEMPEST-KNW-001";

    /// <summary>The example reaches no answer.</summary>
    public const string ExampleHasNoResult = "TEMPEST-KNW-002";

    /// <summary>The example gives a number and does not say what it means.</summary>
    public const string ResultHasNoInterpretation = "TEMPEST-KNW-003";

    /// <summary>A step shows working without saying why.</summary>
    /// <remarks>
    /// A worked example whose steps do not explain themselves teaches a
    /// reader to reproduce the arithmetic, which is the one thing they
    /// did not need the example for.
    /// </remarks>
    public const string StepIsUnexplained = "TEMPEST-KNW-004";

    /// <summary>A quantity carries no unit and is not declared dimensionless.</summary>
    /// <remarks>
    /// An error. Unit mistakes are the commonest way an engineering
    /// calculation goes wrong, and a teaching example that omits units
    /// teaches the habit.
    /// </remarks>
    public const string QuantityHasNoUnit = "TEMPEST-KNW-005";

    /// <summary>Two inputs or steps share one reference.</summary>
    public const string DuplicateExampleReference = "TEMPEST-KNW-006";

    /// <summary>A looked-up value pins no governed record.</summary>
    public const string LookupIsUntraceable = "TEMPEST-KNW-007";

    /// <summary>The example states nothing a reader should take away.</summary>
    public const string NoTeachingPoints = "TEMPEST-KNW-008";

    /// <summary>The answer was never checked against anything independent.</summary>
    public const string ResultIsUnverified = "TEMPEST-KNW-009";

    /// <summary>The example names a calculation pack the library does not hold.</summary>
    public const string CalculationPackMustResolve = "TEMPEST-KNW-010";

    /// <summary>The example states no assumptions, which few real calculations manage.</summary>
    public const string NoAssumptionsStated = "TEMPEST-KNW-011";
}

/// <summary>Governance of the worked-example library itself.</summary>
public interface IWorkedExampleValidationService : IReferenceValidationService<WorkedExample>
{
}

/// <summary>The concrete <see cref="IWorkedExampleValidationService"/> implementation.</summary>
/// <remarks>
/// The findings distinguish an example that is <em>right</em> from one
/// that <em>teaches</em>. Both matter, and a library full of the first
/// kind is a library of answer sheets.
/// </remarks>
public sealed class WorkedExampleValidationService
    : ReferenceValidationService<WorkedExample>, IWorkedExampleValidationService
{
    private readonly EngineeringAssets.CalculationPacks.ICalculationPackCatalog? _calculationPacks;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="WorkedExampleValidationService"/> class.</summary>
    /// <param name="catalog">The example library whose records this service validates.</param>
    /// <param name="calculationPacks">The `E2` library, for confirming a named pack exists. Optional.</param>
    /// <param name="timeProvider">The clock staleness checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public WorkedExampleValidationService(
        IWorkedExampleCatalog catalog,
        EngineeringAssets.CalculationPacks.ICalculationPackCatalog? calculationPacks = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _calculationPacks = calculationPacks;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        WorkedExample definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Worked example '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (definition.Steps.Count == 0)
            errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                WorkedExampleValidationRules.ExampleHasNoSteps,
                $"{subject} shows no working, so there is nothing to follow."));

        if (definition.Result is null)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                WorkedExampleValidationRules.ExampleHasNoResult,
                $"{subject} reaches no answer."));

        else if (string.IsNullOrWhiteSpace(definition.Interpretation))
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                WorkedExampleValidationRules.ResultHasNoInterpretation,
                $"{subject} gives {definition.Result.Value} and does not say what it means. A number is not a conclusion."));

        KnowledgeGovernanceValidation.EvaluateDuplicateReferences(
            definition.Steps.Select(s => s.Reference),
            $"{subject} has two steps sharing the reference",
            errors);

        KnowledgeGovernanceValidation.EvaluateDuplicateReferences(
            definition.Inputs.Select(i => i.Symbol),
            $"{subject} has two inputs sharing the symbol",
            errors);

        foreach (var step in definition.UnexplainedSteps)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                WorkedExampleValidationRules.StepIsUnexplained,
                $"{subject} step '{step.Reference}' shows working and does not say why. A reader can follow arithmetic "
                + "without learning anything."));

        foreach (var value in definition.Inputs.Concat(definition.Result is null ? [] : [definition.Result])
                     .Where(v => !v.HasStatedUnit))
            errors.Add(KnowledgeGovernanceValidation.Diagnostic(
                WorkedExampleValidationRules.QuantityHasNoUnit,
                $"{subject} quantity '{value.Symbol}' carries no unit and is not declared dimensionless. Unit mistakes "
                + "are the commonest way a calculation goes wrong, and an example that omits units teaches the habit."));

        foreach (var step in definition.Steps.Where(s => s.Kind == WorkedStepKind.Lookup && !s.IsTraceable))
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                WorkedExampleValidationRules.LookupIsUntraceable,
                $"{subject} step '{step.Reference}' looks a value up and pins no governed record, so the reader cannot "
                + "check where it came from."));

        if (definition.TeachingPoints.Count == 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                WorkedExampleValidationRules.NoTeachingPoints,
                $"{subject} states nothing a reader should take away beyond the answer."));

        if (!definition.IsVerified)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                WorkedExampleValidationRules.ResultIsUnverified,
                $"{subject} was never checked against anything independent."));

        if (definition.Assumptions.Count == 0)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                WorkedExampleValidationRules.NoAssumptionsStated,
                $"{subject} states no assumptions. Few real calculations have none, and a teaching example that hides "
                + "them teaches that they do not matter."));

        await EvaluateCalculationPackAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);

        KnowledgeGovernanceValidation.Evaluate(
            definition.Provenance,
            definition.Applicability,
            subject,
            today,
            errors,
            warnings);
    }

    private async Task EvaluateCalculationPackAsync(
        WorkedExample definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_calculationPacks is null || definition.CalculationPackReference is not { } reference)
            return;

        var pack = await _calculationPacks.FindByReferenceAsync(reference, cancellationToken).ConfigureAwait(false);

        if (pack is null)
            warnings.Add(KnowledgeGovernanceValidation.Diagnostic(
                WorkedExampleValidationRules.CalculationPackMustResolve,
                $"{subject} cites calculation pack '{reference}', which the library does not hold."));
    }
}
