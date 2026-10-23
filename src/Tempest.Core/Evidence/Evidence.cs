using Tempest.Core.Audit;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Evidence;

/// <summary>
/// A canonical Kind recording an engineer's work done elsewhere — the
/// files, what it is about, the governed references it cites at the
/// revision held, its declared figures, its check and its issue
/// (`ADR-0148`, `D-028`). Follows <c>Tempest.Core.EngineeringDomain.Part</c>'s
/// own shape exactly: an <c>EngineeringObjectBase</c> subtype carrying its
/// own state through <c>CaptureTypeState</c>/<c>ApplyTypeState</c>/
/// <see cref="IRehydratable{Evidence}.Rehydrate"/>, inheriting identity,
/// revisions, audit and attachments (<c>IHasAttachments.AttachContentAsync</c>)
/// from the base rather than reimplementing any of them.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not PLM, not ERP (`D-028`).</b> <see cref="SubjectId"/> is a tag —
/// an opaque id this class never dereferences, never validates as a real
/// object, and never uses to drive any workflow. The subject is whatever
/// the caller says it is.
/// </para>
/// <para>
/// <b>Every act that changes this object's own state is one transaction
/// with an audit row</b> (`ADR-0145`), via the Kind-specific mutator
/// counterpart <c>EngineeringObjectBase.MutateTypeStateAndPersistAsync</c>
/// every method below calls. Whether an act is <em>permitted</em> —
/// whether a citation's own record is Released, whether a status move is
/// in the table, whether an independent check is being asked of the
/// author — is decided by <see cref="IEvidenceService"/> before it calls
/// any of these, exactly as <c>GovernedBracketCheckService.CheckAsync</c>
/// resolves and checks before it ever reaches the calculation engine. This
/// class only ever persists what it is told to.
/// </para>
/// </remarks>
public sealed class Evidence : EngineeringObjectBase, IEvidenceRecord, IRehydratable<Evidence>
{
    /// <summary>The <see cref="IEngineeringObject.Kind"/> every piece of evidence's own backing document carries — this class's own canonical vocabulary (`ADR-0105`).</summary>
    public const string CanonicalKind = "Evidence";

    private readonly EvidenceClassification _classification;
    private Guid? _subjectId;
    private readonly string _authorIdentityId;
    private List<EvidenceCitation> _citations;
    private List<DeclaredFigure> _declaredFigures;
    private EvidenceStatus _status;
    private CheckRecord? _check;
    private IssueRecord? _issue;

    /// <summary>Initialises a new instance of the <see cref="Evidence"/> class.</summary>
    public Evidence(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        EvidenceClassification classification, Guid? subjectId, string authorIdentityId,
        IReadOnlyList<EvidenceCitation>? citations = null, IReadOnlyList<DeclaredFigure>? declaredFigures = null,
        EvidenceStatus status = EvidenceStatus.Draft, CheckRecord? check = null, IssueRecord? issue = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorIdentityId);

