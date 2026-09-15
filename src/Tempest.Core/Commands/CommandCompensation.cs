namespace Tempest.Core.Commands;

/// <summary>
/// How to reverse a <see cref="CommandResult.Succeeded"/> command, and how
/// to re-apply that reversal — carried on the <see cref="CommandResult"/>
/// itself by the one handler that knows what it did (`WP 21.1A`, `ADR-0099`'s
/// own addendum).
/// </summary>
/// <remarks>
/// <para>
/// <b>A compensation on the result, not a second contract.</b> Mirrors
/// <c>Tempest.Workspace.UndoableAction</c>'s own shape exactly — a
/// <see cref="Description"/> and an <see cref="Undo"/>/<see cref="Redo"/>
/// delegate pair — but lives in <c>Tempest.Core.Commands</c>, alongside
/// <see cref="CommandResult"/> itself, because a handler that builds one
/// lives in <c>Tempest.Workspace</c> or lower and cannot reference the
/// Desktop-layer <c>UndoableAction</c> type (`ADR-0098`'s own layering).
/// The Desktop's own reporting path converts one of these into an
/// <c>UndoableAction</c> at the point it records it onto the session's
/// <c>IUndoRedoStack</c> — a trivial, one-line bridge, never a second
/// definition of what Undo/Redo mean.
/// </para>
/// <para>
/// <b>Never a direct repository write.</b> Both <see cref="Undo"/> and
/// <see cref="Redo"/> dispatch a real <see cref="ICommand"/> to its own
/// registered <see cref="ICommandHandler{TCommand}"/> through
/// <see cref="ICommandDispatcher.DispatchAsync{TCommand}"/> — the identical
/// one-transaction, one-audit-row state path any other command's handler
/// commits through — after an explicit archived-project guard check
/// (`WP 19.10R`'s own <c>ArchivedProjectCommandGuard</c>), since a
/// compensation is dispatched directly rather than through
/// <see cref="ICommandRegistry"/>'s own Id-based path and so is never
/// itself a Ribbon- or Palette-visible command.
/// </para>
/// </remarks>
public sealed class CommandCompensation
{
    /// <summary>Initialises a new instance of the <see cref="CommandCompensation"/> class.</summary>
    /// <param name="description">A short, human-readable description of the action being compensated — shown as Undo/Redo's own tooltip text.</param>
    /// <param name="undo">Reverses the action that produced this compensation.</param>
    /// <param name="redo">Re-applies the action after <see cref="Undo"/> has reversed it.</param>
    /// <exception cref="ArgumentException"><paramref name="description"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="undo"/> or <paramref name="redo"/> is <see langword="null"/>.</exception>
    public CommandCompensation(
        string description,
        Func<CancellationToken, Task<CommandResult>> undo,
        Func<CancellationToken, Task<CommandResult>> redo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(undo);
        ArgumentNullException.ThrowIfNull(redo);

        Description = description;
        Undo = undo;
        Redo = redo;
    }

    /// <summary>Gets the short, human-readable description of the action being compensated.</summary>
    public string Description { get; }

    /// <summary>Gets the delegate that reverses the action.</summary>
    public Func<CancellationToken, Task<CommandResult>> Undo { get; }

    /// <summary>Gets the delegate that re-applies the action after <see cref="Undo"/> has reversed it.</summary>
    public Func<CancellationToken, Task<CommandResult>> Redo { get; }
}
