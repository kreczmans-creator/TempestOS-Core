namespace Tempest.Core.Macros;

/// <summary>
/// One step in a macro: the command Id to invoke, and any parameter
/// values recorded for it when the macro was authored (`WP 20.2C`,
/// `ADR-0099`'s own addendum).
/// </summary>
/// <remarks>
/// Before this Work Package a macro step was a bare command Id — a real
/// discipline command declaring a value could never be one, because
/// nothing that could ever answer for it existed
/// (<c>MacroBindingEligibilityTests</c>' own remarks). Recording the
/// values a person actually supplied turns that into an ordinary,
/// replayable step: the same values, offered back automatically the next
/// time this step runs (<see cref="RunMacroCommandHandler"/>). A plain
/// class, not a record — <see cref="Commands.CommandMacro"/>'s own
/// established shape for a validated, reference-identity value.
/// </remarks>
public sealed class MacroStep
{
    private static readonly IReadOnlyDictionary<string, string> EmptyValues =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Initialises a new instance of the <see cref="MacroStep"/> class.</summary>
    /// <param name="commandId">The <see cref="Commands.CommandDescriptor.Id"/> this step invokes.</param>
    /// <param name="recordedValues">
    /// The values collected for this step's own <see cref="Commands.CommandBinding.Parameters"/>
    /// at record time — through the exact <see cref="Commands.CommandParameterPrompt"/>
    /// seam a live invocation already uses — keyed by
    /// <see cref="Commands.CommandParameter.Name"/>.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="commandId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="recordedValues"/> is <see langword="null"/>.</exception>
    public MacroStep(string commandId, IReadOnlyDictionary<string, string> recordedValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        ArgumentNullException.ThrowIfNull(recordedValues);

        CommandId = commandId;
        RecordedValues = recordedValues;
    }

    /// <summary>Initialises a step recording no values — a parameterless command, or a legacy step.</summary>
    /// <param name="commandId">The <see cref="Commands.CommandDescriptor.Id"/> this step invokes.</param>
    /// <exception cref="ArgumentException"><paramref name="commandId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public MacroStep(string commandId)
        : this(commandId, EmptyValues)
    {
    }

    /// <summary>Gets the <see cref="Commands.CommandDescriptor.Id"/> this step invokes.</summary>
    public string CommandId { get; }

    /// <summary>
    /// Gets the values collected for this step's own binding at record
    /// time, keyed by <see cref="Commands.CommandParameter.Name"/>. Never
    /// <see langword="null"/>; empty for a step whose binding needed
    /// none, and for every step a macro carried before this Work Package
    /// (a legacy persisted macro, or one authored through the plain
    /// command-id overload).
    /// </summary>
    public IReadOnlyDictionary<string, string> RecordedValues { get; }
}
