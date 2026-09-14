using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Tasks;

/// <summary>
/// Presents one manual task to <see cref="IWorkspaceNavigation.OpenAsync"/>
/// (`WP 19.5C`) — a plain data wrapper, never itself rendered: this Kind's
/// own real rendering is the Object Editor's declaration-per-Kind path
/// (<c>KindEditorDeclarations.Task</c>), mirroring
/// <c>Quotations.QuotationObjectView</c>'s own identical shape and
/// identical reason.
/// </summary>
/// <remarks>
/// Registering this — the view factory, not a view of its own — is what
/// lets <c>IWorkspaceNavigation.OpenAsync("ManualTask")</c> succeed at all:
/// without an <see cref="IWorkspaceViewFactory"/> registered for a Kind, it
/// throws <c>WorkspaceViewFactoryNotFoundException</c> rather than falling
/// back to anything — and "Create opens right up" (Product Owner guard,
/// `WP 17.9.4`) needs it to open the moment <c>task.create</c> raises a
/// task.
/// </remarks>
public sealed class TaskObjectView : IWorkspaceView
{
    private readonly EngineeringDomainContext _context;
    private string _title;

    /// <summary>Initialises a new instance of the <see cref="TaskObjectView"/> class.</summary>
    public TaskObjectView(Guid objectId, string objectKind, string initialTitle, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(initialTitle);
        ArgumentNullException.ThrowIfNull(context);

        ObjectId = objectId;
        ObjectKind = objectKind;
        _title = initialTitle;
        _context = context;
    }

    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public string Title => _title;

    /// <inheritdoc />
    public Guid ObjectId { get; }

    /// <inheritdoc />
    public string ObjectKind { get; }

    /// <inheritdoc />
    /// <remarks>Always <see langword="false"/> — every mutation dispatches through a Command and commits immediately (`ADR-0063`); this view never buffers a local, uncommitted edit.</remarks>
    public bool IsDirty => false;

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var current = await _context.Repository.FindAsync(ObjectId, cancellationToken).ConfigureAwait(false);

        if (current is IHasBusinessIdentifier identity)
            _title = identity.DisplayName;
    }

    /// <inheritdoc />
    /// <remarks>Always returns <see langword="true"/> — <see cref="IsDirty"/> is always <see langword="false"/>, so no unsaved-edit prompt is ever needed.</remarks>
    public Task<bool> CloseAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

/// <summary>Constructs a <see cref="TaskObjectView"/> for <c>ManualTask.CanonicalKind</c> (`WP 19.5C`).</summary>
public sealed class TaskObjectViewFactory : IWorkspaceViewFactory
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="TaskObjectViewFactory"/> class.</summary>
    public TaskObjectViewFactory(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public string Kind => Core.Tasks.ManualTask.CanonicalKind;

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known task.</exception>
    /// <remarks>
    /// <see cref="IWorkspaceViewFactory.Create"/> is a frozen, synchronous
    /// `WP8.0B` contract; <c>InMemoryEngineeringObjectRepository.FindAsync</c>
    /// always completes synchronously already (no real I/O), so bridging
    /// with <c>GetAwaiter().GetResult()</c> here introduces no actual
    /// blocking — mirrors <c>Quotations.QuotationObjectViewFactory.Create</c>'s
    /// own identical, identically-justified bridge.
    /// </remarks>
    public IWorkspaceView Create(Guid objectId, IWorkspaceContext context)
    {
        var target = _context.Repository.FindAsync(objectId).GetAwaiter().GetResult()
            ?? throw new ArgumentException($"'{objectId}' is not a known {Kind}.", nameof(objectId));

        var title = (target as IHasBusinessIdentifier)?.DisplayName ?? objectId.ToString();

        return new TaskObjectView(objectId, Kind, title, _context);
    }
}
