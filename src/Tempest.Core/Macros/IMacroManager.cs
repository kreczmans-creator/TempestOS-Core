namespace Tempest.Core.Macros;

/// <summary>
/// Creates, lists, and deletes user-authored <see cref="ICommandMacro"/>s,
/// and keeps each one's own <see cref="Commands.CommandDescriptor"/>
/// registered against the shared <see cref="Commands.ICommandRegistry"/> —
/// the mechanism (`ADR-0098`) that lets a macro be invoked through
/// exactly the same path as any other command (Command Palette, a future
/// Ribbon binding, or an <see cref="Input.IInputBindingProvider"/>), with
/// no special-casing anywhere outside this namespace.
/// </summary>
/// <remarks>
/// A Platform Service (ADR-0036), DI-public like <see cref="Commands.ICommandRegistry"/>
/// — resolved via ordinary constructor injection.
/// </remarks>
public interface IMacroManager
{
    /// <summary>The <see cref="Commands.CommandDescriptor.Id"/> prefix every macro's own descriptor is registered under — <c>"macro:{Id}"</c>.</summary>
    const string CommandIdPrefix = "macro:";

    /// <summary>
    /// Loads every previously persisted macro and (re-)registers each
    /// one's own <see cref="Commands.CommandDescriptor"/> against the
    /// shared <see cref="Commands.ICommandRegistry"/>. Idempotent — safe
    /// to call more than once against the same registry (a restart);
    /// re-registering an already-registered descriptor Id is skipped, not
    /// an error.
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets every currently known macro, ordered by <see cref="ICommandMacro.Name"/>.</summary>
    Task<IReadOnlyList<ICommandMacro>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds the macro with the given <paramref name="id"/>, or <see langword="null"/> if none exists (including one already deleted).</summary>
    Task<ICommandMacro?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and persists a new macro, and registers its own
    /// <see cref="Commands.CommandDescriptor"/> against the shared
    /// <see cref="Commands.ICommandRegistry"/> so it is immediately
    /// invokable.
    /// </summary>
    /// <param name="name">The macro's own display name.</param>
    /// <param name="stepCommandIds">The ordered Command Ids this macro invokes when run — each must currently be a registered <see cref="Commands.CommandDescriptor.Id"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null/empty/whitespace, <paramref name="stepCommandIds"/> is empty, or a step Id is not a registered command.</exception>
    Task<ICommandMacro> CreateAsync(string name, IReadOnlyList<string> stepCommandIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the macro with the given <paramref name="id"/>, if one
    /// exists — a no-op otherwise. Its own <see cref="Commands.CommandDescriptor"/>
    /// is <b>not</b> unregistered (<see cref="Commands.ICommandRegistry"/>
    /// exposes no removal method, confirmed, frozen) — a subsequent
    /// invocation of the stale descriptor fails honestly
    /// (<c>RunMacroCommandHandler</c>), rather than throwing.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
