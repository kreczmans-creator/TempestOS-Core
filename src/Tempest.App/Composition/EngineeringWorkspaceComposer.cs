using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Macros;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Samples;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Macros;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Verification;
using Tempest.Samples;

namespace Tempest.App.Composition;

/// <summary>
/// The one, shared composition-root sequence that builds a running
/// <see cref="ITempestHost"/>, starts a <see cref="WorkspaceManager"/> over
/// it, and registers all six real Engineering Disciplines
/// (Mechanical/Requirements/Calculations/Documents/Verification/
/// Manufacturing) — extracted, `WP 10.0B`, from what had been
/// <c>Tempest.App</c>'s own console <c>Program.cs</c> top-level statements,
/// so that a second presentation layer (<c>Tempest.Desktop</c>) can compose
/// the identical Engineering Workspace without duplicating this sequence
/// and risking behavioural drift between the two.
/// </summary>
/// <remarks>
/// <para>
/// Introduces no new capability of its own — every step below is a direct,
/// unmodified extraction of code `Tempest.App`'s own console entry point
/// already ran, in the identical order. This is a composition-root
/// refactor, not a Workspace contract change (`WP 10.0B`'s own explicit
/// "no contract redesign" constraint): <see cref="IWorkspaceManager"/>,
/// <see cref="IWorkspace"/>, and every Kind-keyed registration method are
/// consumed exactly as `WP 8.0B`/`ADR-0067` already shipped them.
/// </para>
/// <para>
/// Deliberately returns the constructed <see cref="ITempestHost"/> and
/// <see cref="WorkspaceManager"/> rather than a console- or
/// Avalonia-specific wrapper — what a caller does with them (a
/// <see cref="Tempest.App.Shell.TempestShell"/>-style console loop, or a
/// graphical <c>MainWindow</c>) is entirely that caller's own
/// presentation-layer decision, mirroring how <see cref="ITempestHostBuilder"/>
/// itself already returns a plain <see cref="ITempestHost"/> rather than
/// anything presentation-specific.
/// </para>
/// </remarks>
public static class EngineeringWorkspaceComposer
{
    /// <summary>
    /// Builds a fresh <see cref="ITempestHost"/> and <see cref="WorkspaceManager"/>
    /// over it, and registers the sample Explorer content (`WP 8.1B`) — the
    /// identical construction the original console `Program.cs` performed
    /// before starting the Host. Does <b>not</b> start either — starting is
    /// left to the caller (a console <see cref="Tempest.App.Workspace.WorkspaceShell"/>,
    /// or a graphical host), since <em>when</em> to start, and what to render
    /// while starting, is a presentation-layer decision this composer does
    /// not make on the caller's behalf.
    /// </summary>
    /// <param name="configurationSources">
    /// Additional <see cref="IConfigurationSource"/>s to add to the
    /// resulting <see cref="ITempestHost"/> before it builds, or
    /// <see langword="null"/> (the default) for none — the real console and
    /// desktop entry points both pass <see langword="null"/>, leaving every
    /// Platform Service's own configuration-driven default (including
    /// <c>PersistenceStore.DefaultRootPath</c>) completely unchanged. Exists
    /// so test code can isolate its own persisted state (`WP 10.1B`, `TD-37`)
    /// the same way every <c>Tempest.Core.Tests</c> fixture already does,
    /// without this composer's own production callers needing to know or
    /// care.
    /// </param>
    /// <returns>An unstarted <see cref="ITempestHost"/> and its owning, unstarted <see cref="WorkspaceManager"/>.</returns>
    public static (ITempestHost Host, WorkspaceManager Manager) Build(IReadOnlyList<IConfigurationSource>? configurationSources = null)
    {
        var builder = new TempestHostBuilder();

        if (configurationSources is not null)
        {
            foreach (var source in configurationSources)
                builder.AddConfigurationSource(source);
        }

        var host = builder.Build();
        var manager = new WorkspaceManager(host);

        // Wires the Project Explorer's own living reference content — a fixed,
        // fictional tree, proving the Kind-keyed provider architecture
        // (ADR-0067) end to end. Needs nothing from the Runtime Host, so it
        // is registered before the Host starts, exactly as the original
        // console Program.cs already did.
        manager.RegisterExplorerArea(WorkspaceExplorerSampleModule.NavigationItemId, new SampleProjectExplorerNodeProvider(WorkspaceExplorerSampleModule.NavigationItemId));
        manager.RegisterView(SampleExplorerContent.ComponentKind, new SampleWorkspaceViewFactory(SampleExplorerContent.ComponentKind));

        return (host, manager);
    }

