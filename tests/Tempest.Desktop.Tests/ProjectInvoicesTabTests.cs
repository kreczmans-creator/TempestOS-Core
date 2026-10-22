using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `v0.25.3` independent test Q5: a project's invoices sit in the project
/// itself, in an Invoices tab beside its Quote — the invoicing surface
/// scoped to that project.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProjectInvoicesTabTests
{
    [AvaloniaFact]
    public async Task AProject_HasAnInvoicesTab_RightAfterQuote_ShowingItsOwnInvoicing()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            var project = await host.ProjectDirectory!.CreateAsync("P-0125", "Invoices tab");

            await host.ShellNavigator!.OpenProjectAsync(project.Id, ProjectArea.Invoices);
            await window.RenderCurrentModuleAsync();
            Dispatcher.UIThread.RunJobs();

            var workspace = window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().Single();
            var tabs = workspace.GetLogicalDescendants().OfType<TabControl>().First().Items.OfType<TabItem>().ToList();
            var quote = tabs.FindIndex(t => Equals(t.Tag, ProjectArea.Quote));
            var invoices = tabs.FindIndex(t => Equals(t.Tag, ProjectArea.Invoices));
            Assert.Equal(quote + 1, invoices);
            Assert.Equal("Invoices", tabs[invoices].Header);

            var invoicing = tabs[invoices].GetLogicalDescendants().OfType<InvoicingView>().Single();
            Assert.Same(invoicing, tabs[invoices].GetLogicalDescendants().OfType<InvoicingView>().First());
            Assert.Equal(project.Id, host.ProjectContext!.Current?.Id);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
