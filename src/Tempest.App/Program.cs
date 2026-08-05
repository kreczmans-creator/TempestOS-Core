using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Samples;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Requirements;
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
// (WP 8.1B Implementation Report.md). Needs nothing from the Runtime Host,
// so it is registered before the Host starts.
manager.RegisterExplorerArea(WorkspaceExplorerSampleModule.NavigationItemId, new SampleProjectExplorerNodeProvider(WorkspaceExplorerSampleModule.NavigationItemId));
manager.RegisterView(SampleExplorerContent.ComponentKind, new SampleWorkspaceViewFactory(SampleExplorerContent.ComponentKind));

await using var shell = new WorkspaceShell(manager, Console.Out, Console.In);

// Starts the Workspace (and, inside it, the Runtime Host — ITempestHost.Services
// only becomes resolvable from this point on). The Mechanical Product
// Structure discipline (WP 9.0A) is the first Workspace registration that
// actually needs a running Host — it reads the real Engineering Domain,
// unlike the fixed sample content above — so it registers here, between
// Start and the input loop, rather than before Start like the sample
// content. Still a composition-root registration (ADR-0071); only when
// within this file it happens is new, and disclosed as such
// (MechanicalWorkspaceRegistration's own XML documentation).
await shell.StartAsync();

var services = host.Services!;
var domainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
var commandDispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
var commandRegistry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));
var referenceIntegrityChecker = (IReferenceIntegrityChecker)services.GetService(typeof(IReferenceIntegrityChecker));
var requirementsService = (IRequirementsService)services.GetService(typeof(IRequirementsService));

MechanicalWorkspaceRegistration.Register(manager, domainContext, commandDispatcher, commandRegistry, referenceIntegrityChecker);

// WP 9.1A: the second real Engineering discipline registered here, after
// the Host has started — same reason as Mechanical's own registration
// immediately above (RequirementsService reads the real Engineering Data
// document store, populated only once the Host is running).
RequirementsWorkspaceRegistration.Register(manager, requirementsService, commandDispatcher, commandRegistry);

await shell.RunInputLoopAsync();
await shell.StopAsync();
