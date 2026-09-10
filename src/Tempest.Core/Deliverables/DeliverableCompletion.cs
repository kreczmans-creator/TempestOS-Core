using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Deliverables;

/// <summary>
/// A canonical Kind recording that a project's own <c>Deliverable</c>
/// (the milestone-parented engineering object,
/// <see cref="Workspace.CanonicalObjectKinds.Deliverable"/>) was actually
/// completed: when, by whom, on what evidence and documents, and — where
/// it is billed at a fixed price rather than time — what that price is
/// (`WP 19.0A`, `ADR-0150`). Follows
/// <c>Tempest.Core.Evidence.Evidence</c>'s own shape exactly.
/// </summary>
/// <remarks>
/// <b>A deliverable can be completed once.</b> <see cref="DeliverableService.CompleteAsync"/>
/// refuses a second completion for the same <see cref="DeliverableId"/>,
/// returning the first — mirroring <c>Evidence.RecordIssueAsync</c>'s own
/// once-only shape at the service layer rather than this class's own.
/// <see cref="InvoicedBy"/> is set once and never cleared, exactly as
/// <c>TimesheetEntry.InvoicedBy</c>. Not ERP, not PLM (`D-028`): this
/// class raises no invoice request of its own — <c>WP 19.1A</c> reads
/// this link, never writes it a second way.
/// </remarks>
public sealed class DeliverableCompletion : EngineeringObjectBase, IRehydratable<DeliverableCompletion>
{
    /// <summary>The <see cref="IEngineeringObject.Kind"/> every deliverable completion's own backing document carries (`ADR-0105`).</summary>
    public const string CanonicalKind = "DeliverableCompletion";

    private readonly Guid _deliverableId;
    private readonly DateOnly _completedOn;
    private readonly string _principalIdentityId;
    private readonly List<Guid> _issuedEvidenceIds;
    private readonly List<Guid> _documentIds;
    private readonly Money? _fixedPriceValue;
    private Guid? _invoicedBy;

    /// <summary>Initialises a new instance of the <see cref="DeliverableCompletion"/> class.</summary>
    public DeliverableCompletion(
        IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context,
        string? identifier, string displayName, EngineeringObjectMetadata metadata,
        Guid deliverableId, DateOnly completedOn, string principalIdentityId,
        IReadOnlyList<Guid>? issuedEvidenceIds = null, IReadOnlyList<Guid>? documentIds = null,
        Money? fixedPriceValue = null, Guid? invoicedBy = null)
        : base(document, currentRevision, context, identifier, displayName, metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(principalIdentityId);

        _deliverableId = deliverableId;
        _completedOn = completedOn;
        _principalIdentityId = principalIdentityId;
        _issuedEvidenceIds = issuedEvidenceIds is null ? [] : [.. issuedEvidenceIds];
        _documentIds = documentIds is null ? [] : [.. documentIds];
        _fixedPriceValue = fixedPriceValue;
        _invoicedBy = invoicedBy;
    }

    /// <summary>The <c>Deliverable</c> this records the completion of.</summary>
    public Guid DeliverableId => _deliverableId;

    /// <summary>The day the deliverable was completed.</summary>
    public DateOnly CompletedOn => _completedOn;

    /// <summary>The principal who completed it.</summary>
    public string PrincipalIdentityId => _principalIdentityId;

    /// <summary>The Evidence records, each Issued, this completion is supported by.</summary>
    public IReadOnlyList<Guid> IssuedEvidenceIds => _issuedEvidenceIds;

    /// <summary>The documents this completion is supported by.</summary>
    public IReadOnlyList<Guid> DocumentIds => _documentIds;

    /// <summary>The fixed price this deliverable is billed at, when it is billed at a fixed price rather than time. <see langword="null"/> otherwise.</summary>
    public Money? FixedPriceValue => _fixedPriceValue;

    /// <summary>The invoice request this completion was billed on, set once and never cleared. <see langword="null"/> until then.</summary>
    public Guid? InvoicedBy => _invoicedBy;

    /// <summary>Sets <see cref="InvoicedBy"/> to <paramref name="requestId"/>, once. <see cref="DeliverableService.MarkInvoicedAsync"/> refuses a second set before this ever runs.</summary>
    internal Task MarkInvoicedAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        MutateTypeStateAndPersistAsync(
            () => new Dictionary<string, string?>(StringComparer.Ordinal) { [nameof(InvoicedBy)] = requestId.ToString() },
            () => _invoicedBy = requestId,
            $"Invoiced by request '{requestId:N}'.",
            cancellationToken);

    /// <inheritdoc />
    protected override void CaptureTypeState(IDictionary<string, string?> state)
    {
        state[nameof(DeliverableId)] = _deliverableId.ToString();
        WriteJson(state, nameof(CompletedOn), _completedOn);
        state[nameof(PrincipalIdentityId)] = _principalIdentityId;
        WriteGuidList(state, nameof(IssuedEvidenceIds), _issuedEvidenceIds);
        WriteGuidList(state, nameof(DocumentIds), _documentIds);
        WriteJson(state, nameof(FixedPriceValue), _fixedPriceValue);
        state[nameof(InvoicedBy)] = _invoicedBy?.ToString();
    }

    /// <inheritdoc />
    protected override void ApplyTypeState(EngineeringObjectState state)
    {
        _invoicedBy = state.TypeGuid(nameof(InvoicedBy));
    }

    static DeliverableCompletion IRehydratable<DeliverableCompletion>.Rehydrate(IEngineeringDocument document, IDocumentRevision currentRevision, EngineeringDomainContext context, EngineeringObjectState state) =>
        new(document, currentRevision, context, state.Identifier, state.DisplayName, state.Metadata,
            state.TypeGuidOrEmpty(nameof(DeliverableId)),
            state.TypeJson<DateOnly>(nameof(CompletedOn)),
            state.Type(nameof(PrincipalIdentityId)) ?? string.Empty,
            state.TypeGuidList(nameof(IssuedEvidenceIds)),
            state.TypeGuidList(nameof(DocumentIds)),
            state.TypeJson<Money?>(nameof(FixedPriceValue)),
            state.TypeGuid(nameof(InvoicedBy)));
}
