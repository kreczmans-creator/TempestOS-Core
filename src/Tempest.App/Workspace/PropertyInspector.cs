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
/// <para>
/// `WP8.1B`'s own disclosed limitation — "every displayed facet is derived
/// purely from the selection tuple itself (Id, Kind), no Engineering Core
/// service is ever consulted" — held until a real discipline existed to
/// consult. `WP 9.0A` closes it with a third Kind-keyed provider category,
/// <see cref="IPropertyFacetProvider"/> (`ADR-0067`): when one is registered
/// for the selection's own Kind, its real facets are shown; otherwise this
/// class's original Id/Kind-only fallback is unchanged, so every
/// still-unregistered Kind (including every `Workspace/Samples` selection)
/// behaves exactly as `WP8.1B` shipped it.
/// </para>
/// </remarks>
internal sealed class PropertyInspector : IPropertyInspector, IEventHandler<WorkspaceSelectionChangedEvent>
{
    private readonly IReadOnlyDictionary<string, IPropertyFacetProvider> _facetProviders;
    private IReadOnlyList<PropertyFacet> _facets = [];

    /// <summary>Initialises a new instance of the <see cref="PropertyInspector"/> class.</summary>
    /// <param name="facetProviders">The Kind-keyed facet providers registered through <see cref="IWorkspaceManager.RegisterFacetProvider"/> (`WP 9.0A`). May be empty — every Kind then falls back to the original Id/Kind-only facets.</param>
    public PropertyInspector(IReadOnlyDictionary<string, IPropertyFacetProvider>? facetProviders = null)
    {
        _facetProviders = facetProviders ?? new Dictionary<string, IPropertyFacetProvider>(StringComparer.Ordinal);
    }

    /// <summary>
    /// This panel's own stable, well-known identifier — see
    /// <see cref="ProjectExplorer.WellKnownId"/>'s own identical remarks
    /// (`WP 10.0B`'s disclosed found-and-fixed defect: this field was
    /// originally <c>Guid.NewGuid()</c>, which could never round-trip a
    /// persisted placement across a genuine process restart).
    /// </summary>
    public static readonly Guid WellKnownId = new("3d4e5f6a-7b8c-4d9e-8f1a-2b3c4d5e6f7a");

    /// <inheritdoc />
    public Guid Id { get; } = WellKnownId;

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
    public async Task InspectAsync(Guid objectId, string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        if (_facetProviders.TryGetValue(kind, out var provider))
        {
            _facets = await provider.GetFacetsAsync(objectId, cancellationToken).ConfigureAwait(false);
            return;
        }

        _facets = new List<PropertyFacet>
        {
            new("Id", objectId.ToString(), PropertyFacetKind.Identity),
            new("Kind", kind, PropertyFacetKind.Identity),
        };
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
