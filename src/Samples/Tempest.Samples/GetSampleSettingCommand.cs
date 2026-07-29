using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler reads
/// <see cref="Tempest.Core.Settings.ISettingsProvider"/> and reports the
/// current value of <see cref="SettingsSampleModule.SampleSettingKey"/>.
/// </summary>
/// <remarks>
/// Carries no data — see <see cref="GetSampleSettingCommandHandler"/>.
/// </remarks>
public sealed class GetSampleSettingCommand : ICommand
{
}