    /// <summary>
    /// Registers all six real Engineering Disciplines
    /// (Mechanical/Requirements/Calculations/Documents/Verification/
    /// Manufacturing) against <paramref name="manager"/> — the identical
    /// sequence and order `Tempest.App`'s own console entry point has run
    /// since `WP 9.5A`. Must be called only after <paramref name="host"/> is
    /// running (<see cref="WorkspaceManager.StartAsync"/> already returned),
    /// since every discipline's own registration reads a real Engineering
    /// Domain service resolvable only once the Host has started.
    /// </summary>
    /// <exception cref="InvalidOperationException"><paramref name="host"/>'s own <see cref="ITempestHost.Services"/> is not yet resolvable.</exception>
    /// <returns>
    /// The <see cref="CalculationTemplateRegistry"/>
    /// <see cref="CalculationsWorkspaceRegistration.Register"/> itself
    /// already constructs and returns — previously discarded here
    /// (`WP 10.7A`, Feature Completion: the Calculations Object Editor
    /// section's own real Execute/Recalculate action needs it, and it was
    /// otherwise unreachable anywhere outside this method's own local
    /// scope). Exposing an already-constructed object a caller was
    /// silently throwing away, not building a new one.
    /// </returns>
    public static CalculationTemplateRegistry RegisterEngineeringDisciplines(WorkspaceManager manager, ITempestHost host)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(host);

        var services = host.Services ?? throw new InvalidOperationException("The Host must be running (ITempestHost.Services resolvable) before Engineering Disciplines can be registered.");
        var domainContext = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var commandDispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));
        var commandRegistry = (ICommandRegistry)services.GetService(typeof(ICommandRegistry));
        var referenceIntegrityChecker = (IReferenceIntegrityChecker)services.GetService(typeof(IReferenceIntegrityChecker));
        var requirementsService = (IRequirementsService)services.GetService(typeof(IRequirementsService));
        var calculationEngine = (ICalculationEngine)services.GetService(typeof(ICalculationEngine));
        var verificationService = (IVerificationService)services.GetService(typeof(IVerificationService));
        var macroManager = (IMacroManager)services.GetService(typeof(IMacroManager));

        MechanicalWorkspaceRegistration.Register(manager, domainContext, commandDispatcher, commandRegistry, referenceIntegrityChecker);
        RequirementsWorkspaceRegistration.Register(manager, requirementsService, commandDispatcher, commandRegistry);
        var calculationTemplateRegistry = CalculationsWorkspaceRegistration.Register(manager, domainContext, calculationEngine, commandDispatcher, commandRegistry);
        DocumentsWorkspaceRegistration.Register(manager, domainContext, commandDispatcher, commandRegistry);
        VerificationWorkspaceRegistration.Register(manager, domainContext, verificationService, commandDispatcher, commandRegistry);

        // Must run after VerificationWorkspaceRegistration — Manufacturing
        // deliberately does not re-register RecordVerificationResultCommand,
        // reusing the handler Verification's own registration above already
        // wired (ManufacturingWorkspaceRegistration's own remarks).
        ManufacturingWorkspaceRegistration.Register(manager, domainContext, commandDispatcher, commandRegistry);

        // The User Command Macro foundation (`WP 10.6A`) — not a seventh
        // Engineering Discipline (MacroWorkspaceRegistration's own
        // remarks); registered here purely because it needs the identical
        // "Host already started" precondition every discipline above it
        // does.
        MacroWorkspaceRegistration.Register(commandDispatcher, commandRegistry, macroManager);

        return calculationTemplateRegistry;
    }
}
