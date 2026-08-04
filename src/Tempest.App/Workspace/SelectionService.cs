using Tempest.Core.Events;

namespace Tempest.App.Workspace;

/// <summary>The concrete <see cref="ISelectionService"/> implementation — publishes every change through the existing <see cref="IEventBus"/>, introducing no new pub/sub mechanism.</summary>
internal sealed class SelectionService : ISelectionService
{
    private readonly IEventBus _eventBus;
    private readonly WorkspaceContext _context;

    /// <summary>Initialises a new instance of the <see cref="SelectionService"/> class.</summary>
    public SelectionService(IEventBus eventBus, WorkspaceContext context)
    {
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(context);

        _eventBus = eventBus;
        _context = context;
    }

    /// <inheritdoc />
    public WorkspaceSelection? Current => _context.CurrentSelection;

    /// <inheritdoc />
    public async Task SelectAsync(Guid objectId, string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        var previous = _context.CurrentSelection;
        var current = new WorkspaceSelection(objectId, kind);
        _context.CurrentSelection = current;

        await _eventBus.PublishAsync(new WorkspaceSelectionChangedEvent(previous, current), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (_context.CurrentSelection is null)
            return;

        var previous = _context.CurrentSelection;
        _context.CurrentSelection = null;

        await _eventBus.PublishAsync(new WorkspaceSelectionChangedEvent(previous, null), cancellationToken).ConfigureAwait(false);
    }
}
