using Tempest.Core.Commands;
using Tempest.Core.Settings;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="GetSampleSettingCommand"/> by reading
/// <see cref="ISettingsProvider"/> and reporting the current value of
/// <see cref="SettingsSampleModule.SampleSettingKey"/>.
/// </summary>
public sealed class GetSampleSettingCommandHandler : ICommandHandler<GetSampleSettingCommand>
{
    private readonly ISettingsProvider _settingsProvider;

    /// <summary>
    /// Initialises a new instance of the <see cref="GetSampleSettingCommandHandler"/> class.
    /// </summary>
    /// <param name="settingsProvider">The Settings service this handler reads from.</param>
    public GetSampleSettingCommandHandler(ISettingsProvider settingsProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);

        _settingsProvider = settingsProvider;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(GetSampleSettingCommand command, CancellationToken cancellationToken)
    {
        var value = await _settingsProvider.GetValueAsync(SettingsSampleModule.SampleSettingKey, cancellationToken)
            .ConfigureAwait(false);

        return CommandResult.Success(value);
    }
}
