using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that contributes a single
/// <see cref="NavigationItem"/> for the Project Explorer's own Requirements
/// area — the area <c>Tempest.App</c>'s own composition root (<c>Program.cs</c>)
/// attaches <c>Tempest.App.Workspace.Requirements.RequirementsNodeProvider</c>
/// to. Mirrors <c>MechanicalWorkspaceExplorerModule</c>'s own identical
/// shape exactly (`WP 9.1A`).
/// </summary>
[ModuleMetadata("tempest.samples.requirements-workspace-explorer", "Requirements Workspace Explorer", "1.0.0")]
public sealed class RequirementsWorkspaceExplorerModule : ModuleLifecycleBase
{
    /// <summary>The <see cref="NavigationItem.Id"/> this module registers — the Requirements Management area's own <c>Kind</c> throughout the Workspace registration surface.</summary>
    public const string NavigationItemId = "tempest.requirements.management";

    private readonly INavigationProvider _navigationProvider;

    /// <summary>Initialises a new instance of the <see cref="RequirementsWorkspaceExplorerModule"/> class.</summary>
    public RequirementsWorkspaceExplorerModule(INavigationProvider navigationProvider)
        : base("tempest.samples.requirements-workspace-explorer", "Requirements Workspace Explorer", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _navigationProvider = navigationProvider;
    }

    /// <inheritdoc />
    /// <remarks>Registers this module's <see cref="NavigationItem"/>.</remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Register(new NavigationItem(NavigationItemId, "Requirements Management"));

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>Unregisters this module's <see cref="NavigationItem"/>.</remarks>
    public override Task DisposeAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Unregister(NavigationItemId);

        return Task.CompletedTask;
    }
}
