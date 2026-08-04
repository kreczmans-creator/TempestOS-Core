using Tempest.Core.Modules;
using Tempest.Core.Navigation;

namespace Tempest.Samples;

/// <summary>
/// A small, self-contained reference module that contributes a single
/// <see cref="NavigationItem"/> — the area <c>Tempest.App</c>'s own
/// composition root (<c>Program.cs</c>) attaches the Project Explorer's own
/// living reference content to (<c>Tempest.App.Workspace.Samples.
/// SampleProjectExplorerNodeProvider</c>).
/// </summary>
/// <remarks>
/// <para>
/// Registers only the navigation item — never the Explorer/View
/// registrations themselves, since <c>WorkspaceManager.RegisterView</c>/
/// <c>RegisterExplorerArea</c> are not reachable from inside a
/// Host-discovered module (`WP 8.1B Implementation Report.md`). Mirrors
/// <see cref="NavigationSampleModule"/>'s own identical shape, deliberately
/// kept as a separate area/module rather than reusing
/// <see cref="NavigationSampleModule.NavigationItemId"/>, so this module's
/// own Explorer content never collides with any existing test's own direct
/// <c>RegisterExplorerArea</c> call against that area.
/// </para>
/// </remarks>
[ModuleMetadata("tempest.samples.workspace-explorer", "Workspace Explorer Sample", "1.0.0")]
public sealed class WorkspaceExplorerSampleModule : ModuleLifecycleBase
{
    /// <summary>The <see cref="NavigationItem.Id"/> this module registers.</summary>
    public const string NavigationItemId = "tempest.samples.workspace-explorer.objects";

    private readonly INavigationProvider _navigationProvider;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceExplorerSampleModule"/> class.</summary>
    /// <param name="navigationProvider">The Navigation Framework this module registers its item through, resolved via ordinary constructor injection.</param>
    public WorkspaceExplorerSampleModule(INavigationProvider navigationProvider)
        : base("tempest.samples.workspace-explorer", "Workspace Explorer Sample", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(navigationProvider);

        _navigationProvider = navigationProvider;
    }

    /// <inheritdoc />
    /// <remarks>Registers this module's <see cref="NavigationItem"/>.</remarks>
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _navigationProvider.Register(new NavigationItem(NavigationItemId, "Sample Objects"));

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
