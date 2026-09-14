using Tempest.Core.Commands;

namespace Tempest.Desktop.Tests;

/// <summary>
/// A shared, deterministic <see cref="CommandParameterPrompt"/> stand-in
/// for <see cref="Tempest.Desktop.Views.MacroManagerDialog"/>'s own
/// record-time value collection (`WP 20.2C`) — every declared parameter
/// answered from its own <see cref="CommandParameter.DefaultValue"/>, or a
/// fixed placeholder when it has none, never a real dialog. Mirrors this
/// suite's own established convention of one small, hand-written test
/// double per seam rather than a mocking framework.
/// </summary>
internal static class TestMacroStepPrompt
{
    public static Task<IReadOnlyDictionary<string, string>?> AutoFill(
        CommandDescriptor descriptor,
        IReadOnlyList<CommandParameter> parameters,
        string? confirmationMessage,
        CancellationToken cancellationToken)
    {
        var values = parameters.ToDictionary(
            p => p.Name,
            p => p.DefaultValue ?? p.AllowedValues?.FirstOrDefault() ?? "Test value",
            StringComparer.Ordinal);

        return Task.FromResult<IReadOnlyDictionary<string, string>?>(values);
    }
}
