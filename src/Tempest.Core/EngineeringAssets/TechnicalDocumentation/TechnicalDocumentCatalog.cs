using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.EngineeringAssets.TechnicalDocumentation;

/// <summary>A deterministic filter over the technical-documentation library.</summary>
public sealed record TechnicalDocumentQuery
{
    /// <summary>Matches any document whose reference or title contains this text, ignoring case. <see langword="null"/> to match any.</summary>
    public string? TextContains { get; init; }

    /// <summary>Matches any of these document types. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<TechnicalDocumentType> Types { get; init; } = [];

    /// <summary>Matches any of these statuses. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<DocumentStatus> Statuses { get; init; } = [];

    /// <summary>Matches only documents on this project. <see langword="null"/> to match any.</summary>
    public string? ProjectIdentifier { get; init; }

    /// <summary>Matches only documents in force on this date. <see langword="null"/> to match any.</summary>
    public DateOnly? InForceOn { get; init; }

    /// <summary>Matches any of these record validation states. Never <see langword="null"/>; empty matches any.</summary>
    public IReadOnlyList<ReferenceValidationState> ValidationStates { get; init; } = [];
}

/// <summary>The technical-documentation library.</summary>
public interface ITechnicalDocumentCatalog : IReferenceDataCatalog<TechnicalDocument>
{
    /// <summary>Returns the document registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<TechnicalDocument>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every registered document matching <paramref name="query"/>, in ascending record-Id order. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    Task<IReadOnlyList<IReferenceRecord<TechnicalDocument>>> SearchAsync(TechnicalDocumentQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// The documents actually in force on <paramref name="asAt"/>, for a
    /// stated project.
    /// </summary>
    /// <remarks>
    /// What somebody about to do the work needs: issued, inside its
    /// effectivity, and not superseded. Everything else in the library is
    /// history or work in progress.
    /// </remarks>
    /// <param name="projectIdentifier">The project. <see langword="null"/> for every project.</param>
    /// <param name="asAt">The date.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<IReadOnlyList<IReferenceRecord<TechnicalDocument>>> FindInForceAsync(
        string? projectIdentifier,
        DateOnly asAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every document that names <paramref name="reference"/> as the one
    /// it replaces.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReadOnlyList<IReferenceRecord<TechnicalDocument>>> FindSupersedingAsync(string reference, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ITechnicalDocumentCatalog"/> implementation.</summary>
public sealed class TechnicalDocumentCatalog : ReferenceDataCatalog<TechnicalDocument>, ITechnicalDocumentCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every technical-document record's own backing document carries.</summary>
    /// <remarks>
    /// The <em>record about</em> a technical document, never the document
    /// itself. The content lives under its own Kind in
    /// <c>EngineeringData</c>; this Kind holds the governance card.
    /// </remarks>
    public const string TechnicalDocumentKind = "EngineeringTechnicalDocumentRecord";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string TechnicalDocumentLibraryName = "EngineeringTechnicalDocuments";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>documentRecordId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "EngineeringTechnicalDocuments.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each document reference to the <c>documentRecordId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "EngineeringTechnicalDocuments.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="TechnicalDocumentCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public TechnicalDocumentCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => TechnicalDocumentLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => TechnicalDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<TechnicalDocument>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(TechnicalDocument.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<TechnicalDocument>>> SearchAsync(
        TechnicalDocumentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return FilterAsync(record => Matches(record, query), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<TechnicalDocument>>> FindInForceAsync(
        string? projectIdentifier,
        DateOnly asAt,
        CancellationToken cancellationToken = default) =>
        FilterAsync(
            record => record.Definition.IsInForceAt(asAt)
                      && (projectIdentifier is null
                          || string.Equals(record.Definition.ProjectIdentifier, projectIdentifier, StringComparison.OrdinalIgnoreCase)),
            cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<TechnicalDocument>>> FindSupersedingAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        var key = reference.Trim();

        return FilterAsync(
            record => string.Equals(record.Definition.SupersedesReference, key, StringComparison.OrdinalIgnoreCase),
            cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(TechnicalDocument definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(TechnicalDocument definition) => $"Document reference '{definition.Reference}'";

    private static bool Matches(IReferenceRecord<TechnicalDocument> record, TechnicalDocumentQuery query)
    {
        var document = record.Definition;

        if (query.TextContains is { } text
            && !document.Reference.Contains(text, StringComparison.OrdinalIgnoreCase)
            && !document.Title.Contains(text, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.Types.Count > 0 && !query.Types.Contains(document.Type))
            return false;

        if (query.Statuses.Count > 0 && !query.Statuses.Contains(document.Status))
            return false;

        if (query.ProjectIdentifier is { } project
            && !string.Equals(document.ProjectIdentifier, project, StringComparison.OrdinalIgnoreCase))
            return false;

        if (query.InForceOn is { } asAt && !document.IsInForceAt(asAt))
            return false;

        if (query.ValidationStates.Count > 0 && !query.ValidationStates.Contains(record.ValidationState))
            return false;

        return true;
    }
}

/// <summary>The diagnostic codes E5's validation service reports.</summary>
public static class TechnicalDocumentValidationRules
{
    /// <summary>The record names no content anybody can retrieve.</summary>
    public const string DocumentIsNotRetrievable = "TEMPEST-EAD-001";

    /// <summary>The document does not say what sort of document it is.</summary>
    public const string DocumentTypeNotStated = "TEMPEST-EAD-002";

    /// <summary>An issued document carries no issue revision.</summary>
    public const string IssuedDocumentHasNoRevision = "TEMPEST-EAD-003";

    /// <summary>An issued document carries no issue date.</summary>
    public const string IssuedDocumentHasNoDate = "TEMPEST-EAD-004";

    /// <summary>An issued document names nobody who approved it.</summary>
    public const string IssuedDocumentHasNoApproval = "TEMPEST-EAD-005";

    /// <summary>An issued document has never been reviewed.</summary>
    public const string IssuedDocumentIsUnreviewed = "TEMPEST-EAD-006";

    /// <summary>The document names a predecessor the library does not hold.</summary>
    public const string SupersededDocumentMustResolve = "TEMPEST-EAD-007";

    /// <summary>The document it replaces is still marked as in force.</summary>
    /// <remarks>
    /// The failure that puts two live issues of one drawing on the shop
    /// floor.
    /// </remarks>
    public const string PredecessorStillInForce = "TEMPEST-EAD-008";

    /// <summary>The document is marked superseded but nothing names it as replaced.</summary>
    public const string SupersededWithoutSuccessor = "TEMPEST-EAD-009";

    /// <summary>A relationship names nothing at the other end.</summary>
    public const string RelationshipIsUnresolvable = "TEMPEST-EAD-010";

    /// <summary>A relationship names a document the platform does not hold.</summary>
    public const string RelationshipTargetMustResolve = "TEMPEST-EAD-011";

    /// <summary>The document has run past its own effectivity while still marked issued.</summary>
    public const string IssuedDocumentHasExpired = "TEMPEST-EAD-012";

    /// <summary>Two documents share one reference.</summary>
    public const string DuplicateDocumentReference = "TEMPEST-EAD-013";
}

/// <summary>Governance of the technical-documentation library itself.</summary>
public interface ITechnicalDocumentValidationService : IReferenceValidationService<TechnicalDocument>
{
}

/// <summary>The concrete <see cref="ITechnicalDocumentValidationService"/> implementation.</summary>
/// <remarks>
/// The findings concentrate on the transition into issue, because that is
/// where a documentation system either holds or fails: an issued document
/// with no revision, no approval or a predecessor still in force is how
/// two live issues of one drawing reach the shop floor.
/// </remarks>
public sealed class TechnicalDocumentValidationService
    : ReferenceValidationService<TechnicalDocument>, ITechnicalDocumentValidationService
{
    private readonly ITechnicalDocumentCatalog _documents;
    private readonly IEngineeringDocumentStore? _documentStore;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="TechnicalDocumentValidationService"/> class.</summary>
    /// <param name="catalog">The documentation library whose records this service validates.</param>
    /// <param name="documentStore">The engineering document store, for confirming referenced content exists. Optional.</param>
    /// <param name="timeProvider">The clock effectivity checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public TechnicalDocumentValidationService(
        ITechnicalDocumentCatalog catalog,
        IEngineeringDocumentStore? documentStore = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _documents = catalog;
        _documentStore = documentStore;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        TechnicalDocument definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Document '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (!definition.IsRetrievable)
            errors.Add(AssetGovernanceValidation.Diagnostic(
                TechnicalDocumentValidationRules.DocumentIsNotRetrievable,
                $"{subject} names neither an engineering document nor an external location, so the record is a card in "
                + "a catalogue with no book behind it."));

        if (definition.Type == TechnicalDocumentType.Unspecified)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                TechnicalDocumentValidationRules.DocumentTypeNotStated,
                $"{subject} does not say what sort of document it is."));

        EvaluateIssueState(definition, subject, today, errors, warnings);

        foreach (var relationship in definition.Relationships.Where(r => !r.IsResolvable))
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                TechnicalDocumentValidationRules.RelationshipIsUnresolvable,
                $"{subject} declares a '{relationship.RelationshipKind}' relationship naming nothing at the other end."));

        AssetGovernanceValidation.Evaluate(definition.Governance, subject, errors, warnings);

        await EvaluateSupersessionAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
        await EvaluateRelationshipTargetsAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private static void EvaluateIssueState(
        TechnicalDocument definition,
        string subject,
        DateOnly today,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings)
    {
        if (!definition.HasBeenIssued)
            return;

        if (string.IsNullOrWhiteSpace(definition.IssueRevision))
            errors.Add(AssetGovernanceValidation.Diagnostic(
                TechnicalDocumentValidationRules.IssuedDocumentHasNoRevision,
                $"{subject} has been issued and carries no issue revision, so nobody holding a copy can tell which "
                + "issue it is."));

        if (definition.IssuedOn is null)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                TechnicalDocumentValidationRules.IssuedDocumentHasNoDate,
                $"{subject} has been issued and carries no issue date."));

