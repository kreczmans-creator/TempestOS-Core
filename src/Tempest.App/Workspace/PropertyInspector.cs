using Tempest.Core.Events;

namespace Tempest.App.Workspace;

/// <summary>
/// The concrete <see cref="IPropertyInspector"/> implementation. Reacts
/// automatically to <see cref="WorkspaceSelectionChangedEvent"/> — never
/// subscribes to <see cref="ISelectionService"/> directly, keeping the "who
/// reacts to what" wiring in the Workspace's own composition
/// (`WP8.0B Workspace Contracts.md` §11).
/// </summary>
/// <remarks>
/// No <see cref="IProjectExplorerNodeProvider"/>-shaped facet source exists
/// for this Work Package (no engineering functionality) — every displayed
/// facet is derived purely from the selection tuple itself (Id, Kind), no
/// Engineering Core service is ever consulted.
/// </remarks>
internal sealed class PropertyInspector : IPropertyInspector, IEventHandler<WorkspaceSelectionChangedEvent>
{
    private IReadOnlyList<PropertyFacet> _facets = [];

    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public string Title => "Properties";

    /// <inheritdoc />
    public WorkspaceDockPosition DockPosition => WorkspaceDockPosition.Right;

    /// <inheritdoc />
    public bool IsVisible { get; private set; } = true;

    /// <inheritdoc />
    public IReadOnlyList<PropertyFacet> CurrentFacets => _facets;

    /// <inheritdoc />
    public Task ShowAsync(CancellationToken cancellationToken = default)
    {
        IsVisible = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HideAsync(CancellationToken cancellationToken = default)
    {
        IsVisible = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task InspectAsync(Guid objectId, string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        _facets = new List<PropertyFacet>
        {
            new("Id", objectId.ToString(), PropertyFacetKind.Identity),
            new("Kind", kind, PropertyFacetKind.Identity),
        };

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _facets = [];
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(WorkspaceSelectionChangedEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return @event.Current is null
            ? ClearAsync(cancellationToken)
            : InspectAsync(@event.Current.ObjectId, @event.Current.Kind, cancellationToken);
    }
}
