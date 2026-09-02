using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Contributes the Project Explorer's own Verification area to the
/// navigation model — the area VerificationWorkspaceRegistration attaches
/// <see cref="VerificationNodeProvider"/> to.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is product navigation, and it lives with the discipline that
/// owns it (`TD-75` phase 1).</b> It was declared in <c>Tempest.Samples</c>
/// until 2026-08-30, which meant the real Engineering Workspace took its
/// navigation identity from the sample harness: removing the sample
/// assembly removed the Verification area — Verification Plans and Activities. The
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
/// <see cref="VerificationWorkspaceRegistration"/>, which is why the two must
/// agree on <see cref="NavigationItemId"/> and now do so from the same
/// assembly.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.verification.workspace-explorer", "Verification Workspace Explorer", "1.0.0")]
public sealed class VerificationWorkspaceExplorerModule : ModuleLifecycleBase
{
    /// <summary>The <see cref="NavigationItem.Id"/> this module registers — the Verification area's own <c>Kind</c> throughout the Workspace registration surface.</summary>
    public const string NavigationItemId = "tempest.verification.management";

    private readonly INavigationProvider _navigationProvider;

    /// <summary>Initialises a new instance of the <see cref="VerificationWorkspaceExplorerModule"/> class.</summary>
    public VerificationWorkspaceExplorerModule(INavigationProvider navigationProvider)
        : base("tempest.verification.workspace-explorer", "Verification Workspace Explorer", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _navigationProvider = navigationProvider;
    }

    /// <inheritdoc />
    /// <remarks>Registers this module's <see cref="NavigationItem"/>.</remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Register(new NavigationItem(NavigationItemId, "Verification"));

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