        if (definition.Governance.Approval is null)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                TechnicalDocumentValidationRules.IssuedDocumentHasNoApproval,
                $"{subject} has been issued and names nobody who approved it."));

        if (definition.Governance.Reviews.Count == 0)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                TechnicalDocumentValidationRules.IssuedDocumentIsUnreviewed,
                $"{subject} has been issued without anybody having reviewed it."));

        if (definition.Status == DocumentStatus.Issued && definition.HasExpiredAt(today))
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                TechnicalDocumentValidationRules.IssuedDocumentHasExpired,
                $"{subject} is still marked issued but its effectivity ended on {definition.Effectivity!.To:O}."));
    }

    private async Task EvaluateSupersessionAsync(
        TechnicalDocument definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (definition.SupersedesReference is { } predecessorReference)
        {
            var predecessor = await _documents
                .FindByReferenceAsync(predecessorReference, cancellationToken)
                .ConfigureAwait(false);

            if (predecessor is null)
                warnings.Add(AssetGovernanceValidation.Diagnostic(
                    TechnicalDocumentValidationRules.SupersededDocumentMustResolve,
                    $"{subject} replaces '{predecessorReference}', which the library does not hold."));

            else if (definition.Status == DocumentStatus.Issued && predecessor.Definition.IsInForce)
                warnings.Add(AssetGovernanceValidation.Diagnostic(
                    TechnicalDocumentValidationRules.PredecessorStillInForce,
                    $"{subject} is issued and replaces '{predecessorReference}', which is also still marked issued. "
                    + "Two live issues of the same document is how the wrong one gets worked to."));
        }

        if (definition.Status != DocumentStatus.Superseded)
            return;

        var successors = await _documents
            .FindSupersedingAsync(definition.Reference, cancellationToken)
            .ConfigureAwait(false);

        if (successors.Count == 0)
            warnings.Add(AssetGovernanceValidation.Diagnostic(
                TechnicalDocumentValidationRules.SupersededWithoutSuccessor,
                $"{subject} is marked superseded but no document in the library names it as replaced."));
    }

    private async Task EvaluateRelationshipTargetsAsync(
        TechnicalDocument definition,
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
                warnings.Add(AssetGovernanceValidation.Diagnostic(
                    TechnicalDocumentValidationRules.RelationshipTargetMustResolve,
                    $"{subject} declares a '{relationship.RelationshipKind}' relationship to document "
                    + $"'{relationship.TargetDocumentId}', which the store does not hold."));
        }
    }
}
