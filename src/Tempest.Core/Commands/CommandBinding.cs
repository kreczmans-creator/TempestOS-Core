namespace Tempest.Core.Commands;

/// <summary>
/// How one <see cref="CommandDescriptor"/> turns the application's current
/// <see cref="CommandContext"/>, plus any values it declares it needs, into
/// the concrete <see cref="ICommand"/> its handler already expects — the
/// missing half of Id-based invocation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Explicit and typed, never reflective.</b> The <c>build</c> lambda is
/// hand-written at the same call site that registers the descriptor and the
/// handler, closing over the command's real constructor. Nothing here
/// inspects a constructor, scans an attribute, or resolves a service:
/// <c>ADR-0037</c> rejected all three for this framework, and
/// <see cref="CommandHandlerTable"/> already avoids reflection the same way,
/// by capturing type knowledge in a closure created where that knowledge is
/// static.
/// </para>
/// <para>
/// <b>No second registration mechanism.</b> A binding is a field on the
/// descriptor a module already registers, which is what keeps
/// <c>ADR-0070</c>'s "the palette is a view over
/// <see cref="ICommandRegistry"/>, not a second registry" true.
/// </para>
/// <para>
/// <b>Unavailability is declared, not left as an absence.</b> A command
/// this platform genuinely cannot yet invoke — one needing a destination
/// picker, or structured input no prompt can collect — registers
/// <see cref="Unavailable"/> with the real reason, so the surface can say
/// what is missing instead of falling through to a generic message.
/// </para>
/// </remarks>
public sealed class CommandBinding
{
    private readonly Func<CommandContext, IReadOnlyDictionary<string, string>, ICommand>? _build;

    /// <summary>
    /// Initialises a new, invocable instance of the
    /// <see cref="CommandBinding"/> class.
    /// </summary>
    /// <param name="requires">What must be present in the context.</param>
    /// <param name="build">
    /// Constructs the command from the context and the collected values.
    /// Hand-written; never generated or reflected.
    /// </param>
    /// <param name="parameters">
    /// The values to collect before building, or <see langword="null"/> for
    /// a command that needs none.
    /// </param>
    /// <param name="appliesToKinds">
    /// The <c>Kind</c>s this command acts on, or <see langword="null"/> for
    /// a command that applies to any. Matched ordinally, because a
    /// <c>Kind</c> is a canonical vocabulary value, not free text.
    /// </param>
    /// <param name="confirmationMessage">
    /// A message to confirm before the command runs, or
    /// <see langword="null"/> for an action needing no confirmation.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="build"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="parameters"/> contains a <see langword="null"/> entry
    /// or two parameters sharing a <see cref="CommandParameter.Name"/>;
    /// <paramref name="appliesToKinds"/> is empty or contains a blank entry;
    /// or <paramref name="confirmationMessage"/> is blank.
    /// </exception>
    public CommandBinding(
        CommandContextRequirement requires,
        Func<CommandContext, IReadOnlyDictionary<string, string>, ICommand> build,
        IReadOnlyList<CommandParameter>? parameters = null,
        IReadOnlyList<string>? appliesToKinds = null,
        string? confirmationMessage = null)
    {
        ArgumentNullException.ThrowIfNull(build);

        if (confirmationMessage is not null && string.IsNullOrWhiteSpace(confirmationMessage))
            throw new ArgumentException("Confirmation message must not be empty or whitespace.", nameof(confirmationMessage));

        Requires = requires;
        _build = build;
        Parameters = Validated(parameters);
        AppliesToKinds = Validated(appliesToKinds);
        ConfirmationMessage = confirmationMessage;
    }

    private CommandBinding(string unavailableReason)
    {
        UnavailableReason = unavailableReason;
        Parameters = [];
    }

