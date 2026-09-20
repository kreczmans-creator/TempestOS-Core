using Tempest.Core.Commands;
using Tempest.Core.Modules;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Registers the Dashboard Export capability's own manual "export now"
/// command. <see cref="DashboardExportHostedService"/> itself needs no
/// registration here — it is a concrete, non-abstract <see cref="Tempest.Core.BackgroundServices.IHostedService"/>,
/// found by reflection and registered as a DI singleton automatically (see
/// its own remarks) — mirroring
/// <c>Tempest.Samples.ExportImportSampleModule</c>'s own identical shape:
/// a discovered <see cref="ModuleLifecycleBase"/> whose constructor takes
/// only ordinary, already-registered platform services, including the
/// hosted service's own concrete type.
/// </summary>
[ModuleMetadata("tempest.workspace.dashboardexport", "Dashboard Export", "1.0.0")]
public sealed class DashboardExportModule : ModuleLifecycleBase
{
    /// <summary>The <see cref="CommandDescriptor.Id"/> this module registers for <see cref="ExportDashboardDataNowCommand"/>.</summary>
    public const string ExportNowCommandId = "dashboard-export.export-now";

    private readonly DashboardExportHostedService _hostedService;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;

    /// <summary>Initialises a new instance of the <see cref="DashboardExportModule"/> class.</summary>
    public DashboardExportModule(
        DashboardExportHostedService hostedService,
        ICommandDispatcher commandDispatcher,
        ICommandRegistry commandRegistry)
        : base("tempest.workspace.dashboardexport", "Dashboard Export", "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(hostedService);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _hostedService = hostedService;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
    }

    /// <inheritdoc />
    public override Task InitialiseAsync(CancellationToken cancellationToken)
    {
        _commandDispatcher.RegisterHandler<ExportDashboardDataNowCommand>(new ExportDashboardDataNowCommandHandler(_hostedService));

        _commandRegistry.RegisterDescriptor(new CommandDescriptor(
            id: ExportNowCommandId,
            displayName: "Export Dashboard Data Now",
            category: "Integration",
            description: "Writes engineering-status.json and programme.json for Tempest-Dashboard immediately, rather than waiting for the next scheduled export.",
            createDefault: () => new ExportDashboardDataNowCommand()));

        return Task.CompletedTask;
    }
}
