using Tempest.Core.Commands;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Runs one dashboard export pass immediately, rather than waiting for
/// <see cref="DashboardExportHostedService"/>'s own next timer tick — the
/// manual "export now" trigger the integration contract document names as a
/// nice-to-have (§3). Not object-specific (implements plain
/// <see cref="ICommand"/>, not <c>IWorkspaceCommand</c> — there is no
/// single engineering object this command acts on), mirroring
/// <c>Tempest.Samples.PublishSampleNotificationCommand</c>'s own identical
/// shape.
/// </summary>
public sealed class ExportDashboardDataNowCommand : ICommand
{
}
