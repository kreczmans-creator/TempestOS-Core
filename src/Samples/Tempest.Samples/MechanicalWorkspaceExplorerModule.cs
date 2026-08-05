using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that contributes a single
/// <see cref="NavigationItem"/> for the Project Explorer's own Mechanical
/// area — the area <c>Tempest.App</c>'s own composition root (<c>Program.cs</c>)
/// attaches <c>Tempest.App.Workspace.Mechanical.MechanicalProductStructureNodeProvider</c>
/// to.
/// </summary>
/// <remarks>
/// Registers only the navigation item — never the Explorer/View/Facet
/// registrations themselves, since <c>WorkspaceManager.RegisterExplorerArea</c>/
/// <c>RegisterView</c>/<c>RegisterFacetProvider</c> are not reachable from
/// inside a Host-discovered module (`ADR-0071`). Mirrors
/// <c>WorkspaceExplorerSampleModule</c>'s own identical shape exactly.
/// </remarks>
[ModuleMetadata("tempest.samples.mechanical-workspace-explorer", "Mechanical Workspace Explorer", "1.0.0")]
public sealed class MechanicalWorkspaceExplorerModule : ModuleLifecycleBase
{
    /// <summary>The <see cref="NavigationItem.Id"/> this module registers — the Mechanical Product Structure area's own <c>Kind</c> throughout the Workspace registration surface.</summary>
    public const string NavigationItemId = "tempest.mechanical.product-structure";

    private readonly INavigationProvider _navigationProvider;

    /// <summary>Initialises a new instance of the <see cref="MechanicalWorkspaceExplorerModule"/> class.</summary>
    public MechanicalWorkspaceExplorerModule(INavigationProvider navigationProvider)
        : base("tempest.samples.mechanical-workspace-explorer", "Mechanical Workspace Explorer", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _navigationProvider = navigationProvider;
    }

    /// <inheritdoc />
    /// <remarks>Registers this module's <see cref="NavigationItem"/>.</remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Register(new NavigationItem(NavigationItemId, "Mechanical Product Structure"));

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
