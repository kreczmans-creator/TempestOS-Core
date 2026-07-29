using Tempest.App.Shell;
using Tempest.Core.Runtime;

Console.Title = "TempestOS";

var host = new TempestHostBuilder().Build();

await using var shell = new TempestShell(host, Console.Out, Console.In);

await shell.RunAsync();
