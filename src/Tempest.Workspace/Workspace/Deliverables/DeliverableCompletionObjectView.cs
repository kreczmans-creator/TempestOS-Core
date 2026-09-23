using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Deliverables;

/// <summary>
/// Presents one deliverable completion to <see cref="IWorkspaceNavigation.OpenAsync"/>
/// (`WP 19.0A`, `ADR-0150`) — a plain data wrapper, never itself rendered,
/// mirroring <c>Evidence.EvidenceObjectView</c>'s own identical shape and
/// reason: this Kind's own real rendering is the Desktop project
/// deliverables view (part 2 of this Work Package); registering this is
/// what lets <c>IWorkspaceNavigation.OpenAsync(DeliverableCompletion.CanonicalKind)</c>
/// succeed at all, which "create opens right up" (Product Owner guard,
/// `WP 17.9.4`) needs.
/// </summary>
public sealed class DeliverableCompletionObjectView : IWorkspaceView
{
    private readonly EngineeringDomainContext _context;
    private string _title;

    /// <summary>Initialises a new instance of the <see cref="DeliverableCompletionObjectView"/> class.</summary>
    public DeliverableCompletionObjectView(Guid objectId, string objectKind, string initialTitle, EngineeringDomainContext context)
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
    /// <remarks>Always <see langword="false"/> — every mutation dispatches through a Command and commits immediately (`ADR-0063`).</remarks>
    public bool IsDirty => false;

    /// <inheritdoc />
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var current = await _context.Repository.FindAsync(ObjectId, cancellationToken).ConfigureAwait(false);

        if (current is IHasBusinessIdentifier identity)
            _title = identity.DisplayName;
    }

    /// <inheritdoc />
    public Task<bool> CloseAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

/// <summary>Constructs a <see cref="DeliverableCompletionObjectView"/> for <see cref="DeliverableCompletion.CanonicalKind"/> (`WP 19.0A`).</summary>
public sealed class DeliverableCompletionObjectViewFactory : IWorkspaceViewFactory
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="DeliverableCompletionObjectViewFactory"/> class.</summary>
    public DeliverableCompletionObjectViewFactory(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public string Kind => DeliverableCompletion.CanonicalKind;

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known deliverable completion.</exception>
    public IWorkspaceView Create(Guid objectId, IWorkspaceContext context)
    {
        var target = _context.Repository.FindAsync(objectId).GetAwaiter().GetResult()
            ?? throw new ArgumentException($"'{objectId}' is not a known {Kind}.", nameof(objectId));

        var title = (target as IHasBusinessIdentifier)?.DisplayName ?? objectId.ToString();

        return new DeliverableCompletionObjectView(objectId, Kind, title, _context);
    }
}
