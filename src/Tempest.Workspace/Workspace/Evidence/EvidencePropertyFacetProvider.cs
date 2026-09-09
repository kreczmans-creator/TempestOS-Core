using Tempest.Core.Evidence;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Evidence;

/// <summary>
/// Supplies the Property Inspector's own real facets for
/// <see cref="Core.Evidence.Evidence.CanonicalKind"/>: classification,
/// subject (named, not a bare id), status, citations, declared figures,
/// check and issue (`ADR-0148`). Mirrors
/// <see cref="Verification.VerificationActivityPropertyFacetProvider"/>'s
/// own shape — the generic facets every provider reads via the same casts,
/// plus this Kind's own additions.
/// </summary>
public sealed class EvidencePropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="EvidencePropertyFacetProvider"/> class.</summary>
    public EvidencePropertyFacetProvider(string kind, EngineeringDomainContext context)
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

        if (target is not Core.Evidence.Evidence evidence)
            return facets;

        facets.Add(new("Classification", evidence.Classification.ToString(), PropertyFacetKind.DisciplineSpecific));

        // Named, not a bare Guid (`WP 18.0A` acceptance): this Kind's own
        // subject is a tag over the project's own structure, and the one
        // fact a reader actually wants is what it points at, not the id
        // that happens to encode it.
        if (evidence.SubjectId is { } subjectId)
        {
            var subject = await _context.Repository.FindAsync(subjectId, cancellationToken).ConfigureAwait(false);
            var subjectName = subject is IHasBusinessIdentifier subjectIdentity
                ? $"{subjectIdentity.DisplayName} ({subject!.Kind})"
                : $"{subjectId} (not found)";

            facets.Add(new("Subject", subjectName, PropertyFacetKind.ObjectReference));
        }

        facets.Add(new("Author", evidence.AuthorIdentityId, PropertyFacetKind.Principal));
        facets.Add(new("Status", evidence.Status.ToString(), PropertyFacetKind.DisciplineSpecific));

        facets.Add(new(
            "Citations",
            evidence.Citations.Count == 0 ? "(none)" : $"{evidence.Citations.Count}: " + string.Join("; ", evidence.Citations.Select(c => $"{c.Pin} — {c.RecordDisplayName}")),
            PropertyFacetKind.Relationship));

        facets.Add(new(
            "Declared Figures",
            evidence.DeclaredFigures.Count == 0 ? "(none)" : string.Join("; ", evidence.DeclaredFigures.Select(f => $"{f.Name} ({f.Role}) = {f.Quantity}")),
            PropertyFacetKind.DisciplineSpecific));

        facets.Add(new(
            "Check",
            evidence.Check is { } check ? $"{check.Outcome} by {check.CheckerName} ({check.CheckerOrganisation}) on {check.DateUtc:u}" : "(not checked)",
            PropertyFacetKind.DisciplineSpecific));

        facets.Add(new(
            "Issue",
            evidence.Issue is { } issue ? $"'{issue.IssueReference}' rev '{issue.Revision}' to '{issue.Client}' on {issue.DateUtc:u}" : "(not issued)",
            PropertyFacetKind.DisciplineSpecific));

        if (target is IHasParent hasParent)
            facets.Add(new("Parent", hasParent.ParentId?.ToString() ?? "(top level)", PropertyFacetKind.ObjectReference));

        if (target is IHasRevisions revisions)
        {
            facets.Add(new("Revision", target.CurrentRevisionNumber.ToString(), PropertyFacetKind.Revision));
            facets.Add(new("Last Revised By", revisions.AuthorPrincipalId, PropertyFacetKind.Principal));
        }

        if (target is IDeletable { IsDeleted: true })
            facets.Add(new("Deleted", "Yes", PropertyFacetKind.DisciplineSpecific));

        return facets;
    }
}
