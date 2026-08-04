using Tempest.App.Workspace;
using Tempest.Core.Runtime;

Console.Title = "TempestOS";

var host = new TempestHostBuilder().Build();
var manager = new WorkspaceManager(host);

await using var shell = new WorkspaceShell(manager, Console.Out, Console.In);

await shell.RunAsync();
