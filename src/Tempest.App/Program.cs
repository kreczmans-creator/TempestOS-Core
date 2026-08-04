using Tempest.App.Workspace;
using Tempest.App.Workspace.Samples;
using Tempest.Core.Runtime;
using Tempest.Samples;

Console.Title = "TempestOS";

var host = new TempestHostBuilder().Build();
var manager = new WorkspaceManager(host);

// Wires the Project Explorer's own living reference content — a fixed,
// fictional tree, proving the Kind-keyed provider architecture (ADR-0067)
// end to end. Registration happens here, not inside WorkspaceManager
// itself, since it is Tempest.App's own composition-root decision which
// area to populate — mirroring the same separation of concerns a future
// real Engineering Discipline Module's own registration will follow
// (WP 8.1B Implementation Report.md).
manager.RegisterExplorerArea(WorkspaceExplorerSampleModule.NavigationItemId, new SampleProjectExplorerNodeProvider(WorkspaceExplorerSampleModule.NavigationItemId));
manager.RegisterView(SampleExplorerContent.ComponentKind, new SampleWorkspaceViewFactory(SampleExplorerContent.ComponentKind));

await using var shell = new WorkspaceShell(manager, Console.Out, Console.In);

await shell.RunAsync();
