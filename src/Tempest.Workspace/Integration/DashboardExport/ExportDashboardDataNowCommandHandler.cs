using Tempest.Core.Commands;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Handles <see cref="ExportDashboardDataNowCommand"/> by calling
/// <see cref="DashboardExportHostedService.ExportOnceAsync"/> directly —
/// the identical export path the hosted service's own timer loop calls, so
/// a manual trigger and a scheduled tick can never disagree about what an
/// export attempt does.
/// </summary>
public sealed class ExportDashboardDataNowCommandHandler : ICommandHandler<ExportDashboardDataNowCommand>
{
    private readonly DashboardExportHostedService _hostedService;

    /// <summary>Initialises a new instance of the <see cref="ExportDashboardDataNowCommandHandler"/> class.</summary>
    /// <param name="hostedService">The running dashboard export hosted service this handler triggers an immediate export on.</param>
    public ExportDashboardDataNowCommandHandler(DashboardExportHostedService hostedService)
    {
        ArgumentNullException.ThrowIfNull(hostedService);

        _hostedService = hostedService;
    }

    /// <inheritdoc />
    public async Task<CommandResult> HandleAsync(ExportDashboardDataNowCommand command, CancellationToken cancellationToken)
    {
        await _hostedService.ExportOnceAsync(cancellationToken).ConfigureAwait(false);

        return _hostedService.LastExportSucceeded == false
            ? CommandResult.Failure("Dashboard export failed — see the platform log for detail.")
            : CommandResult.Success("Dashboard export files written.");
    }
}
