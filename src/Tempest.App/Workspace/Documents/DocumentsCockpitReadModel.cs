using Tempest.App.Workspace;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Documents;

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
internal sealed class DocumentsCockpitReadModel
{
    private readonly EngineeringDomainContext _domainContext;

    /// <summary>Initialises a new instance of the <see cref="DocumentsCockpitReadModel"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this read-model queries directly.</param>
    public DocumentsCockpitReadModel(EngineeringDomainContext domainContext)
    {
        ArgumentNullException.ThrowIfNull(domainContext);

        _domainContext = domainContext;
    }

    /// <summary>Gets every live (non-deleted) Document Domain object — <c>"Document"</c>, <c>"Drawing"</c>, or <c>"CadModel"</c> — a real read via <see cref="EngineeringDomainContext.Repository"/>.</summary>
    public IReadOnlyList<IEngineeringObject> LiveDocuments
    {
        get
        {
            var documents = new List<IEngineeringObject>();

            foreach (var kind in DocumentObjectFactoryRegistry.SupportedKinds)
            {
                documents.AddRange(_domainContext.Repository.ListByKindAsync(kind).GetAwaiter().GetResult()
                    .Where(o => o is not IDeletable { IsDeleted: true }));
            }

            return documents;
        }
    }

    /// <summary>Gets the number of live Documents — the Cockpit's own cross-discipline KPI summary reads this directly.</summary>
    public int Count => LiveDocuments.Count;

    /// <summary>
    /// Gets whether <paramref name="document"/> has "Missing Evidence" —
    /// a disclosed heuristic: zero Attachments recorded and zero
    /// <c>"documentedBy"</c>/<c>"references"</c> relationships in either
    /// direction (the existing Digital Thread read, never a new
    /// traversal).
    /// </summary>
    private bool HasMissingEvidence(IEngineeringObject document)
    {
        var hasAttachment = document is IHasAttachments attachable
            && attachable.GetAttachmentsAsync().GetAwaiter().GetResult().Count > 0;

        if (hasAttachment)
            return false;

        var outgoing = _domainContext.RelationshipRepository.GetOutgoingAsync(document.Id).GetAwaiter().GetResult();
        var incoming = _domainContext.RelationshipRepository.GetIncomingAsync(document.Id).GetAwaiter().GetResult();

        var hasLink = outgoing.Any(r => r.RelationshipKind is "references" or "documentedBy")
            || incoming.Any(r => r.RelationshipKind is "references" or "documentedBy");

        return !hasLink;
    }

    /// <summary>Gets the number of live Documents with <see cref="HasMissingEvidence"/> — the Cockpit's own "Missing Evidence" KPI.</summary>
    private int MissingEvidenceCount => LiveDocuments.Count(HasMissingEvidence);

    /// <summary>Gets the number of live Documents that are <see cref="LifecycleState.InReview"/> — the Cockpit's own "Outstanding Reviews" KPI/"Outstanding Actions" signal.</summary>
    public int OutstandingReviews =>
        LiveDocuments.Count(d => d is IHasLifecycle { Status: LifecycleState.InReview });

    /// <summary>Gets the number of live Documents that are <see cref="LifecycleState.InReview"/> or have <see cref="HasMissingEvidence"/> — the Cockpit's own "Documents need attention"/"Outstanding Actions" signal.</summary>
    public int OutstandingActions => OutstandingReviews + MissingEvidenceCount;

    /// <summary>
    /// Gets the Documentation discipline's own status:
    /// <see cref="EngineeringHealthStatus.Unknown"/> if no live Document
    /// exists yet; <see cref="EngineeringHealthStatus.Attention"/> if any
    /// is awaiting review or has <see cref="HasMissingEvidence"/>;
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
