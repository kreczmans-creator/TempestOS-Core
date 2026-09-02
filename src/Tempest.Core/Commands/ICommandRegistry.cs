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

    /// <summary>
    /// Reports whether the command registered under <paramref name="id"/>
    /// can be invoked against <paramref name="context"/> right now, and — if
    /// not — a reason a person can act on.
    /// </summary>
    /// <param name="id">The Id of the command to evaluate.</param>
    /// <param name="context">The application's current context.</param>
    /// <returns>The command's availability, with a reason when it is unavailable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// <b>The single availability implementation.</b> Every surface asks
    /// this one question — a ribbon deciding whether to enable a button, a
    /// palette rendering a disabled row with its reason (<c>ADR-0070</c>),
    /// and <see cref="InvokeAsync(string, CommandContext, CommandParameterPrompt?, CancellationToken)"/>
    /// itself before it dispatches anything. No caller re-derives
    /// eligibility from a command's Id or Kind.
    /// </para>
    /// <para>
    /// Answers from the context alone. Whether a collected <i>value</i> is
    /// acceptable is a different question, needing values this method is
    /// never given, and is answered during invocation instead.
    /// </para>
    /// <para>
    /// An unregistered Id is reported here as unavailable rather than
    /// thrown, because a surface asking "can I offer this?" about something
    /// that does not exist has asked a fair question. Invoking one still
    /// throws <see cref="CommandNotFoundException"/>, unchanged.
    /// </para>
    /// </remarks>
    CommandAvailability Evaluate(string id, CommandContext context);

    /// <summary>
    /// Constructs the command registered under <paramref name="id"/> from
    /// <paramref name="context"/> and its own declared parameters, then
    /// dispatches it to its one registered handler.
    /// </summary>
    /// <param name="id">The Id of the command to invoke.</param>
    /// <param name="context">The application's current context.</param>
    /// <param name="prompt">
    /// Collects the values, and the confirmation, the command's own
    /// <see cref="CommandDescriptor.Binding"/> declares. May be
    /// <see langword="null"/> for a command that declares neither; a
    /// command that does declare either is reported unavailable rather than
    /// invoked without asking.
    /// </param>
    /// <param name="cancellationToken">A token observed while collecting and while the handler runs.</param>
    /// <returns>Whether the command ran, was declined, or could not be invoked.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="CommandNotFoundException">No descriptor is registered under <paramref name="id"/>.</exception>
    /// <exception cref="CommandHandlerNotRegisteredException">No handler is registered for the constructed command's own concrete type.</exception>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="InvokeAsync(string, CancellationToken)"/>, this
    /// <i>does</i> consult <see cref="Evaluate"/> first — deliberately. That
    /// overload's caller already held everything the command needed; this
    /// one supplied only an Id and a context, so the framework is the only
    /// thing positioned to say whether the context suffices.
    /// </para>
    /// <para>
    /// A handler's own exception propagates directly out of this method,
    /// exactly as it does from every other dispatch path (<c>ADR-0038</c>),
    /// as does an exception thrown while a binding constructs its command —
    /// that is a defect in the binding, not an outcome.
    /// </para>
    /// </remarks>
    Task<CommandInvocation> InvokeAsync(
        string id,
        CommandContext context,
        CommandParameterPrompt? prompt = null,
        CancellationToken cancellationToken = default);
}