    /// <summary>
    /// Declares that this command cannot currently be invoked by Id, and
    /// why — the honest alternative to a descriptor that silently does
    /// nothing.
    /// </summary>
    /// <param name="reason">
    /// A user-facing explanation of what is missing, reported verbatim by
    /// <see cref="ICommandRegistry.Evaluate"/>.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static CommandBinding Unavailable(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new CommandBinding(reason);
    }

    /// <summary>Gets what must be present in the context for this command to be available.</summary>
    public CommandContextRequirement Requires { get; }

    /// <summary>Gets the values this command needs collected. Never <see langword="null"/>; empty if it needs none.</summary>
    public IReadOnlyList<CommandParameter> Parameters { get; }

    /// <summary>
    /// Gets the <c>Kind</c>s this command acts on, or <see langword="null"/>
    /// if it applies to any.
    /// </summary>
    public IReadOnlyList<string>? AppliesToKinds { get; }

    /// <summary>
    /// Gets the message to confirm before this command runs, or
    /// <see langword="null"/> if it needs no confirmation.
    /// </summary>
    /// <remarks>
    /// A string, deliberately — Core states that confirmation is required
    /// and what to say; how it is asked belongs entirely to whichever
    /// surface supplies the <see cref="CommandParameterPrompt"/>. This is
    /// also what keeps a destructive command out of an unattended macro:
    /// a step that needs a person cannot run without one.
    /// </remarks>
    public string? ConfirmationMessage { get; }

    /// <summary>
    /// Gets why this command cannot be invoked by Id, or
    /// <see langword="null"/> when it can.
    /// </summary>
    public string? UnavailableReason { get; }

    /// <summary>Gets whether this binding can actually construct a command.</summary>
    public bool IsInvocable => UnavailableReason is null;

    /// <summary>
    /// Gets whether invoking this command needs a
    /// <see cref="CommandParameterPrompt"/> — because it declares values to
    /// collect, a confirmation, or both.
    /// </summary>
    public bool RequiresPrompt => Parameters.Count > 0 || ConfirmationMessage is not null;

    /// <summary>
    /// Constructs the command from <paramref name="context"/> and
    /// <paramref name="values"/>.
    /// </summary>
    /// <param name="context">The context this command was found available for.</param>
    /// <param name="values">The collected values, keyed by <see cref="CommandParameter.Name"/>.</param>
    /// <returns>The constructed command, ready to dispatch.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">This binding is an <see cref="Unavailable"/> declaration and has nothing to build.</exception>
    /// <remarks>
    /// An exception thrown out of the <c>build</c> lambda itself is a defect
    /// in that lambda — it was handed a context this binding's own
    /// requirements said was sufficient — and propagates uncaught, exactly
    /// as a handler's own exception does (<c>ADR-0038</c>).
    /// </remarks>
    public ICommand Build(CommandContext context, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(values);

        if (_build is null)
        {
            throw new InvalidOperationException(
                $"This binding is declared unavailable ({UnavailableReason}) and cannot construct a command.");
        }

        return _build(context, values);
    }

    private static IReadOnlyList<CommandParameter> Validated(IReadOnlyList<CommandParameter>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return [];

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var copy = new CommandParameter[parameters.Count];

        for (var i = 0; i < parameters.Count; i++)
        {
            var parameter = parameters[i]
                ?? throw new ArgumentException($"Parameter {i} is null.", nameof(parameters));

            if (!seen.Add(parameter.Name))
                throw new ArgumentException($"Two parameters share the name '{parameter.Name}'.", nameof(parameters));

            copy[i] = parameter;
        }

        return copy;
    }

    private static IReadOnlyList<string>? Validated(IReadOnlyList<string>? appliesToKinds)
    {
        if (appliesToKinds is null)
            return null;

        if (appliesToKinds.Count == 0)
        {
            throw new ArgumentException(
                "An empty Kind list would make this command apply to nothing — pass null for 'any Kind'.",
                nameof(appliesToKinds));
        }

        var copy = new string[appliesToKinds.Count];

        for (var i = 0; i < appliesToKinds.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(appliesToKinds[i]))
                throw new ArgumentException($"Kind {i} is null, empty, or whitespace.", nameof(appliesToKinds));

            copy[i] = appliesToKinds[i];
        }

        return copy;
    }
}
