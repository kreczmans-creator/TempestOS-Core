using Tempest.Core.EngineeringDomain;
using Tempest.Core.Timesheets;

namespace Tempest.Workspace.Timesheets;

/// <summary>
/// Presents one timesheet entry to <see cref="IWorkspaceNavigation.OpenAsync"/>
/// (`WP 19.0A`, `ADR-0150`) — a plain data wrapper, never itself rendered,
/// mirroring <c>Evidence.EvidenceObjectView</c>'s own identical shape and
/// reason: this Kind's own real rendering is the Desktop weekly timesheet
/// view (part 2 of this Work Package); registering this is what lets
/// <c>IWorkspaceNavigation.OpenAsync(TimesheetEntry.CanonicalKind)</c>
/// succeed at all, which "create opens right up" (Product Owner guard,
/// `WP 17.9.4`) needs.
/// </summary>
public sealed class TimesheetEntryObjectView : IWorkspaceView
{
    private readonly EngineeringDomainContext _context;
    private string _title;

    /// <summary>Initialises a new instance of the <see cref="TimesheetEntryObjectView"/> class.</summary>
    public TimesheetEntryObjectView(Guid objectId, string objectKind, string initialTitle, EngineeringDomainContext context)
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

/// <summary>Constructs a <see cref="TimesheetEntryObjectView"/> for <see cref="TimesheetEntry.CanonicalKind"/> (`WP 19.0A`).</summary>
public sealed class TimesheetEntryObjectViewFactory : IWorkspaceViewFactory
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="TimesheetEntryObjectViewFactory"/> class.</summary>
    public TimesheetEntryObjectViewFactory(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    /// <inheritdoc />
    public string Kind => TimesheetEntry.CanonicalKind;

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="objectId"/> does not identify a known timesheet entry.</exception>
    public IWorkspaceView Create(Guid objectId, IWorkspaceContext context)
    {
        var target = _context.Repository.FindAsync(objectId).GetAwaiter().GetResult()
            ?? throw new ArgumentException($"'{objectId}' is not a known {Kind}.", nameof(objectId));

        var title = (target as IHasBusinessIdentifier)?.DisplayName ?? objectId.ToString();

        return new TimesheetEntryObjectView(objectId, Kind, title, _context);
    }
}
