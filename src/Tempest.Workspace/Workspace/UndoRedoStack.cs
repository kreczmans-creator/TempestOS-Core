using Tempest.Core.Commands;

namespace Tempest.App.Workspace;

/// <summary>The concrete <see cref="IUndoRedoStack"/> implementation — two bounded, in-memory stacks.</summary>
public sealed class UndoRedoStack : IUndoRedoStack
{
    /// <summary>The maximum number of actions either stack retains — the oldest is discarded once exceeded, mirroring most desktop applications' own bounded Undo history.</summary>
    public const int Capacity = 50;

    private readonly List<UndoableAction> _undoStack = [];
    private readonly List<UndoableAction> _redoStack = [];

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public bool CanUndo => _undoStack.Count > 0;

    /// <inheritdoc />
    public bool CanRedo => _redoStack.Count > 0;

    /// <inheritdoc />
    public string? NextUndoDescription => CanUndo ? _undoStack[^1].Description : null;

    /// <inheritdoc />
    public string? NextRedoDescription => CanRedo ? _redoStack[^1].Description : null;

    /// <inheritdoc />
    public void Record(UndoableAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _undoStack.Add(action);
        if (_undoStack.Count > Capacity)
            _undoStack.RemoveAt(0);

        _redoStack.Clear();

        Changed?.Invoke();
    }

    /// <inheritdoc />
    public async Task<CommandResult?> UndoAsync(CancellationToken cancellationToken = default)
    {
        if (!CanUndo)
            return null;

        var action = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        var result = await action.Undo(cancellationToken).ConfigureAwait(false);

        _redoStack.Add(action);
        if (_redoStack.Count > Capacity)
            _redoStack.RemoveAt(0);

        Changed?.Invoke();

        return result;
    }

    /// <inheritdoc />
    public async Task<CommandResult?> RedoAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRedo)
            return null;

        var action = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);

        var result = await action.Redo(cancellationToken).ConfigureAwait(false);

        _undoStack.Add(action);
        if (_undoStack.Count > Capacity)
            _undoStack.RemoveAt(0);

        Changed?.Invoke();

        return result;
    }
}
