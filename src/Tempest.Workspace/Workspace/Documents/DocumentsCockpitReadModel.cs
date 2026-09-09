using Tempest.Workspace;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Documents;

/// <summary>
/// The Documents discipline's own Engineering Cockpit read-model —
/// extracted, `WP 12.0B` (`ADR-0103`), from <see cref="EngineeringCockpit"/>'s
/// own previous Documents-specific members, unmodified in behaviour. A
/// collaborator under `ADR-0103`: constructed once by
/// <see cref="EngineeringCockpit"/> (the composition root), declaring
/// only the one dependency it actually needs, never DI-registered, never
/// referencing <see cref="EngineeringCockpit"/> or any sibling
/// discipline collaborator back.
/// </summary>
/// <remarks>
/// Documents deliberately has no <c>GetBlockedMessages</c> member — no
/// Document Domain concept represents an unrecoverable failure state, so
/// this discipline never contributed to <see cref="EngineeringCockpit.BlockedItems"/>
/// before this move either.
/// </remarks>
/// <remarks>
/// <b>`WP 18.1A-R1`.</b> Every persistence-backed read (the per-Kind
/// listing, and the per-document attachment/relationship reads
/// <see cref="HasMissingEvidenceAsync"/> needs) now runs inside
/// <see cref="LoadAsync"/>, awaited once per Cockpit render
/// (<see cref="EngineeringCockpit.PrimeAsync"/>) rather than blocked on
/// synchronously from every property access. Every property below is now
/// a pure, in-memory read of what <see cref="LoadAsync"/> last loaded,
/// honestly empty until the first call completes.
/// </remarks>
internal sealed class DocumentsCockpitReadModel
{
    private readonly EngineeringDomainContext _domainContext;
    private IReadOnlyList<IEngineeringObject> _liveDocuments = [];
    private int _missingEvidenceCount;

    /// <summary>Initialises a new instance of the <see cref="DocumentsCockpitReadModel"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this read-model queries directly.</param>
    public DocumentsCockpitReadModel(EngineeringDomainContext domainContext)
    {
        ArgumentNullException.ThrowIfNull(domainContext);

        _domainContext = domainContext;
    }

    /// <summary>Loads every live Document and, for each, whether it <see cref="HasMissingEvidenceAsync"/> — the two reads every property below is derived from.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var documents = new List<IEngineeringObject>();

