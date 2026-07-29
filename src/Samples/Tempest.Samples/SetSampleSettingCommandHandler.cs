using Tempest.Core.Commands;
using Tempest.Core.Settings;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="SetSampleSettingCommand"/> by writing
/// <see cref="SetSampleSettingCommand.NewValue"/> for
/// <see cref="SettingsSampleModule.SampleSettingKey"/> through
/// <see cref="ISettingsProvider"/>.
/// </summary>
/// <remarks>
/// Always succeeds — <see cref="ISettingsProvider.SetValueAsync"/> has no
/// foreseeable failure mode for an already-registered key beyond a
/// storage-level failure, which propagates unhandled rather than being
/// reported as an ordinary <see cref="CommandResult.Failure(string)"/>.
/// </remarks>
public sealed class SetSampleSettingCommandHandler : ICommandHandler<SetSampleSettingCommand>
{
    private readonly ISettingsProvider _settingsProvider;

    /// <summary>
    /// Initialises a new instance of the <see cref="SetSampleSettingCommandHandler"/> class.
    /// </summary>
    /// <param name="settingsProvider">The Settings service this handler writes through.</param>
    public SetSampleSettingCommandHandler(ISettingsProvider settingsProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);

        _settingsProvider = settingsProvider;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(SetSampleSettingCommand command, CancellationToken cancellationToken)
    {
        await _settingsProvider.SetValueAsync(SettingsSampleModule.SampleSettingKey, command.NewValue, cancellationToken)
            .ConfigureAwait(false);

        return CommandResult.Success($"'{SettingsSampleModule.SampleSettingKey}' set to '{command.NewValue}'.");
    }
}
