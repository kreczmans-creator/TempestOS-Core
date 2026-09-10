using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Invoicing.Connectors;

/// <summary>
/// Builds a real host/manager pair with extra configuration entries — the
/// identical convention <c>Invoicing.InvoicingTestHost.StartAsync</c>
/// already establishes, widened here (rather than there) so
/// <c>ConnectorSelectionTests</c> can set <c>Invoicing:Connector</c> and
/// provider credentials without touching a file this part does not own
/// (`WP 19.1A` part 2).
/// </summary>
internal static class ConnectorHostFixture
{
    public static async Task<(ITempestHost Host, WorkspaceManager Manager)> StartAsync(
        string persistenceRoot, params KeyValuePair<string, string>[] extraConfiguration)
    {
        var entries = new List<KeyValuePair<string, string>> { new(SqlitePersistenceStore.RootPathConfigurationKey, persistenceRoot) };
        entries.AddRange(extraConfiguration);

        var host = new TempestHostBuilder([typeof(MechanicalWorkspaceExplorerModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(entries))
            .Build();
        var manager = new WorkspaceManager(host);

        await manager.StartAsync();

        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);

        return (host, manager);
    }

    public static IInvoicingConnector Connector(ITempestHost host) =>
        (IInvoicingConnector)host.Services!.GetService(typeof(IInvoicingConnector));
}