        foreach (var kind in DocumentObjectFactoryRegistry.SupportedKinds)
        {
            var byKind = await _domainContext.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false);
            documents.AddRange(byKind.Where(o => o is not IDeletable { IsDeleted: true }));
        }

        _liveDocuments = documents;

        var missingEvidenceCount = 0;
        foreach (var document in documents)
        {
            if (await HasMissingEvidenceAsync(document, cancellationToken).ConfigureAwait(false))
                missingEvidenceCount++;
        }

        _missingEvidenceCount = missingEvidenceCount;
    }

    /// <summary>Gets every live (non-deleted) Document Domain object — <c>"Document"</c>, <c>"Drawing"</c>, or <c>"CadModel"</c> — loaded by <see cref="LoadAsync"/>.</summary>
    public IReadOnlyList<IEngineeringObject> LiveDocuments => _liveDocuments;

    /// <summary>Gets the number of live Documents — the Cockpit's own cross-discipline KPI summary reads this directly.</summary>
    public int Count => LiveDocuments.Count;

    /// <summary>
    /// Gets whether <paramref name="document"/> has "Missing Evidence" —
    /// a disclosed heuristic: zero Attachments recorded and zero
    /// <c>"documentedBy"</c>/<c>"references"</c> relationships in either
    /// direction (the existing Digital Thread read, never a new
    /// traversal).
    /// </summary>
    private async Task<bool> HasMissingEvidenceAsync(IEngineeringObject document, CancellationToken cancellationToken)
    {
        var hasAttachment = document is IHasAttachments attachable
            && (await attachable.GetAttachmentsAsync(cancellationToken).ConfigureAwait(false)).Count > 0;

        if (hasAttachment)
            return false;

        var outgoing = await _domainContext.RelationshipRepository.GetOutgoingAsync(document.Id, cancellationToken).ConfigureAwait(false);
        var incoming = await _domainContext.RelationshipRepository.GetIncomingAsync(document.Id, cancellationToken).ConfigureAwait(false);

        var hasLink = outgoing.Any(r => r.RelationshipKind is "references" or "documentedBy")
            || incoming.Any(r => r.RelationshipKind is "references" or "documentedBy");

        return !hasLink;
    }

    /// <summary>Gets the number of live Documents with missing evidence (<see cref="HasMissingEvidenceAsync"/>) — the Cockpit's own "Missing Evidence" KPI.</summary>
    private int MissingEvidenceCount => _missingEvidenceCount;

    /// <summary>Gets the number of live Documents that are <see cref="LifecycleState.InReview"/> — the Cockpit's own "Outstanding Reviews" KPI/"Outstanding Actions" signal.</summary>
    public int OutstandingReviews =>
        LiveDocuments.Count(d => d is IHasLifecycle { Status: LifecycleState.InReview });

    /// <summary>Gets the number of live Documents that are <see cref="LifecycleState.InReview"/> or have missing evidence — the Cockpit's own "Documents need attention"/"Outstanding Actions" signal.</summary>
    public int OutstandingActions => OutstandingReviews + MissingEvidenceCount;

    /// <summary>
    /// Gets the Documentation discipline's own status:
    /// <see cref="EngineeringHealthStatus.Unknown"/> if no live Document
    /// exists yet; <see cref="EngineeringHealthStatus.Attention"/> if any
    /// is awaiting review or has missing evidence;
    /// <see cref="EngineeringHealthStatus.Healthy"/> otherwise. Never
    /// <see cref="EngineeringHealthStatus.Blocked"/>.
    /// </summary>
    public EngineeringHealthStatus Status
    {
        get
        {
            if (LiveDocuments.Count == 0)
                return EngineeringHealthStatus.Unknown;

            return OutstandingActions > 0
                ? EngineeringHealthStatus.Attention
                : EngineeringHealthStatus.Healthy;
        }
    }

    /// <summary>
    /// Gets the Documents discipline's own dedicated KPI card set: Total
    /// Documents, Draft, Review, Approved, Released, Outstanding Reviews,
    /// Missing Evidence, Documentation Health.
    /// </summary>
    public IReadOnlyList<CockpitKpiCard> KpiCards
    {
        get
        {
            var documents = LiveDocuments;
            var total = documents.Count;

            int CountStatus(LifecycleState status) =>
                documents.Count(d => d is IHasLifecycle lifecycle && lifecycle.Status == status);

            return
            [
                new("Total Documents", total.ToString(), IsPlaceholder: false),
                new("Draft", CountStatus(LifecycleState.Draft).ToString(), IsPlaceholder: false),
                new("Review", CountStatus(LifecycleState.InReview).ToString(), IsPlaceholder: false),
                new("Approved", CountStatus(LifecycleState.Approved).ToString(), IsPlaceholder: false),
                new("Released", CountStatus(LifecycleState.Released).ToString(), IsPlaceholder: false),
                new("Outstanding Reviews", OutstandingReviews.ToString(), IsPlaceholder: false),
                new("Missing Evidence", MissingEvidenceCount.ToString(), IsPlaceholder: false),
                new("Documentation Health", Status.ToString(), IsPlaceholder: false),
            ];
        }
    }

    /// <summary>Gets this discipline's own "What Needs Attention" contribution — a base entry, plus a conditional second entry when <see cref="OutstandingActions"/> is non-zero.</summary>
    public IReadOnlyList<CockpitAttentionItem> GetAttentionItems()
    {
        var items = new List<CockpitAttentionItem>
        {
            LiveDocuments.Count > 0
                ? new("Documents are live", $"{LiveDocuments.Count} Document(s) registered - the Project Explorer's own Documents area and the Engineering Cockpit's own Documentation KPIs reflect real Engineering Domain data (WP 9.4A).")
                : new("No Documents registered yet", "The Documents area has no live Document yet - this is expected, not a defect."),
        };

        if (OutstandingActions > 0)
        {
            items.Add(new(
                "Documents need attention",
                $"{OutstandingActions} Document(s) awaiting review or with missing evidence across {LiveDocuments.Count} live document(s). See the Documents area's own Property Inspector for detail."));
        }

        return items;
    }

    /// <summary>Gets this discipline's own "Open Actions" triage entry, or <see langword="null"/> if nothing is currently outstanding.</summary>
    public CockpitActionItem? GetOpenActionItem() =>
        OutstandingActions > 0
            ? new($"Triage {OutstandingActions} outstanding Document(s) (awaiting review or missing evidence)", "Engineer")
            : null;
}
