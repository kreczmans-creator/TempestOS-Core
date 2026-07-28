namespace Tempest.Core.Commands;

/// <summary>
/// The Id-keyed catalogue of every registered <see cref="CommandDescriptor"/>
/// — the surface a menu, toolbar, keyboard-shortcut map, or future
/// automation/AI caller enumerates and invokes against.
/// </summary>
/// <remarks>
/// A Platform Service (ADR-0036), DI-public like <see cref="ICommandDispatcher"/>
/// — the Command Framework's own application of the Registry pattern,
/// mirroring <see cref="Navigation.INavigationProvider"/> directly. See
/// <c>Command Framework Architecture.md</c> for the complete design.
/// </remarks>
public interface ICommandRegistry
{
    /// <summary>
    /// Registers <paramref name="descriptor"/>.
    /// </summary>
    /// <param name="descriptor">The descriptor to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateCommandIdException">
    /// A descriptor is already registered under <see cref="CommandDescriptor.Id"/>.
    /// </exception>
    void RegisterDescriptor(CommandDescriptor descriptor);

    /// <summary>
    /// Gets every registered descriptor. Never <see langword="null"/>;
    /// empty if none have been registered. Ordered deterministically:
    /// ascending ordinal by <see cref="CommandDescriptor.Category"/> (nulls
    /// first), then ascending ordinal by <see cref="CommandDescriptor.Id"/>.
    /// </summary>
    /// <remarks>
    /// Returns every registered descriptor regardless of its own
    /// <see cref="CommandDescriptor.CanExecute"/> result — filtering by
    /// availability is the caller's own decision, exactly as
    /// <see cref="Navigation.INavigationProvider.Items"/> does not filter by
    /// <c>IsVisible</c>.
    /// </remarks>
    IReadOnlyList<CommandDescriptor> Items { get; }

    /// <summary>
    /// Constructs the default instance of the command registered under
    /// <paramref name="id"/> and dispatches it to its one registered
    /// handler.
    /// </summary>
    /// <param name="id">The Id of the command to invoke.</param>
    /// <param name="cancellationToken">A token observed while the handler runs.</param>
    /// <returns>The <see cref="CommandResult"/> the handler returned.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    /// <exception cref="CommandNotFoundException">
    /// No descriptor is registered under <paramref name="id"/>.
    /// </exception>
    /// <exception cref="CommandException">
    /// The descriptor registered under <paramref name="id"/> has no
    /// <see cref="CommandDescriptor.CreateDefault"/> factory and cannot be
    /// invoked by Id.
    /// </exception>
    /// <exception cref="CommandHandlerNotRegisteredException">
    /// No handler is registered for the constructed command's own concrete type.
    /// </exception>
    /// <remarks>
    /// Does not itself re-check <see cref="CommandDescriptor.CanExecute"/>
    /// before dispatching — a caller that already decided to invoke a
    /// command has already made that judgement. A handler's own exception
    /// propagates directly out of this method, exactly as
    /// <see cref="ICommandDispatcher.DispatchAsync{TCommand}"/>'s own does.
    /// </remarks>
    Task<CommandResult> InvokeAsync(string id, CancellationToken cancellationToken = default);
}
