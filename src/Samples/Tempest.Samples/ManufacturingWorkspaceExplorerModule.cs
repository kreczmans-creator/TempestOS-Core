using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that contributes a single
/// <see cref="NavigationItem"/> for the Project Explorer's own
/// Manufacturing area — the area <c>Tempest.App</c>'s own composition
/// root (<c>Program.cs</c>) attaches
/// <c>Tempest.App.Workspace.Manufacturing.ManufacturingNodeProvider</c> to.
/// Mirrors <see cref="VerificationWorkspaceExplorerModule"/>'s own
/// identical shape exactly (`WP 9.5A`).
/// </summary>
[ModuleMetadata("tempest.samples.manufacturing-workspace-explorer", "Manufacturing Workspace Explorer", "1.0.0")]
public sealed class ManufacturingWorkspaceExplorerModule : ModuleLifecycleBase
{
    /// <summary>The <see cref="NavigationItem.Id"/> this module registers — the Manufacturing area's own <c>Kind</c> throughout the Workspace registration surface.</summary>
    public const string NavigationItemId = "tempest.manufacturing.management";

    private readonly INavigationProvider _navigationProvider;

    /// <summary>Initialises a new instance of the <see cref="ManufacturingWorkspaceExplorerModule"/> class.</summary>
    public ManufacturingWorkspaceExplorerModule(INavigationProvider navigationProvider)
        : base("tempest.samples.manufacturing-workspace-explorer", "Manufacturing Workspace Explorer", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _navigationProvider = navigationProvider;
    }

    /// <inheritdoc />
    /// <remarks>Registers this module's <see cref="NavigationItem"/>.</remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Register(new NavigationItem(NavigationItemId, "Manufacturing"));

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
