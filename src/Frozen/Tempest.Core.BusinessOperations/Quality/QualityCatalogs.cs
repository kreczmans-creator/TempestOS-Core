using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessOperations.Quality;

/// <summary>A deterministic filter over the non-conformance library.</summary>
public sealed record NonConformanceQuery
{
    /// <summary>Matches any record whose reference or description contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these kinds. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<NonConformanceKind> Kinds { get; init; } = [];

    /// <summary>Matches any of these severities. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<NonConformanceSeverity> Severities { get; init; } = [];

    /// <summary>Matches any of these operational states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<OperationalState> States { get; init; } = [];

    /// <summary>Matches only records where the item is being used despite not conforming. <see langword="null"/> to match any.</summary>
    public bool? IsConcession { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>What did not conform, and what was done about it.</summary>
public interface INonConformanceCatalog : IReferenceDataCatalog<NonConformance>
{
    /// <summary>Returns the record registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<NonConformance>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered record matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<NonConformance>>> SearchAsync(NonConformanceQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every record marked closed where the problem is not actually
    /// solved, most serious first.
    /// </summary>
    /// <remarks>
    /// The report a quality system exists to produce and rarely does.
    /// Somebody can close a record; whether the cause was addressed and
    /// the fix shown to work is a different question.
    /// </remarks>
    Task<IReadOnlyList<IReferenceRecord<NonConformance>>> FindClosedButUnresolvedAsync(CancellationToken cancellationToken = default);

    /// <summary>Every record still needing somebody's attention, most serious first. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IReferenceRecord<NonConformance>>> FindOutstandingAsync(CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="INonConformanceCatalog"/> implementation.</summary>
public sealed class NonConformanceCatalog : ReferenceDataCatalog<NonConformance>, INonConformanceCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every record's own backing document carries.</summary>
    public const string NonConformanceDocumentKind = "BusinessNonConformance";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string NonConformanceLibraryName = "BusinessNonConformances";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>recordId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessNonConformances.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each reference to the <c>recordId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessNonConformances.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="NonConformanceCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public NonConformanceCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => NonConformanceLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => NonConformanceDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<NonConformance>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(NonConformance.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<NonConformance>>> SearchAsync(
        NonConformanceQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<NonConformance>>> FindClosedButUnresolvedAsync(CancellationToken cancellationToken = default)
    {
        var unresolved = await FilterAsync(record => record.Definition.IsClosedButUnresolved, cancellationToken).ConfigureAwait(false);

        return Order(unresolved);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<NonConformance>>> FindOutstandingAsync(CancellationToken cancellationToken = default)
    {
        var outstanding = await FilterAsync(record => record.Definition.Facts.IsOutstanding, cancellationToken).ConfigureAwait(false);

        return Order(outstanding);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(NonConformance definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(NonConformance definition) => $"Non-conformance reference '{definition.Reference}'";

    private static IReadOnlyList<IReferenceRecord<NonConformance>> Order(IReadOnlyList<IReferenceRecord<NonConformance>> records) =>
        records
            .OrderByDescending(r => (int)r.Definition.Severity)
            .ThenBy(r => r.Definition.Reference, StringComparer.Ordinal)
            .ToList();

    private static bool Matches(IReferenceRecord<NonConformance> record, NonConformanceQuery query)
    {
        var nonConformance = record.Definition;

        if (query.TextContains is { } text
            && !nonConformance.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !nonConformance.Description.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Kinds.Count > 0 && !query.Kinds.Contains(nonConformance.Kind))
            return false;

        if (query.Severities.Count > 0 && !query.Severities.Contains(nonConformance.Severity))
            return false;

        if (query.States.Count > 0 && !query.States.Contains(nonConformance.Facts.State))
            return false;

        if (query.IsConcession is { } concession && nonConformance.IsConcession != concession)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes WP04.5's validation reports.</summary>
public static class QualityValidationRules
{
    /// <summary>The record does not say what requirement was not met.</summary>
    public const string RequirementNotStated = "TEMPEST-BOQ-001";

    /// <summary>Nobody has decided what to do with the affected item.</summary>
    public const string NoDisposition = "TEMPEST-BOQ-002";

    /// <summary>The item is being used despite not conforming, with no stated justification.</summary>
    /// <remarks>
    /// An error. A concession is a deliberate decision to accept
    /// something that does not meet its specification, and one with no
    /// stated reason cannot be defended to a customer or an auditor.
    /// </remarks>
    public const string ConcessionIsUnjustified = "TEMPEST-BOQ-003";

    /// <summary>No root cause has been identified.</summary>
    public const string NoRootCauseIdentified = "TEMPEST-BOQ-004";

    /// <summary>A root cause nothing addresses.</summary>
    public const string RootCauseIsUnaddressed = "TEMPEST-BOQ-005";

    /// <summary>An action addresses no cause the record states.</summary>
    public const string ActionAddressesNoStatedCause = "TEMPEST-BOQ-006";

    /// <summary>An action was closed without anybody showing it worked.</summary>
    public const string EffectivenessNotVerified = "TEMPEST-BOQ-007";

    /// <summary>The record is marked closed while the problem is not actually solved.</summary>
    /// <remarks>
    /// An error. Closing a record and solving a problem are different
    /// acts, and a system that lets the first stand for the second
    /// produces a clean register and a recurring fault.
    /// </remarks>
    public const string ClosedButUnresolved = "TEMPEST-BOQ-008";

    /// <summary>Two causes or actions share one reference.</summary>
    public const string DuplicateQualityReference = "TEMPEST-BOQ-009";

    /// <summary>A critical or major non-conformance has no corrective action at all.</summary>
    public const string SeriousNonConformanceHasNoAction = "TEMPEST-BOQ-010";

    /// <summary>A supplier non-conformance names a supplier that resolves to no governed record.</summary>
    public const string SupplierIsUnresolved = "TEMPEST-BOQ-011";

    /// <summary>The record does not say how serious it is.</summary>
    public const string SeverityNotStated = "TEMPEST-BOQ-012";
}

/// <summary>Governance of the non-conformance library.</summary>
public interface INonConformanceValidationService : IReferenceValidationService<NonConformance>
{
}

/// <summary>The concrete <see cref="INonConformanceValidationService"/> implementation.</summary>
/// <remarks>
/// Two findings are errors: a concession nobody justified, and a record
/// closed while the problem stands. Both are the same failure — a quality
/// system that produces a clean register and a recurring fault.
/// </remarks>
public sealed class NonConformanceValidationService
    : ReferenceValidationService<NonConformance>, INonConformanceValidationService
{
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="NonConformanceValidationService"/> class.</summary>
    /// <param name="catalog">The non-conformance library whose records this service validates.</param>
    /// <param name="timeProvider">The clock date checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public NonConformanceValidationService(INonConformanceCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        NonConformance definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Non-conformance '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (definition.Severity == NonConformanceSeverity.Unspecified)
            warnings.Add(OperationalValidation.Diagnostic(
                QualityValidationRules.SeverityNotStated,
                $"{subject} does not say how serious it is, so nothing prioritises it."));

        if (!definition.IsDisposed)
            warnings.Add(OperationalValidation.Diagnostic(
                QualityValidationRules.NoDisposition,
                $"{subject} records no decision about what to do with the affected item."));

        if (definition.Disposition is { IsJustified: false })
            errors.Add(OperationalValidation.Diagnostic(
                QualityValidationRules.ConcessionIsUnjustified,
                $"{subject} accepts the item as {definition.Disposition.Kind} without a stated justification. A "
                + "concession is a deliberate decision to accept something out of specification, and one with no "
                + "reason cannot be defended to a customer or an auditor."));

        if (definition.RootCauses.Count == 0)
            warnings.Add(OperationalValidation.Diagnostic(
                QualityValidationRules.NoRootCauseIdentified,
                $"{subject} identifies no root cause, so nothing here prevents a recurrence."));

        foreach (var cause in definition.UnaddressedRootCauses)
            warnings.Add(OperationalValidation.Diagnostic(
                QualityValidationRules.RootCauseIsUnaddressed,
                $"{subject} identifies root cause '{cause.Reference}' and nothing addresses it."));

        OperationalValidation.EvaluateDuplicateReferences(
            definition.Causes.Select(c => c.Reference),
            $"{subject} has two causes sharing the reference",
            errors);

        OperationalValidation.EvaluateDuplicateReferences(
            definition.Actions.Select(a => a.Reference),
            $"{subject} has two actions sharing the reference",
            errors);

        var causeReferences = definition.Causes.Select(c => c.Reference).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var action in definition.Actions)
        {
            if (action.AddressesCauseReferences.Count > 0 && !action.AddressesCauseReferences.Any(causeReferences.Contains))
                warnings.Add(OperationalValidation.Diagnostic(
                    QualityValidationRules.ActionAddressesNoStatedCause,
                    $"{subject} action '{action.Reference}' addresses no cause the record states."));

            if (action.Facts.State == OperationalState.Closed && !action.IsVerifiedEffective)
                warnings.Add(OperationalValidation.Diagnostic(
                    QualityValidationRules.EffectivenessNotVerified,
                    $"{subject} action '{action.Reference}' is closed with nothing showing it worked. Closed and "
                    + "effective are different things."));
        }

        if (definition.Severity is NonConformanceSeverity.Major or NonConformanceSeverity.Critical
            && definition.CorrectiveActions.Count == 0)
            warnings.Add(OperationalValidation.Diagnostic(
                QualityValidationRules.SeriousNonConformanceHasNoAction,
                $"{subject} is {definition.Severity} and records no corrective action."));

        if (definition.IsClosedButUnresolved)
            errors.Add(OperationalValidation.Diagnostic(
                QualityValidationRules.ClosedButUnresolved,
                $"{subject} is marked closed while the item is undealt with, a root cause is unaddressed, or an action "
                + "has not been shown to work. Closing a record and solving a problem are different acts."));

        if (definition.Supplier is { } supplier)
            OperationalValidation.EvaluateParty(supplier, subject, warnings);

        OperationalValidation.Evaluate(definition.Facts, subject, today, errors, warnings);

        return Task.CompletedTask;
    }
}
