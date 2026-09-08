using Tempest.Core.Commands;

namespace Tempest.App.Workspace;

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

    /// <summary>Reverses the most recently recorded (or redone) action, if any, by invoking its own <see cref="UndoableAction.Undo"/> and moving it to the Redo stack. Returns <see langword="null"/> if <see cref="CanUndo"/> is <see langword="false"/>.</summary>
    Task<CommandResult?> UndoAsync(CancellationToken cancellationToken = default);

    /// <summary>Re-applies the most recently undone action, if any, by invoking its own <see cref="UndoableAction.Redo"/> and moving it back to the Undo stack. Returns <see langword="null"/> if <see cref="CanRedo"/> is <see langword="false"/>.</summary>
    Task<CommandResult?> RedoAsync(CancellationToken cancellationToken = default);
}
