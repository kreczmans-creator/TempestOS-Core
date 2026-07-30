using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler checks whether the current principal
/// (<see cref="Tempest.Core.Identity.ICurrentPrincipalAccessor"/>) holds
/// <see cref="IdentitySampleModule.SamplePermissionKey"/> — demonstrating
/// the Command Framework and Identity &amp; Permissions interacting,
/// exactly as a future authorization-gated command realistically would.
/// </summary>
/// <remarks>
/// Carries no data — see <see cref="CheckSamplePermissionCommandHandler"/>.
/// </remarks>
public sealed class CheckSamplePermissionCommand : ICommand
{
}