        _classification = classification;
        _subjectId = subjectId;
        _authorIdentityId = authorIdentityId;
        _citations = citations is null ? [] : [.. citations];
        _declaredFigures = declaredFigures is null ? [] : [.. declaredFigures];
        _status = status;
        _check = check;
        _issue = issue;
    }

    /// <inheritdoc cref="IEvidenceRecord.Classification" />
    /// <remarks>Hides <c>IHasMetadata.Classification</c> (a free-text <see cref="string"/>) — this Kind's own classification is a closed, typed vocabulary.</remarks>
    public new EvidenceClassification Classification => _classification;

    /// <inheritdoc />
    public Guid? SubjectId => _subjectId;

    /// <inheritdoc />
    public string AuthorIdentityId => _authorIdentityId;

    /// <inheritdoc />
    public IReadOnlyList<EvidenceCitation> Citations => _citations;

    /// <inheritdoc />
    public IReadOnlyList<DeclaredFigure> DeclaredFigures => _declaredFigures;

    /// <inheritdoc cref="IEvidenceRecord.Status" />
    /// <remarks>Hides <c>IHasLifecycle.Status</c> (the eight-value canonical <see cref="LifecycleState"/>) with this Kind's own four-value specialisation (`ADR-0074`, `ADR-0148`).</remarks>
    public new EvidenceStatus Status => _status;

    /// <inheritdoc />
    public CheckRecord? Check => _check;

    /// <inheritdoc />
    public IssueRecord? Issue => _issue;

    /// <summary>
    /// Retags this evidence's own <see cref="SubjectId"/> — a Part,
    /// Assembly, Requirement or Deliverable, or <see langword="null"/> to
    /// clear it (`WP 18.2B`, closing a gap `WP 18.2A` disclosed). Never
    /// validated as a structure, exactly as the constructor's own
    /// <paramref name="subjectId"/> parameter never was — permission to
    /// retag (refused once <see cref="Status"/> is <see cref="EvidenceStatus.Issued"/>)
    /// is <see cref="IEvidenceService.SetSubjectAsync"/>'s own concern, not
    /// this mutator's.
    /// </summary>
    internal Task SetSubjectAsync(Guid? subjectId, CancellationToken cancellationToken = default)
    {
        return MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(SubjectId)] = subjectId?.ToString() },
            () => _subjectId = subjectId,
            subjectId is { } id ? $"Subject set to '{id:N}'." : "Subject cleared.",
            cancellationToken);
    }

    /// <summary>
    /// Points this evidence's own <see cref="Issue"/> record at the issue
    /// sheet's own attachment, once rendered and attached (`WP 18.2B`).
    /// The bytes and their attachment metadata are written by a prior,
    /// separate <see cref="IHasAttachments.AttachContentAsync"/> call — see
    /// <see cref="IEvidenceService.RecordIssueSheetAsync"/>'s own remarks
    /// for why this cannot be the identical transaction as either that
    /// write or <see cref="RecordIssueAsync"/> itself.
    /// </summary>
    internal Task SetIssueSheetAttachmentAsync(Guid issueSheetAttachmentId, CancellationToken cancellationToken = default)
    {
        if (_issue is not { } issue)
            throw new InvalidOperationException($"Evidence '{Id}' has not been issued; there is no issue record to attach a sheet to.");

        var updated = issue with { IssueSheetAttachmentId = issueSheetAttachmentId };

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(Issue), updated);
                return state;
            },
            () => _issue = updated,
            $"Issue sheet attached ('{issueSheetAttachmentId:N}').",
            cancellationToken);
    }

    /// <summary>Adds <paramref name="citation"/> to this evidence's own list, in one transaction with an audit row.</summary>
    internal Task AddCitationAsync(EvidenceCitation citation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(citation);

        List<EvidenceCitation> updated = [];

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                updated = [.. _citations, citation];
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(Citations), updated);
                return state;
            },
            () => _citations = updated,
            $"Cited '{citation.Pin}'.",
            cancellationToken);
    }

    /// <summary>Removes the citation pinning <paramref name="pin"/>'s own record, if this evidence has one.</summary>
    internal Task RemoveCitationAsync(ReferencePin pin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pin);

        List<EvidenceCitation> updated = [];

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                updated = _citations.Where(c => c.Pin != pin).ToList();
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(Citations), updated);
                return state;
            },
            () => _citations = updated,
            $"Removed citation '{pin}'.",
            cancellationToken);
    }

    /// <summary>Adds <paramref name="figure"/> to this evidence's own declared figures.</summary>
    internal Task AddDeclaredFigureAsync(DeclaredFigure figure, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(figure);

        List<DeclaredFigure> updated = [];

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                updated = [.. _declaredFigures, figure];
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(DeclaredFigures), updated);
                return state;
            },
            () => _declaredFigures = updated,
            $"Declared figure '{figure.Name}' = {figure.Quantity}.",
            cancellationToken);
    }

    /// <summary>Records <paramref name="check"/> and moves <see cref="Status"/> to <see cref="EvidenceStatus.Checked"/>, as one transaction.</summary>
    internal Task RecordCheckAsync(CheckRecord check, CancellationToken cancellationToken = default) => RecordCheckAsync(check, alsoWrite: null, cancellationToken);

    /// <summary>
    /// `v0.26.0` Phase 6 (R-15, R-19): records <paramref name="check"/> and
    /// moves <see cref="Status"/> to <see cref="EvidenceStatus.Checked"/> in
    /// one transaction with whatever <paramref name="alsoWrite"/> adds (a
    /// calculation instance's own Checked mark), after re-reading the
    /// stored status under that transaction: a second check racing this
    /// one is refused with <see cref="ConcurrentCheckException"/> and
    /// nothing is written.
    /// </summary>
    internal Task RecordCheckAsync(CheckRecord check, Func<IPersistenceTransaction, CancellationToken, Task>? alsoWrite, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(check);

        return MutateAndPersistAsync(
            current =>
            {
                var state = new Dictionary<string, string?>(current.TypeState, StringComparer.Ordinal);
                WriteJson(state, nameof(Check), check);
                state[nameof(Status)] = EvidenceStatus.Checked.ToString();
                return current with { TypeState = state };
            },
            EngineeringAuditActions.StateChanged,
            $"Checked by '{check.CheckerName}' ({check.CheckerOrganisation}): {check.Outcome}."
            + (check.SelfCheck ? $" Checked by its own author — {Governance.SignOffPolicy.SelfApprovalNote}." : string.Empty),
            WorkspaceChangeType.Updated,
            cancellationToken,
            alsoWrite: alsoWrite is null ? null : (transaction, _, token) => alsoWrite(transaction, token),
            alsoApply: _ =>
            {
                _check = check;
                _status = EvidenceStatus.Checked;
            },
            verifyBeforeWrite: async (transaction, token) =>
            {
                var stored = await EngineeringDomain.EngineeringObjectStateStore.ReadInTransactionAsync(transaction, Id, token).ConfigureAwait(false);
                var storedStatus = stored?.TypeState.TryGetValue(nameof(Status), out var s) == true && Enum.TryParse<EvidenceStatus>(s, out var parsed) ? parsed : Status;

                if (!EvidenceStatusTransitions.IsPermitted(storedStatus, EvidenceStatus.Checked))
                    throw new ConcurrentCheckException($"Evidence '{Id}' is {storedStatus}: another check completed first; nothing was written.");
            });
    }

    /// <summary>Records <paramref name="issue"/> and moves <see cref="Status"/> to <see cref="EvidenceStatus.Issued"/>, as one transaction.</summary>
    /// <summary>
    /// `v0.26.0` (ADR-0165): records the issue, attaches the rendered sheet
    /// and runs <paramref name="alsoWrite"/> (a calculation instance
    /// snapshot, a register link) in <b>one</b> transaction. A fault
    /// anywhere leaves this evidence exactly as it was: Checked, with no
    /// attachment, no issue record and no orphan.
    /// </summary>
    internal async Task<Guid?> RecordIssueAsync(
        IssueRecord issue,
        IssueSheetContent? sheet,
        Func<IPersistenceTransaction, CancellationToken, Task>? alsoWrite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        if (sheet is not null && sheet.Content.Length > AttachmentContentLimits.MaximumSizeInBytes)
            throw new AttachmentContentTooLargeException(sheet.FileName, sheet.Content.Length);

        var attachmentId = sheet is null ? (Guid?)null : sheet.AttachmentId ?? Guid.NewGuid();
        var contentHash = sheet is null ? null : AttachmentContentStore.ComputeHash(sheet.Content.Span);
        var attachment = sheet is null || attachmentId is null ? null : new Attachment(attachmentId.Value, sheet.FileName, sheet.ContentType, sheet.Content.Length, contentHash);
        var recorded = issue with { IssueSheetAttachmentId = attachmentId };

        await MutateAndPersistAsync(
            current =>
            {
                var state = new Dictionary<string, string?>(current.TypeState, StringComparer.Ordinal);
                WriteJson(state, nameof(Issue), recorded);
                state[nameof(Status)] = EvidenceStatus.Issued.ToString();

                return current with
                {
                    TypeState = state,
                    Attachments = sheet is null || attachmentId is null
                        ? current.Attachments
                        : [.. current.Attachments, new EngineeringObjectAttachmentState(attachmentId.Value, sheet.FileName, sheet.ContentType, sheet.Content.Length, contentHash)],
                };
            },
            EngineeringAuditActions.StateChanged,
            $"Issued as '{issue.IssueReference}' revision '{issue.Revision}' to '{issue.Client}'.",
            WorkspaceChangeType.Updated,
            cancellationToken,
            alsoWrite: async (transaction, _, token) =>
            {
                if (sheet is not null && attachmentId is not null)
                    await Context.AttachmentWriter.SaveAsync(transaction, attachmentId.Value, sheet.Content, token).ConfigureAwait(false);

                if (alsoWrite is not null)
                    await alsoWrite(transaction, token).ConfigureAwait(false);
            },
            alsoApply: _ =>
            {
                _issue = recorded;
                _status = EvidenceStatus.Issued;
            },
            applyAttachments: attachment is null ? null : () => [.. AttachmentsSnapshot(), attachment],
            verifyBeforeWrite: async (transaction, token) =>
            {
                // The caller checked the status before the transaction opened;
                // a second Issue racing this one may have committed since.
                // Read the stored state under this transaction and refuse
                // unless it still permits the move — the whole transaction
                // (attachment, issue record, anything in alsoWrite) rolls back.
                var stored = await EngineeringDomain.EngineeringObjectStateStore.ReadInTransactionAsync(transaction, Id, token).ConfigureAwait(false);
                var storedStatus = stored?.TypeState.TryGetValue(nameof(Status), out var s) == true && Enum.TryParse<EvidenceStatus>(s, out var parsed) ? parsed : Status;

                if (!EvidenceStatusTransitions.IsPermitted(storedStatus, EvidenceStatus.Issued))
                    throw new ConcurrentIssueException($"Evidence '{Id}' is {storedStatus}: another issue completed first; nothing was written.");
            }).ConfigureAwait(false);

        return attachmentId;
    }

    internal Task RecordIssueAsync(IssueRecord issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);

        return MutateTypeStateAndPersistAsync(
            () =>
            {
                var state = new Dictionary<string, string?>(StringComparer.Ordinal);
                WriteJson(state, nameof(Issue), issue);
                state[nameof(Status)] = EvidenceStatus.Issued.ToString();
                return state;
            },
            () =>
            {
                _issue = issue;
                _status = EvidenceStatus.Issued;
            },
            $"Issued as '{issue.IssueReference}' rev '{issue.Revision}' to '{issue.Client}'.",
            cancellationToken);
    }

    /// <summary>Moves <see cref="Status"/> to <paramref name="target"/> alone, with no other field changed. Used by <c>ReviseAsync</c> to reopen an Issued record as Draft on its new revision.</summary>
    internal Task SetStatusOnlyAsync(EvidenceStatus target, string auditDetail, CancellationToken cancellationToken = default)
    {
        return MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(Status)] = target.ToString() },
            () => _status = target,
            auditDetail,
            cancellationToken);
    }

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(Classification)] = _classification.ToString();
        state[nameof(SubjectId)] = _subjectId?.ToString();
        state[nameof(AuthorIdentityId)] = _authorIdentityId;
        WriteJson(state, nameof(Citations), _citations);
        WriteJson(state, nameof(DeclaredFigures), _declaredFigures);
        state[nameof(Status)] = _status.ToString();
        WriteJson(state, nameof(Check), _check);
        WriteJson(state, nameof(Issue), _issue);
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _subjectId = state.TypeGuid(nameof(SubjectId));
        _citations = ReadCitations(state);
        _declaredFigures = ReadDeclaredFigures(state);
        _status = ReadStatus(state);
        _check = state.TypeJson<CheckRecord>(nameof(Check));
        _issue = state.TypeJson<IssueRecord>(nameof(Issue));
    }

    private static EvidenceClassification ReadClassification(EngineeringObjectState state) =>
        Enum.TryParse<EvidenceClassification>(state.Type(nameof(Classification)), out var value) ? value : EvidenceClassification.Other;

    private static EvidenceStatus ReadStatus(EngineeringObjectState state) =>
        Enum.TryParse<EvidenceStatus>(state.Type(nameof(Status)), out var value) ? value : EvidenceStatus.Draft;

    private static List<EvidenceCitation> ReadCitations(EngineeringObjectState state) =>
        state.TypeJson<List<EvidenceCitation>>(nameof(Citations)) ?? [];

    private static List<DeclaredFigure> ReadDeclaredFigures(EngineeringObjectState state) =>
        state.TypeJson<List<DeclaredFigure>>(nameof(DeclaredFigures)) ?? [];

    static Evidence IRehydratable<Evidence>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            ReadClassification(state), state.TypeGuid(nameof(SubjectId)), state.Type(nameof(AuthorIdentityId)) ?? string.Empty,
            ReadCitations(state), ReadDeclaredFigures(state), ReadStatus(state),
            state.TypeJson<CheckRecord>(nameof(Check)), state.TypeJson<IssueRecord>(nameof(Issue)));
}

/// <summary>Raised inside the Issue transaction when the stored Evidence no longer permits issuing (a concurrent Issue won); the transaction rolls back.</summary>
public sealed class ConcurrentIssueException : InvalidOperationException
{
    public ConcurrentIssueException(string message) : base(message) { }

    public ConcurrentIssueException() { }

    public ConcurrentIssueException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised inside the Check transaction when the stored Evidence no longer permits checking (a concurrent Check won); the transaction rolls back.</summary>
public sealed class ConcurrentCheckException : InvalidOperationException
{
    public ConcurrentCheckException(string message) : base(message) { }

    public ConcurrentCheckException() { }

    public ConcurrentCheckException(string message, Exception innerException) : base(message, innerException) { }
}
