using Tempest.Core.Commands;

namespace Tempest.Samples;

/// <summary>
/// A reference command whose handler calls
/// <see cref="Tempest.Core.Navigation.INavigationProvider.Navigate"/> —
/// the first concrete realisation of ADR-0022's own illustrated
/// <c>OpenModuleCommand → NavigationService.Navigate(...)</c> shape.
/// </summary>
/// <remarks>
/// Carries no data — see <see cref="NavigateToSampleHomeCommandHandler"/>.
/// The Command Framework itself never references
/// <see cref="Tempest.Core.Navigation.INavigationProvider"/>; only this
/// command's own application logic does, exactly as ADR-0022 requires.
/// </remarks>
public sealed class NavigateToSampleHomeCommand : ICommand
{
}
