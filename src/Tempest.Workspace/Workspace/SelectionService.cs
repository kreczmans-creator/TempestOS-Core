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
    public IReadOnlyList<WorkspaceSelection> SelectedItems => _context.SelectedItems;

    /// <inheritdoc />
    public async Task SelectAsync(Guid objectId, string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        var previousCurrent = _context.CurrentSelection;
        var previousItems = _context.SelectedItems;
        var current = new WorkspaceSelection(objectId, kind);
        var currentItems = new List<WorkspaceSelection> { current };

        _context.CurrentSelection = current;
        _context.ReplaceSelectedItems(currentItems);

        await _eventBus.PublishAsync(new WorkspaceSelectionChangedEvent(previousCurrent, current), cancellationToken).ConfigureAwait(false);
        await _eventBus.PublishAsync(new WorkspaceSelectionSetChangedEvent(previousItems, currentItems), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (_context.CurrentSelection is null)
            return;

        var previousCurrent = _context.CurrentSelection;
        var previousItems = _context.SelectedItems;

        _context.CurrentSelection = null;
        _context.ReplaceSelectedItems([]);

        await _eventBus.PublishAsync(new WorkspaceSelectionChangedEvent(previousCurrent, null), cancellationToken).ConfigureAwait(false);
        await _eventBus.PublishAsync(new WorkspaceSelectionSetChangedEvent(previousItems, []), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ToggleSelectionAsync(Guid objectId, string kind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        var previousCurrent = _context.CurrentSelection;
        var previousItems = _context.SelectedItems;
        var currentItems = new List<WorkspaceSelection>(previousItems);

        var existingIndex = currentItems.FindIndex(selection => selection.ObjectId == objectId);
        WorkspaceSelection? current;

        if (existingIndex >= 0)
        {
            currentItems.RemoveAt(existingIndex);
            current = currentItems.Count > 0 ? currentItems[^1] : null;
        }
        else
        {
            current = new WorkspaceSelection(objectId, kind);
            currentItems.Add(current);
        }

        _context.CurrentSelection = current;
        _context.ReplaceSelectedItems(currentItems);

        await _eventBus.PublishAsync(new WorkspaceSelectionSetChangedEvent(previousItems, currentItems), cancellationToken).ConfigureAwait(false);
        await _eventBus.PublishAsync(new WorkspaceSelectionChangedEvent(previousCurrent, current), cancellationToken).ConfigureAwait(false);
    }
}
