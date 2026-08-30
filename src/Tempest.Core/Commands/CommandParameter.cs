namespace Tempest.Core.Commands;

/// <summary>
/// One value a <see cref="CommandBinding"/> needs from whoever is invoking
/// the command, beyond what the <see cref="CommandContext"/> already
/// supplies.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every declared parameter is required.</b> There is no optional
/// parameter: a binding that can proceed without a value simply does not
/// declare one and defaults it in its own <c>build</c> lambda, exactly as
/// the commands' own optional constructor parameters already work. "Missing"
/// means absent from the collected values — an empty string is a value, and
/// a binding that will not accept one says so through <see cref="Validate"/>.
/// </para>
/// <para>
/// <b>Values are strings because the collection primitive is.</b> The only
/// input surface this platform has returns <c>string?</c>, so a parameter
/// is a string and the binding's own lambda does the typed parse. Where a
/// value cannot be safely expressed as text — a file's bytes, a
/// per-template JSON payload, a destination object — the correct answer is
/// an explicitly unavailable binding
/// (<see cref="CommandBinding.Unavailable"/>), never a weaker parameter.
/// </para>
/// </remarks>
/// <param name="Name">The key this value is collected and read under.</param>
/// <param name="Label">The human-readable prompt shown when asking for it.</param>
/// <param name="DefaultValue">An optional value to offer as the starting point.</param>
/// <param name="AllowedValues">
/// An optional closed set of acceptable values — an enum's own names, for
/// example. Matched case-insensitively, because the validation this
/// replaces parsed enums with <c>ignoreCase: true</c>.
/// </param>
/// <param name="Validate">
/// An optional check returning <see langword="null"/> when the value is
/// acceptable and a human-readable message when it is not — the exact
/// shape of the validation callback the platform's own input prompt
/// already takes, so an existing length limit or non-blank rule moves here
/// unchanged rather than being lost.
/// </param>
public sealed record CommandParameter(
    string Name,
    string Label,
    string? DefaultValue = null,
    IReadOnlyList<string>? AllowedValues = null,
    Func<string, string?>? Validate = null)
{
    /// <summary>
    /// Checks <paramref name="value"/> against this parameter's own
    /// <see cref="AllowedValues"/> and then <see cref="Validate"/>.
    /// </summary>
    /// <param name="value">The collected value.</param>
    /// <returns>
    /// <see langword="null"/> if the value is acceptable; otherwise a
    /// human-readable reason it is not.
    /// </returns>
    public string? Check(string value)
    {
        if (value is null)
            return $"'{Label}' is required.";

        if (AllowedValues is { Count: > 0 } allowed
            && !allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            return $"'{Label}' must be one of: {string.Join(", ", allowed)}.";
        }

        return Validate?.Invoke(value);
    }
}

/// <summary>
/// Collects the values a <see cref="CommandBinding"/> declared, and
/// confirms the action where the binding asks for confirmation.
/// </summary>
/// <remarks>
/// Supplied by whichever surface is invoking the command, because only that
/// surface knows how to ask a person a question. Core never renders
/// anything: it hands over a descriptor, a list of parameters and an
/// optional confirmation message, and takes back values or a refusal.
/// </remarks>
/// <param name="descriptor">The command being invoked.</param>
/// <param name="parameters">The values to collect. May be empty when only a confirmation is needed.</param>
/// <param name="confirmationMessage">
/// The binding's own <see cref="CommandBinding.ConfirmationMessage"/>, or
/// <see langword="null"/> when the action needs no confirmation.
/// </param>
/// <param name="cancellationToken">A token observed while collecting.</param>
/// <returns>
/// The collected values keyed by <see cref="CommandParameter.Name"/>, or
/// <see langword="null"/> if the person declined — a refusal is not a
/// failure, and must not be reported as one.
/// </returns>
public delegate Task<IReadOnlyDictionary<string, string>?> CommandParameterPrompt(
    CommandDescriptor descriptor,
    IReadOnlyList<CommandParameter> parameters,
    string? confirmationMessage,
    CancellationToken cancellationToken);
