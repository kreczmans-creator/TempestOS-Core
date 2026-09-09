using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessOperations.Records;

/// <summary>A deterministic filter over the business-record library.</summary>
public sealed record BusinessRecordQuery
{
    /// <summary>Matches any record whose reference or title contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these kinds. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<BusinessRecordKind> Kinds { get; init; } = [];

    /// <summary>Matches only records at or below this classification. <see langword="null"/> to match any.</summary>
    public ConfidentialityClassification? MaximumClassification { get; init; }

    /// <summary>Matches only records on this project. <see langword="null"/> to match any.</summary>
    public Guid? ProjectId { get; init; }

    /// <summary>Matches only records with, or without, a retention decision. <see langword="null"/> to match any.</summary>
    public bool? HasRetentionDecision { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The business records the organisation keeps.</summary>
public interface IBusinessRecordCatalog : IReferenceDataCatalog<BusinessRecord>
{
    /// <summary>Returns the record registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<BusinessRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered record matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<BusinessRecord>>> SearchAsync(BusinessRecordQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every record past the date the organisation decided to keep it to.
    /// </summary>
    /// <remarks>
    /// A list for a person to review. Nothing here deletes anything:
    /// disposal has legal consequence and `P04` performs none of it.
    /// </remarks>
    Task<IReadOnlyList<IReferenceRecord<BusinessRecord>>> FindDueForRetentionReviewAsync(DateOnly asAt, CancellationToken cancellationToken = default);

    /// <summary>Every record likely to carry a statutory period with no retention decision against it. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IReferenceRecord<BusinessRecord>>> FindNeedingRetentionDecisionAsync(CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IBusinessRecordCatalog"/> implementation.</summary>
public sealed class BusinessRecordCatalog : ReferenceDataCatalog<BusinessRecord>, IBusinessRecordCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every business record's own backing document carries.</summary>
    /// <remarks>
    /// Distinct from `P05`'s <c>EngineeringTechnicalDocumentRecord</c>.
    /// An invoice and a drawing are governed differently and never share
    /// a library (`ADR-0142`).
    /// </remarks>
    public const string BusinessRecordDocumentKind = "BusinessRecordCard";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string BusinessRecordLibraryName = "BusinessRecords";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>recordId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessRecords.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each reference to the <c>recordId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessRecords.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="BusinessRecordCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public BusinessRecordCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => BusinessRecordLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => BusinessRecordDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<BusinessRecord>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(BusinessRecord.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<BusinessRecord>>> SearchAsync(
        BusinessRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<BusinessRecord>>> FindDueForRetentionReviewAsync(
        DateOnly asAt,
        CancellationToken cancellationToken = default) =>
        FilterAsync(record => record.Definition.IsDueForRetentionReviewAt(asAt), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<BusinessRecord>>> FindNeedingRetentionDecisionAsync(CancellationToken cancellationToken = default) =>
        FilterAsync(record => record.Definition.NeedsRetentionDecision, cancellationToken);

    /// <inheritdoc />
    protected override string? GetSecondaryKey(BusinessRecord definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(BusinessRecord definition) => $"Business record reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<BusinessRecord> record, BusinessRecordQuery query)
    {
        var businessRecord = record.Definition;

        if (query.TextContains is { } text
            && !businessRecord.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !businessRecord.Title.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Kinds.Count > 0 && !query.Kinds.Contains(businessRecord.Kind))
            return false;

        if (query.MaximumClassification is { } ceiling && businessRecord.Classification > ceiling)
            return false;

        if (query.ProjectId is { } projectId && businessRecord.ProjectId != projectId)
            return false;

        if (query.HasRetentionDecision is { } decided && businessRecord.HasRetentionDecision != decided)
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes WP04.6's validation reports.</summary>
public static class RecordValidationRules
{
    /// <summary>The card names no content anybody can retrieve.</summary>
    public const string RecordIsNotRetrievable = "TEMPEST-BOR-001";

    /// <summary>The record does not say what sort of record it is.</summary>
    public const string RecordKindNotStated = "TEMPEST-BOR-002";

    /// <summary>A record likely to carry a statutory period has no retention decision.</summary>
    public const string RetentionNotDecided = "TEMPEST-BOR-003";

    /// <summary>A retention period is stated with no basis for it.</summary>
    public const string RetentionHasNoBasis = "TEMPEST-BOR-004";

    /// <summary>The record is past the date the organisation decided to keep it to.</summary>
    public const string RecordIsDueForRetentionReview = "TEMPEST-BOR-005";

    /// <summary>A relationship names nothing at the other end.</summary>
    public const string RelationshipIsUnresolvable = "TEMPEST-BOR-006";

    /// <summary>A relationship names a document the store does not hold.</summary>
    public const string RelationshipTargetMustResolve = "TEMPEST-BOR-007";

    /// <summary>The record carries no date.</summary>
    public const string RecordHasNoDate = "TEMPEST-BOR-008";

    /// <summary>A record concerning an outside party names one that resolves to no governed record.</summary>
    public const string PartyIsUnresolved = "TEMPEST-BOR-009";

    /// <summary>A statutory record is classified no more sensitively than internal.</summary>
    public const string StatutoryRecordIsLooselyClassified = "TEMPEST-BOR-010";
}

/// <summary>Governance of the business-record library.</summary>
public interface IBusinessRecordValidationService : IReferenceValidationService<BusinessRecord>
{
}

/// <summary>The concrete <see cref="IBusinessRecordValidationService"/> implementation.</summary>
/// <remarks>
/// The findings concentrate on the two things a records system exists
/// for: that the record can be found, and that somebody has decided how
/// long to keep it. The platform holds no retention law and computes no
/// period; it reports where the organisation has not decided.
/// </remarks>
public sealed class BusinessRecordValidationService
    : ReferenceValidationService<BusinessRecord>, IBusinessRecordValidationService
{
    private readonly IEngineeringDocumentStore? _documentStore;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="BusinessRecordValidationService"/> class.</summary>
    /// <param name="catalog">The record library whose entries this service validates.</param>
    /// <param name="documentStore">The engineering document store, for confirming referenced content exists. Optional.</param>
    /// <param name="timeProvider">The clock retention checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public BusinessRecordValidationService(
        IBusinessRecordCatalog catalog,
        IEngineeringDocumentStore? documentStore = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _documentStore = documentStore;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        BusinessRecord definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Business record '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (!definition.IsRetrievable)
            errors.Add(OperationalValidation.Diagnostic(
                RecordValidationRules.RecordIsNotRetrievable,
                $"{subject} names neither a document nor an external location, so the card refers to nothing."));

        if (definition.Kind == BusinessRecordKind.Unspecified)
            warnings.Add(OperationalValidation.Diagnostic(
                RecordValidationRules.RecordKindNotStated,
                $"{subject} does not say what sort of record it is."));

        if (definition.RecordedOn is null)
            warnings.Add(OperationalValidation.Diagnostic(
                RecordValidationRules.RecordHasNoDate,
                $"{subject} carries no date, so no retention period can be reasoned about from it."));

        if (definition.NeedsRetentionDecision)
            warnings.Add(OperationalValidation.Diagnostic(
                RecordValidationRules.RetentionNotDecided,
                $"{subject} is a {definition.Kind} with no retention decision. TempestOS holds no retention law and "
                + "will not invent a period; somebody must decide one."));

        if (definition.Retention.IsDecided && string.IsNullOrWhiteSpace(definition.Retention.Basis))
            warnings.Add(OperationalValidation.Diagnostic(
                RecordValidationRules.RetentionHasNoBasis,
                $"{subject} states a retention period with no basis for it, so nobody can tell whether it is still right."));

        if (definition.IsDueForRetentionReviewAt(today))
            warnings.Add(OperationalValidation.Diagnostic(
                RecordValidationRules.RecordIsDueForRetentionReview,
                $"{subject} passed its retention date on {definition.Retention.RetainUntil:O} and is for a person to "
                + "review. Nothing has been or will be deleted."));

        if (definition.Kind == BusinessRecordKind.StatutoryRecord
            && definition.Classification <= ConfidentialityClassification.Internal)
            warnings.Add(OperationalValidation.Diagnostic(
                RecordValidationRules.StatutoryRecordIsLooselyClassified,
                $"{subject} is a statutory record classified only {definition.Classification}."));

        foreach (var relationship in definition.Relationships.Where(r => !r.IsResolvable))
            warnings.Add(OperationalValidation.Diagnostic(
                RecordValidationRules.RelationshipIsUnresolvable,
                $"{subject} declares a '{relationship.RelationshipKind}' relationship naming nothing at the other end."));

        if (definition.Party is { } party)
            OperationalValidation.EvaluateParty(party, subject, warnings);

        OperationalValidation.Evaluate(definition.Facts, subject, today, errors, warnings, requireDueDate: false);

        await EvaluateRelationshipTargetsAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private async Task EvaluateRelationshipTargetsAsync(
        BusinessRecord definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_documentStore is null)
            return;

        foreach (var relationship in definition.Relationships.Where(r => r.TargetDocumentId is not null))
        {
            var target = await _documentStore
                .FindAsync(relationship.TargetDocumentId!.Value, cancellationToken)
                .ConfigureAwait(false);

            if (target is null)
                warnings.Add(OperationalValidation.Diagnostic(
                    RecordValidationRules.RelationshipTargetMustResolve,
                    $"{subject} declares a '{relationship.RelationshipKind}' relationship to document "
                    + $"'{relationship.TargetDocumentId}', which the store does not hold."));
        }
    }
}
