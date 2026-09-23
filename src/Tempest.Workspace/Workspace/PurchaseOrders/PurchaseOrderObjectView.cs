using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.PurchaseOrders;

/// <summary>
/// Presents one purchase order to <see cref="IWorkspaceNavigation.OpenAsync"/>
/// (`WP 21.3B`) — a plain data wrapper, never itself rendered, mirroring
/// <c>Quotations.QuotationObjectView</c>'s own identical shape and reason:
/// registering this is what lets
/// <c>IWorkspaceNavigation.OpenAsync(PurchaseOrder.CanonicalKind)</c>
/// succeed at all, which "create opens right up" (Product Owner guard,
/// `WP 17.9.4`) needs.
/// </summary>
public sealed class PurchaseOrderObjectView : IWorkspaceView
{
    private readonly EngineeringDomainContext _context;
    private string _title;

    /// <summary>Initialises a new instance of the <see cref="PurchaseOrderObjectView"/> class.</summary>
    public PurchaseOrderObjectView(Guid objectId, string objectKind, string initialTitle, EngineeringDomainContext context)
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

/// <summary>Constructs a <see cref="PurchaseOrderObjectView"/> for <c>PurchaseOrder.CanonicalKind</c> (`WP 21.3B`).</summary>
public sealed class PurchaseOrderObjectViewFactory : IWorkspaceViewFactory
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="PurchaseOrderObjectViewFactory"/> class.</summary>
    public PurchaseOrderObjectViewFactory(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public string Kind => Core.PurchaseOrders.PurchaseOrder.CanonicalKind;

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known purchase order.</exception>
    /// <remarks>
    /// <see cref="IWorkspaceViewFactory.Create"/> is a frozen, synchronous
    /// `WP8.0B` contract; <c>InMemoryEngineeringObjectRepository.FindAsync</c>
    /// always completes synchronously already (no real I/O), so bridging
    /// with <c>GetAwaiter().GetResult()</c> here introduces no actual
    /// blocking — mirrors <c>Quotations.QuotationObjectViewFactory.Create</c>'s
    /// own identical, identically-justified bridge (allow-listed in
    /// <c>NoBlockingPersistenceCallsTests</c>).
    /// </remarks>
    public IWorkspaceView Create(Guid objectId, IWorkspaceContext context)
    {
        var target = _context.Repository.FindAsync(objectId).GetAwaiter().GetResult()
            ?? throw new ArgumentException($"'{objectId}' is not a known {Kind}.", nameof(objectId));

        var title = (target as IHasBusinessIdentifier)?.DisplayName ?? objectId.ToString();

        return new PurchaseOrderObjectView(objectId, Kind, title, _context);
    }
}
