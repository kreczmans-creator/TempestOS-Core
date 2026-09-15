using Tempest.Core.Commands;

namespace Tempest.Workspace;

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

        // `WP 21.1A`: a refused or failed undo changed nothing, so the
        // action goes back where it came from rather than to Redo — moving
        // it to Redo would let a person "redo" an action that was never
        // actually reversed, the stack-consistency requirement this Work
        // Package's own brief names directly.
        if (result.Succeeded)
        {
            _redoStack.Add(action);
            if (_redoStack.Count > Capacity)
                _redoStack.RemoveAt(0);
        }
        else
        {
            _undoStack.Add(action);
        }

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

        // `WP 21.1A`: the identical consistency rule as `UndoAsync`, mirrored.
        if (result.Succeeded)
        {
            _undoStack.Add(action);
            if (_undoStack.Count > Capacity)
                _undoStack.RemoveAt(0);
        }
        else
        {
            _redoStack.Add(action);
        }

        Changed?.Invoke();

        return result;
    }

    /// <inheritdoc />
    public void Clear()
    {
        if (_undoStack.Count == 0 && _redoStack.Count == 0)
            return;

        _undoStack.Clear();
        _redoStack.Clear();

        Changed?.Invoke();
    }
}
