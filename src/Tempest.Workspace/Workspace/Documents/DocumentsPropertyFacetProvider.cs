using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Documents;

/// <summary>
/// Supplies the Property Inspector's own real facets for one Documents Kind
/// (<c>"Document"</c>/<c>"Drawing"</c>/<c>"CadModel"</c>) — the fourth
/// <see cref="IPropertyFacetProvider"/> backed by a real Engineering
/// discipline, after Mechanical's (`WP 9.0A`), Requirements' (`WP 9.1A`),
/// and Calculations' (`WP 9.2A`). Every generic facet
/// <see cref="Calculations.CalculationsPropertyFacetProvider"/> already
/// reads via the same casts (Identity/Metadata/Lifecycle/Revisions/Parent/
/// Deletable), plus this Work Package's own additions: Classification/
/// Category (this Work Package's own realisation of "document
/// classification"), Drawing Number/Model Format (Kind-specific), Document
/// Number (<see cref="IHasBusinessIdentifier.Identifier"/>, named to match
/// this Work Package's own vocabulary — the same underlying facet the
/// generic block already exposes as "Engineering Identifier", not a
/// duplicate mechanism), Attachments, and Digital Thread links, read via
/// <see cref="IHasRelationships.GetRelationshipsAsync"/>/
/// <see cref="EngineeringDomainContext.RelationshipRepository"/> directly —
/// never <see cref="ITraceable.GetEvidenceAsync"/>, which honestly resolves
/// empty for every Document today (mirrors
/// <see cref="Calculations.CalculationsPropertyFacetProvider"/>'s own
/// identical, already-disclosed treatment of the same pre-existing gap).
/// </summary>
public sealed class DocumentsPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="DocumentsPropertyFacetProvider"/> class.</summary>
    /// <param name="kind">The single object Kind this provider supplies facets for.</param>
    public DocumentsPropertyFacetProvider(string kind, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);

        Kind = kind;
        _context = context;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known object of this provider's own <see cref="Kind"/>.</exception>
    public async Task<IReadOnlyList<PropertyFacet>> GetFacetsAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        var target = await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ArgumentException($"'{objectId}' is not a known {Kind}.", nameof(objectId));

        var facets = new List<PropertyFacet>
        {
            new("Id", target.Id.ToString(), PropertyFacetKind.Identity),
            new("Kind", target.Kind, PropertyFacetKind.Identity),
        };

        if (target is IHasBusinessIdentifier identity)
        {
            facets.Add(new("Name", identity.DisplayName, PropertyFacetKind.Identity));

            if (identity.Identifier is { } engineeringIdentifier)
            {
                facets.Add(new("Engineering Identifier", engineeringIdentifier, PropertyFacetKind.Identity));
                facets.Add(new("Document Number", engineeringIdentifier, PropertyFacetKind.Identity));
            }
        }

        if (target is IDrawing { DrawingNumber: { } drawingNumber })
            facets.Add(new("Drawing Number", drawingNumber, PropertyFacetKind.Identity));

        if (target is ICadModel { ModelFormat: { } modelFormat })
            facets.Add(new("Model Format", modelFormat, PropertyFacetKind.DisciplineSpecific));

        if (target is IHasMetadata metadata)
        {
            facets.Add(new("Classification", metadata.Classification ?? "(none)", PropertyFacetKind.DisciplineSpecific));

            if (metadata.Owner is { } owner)
                facets.Add(new("Owner", owner, PropertyFacetKind.Provenance));

            if (metadata.Discipline is { } discipline)
                facets.Add(new("Discipline", discipline, PropertyFacetKind.DisciplineSpecific));

            if (metadata.Category is { } category)
                facets.Add(new("Category", category, PropertyFacetKind.DisciplineSpecific));

            if (metadata.Tags.Count > 0)
                facets.Add(new("Tags", string.Join(", ", metadata.Tags), PropertyFacetKind.DisciplineSpecific));

            if (metadata.Notes is { } notes)
                facets.Add(new("Description / Notes", notes, PropertyFacetKind.DisciplineSpecific));
        }

        // Status/Approval — IHasLifecycle, via the existing LifecycleTransitionTable.
        // This Work Package's own named statuses (Draft/Review/Approved/
        // Released) map directly onto LifecycleState's own existing values —
        // see SetDocumentStatusCommand's own remarks.
        if (target is IHasLifecycle lifecycle)
        {
            facets.Add(new("Status", lifecycle.Status.ToString(), PropertyFacetKind.DisciplineSpecific));
            facets.Add(new(
                "Approved",
                lifecycle.Status is LifecycleState.Approved or LifecycleState.Released ? "Yes" : "No",
                PropertyFacetKind.DisciplineSpecific));
        }

        if (target is IHasRevisions revisions)
        {
            facets.Add(new("Revision", target.CurrentRevisionNumber.ToString(), PropertyFacetKind.Revision));
            facets.Add(new("Last Revised By", revisions.AuthorPrincipalId, PropertyFacetKind.Provenance));
        }

        if (target is IHasParent hasParent)
            facets.Add(new("Parent", hasParent.ParentId?.ToString() ?? "(top level)", PropertyFacetKind.Relationship));

        if (target is IDeletable { IsDeleted: true })
            facets.Add(new("Deleted", "Yes", PropertyFacetKind.DisciplineSpecific));

        if (target is IHasAttachments attachable)
        {
            var attachments = await attachable.GetAttachmentsAsync(cancellationToken).ConfigureAwait(false);
            facets.Add(new(
                "Attachments",
                attachments.Count > 0 ? string.Join(", ", attachments.Select(a => a.FileName)) : "(none)",
                PropertyFacetKind.DisciplineSpecific));
        }

        await AddDigitalThreadFacetsAsync(objectId, facets, cancellationToken).ConfigureAwait(false);

        return facets;
    }

    /// <summary>
    /// Adds this Work Package's own Digital Thread facets — every
    /// <c>"documentedBy"</c>/<c>"references"</c> relationship in either
    /// direction, the existing relationship read
    /// (<see cref="EngineeringDomainContext.RelationshipRepository"/>),
    /// never a new traversal.
    /// </summary>
    private async Task AddDigitalThreadFacetsAsync(Guid objectId, List<PropertyFacet> facets, CancellationToken cancellationToken)
    {
        var outgoing = await _context.RelationshipRepository.GetOutgoingAsync(objectId, cancellationToken).ConfigureAwait(false);
        var references = outgoing.Where(r => string.Equals(r.RelationshipKind, "references", StringComparison.Ordinal)).ToList();
        if (references.Count > 0)
            facets.Add(new("References (Digital Thread)", string.Join(", ", references.Select(r => r.TargetId.ToString())), PropertyFacetKind.Relationship));

        var incoming = await _context.RelationshipRepository.GetIncomingAsync(objectId, cancellationToken).ConfigureAwait(false);
        var documents = incoming.Where(r => string.Equals(r.RelationshipKind, "documentedBy", StringComparison.Ordinal)).ToList();
        if (documents.Count > 0)
            facets.Add(new("Documents (Digital Thread)", string.Join(", ", documents.Select(r => r.SourceId.ToString())), PropertyFacetKind.Relationship));
    }
}
