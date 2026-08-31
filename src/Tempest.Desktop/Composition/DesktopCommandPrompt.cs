using Tempest.Core.Commands;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// The one <see cref="CommandParameterPrompt"/> this shell supplies —
/// TD-77 Stage 5. Collects a binding's declared values with the existing
/// <see cref="InputDialog"/>, and its declared confirmation with the
/// existing <see cref="ConfirmationDialog"/>.
/// </summary>
/// <remarks>
/// <para>
/// Core states what is needed and what to say; this states how to ask.
/// Nothing here decides whether a command is available, what its values
/// mean, or whether a value is acceptable — the binding's own
/// <see cref="CommandParameter.Check"/> is passed straight to the dialog
/// as its validator, so the rule that rejects a value is the same rule
/// <see cref="ICommandRegistry.InvokeAsync(string, CommandContext, CommandParameterPrompt?, CancellationToken)"/>
/// re-checks afterwards.
/// </para>
/// <para>
/// <b>Declining is a first-class answer.</b> Closing either dialog returns
/// <see langword="null"/> all the way out, which the registry reports as
/// <see cref="CommandOutcome.Cancelled"/> — no error, no toast, nothing
/// dispatched.
/// </para>
/// </remarks>
internal sealed class DesktopCommandPrompt
{
    private readonly InputDialog _inputDialog;
    private readonly Func<CommandDescriptor, string, Task<bool>> _confirm;

    /// <summary>
    /// Initialises a new instance of the <see cref="DesktopCommandPrompt"/> class.
    /// </summary>
    /// <param name="inputDialog">The shell's own single-value input dialog.</param>
    /// <param name="confirm">
    /// Asks the person to confirm an action, given the command being
    /// invoked and the binding's own message. Separate from the dialog
    /// itself because delete confirmation is settings-controlled
    /// (<c>UserSettings.ConfirmBeforeDelete</c>) while every other
    /// confirmation is unconditional — a policy the shell owns, not this
    /// class.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public DesktopCommandPrompt(InputDialog inputDialog, Func<CommandDescriptor, string, Task<bool>> confirm)
    {
        ArgumentNullException.ThrowIfNull(inputDialog);
        ArgumentNullException.ThrowIfNull(confirm);

        _inputDialog = inputDialog;
        _confirm = confirm;
    }

    /// <summary>This prompt, as the delegate the registry takes.</summary>
    public CommandParameterPrompt Prompt => CollectAsync;

    private async Task<IReadOnlyDictionary<string, string>?> CollectAsync(
        CommandDescriptor descriptor,
        IReadOnlyList<CommandParameter> parameters,
        string? confirmationMessage,
        CancellationToken cancellationToken)
    {
        // Confirmation first: a person who is going to say no should not be
        // asked to fill anything in beforehand.
        if (confirmationMessage is not null && !await _confirm(descriptor, confirmationMessage).ConfigureAwait(true))
            return null;

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var parameter in parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var value = await _inputDialog.PromptAsync(
                descriptor.DisplayName,
                LabelFor(parameter),
                initialValue: parameter.DefaultValue ?? string.Empty,
                validate: parameter.Check).ConfigureAwait(true);

            // Declined, at any step. Nothing collected so far is used.
            if (value is null)
                return null;

            values[parameter.Name] = value;
        }

        return values;
    }

    /// <summary>
    /// The parameter's own label, with its closed set spelled out when it
    /// has one — the same "(Pass, Fail, Conditional)" shape the Ribbon's
    /// own prompts already used, now derived from the binding instead of
    /// being written out per call site.
    /// </summary>
    private static string LabelFor(CommandParameter parameter) =>
        parameter.AllowedValues is { Count: > 0 } allowed
            ? $"{parameter.Label} ({string.Join(", ", allowed)}):"
            : $"{parameter.Label}:";
}
