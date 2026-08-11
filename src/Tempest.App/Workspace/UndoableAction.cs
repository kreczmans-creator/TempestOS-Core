using Tempest.Core.Commands;

namespace Tempest.App.Workspace;

/// <summary>
/// One reversible user action, recorded on an <see cref="IUndoRedoStack"/>
/// after it has already been performed once — a Do/Undo delegate pair
/// built from data the recording call site already holds, not a new
/// Command Framework contract (`ADR-0099`: a command's own constructor
/// only ever carries forward-facing data, so inverting it needs data only
/// the UI call site — never the command itself — already has).
/// </summary>
/// <remarks>
/// Mirrors this platform's own established "small, additive delegate
/// bundle" pattern (<c>RibbonView.ObjectCreationHandlers</c>,
/// <c>MainWindow.ConfirmDeleteAsync</c>) rather than a deep interface
/// hierarchy. <see cref="Undo"/>/<see cref="Redo"/> both return a real
/// <see cref="CommandResult"/> — whether the underlying reversal is an
/// actual Command dispatch (Rename, via <see cref="IWorkspaceManager.RenameObjectAsync"/>)
/// or a plain local state mutation (a Favourite toggle) is invisible to
/// <see cref="IUndoRedoStack"/> itself, which treats both identically.
/// </remarks>
public sealed class UndoableAction
{
    /// <summary>Initialises a new instance of the <see cref="UndoableAction"/> class.</summary>
    /// <param name="description">A short, human-readable description of the action — shown as Undo/Redo's own tooltip text.</param>
    /// <param name="undo">Reverses the action.</param>
    /// <param name="redo">Re-applies the action after it has been undone.</param>
    /// <exception cref="ArgumentException"><paramref name="description"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="undo"/> or <paramref name="redo"/> is <see langword="null"/>.</exception>
    public UndoableAction(string description, Func<CancellationToken, Task<CommandResult>> undo, Func<CancellationToken, Task<CommandResult>> redo)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description must not be null, empty, or whitespace.", nameof(description));

        ArgumentNullException.ThrowIfNull(undo);
        ArgumentNullException.ThrowIfNull(redo);

        Description = description;
        Undo = undo;
        Redo = redo;
    }

    /// <summary>Gets the short, human-readable description of this action.</summary>
    public string Description { get; }

    /// <summary>Gets the delegate that reverses this action.</summary>
    public Func<CancellationToken, Task<CommandResult>> Undo { get; }

    /// <summary>Gets the delegate that re-applies this action after it has been undone.</summary>
    public Func<CancellationToken, Task<CommandResult>> Redo { get; }
}
