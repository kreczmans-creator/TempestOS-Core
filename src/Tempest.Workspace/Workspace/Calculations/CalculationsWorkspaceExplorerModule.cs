using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Contributes the Project Explorer's own Calculations area to the
/// navigation model — the area CalculationsWorkspaceRegistration attaches
/// <see cref="CalculationsNodeProvider"/> to.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is product navigation, and it lives with the discipline that
/// owns it (`TD-75` phase 1).</b> It was declared in <c>Tempest.Samples</c>
/// until 2026-08-30, which meant the real Engineering Workspace took its
/// navigation identity from the sample harness: removing the sample
/// assembly removed the Calculations area — Calculations and Calculation Sets. The
/// 2026-08-30 Product Gap Reconciliation audit measured that coupling and
/// found it was never packaging — it was product content filed in the
/// wrong assembly.
/// </para>
/// <para>
/// It registers only the <see cref="NavigationItem"/>, never the
/// Explorer/View/Facet registrations themselves, because
/// <c>WorkspaceManager.RegisterExplorerArea</c>/<c>RegisterView</c>/
/// <c>RegisterFacetProvider</c> are not reachable from inside a
/// Host-discovered module (`ADR-0071`). Those stay in
/// <see cref="CalculationsWorkspaceRegistration"/>, which is why the two must
/// agree on <see cref="NavigationItemId"/> and now do so from the same
/// assembly.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.calculations.workspace-explorer", "Calculations Workspace Explorer", "1.0.0")]
public sealed class CalculationsWorkspaceExplorerModule : ModuleLifecycleBase
{
    /// <summary>The <see cref="NavigationItem.Id"/> this module registers — the Calculations area's own <c>Kind</c> throughout the Workspace registration surface.</summary>
    public const string NavigationItemId = "tempest.calculations.management";

    private readonly INavigationProvider _navigationProvider;

    /// <summary>Initialises a new instance of the <see cref="CalculationsWorkspaceExplorerModule"/> class.</summary>
    public CalculationsWorkspaceExplorerModule(INavigationProvider navigationProvider)
        : base("tempest.calculations.workspace-explorer", "Calculations Workspace Explorer", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _navigationProvider = navigationProvider;
    }

    /// <inheritdoc />
    /// <remarks>Registers this module's <see cref="NavigationItem"/>.</remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Register(new NavigationItem(NavigationItemId, "Calculations"));

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
