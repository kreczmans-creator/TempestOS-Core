using Tempest.Harness;
using Tempest.Workspace.Composition;
using Tempest.Workspace;
using Tempest.Core.Configuration;
using Tempest.Core.Identity;

Console.Title = "TempestOS";

// WP 10.0B: this file's own composition (building the Host, wiring the
// sample Explorer content, registering all six real Engineering
// Disciplines) has been extracted into EngineeringWorkspaceComposer, shared
// with Tempest.Desktop, so both presentation layers register the identical
// sequence in the identical order rather than risking two, independently
// maintained copies drifting apart. This file itself now only decides when
// to start (WorkspaceShell.StartAsync, below) and how to present (a
// console loop) — a clean separation from ITempestHostBuilder/ITempestHost's
// own, presentation-agnostic construction.
//
// WP 11.3B: this project's own console entry point is TempestOS's
// Internal Engineering Harness (ADR-0101) — a fast, scriptable surface
// for verifying the Runtime Host and Workspace domain layer compose and
// run correctly, not a shipped end-user product. TempestOS's shipped
// desktop application is Tempest.Desktop.
var (host, manager) = EngineeringWorkspaceComposer.Build(commandLineArgs: args);

await using var shell = new WorkspaceShell(manager, Console.Out, Console.In);

// Starts the Workspace (and, inside it, the Runtime Host — ITempestHost.Services
// only becomes resolvable from this point on).
await shell.StartAsync();

// `WP 21.5F` Offensive Security Audit, item 9: establishes the session
// principal the identical way Tempest.Desktop's own WorkspaceHost does
// (`TD-103`, `ADR-0146`) — before this fix, this Harness never set one at
// all, so every audit row, authorship field and check record a Harness
// session produced fell back to
// EngineeringDocumentStore.UnknownAuthorPrincipalId, indistinguishable
// from a genuinely unauthenticated write. Tempest.Harness is a shipped
// release asset (release.yml's own "engineering-harness" archive), not
// only a developer tool, so this is a real audit-trail gap, not a test-only
// one. Published unconditionally, null included, mirroring WorkspaceHost's
// own identical remark: a session that genuinely has no principal must
// report none, not silently inherit whatever a sample module happened to
// establish during its own initialisation.
//
// `WP 21.6A` (OSA-12/OSA-14): reached through PrincipalSession, the same
// single seam WorkspaceHost's own start-up uses — CurrentPrincipalAccessor
// itself is no longer registered under its own concrete type at all, and
// its SetCurrent is internal to Tempest.Core regardless.
if (host.Services!.GetService(typeof(PrincipalSession)) is PrincipalSession principalSession)
{
    var configuration = (IConfigurationProvider)host.Services!.GetService(typeof(IConfigurationProvider));
    principalSession.Establish(new SessionPrincipalSource(configuration).Resolve());
}

// The Mechanical Product Structure discipline (WP 9.0A) was the first
// Workspace registration to need a running Host, so all six real
// disciplines are registered here, between Start and the input loop,
// exactly as this file has done since WP 9.5A.
// Return value (the CalculationTemplateRegistry, `WP 10.7A`) intentionally
// discarded here — the console presentation layer has no Object Editor to
// thread it into; Tempest.Desktop's own WorkspaceHost.StartAsync captures
// the identical call's return value instead.
_ = EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);

// `TD-85`. Bring back every engineering object a previous run persisted,
// before the input loop can read the object graph — the console shell and
// the desktop shell recover the identical work, from the identical store.
_ = await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);

await shell.RunInputLoopAsync();
await shell.StopAsync();
