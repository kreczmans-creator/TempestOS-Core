namespace Tempest.App.Workspace;

/// <summary>Displays the shared and discipline-specific facets of the currently selected engineering object.</summary>
public interface IPropertyInspector : IWorkspacePanel
{
    /// <summary>Inspects <paramref name="objectId"/>, populating <see cref="CurrentFacets"/>.</summary>
    Task InspectAsync(Guid objectId, string kind, CancellationToken cancellationToken = default);

    /// <summary>Clears <see cref="CurrentFacets"/> — nothing selected.</summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the currently displayed facets. Empty if nothing is selected, or if this Work Package's own shell has no facet source registered yet.</summary>
    IReadOnlyList<PropertyFacet> CurrentFacets { get; }
}
