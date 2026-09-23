using Tempest.Core.Commands;

namespace Tempest.Workspace;

/// <summary>
/// Records <see cref="UndoableAction"/>s already performed once, and
/// reverses/re-applies the most recent one on request — the Undo/Redo
/// architecture (`WP 10.6A`, `ADR-0099`).
/// </summary>
/// <remarks>
/// Session-only, by design — never persisted across a restart, matching
/// most desktop applications' own established convention (disclosed,
/// `WP10.6A Implementation Report.md`).
/// </remarks>
public interface IUndoRedoStack
{
    /// <summary>Gets whether <see cref="UndoAsync"/> currently has an action to reverse.</summary>
    bool CanUndo { get; }

    /// <summary>Gets whether <see cref="RedoAsync"/> currently has an action to re-apply.</summary>
    bool CanRedo { get; }

    /// <summary>Gets the <see cref="UndoableAction.Description"/> <see cref="UndoAsync"/> would currently reverse, or <see langword="null"/> if <see cref="CanUndo"/> is <see langword="false"/>.</summary>
    string? NextUndoDescription { get; }

    /// <summary>Gets the <see cref="UndoableAction.Description"/> <see cref="RedoAsync"/> would currently re-apply, or <see langword="null"/> if <see cref="CanRedo"/> is <see langword="false"/>.</summary>
    string? NextRedoDescription { get; }

    /// <summary>Raised after <see cref="Record"/>, <see cref="UndoAsync"/>, or <see cref="RedoAsync"/> changes <see cref="CanUndo"/>/<see cref="CanRedo"/> — a UI's own enablement-refresh hook.</summary>
    event Action? Changed;

    /// <summary>
    /// Records <paramref name="action"/> as the most recently performed
    /// action — pushed onto the Undo stack; clears the Redo stack (the
    /// standard convention: performing a genuinely new action invalidates
    /// whatever Redo history existed).
    /// </summary>
    void Record(UndoableAction action);

    /// <summary>
    /// Reverses the most recently recorded (or redone) action, if any, by
    /// invoking its own <see cref="UndoableAction.Undo"/>. On success, moves
    /// the action to the Redo stack; on a refused or failed undo (`WP 21.1A`
    /// — a compensation whose own <see cref="CommandResult.Succeeded"/> came
    /// back <see langword="false"/>, the parent since deleted, the archived-
    /// project guard), the action is put back on the Undo stack unchanged,
    /// exactly as if this call had never been made — never moved to Redo,
    /// which would let a person "redo" an action that was never actually
    /// reversed. Returns <see langword="null"/> if <see cref="CanUndo"/> is
    /// <see langword="false"/>.
    /// </summary>
    Task<CommandResult?> UndoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-applies the most recently undone action, if any, by invoking its
    /// own <see cref="UndoableAction.Redo"/>. On success, moves the action
    /// back to the Undo stack; on a refused or failed redo (`WP 21.1A`), the
    /// action is put back on the Redo stack unchanged, for the identical
    /// consistency reason <see cref="UndoAsync"/>'s own remarks give.
    /// Returns <see langword="null"/> if <see cref="CanRedo"/> is
    /// <see langword="false"/>.
    /// </summary>
    Task<CommandResult?> RedoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears both stacks (`WP 21.1A`) — every recorded action is discarded,
    /// with no attempt to undo or redo any of them. The one call site is a
    /// project switch: an action recorded against the project that was open
    /// no longer has anywhere safe to replay against once a different
    /// project (or none) is open, so the session's own Undo/Redo history
    /// starts over, exactly like most desktop applications' own established
    /// convention for switching documents. A no-op, not an error, when both
    /// stacks are already empty.
    /// </summary>
    void Clear();
}
