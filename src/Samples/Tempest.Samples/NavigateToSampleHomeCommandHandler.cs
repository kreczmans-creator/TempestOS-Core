using Tempest.Core.Commands;
using Tempest.Core.Navigation;

namespace Tempest.Samples;

/// <summary>
/// Handles <see cref="NavigateToSampleHomeCommand"/> by navigating to
/// <see cref="NavigationSampleModule.NavigationItemId"/>.
/// </summary>
/// <remarks>
/// Depends on <see cref="INavigationProvider"/> directly, as an ordinary,
/// explicit peer dependency of this command's own application logic — the
/// Command Framework itself never depends on Navigation, and
/// <see cref="Tempest.Core.Navigation.NavigationService"/> never depends on
/// the Command Framework. See ADR-0022.
/// </remarks>
public sealed class NavigateToSampleHomeCommandHandler : ICommandHandler<NavigateToSampleHomeCommand>
{
    private readonly INavigationProvider _navigationProvider;

    /// <summary>
    /// Initialises a new instance of the <see cref="NavigateToSampleHomeCommandHandler"/> class.
    /// </summary>
    /// <param name="navigationProvider">The Navigation Framework this handler navigates through.</param>
    public NavigateToSampleHomeCommandHandler(INavigationProvider navigationProvider)
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _navigationProvider = navigationProvider;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(NavigateToSampleHomeCommand command, CancellationToken cancellationToken)
    {
        await _navigationProvider.Navigate(NavigationSampleModule.NavigationItemId, cancellationToken).ConfigureAwait(false);

        return CommandResult.Success();
    }
}
