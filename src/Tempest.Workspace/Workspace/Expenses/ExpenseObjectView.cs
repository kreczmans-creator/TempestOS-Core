using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;

namespace Tempest.Workspace.Expenses;

/// <summary>
/// Presents one expense to <see cref="IWorkspaceNavigation.OpenAsync"/>
/// (`WP 21.3B`) — a plain data wrapper, never itself rendered, mirroring
/// <c>Timesheets.TimesheetEntryObjectView</c>'s own identical shape and
/// reason: registering this is what lets
/// <c>IWorkspaceNavigation.OpenAsync(ProjectExpense.CanonicalKind)</c>
/// succeed at all, which "create opens right up" (Product Owner guard,
/// `WP 17.9.4`) needs.
/// </summary>
public sealed class ExpenseObjectView : IWorkspaceView
{
    private readonly EngineeringDomainContext _context;
    private string _title;

    /// <summary>Initialises a new instance of the <see cref="ExpenseObjectView"/> class.</summary>
    public ExpenseObjectView(Guid objectId, string objectKind, string initialTitle, EngineeringDomainContext context)
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

/// <summary>Constructs an <see cref="ExpenseObjectView"/> for <see cref="ProjectExpense.CanonicalKind"/> (`WP 21.3B`).</summary>
public sealed class ExpenseObjectViewFactory : IWorkspaceViewFactory
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="ExpenseObjectViewFactory"/> class.</summary>
    public ExpenseObjectViewFactory(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public string Kind => ProjectExpense.CanonicalKind;

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known expense.</exception>
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

        return new ExpenseObjectView(objectId, Kind, title, _context);
    }
}
