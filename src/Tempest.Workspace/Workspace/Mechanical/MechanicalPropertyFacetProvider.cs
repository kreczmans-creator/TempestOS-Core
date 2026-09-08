using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>
/// Supplies the Property Inspector's own real facets for one Mechanical
/// Product Structure Kind — Engineering identifier, Name, Description/Notes,
/// Revision, Status, Owner, Discipline, Classification, Tags, a
/// Baseline/Released-state display sourced from any <c>Configuration</c>/
/// <c>Baseline</c>/<c>Release</c> that references the object, and (`WP 9.0B`)
/// BOM line facets (Quantity/Unit of Measure/Find Number/Item Number/
/// Reference Designator) plus, for a selected <c>Configuration</c>/
/// <c>Baseline</c>/<c>Release</c> itself, its own member count — never a new
/// Domain read, only the facets `Contracts/PhysicalConfiguration.cs`/
/// `Contracts/BillOfMaterials.cs` already expose. Configuration Management
/// remains display only, no workflow, exactly as both Work Packages'
/// own controlling instructions require.
/// </summary>
public sealed class MechanicalPropertyFacetProvider : IPropertyFacetProvider
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="MechanicalPropertyFacetProvider"/> class.</summary>
    /// <param name="kind">The single object Kind this provider supplies facets for.</param>
    public MechanicalPropertyFacetProvider(string kind, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);

        Kind = kind;
        _context = context;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known object.</exception>
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
                facets.Add(new("Engineering Identifier", engineeringIdentifier, PropertyFacetKind.Identity));
        }

        if (target is IHasMetadata metadata)
        {
            if (metadata.Owner is { } owner)
                facets.Add(new("Owner", owner, PropertyFacetKind.Provenance));

            if (metadata.Discipline is { } discipline)
                facets.Add(new("Discipline", discipline, PropertyFacetKind.DisciplineSpecific));

            if (metadata.Classification is { } classification)
                facets.Add(new("Classification", classification, PropertyFacetKind.DisciplineSpecific));

            if (metadata.Tags.Count > 0)
                facets.Add(new("Tags", string.Join(", ", metadata.Tags), PropertyFacetKind.DisciplineSpecific));

            if (metadata.Notes is { } notes)
                facets.Add(new("Description / Notes", notes, PropertyFacetKind.DisciplineSpecific));
        }

        if (target is IHasLifecycle lifecycle)
        {
            facets.Add(new("Status", lifecycle.Status.ToString(), PropertyFacetKind.DisciplineSpecific));
            facets.Add(new("Released", lifecycle.Status == LifecycleState.Released ? "Yes" : "No", PropertyFacetKind.DisciplineSpecific));
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

        // WP 9.0B — Bill of Materials line.
        if (target is IHasBomLine bomLine)
        {
            facets.Add(new("Quantity", bomLine.Quantity.ToString("0.####"), PropertyFacetKind.DisciplineSpecific));

            if (bomLine.UnitOfMeasure is { } unitOfMeasure)
                facets.Add(new("Unit of Measure", unitOfMeasure, PropertyFacetKind.DisciplineSpecific));

            if (bomLine.FindNumber is { } findNumber)
                facets.Add(new("Find Number", findNumber, PropertyFacetKind.DisciplineSpecific));

            if (bomLine.ItemNumber is { } itemNumber)
                facets.Add(new("Item Number", itemNumber, PropertyFacetKind.DisciplineSpecific));

            if (bomLine.ReferenceDesignator is { } referenceDesignator)
                facets.Add(new("Reference Designator", referenceDesignator, PropertyFacetKind.DisciplineSpecific));
        }

        // WP 9.0B — the selected object is itself a Configuration/Baseline/Release.
        if (target is IConfiguration configuration)
            facets.Add(new("Configuration Members", configuration.MemberRevisions.Count.ToString(), PropertyFacetKind.DisciplineSpecific));

        if (await GetBaselineDisplayAsync(objectId, cancellationToken).ConfigureAwait(false) is { } baselineDisplay)
            facets.Add(new("Baseline", baselineDisplay, PropertyFacetKind.DisciplineSpecific));

        return facets;
    }

    /// <summary>Every <c>Configuration</c>/<c>Baseline</c>/<c>Release</c> object (three distinct <c>Kind</c> strings, `WP 9.0B`) whose own <see cref="IConfiguration.MemberRevisions"/> references <paramref name="objectId"/>.</summary>
    private async Task<string?> GetBaselineDisplayAsync(Guid objectId, CancellationToken cancellationToken)
    {
        var memberOf = new List<string>();

        foreach (var kind in new[] { "Configuration", "Baseline", "Release" })
        {
            var objects = await _context.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false);

            memberOf.AddRange(objects
                .OfType<IConfiguration>()
                .Where(c => c.MemberRevisions.Any(m => m.ObjectId == objectId))
                .Select(c => (c as IHasBusinessIdentifier)?.DisplayName ?? c.Id.ToString()));
        }

        return memberOf.Count > 0 ? string.Join(", ", memberOf) : null;
    }
}
