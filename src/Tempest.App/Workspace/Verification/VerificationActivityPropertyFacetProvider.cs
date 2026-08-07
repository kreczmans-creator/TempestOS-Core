using Tempest.Core.EngineeringDomain;
using Tempest.Core.Verification;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Supplies the Property Inspector's own real facets for
/// <c>"VerificationActivity"</c> — the fifth <see cref="IPropertyFacetProvider"/>
/// backed by a real Engineering discipline, after Mechanical (`WP 9.0A`),
/// Requirements (`WP 9.1A`), Calculations (`WP 9.2A`), and Documents
/// (`WP 9.4A`). Every generic facet
/// <see cref="Documents.DocumentsPropertyFacetProvider"/> already reads
/// via the same casts (Identity/Metadata/Lifecycle/Revisions/Parent/
/// Deletable), plus this Work Package's own additions — Subject/Method
/// (from <see cref="IVerificationActivity"/> directly), Result History,
/// Latest Outcome, Latest Criteria/Evidence (from
/// <see cref="VerificationRecordReader"/>), and Digital Thread links, read
/// via <see cref="IHasRelationships.GetRelationshipsAsync"/>/
/// <see cref="EngineeringDomainContext.RelationshipRepository"/> directly
/// — never <see cref="ITraceable.GetEvidenceAsync"/> (honestly resolves
/// empty for every Verification Activity today, `TD-30`, not introduced
/// here) and never <see cref="IVerificationService.GetVerificationHistoryAsync"/>
/// (permission-gated — see <see cref="VerificationRecordReader"/>'s own
/// remarks).
/// </summary>
public sealed class VerificationActivityPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="VerificationActivityPropertyFacetProvider"/> class.</summary>
    public VerificationActivityPropertyFacetProvider(string kind, EngineeringDomainContext context)
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
            facets.Add(new("Name", identity.DisplayName, PropertyFacetKind.Identity));

        if (target is IVerificationActivity activity)
        {
            facets.Add(new("Subject", activity.SubjectId.ToString(), PropertyFacetKind.Relationship));
            facets.Add(new("Method", activity.Method, PropertyFacetKind.DisciplineSpecific));
        }

        if (target is IHasMetadata metadata)
        {
            if (metadata.Owner is { } owner)
                facets.Add(new("Owner", owner, PropertyFacetKind.Provenance));

            if (metadata.Discipline is { } discipline)
                facets.Add(new("Discipline", discipline, PropertyFacetKind.DisciplineSpecific));

            if (metadata.Notes is { } notes)
                facets.Add(new("Description / Notes", notes, PropertyFacetKind.DisciplineSpecific));
        }

        // Status/Approval — IHasLifecycle, via the existing LifecycleTransitionTable.
        // Draft = "Verification Plan," InReview+ = "Verification Activity"
        // under way — see SetVerificationActivityStatusCommand's own
        // remarks (`ADR-0090`).
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

        if (target is IVerificationActivity)
            await AddVerificationResultFacetsAsync(objectId, facets, cancellationToken).ConfigureAwait(false);

        return facets;
    }

    /// <summary>
    /// Adds this Work Package's own Verification-Activity-specific facets:
    /// Result History, Latest Outcome, Latest Criteria/Evidence (from
    /// <see cref="VerificationRecordReader"/>), and Digital Thread links.
    /// </summary>
    private async Task AddVerificationResultFacetsAsync(Guid objectId, List<PropertyFacet> facets, CancellationToken cancellationToken)
    {
        var history = await VerificationRecordReader.GetResultHistoryAsync(_context, objectId, cancellationToken).ConfigureAwait(false);

        facets.Add(new("Result History", history.Count > 0 ? $"{history.Count} record(s)" : "Never recorded", PropertyFacetKind.DisciplineSpecific));

        if (history.Count > 0)
        {
            var latest = history[^1];

            facets.Add(new("Latest Outcome", latest.Outcome.ToString(), PropertyFacetKind.DisciplineSpecific));
            facets.Add(new("Latest Verified At", latest.VerifiedAt.ToString("u"), PropertyFacetKind.Provenance));
            facets.Add(new("Latest Verified By", latest.VerifiedByPrincipalId, PropertyFacetKind.Provenance));

            facets.Add(new(
                "Latest Criteria",
                latest.Criteria.Count > 0 ? string.Join("; ", latest.Criteria.Select(c => $"{c.Description} ({(c.IsSatisfied ? "Satisfied" : "Not Satisfied")})")) : "(none)",
                PropertyFacetKind.DisciplineSpecific));

            facets.Add(new(
                "Latest Evidence",
                latest.Evidence.Count > 0 ? string.Join("; ", latest.Evidence.Select(e => e.Reference is { } r ? $"{e.Description} ({r})" : e.Description)) : "(none)",
                PropertyFacetKind.DisciplineSpecific));

            if (latest.ReferencedMaterialIds.Count > 0)
                facets.Add(new("Referenced Materials", string.Join(", ", latest.ReferencedMaterialIds), PropertyFacetKind.Relationship));

            if (latest.LinkedCalculationRecordIds.Count > 0)
                facets.Add(new("Based On Calculation Record(s)", string.Join(", ", latest.LinkedCalculationRecordIds), PropertyFacetKind.Relationship));

            if (latest.LinkedDocumentIds.Count > 0)
                facets.Add(new("Referenced Document(s)", string.Join(", ", latest.LinkedDocumentIds), PropertyFacetKind.Relationship));
        }

        // Digital Thread — the existing relationship read, never a new
        // traversal. Incoming "verifiedBy" = what subject this activity
        // verifies is asserted from (e.g. a Requirement/Assembly/Component
        // linking to this activity); the activity's own outgoing links
        // (beyond its own records) are read the same way.
        var incoming = await _context.RelationshipRepository.GetIncomingAsync(objectId, cancellationToken).ConfigureAwait(false);
        var verifies = incoming.Where(r => string.Equals(r.RelationshipKind, "verifiedBy", StringComparison.Ordinal)).ToList();
        if (verifies.Count > 0)
            facets.Add(new("Verifies (Digital Thread)", string.Join(", ", verifies.Select(r => r.SourceId.ToString())), PropertyFacetKind.Relationship));

        var outgoingReferences = await _context.RelationshipRepository.GetOutgoingAsync(objectId, cancellationToken).ConfigureAwait(false);
        var references = outgoingReferences.Where(r => string.Equals(r.RelationshipKind, "references", StringComparison.Ordinal)).ToList();
        if (references.Count > 0)
            facets.Add(new("References (Digital Thread)", string.Join(", ", references.Select(r => r.TargetId.ToString())), PropertyFacetKind.Relationship));
    }
}
