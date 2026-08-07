using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that contributes a single
/// <see cref="NavigationItem"/> for the Project Explorer's own
/// Verification area — the area <c>Tempest.App</c>'s own composition
/// root (<c>Program.cs</c>) attaches
/// <c>Tempest.App.Workspace.Verification.VerificationActivityNodeProvider</c>
/// to. Mirrors <see cref="DocumentsWorkspaceExplorerModule"/>'s own
/// identical shape exactly (`WP 9.3A`).
/// </summary>
[ModuleMetadata("tempest.samples.verification-workspace-explorer", "Verification Workspace Explorer", "1.0.0")]
public sealed class VerificationWorkspaceExplorerModule : ModuleLifecycleBase
{
    /// <summary>The <see cref="NavigationItem.Id"/> this module registers — the Verification area's own <c>Kind</c> throughout the Workspace registration surface.</summary>
    public const string NavigationItemId = "tempest.verification.management";

    private readonly INavigationProvider _navigationProvider;

    /// <summary>Initialises a new instance of the <see cref="VerificationWorkspaceExplorerModule"/> class.</summary>
    public VerificationWorkspaceExplorerModule(INavigationProvider navigationProvider)
        : base("tempest.samples.verification-workspace-explorer", "Verification Workspace Explorer", "1.0.0")
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
