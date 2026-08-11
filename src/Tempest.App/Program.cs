using Tempest.App.Composition;
using Tempest.App.Workspace;

Console.Title = "TempestOS";

// WP 10.0B: this file's own composition (building the Host, wiring the
// sample Explorer content, registering all six real Engineering
// Disciplines) has been extracted into EngineeringWorkspaceComposer, shared
// with Tempest.Desktop, so both presentation layers register the identical
// sequence in the identical order rather than risking two, independently
// maintained copies drifting apart. This file itself now only decides when
// to start (WorkspaceShell.StartAsync, below) and how to present (a
// console loop) — the same responsibility split TempestShell already has
// relative to ITempestHostBuilder/ITempestHost.
var (host, manager) = EngineeringWorkspaceComposer.Build();

await using var shell = new WorkspaceShell(manager, Console.Out, Console.In);

// Starts the Workspace (and, inside it, the Runtime Host — ITempestHost.Services
// only becomes resolvable from this point on).
await shell.StartAsync();

// The Mechanical Product Structure discipline (WP 9.0A) was the first
// Workspace registration to need a running Host, so all six real
// disciplines are registered here, between Start and the input loop,
// exactly as this file has done since WP 9.5A.
// Return value (the CalculationTemplateRegistry, `WP 10.7A`) intentionally
// discarded here — the console presentation layer has no Object Editor to
// thread it into; Tempest.Desktop's own WorkspaceHost.StartAsync captures
// the identical call's return value instead.
_ = EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);

await shell.RunInputLoopAsync();
await shell.StopAsync();
