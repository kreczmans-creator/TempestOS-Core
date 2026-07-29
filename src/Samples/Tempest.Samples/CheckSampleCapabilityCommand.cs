using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler checks whether the current license
/// enables <see cref="LicensingSampleModule.SampleCapabilityKey"/>,
/// demonstrating Identity (permission-gated), Licensing (capability
/// evaluation), Settings (a customised message when granted), Audit
/// (recording), and Notifications (a completion notice) integration
/// together — see <see cref="CheckSampleCapabilityCommandHandler"/>.
/// </summary>
/// <remarks>
/// Carries no data — the sample capability check takes no
/// caller-supplied parameters.
/// </remarks>
public sealed class CheckSampleCapabilityCommand : ICommand
{
}
