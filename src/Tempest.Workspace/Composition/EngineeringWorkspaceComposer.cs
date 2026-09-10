using Tempest.Workspace;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Deliverables;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Evidence;
using Tempest.Workspace.Invoicing;
using Tempest.Workspace.Macros;
using Tempest.Workspace.Manufacturing;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Timesheets;
using Tempest.Workspace.Verification;
using Tempest.Core.Bearings;
using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Constants;
using Tempest.Core.Deliverables;
using Tempest.Core.DependencyInjection;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Fasteners;
using Tempest.Core.Identity;
using Tempest.Core.Invoicing;
using Tempest.Core.Macros;
using Tempest.Core.Materials;
using Tempest.Core.Projects;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Seeding;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Standards;
using Tempest.Core.Timesheets;
using Tempest.Core.Verification;
using Tempest.Core.Versioning;

namespace Tempest.Workspace.Composition;

/// <summary>
/// The one, shared composition-root sequence that builds a running
/// <see cref="ITempestHost"/>, starts a <see cref="WorkspaceManager"/> over
/// it, and registers all six real Engineering Disciplines
/// (Mechanical/Requirements/Calculations/Documents/Verification/
/// Manufacturing) — extracted, `WP 10.0B`, from what had been
/// <c>Tempest.Harness</c>'s own console <c>Program.cs</c> top-level statements,
/// so that a second presentation layer (<c>Tempest.Desktop</c>) can compose
/// the identical Engineering Workspace without duplicating this sequence
/// and risking behavioural drift between the two.
/// </summary>
/// <remarks>
/// <para>
/// Introduces no new capability of its own — every step below is a direct,
/// unmodified extraction of code `Tempest.Harness`'s own console entry point
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
/// <see cref="Tempest.Workspace.WorkspaceShell"/> console loop, or a
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
    /// left to the caller (a console <see cref="Tempest.Workspace.WorkspaceShell"/>,
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
    /// <param name="commandLineArgs">
    /// The process's own command-line arguments (<c>Program.Main(string[] args)</c>),
    /// or <see langword="null"/> (the default) to contribute none — reaches
    /// the resulting <see cref="ITempestHost"/>'s default configuration
    /// source (`WP 17.2A`, ADR-0146) via
    /// <see cref="ITempestHostBuilder.AddCommandLineArgs"/>.
    /// </param>
    /// <returns>An unstarted <see cref="ITempestHost"/> and its owning, unstarted <see cref="WorkspaceManager"/>.</returns>
    public static (ITempestHost Host, WorkspaceManager Manager) Build(
        IReadOnlyList<IConfigurationSource>? configurationSources = null,
        IReadOnlyList<string>? commandLineArgs = null)
    {
        var builder = new TempestHostBuilder();

        if (commandLineArgs is not null)
            builder.AddCommandLineArgs(commandLineArgs);

        if (configurationSources is not null)
        {
            foreach (var source in configurationSources)
                builder.AddConfigurationSource(source);
        }

        var host = builder.Build();
        var manager = new WorkspaceManager(host);

        // No sample explorer area is registered here. `TD-75` phase 2 removed
        // the pair of registrations that used to sit at this point, which
        // attached a fixed, fictional tree to the navigation area
        // `tempest.samples.workspace-explorer.objects`. That area's only
        // registrar is `Tempest.Samples.WorkspaceExplorerSampleModule`, and
        // phase 1 stopped the product loading that assembly at all — so from
        // then on these two lines keyed a provider and a view factory to an
        // area no production run ever contained. Measured before removal: a
        // real composition root with no `Tempest.Samples.dll` on disk
        // registers exactly six navigation items, all real disciplines, and
        // none of them that area. The fictional content itself now lives in
        // `Tempest.Core.Tests`, which is the only thing that still drives it.

        return (host, manager);
    }

    /// <summary>
    /// Registers all six real Engineering Disciplines
    /// (Mechanical/Requirements/Calculations/Documents/Verification/
    /// Manufacturing) against <paramref name="manager"/> — the identical
    /// sequence and order `Tempest.Harness`'s own console entry point has run
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
    /// <param name="issueSheetRenderer">
    /// Renders Evidence's own issue sheet on Issue (`WP 18.2B`, §2) —
    /// <see langword="null"/> (the default; every caller but
    /// <c>Tempest.Desktop.WorkspaceHost</c>) for a composition root with no
    /// renderer available. Issuing still succeeds; only the sheet is
    /// skipped (<see cref="Evidence.IssueEvidenceCommandHandler"/>'s own
    /// remarks).
    /// </param>
    public static CalculationTemplateRegistry RegisterEngineeringDisciplines(
        WorkspaceManager manager, ITempestHost host, Tempest.Workspace.Evidence.IIssueSheetRenderer? issueSheetRenderer = null)
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
        var evidenceService = (IEvidenceService)services.GetService(typeof(IEvidenceService));
        var principalDirectory = (IPrincipalDirectory)services.GetService(typeof(IPrincipalDirectory));
        var platformVersionProvider = (IPlatformVersionProvider)services.GetService(typeof(IPlatformVersionProvider));
        var projectCommercialService = (IProjectCommercialService)services.GetService(typeof(IProjectCommercialService));
        var timesheetService = (ITimesheetService)services.GetService(typeof(ITimesheetService));
        var deliverableService = (IDeliverableService)services.GetService(typeof(IDeliverableService));
        var invoicingService = (IInvoicingService)services.GetService(typeof(IInvoicingService));

        MechanicalWorkspaceRegistration.Register(manager, domainContext, commandDispatcher, commandRegistry, referenceIntegrityChecker);
        RequirementsWorkspaceRegistration.Register(manager, requirementsService, commandDispatcher, commandRegistry);
        var calculationTemplateRegistry = CalculationsWorkspaceRegistration.Register(manager, domainContext, calculationEngine, commandDispatcher, commandRegistry);
        DocumentsWorkspaceRegistration.Register(manager, domainContext, commandDispatcher, commandRegistry);
        VerificationWorkspaceRegistration.Register(manager, domainContext, verificationService, commandDispatcher, commandRegistry);

        // `ADR-0148` (`WP 18.0A`). Must run after Mechanical — it reuses
        // Mechanical's own already-registered rename/delete command
        // handlers (this class's own remarks).
        EvidenceWorkspaceRegistration.Register(
            manager, domainContext, evidenceService, commandDispatcher, commandRegistry,
            issueSheetRenderer, principalDirectory, platformVersionProvider);

        // `ADR-0150` (`WP 19.0A`). The project commercial core's own six
        // commands, over the Project Kind Mechanical already owns and
        // registers a node/facet provider for.
        ProjectCommercialWorkspaceRegistration.Register(projectCommercialService, commandDispatcher, commandRegistry);

        // `ADR-0150` (`WP 19.0A`). Time and deliverable completion — each a
        // new canonical Kind with its own discipline registration,
        // mirroring Evidence's own shape.
        TimesheetsWorkspaceRegistration.Register(manager, domainContext, timesheetService, principalDirectory, commandDispatcher, commandRegistry);
        DeliverableCompletionWorkspaceRegistration.Register(manager, domainContext, deliverableService, principalDirectory, commandDispatcher, commandRegistry);

        // `ADR-0151` (`WP 19.1A`). Outbound invoicing: the connector seam,
        // the request Kind and its own discipline registration, mirroring
        // Evidence's own shape — no view, no rail entry (this Work
        // Package's own scope is the model and the substrate; parts 2/3
        // build the real connectors and the Invoicing area). Wired here,
        // after DeliverableCompletionWorkspaceRegistration, so completing a
        // deliverable can raise a request the moment this registration's
        // own hook is set, below.
        InvoicingWorkspaceRegistration.Register(manager, domainContext, invoicingService, commandDispatcher, commandRegistry);

        // The optional completion hook (`DeliverableService`'s own
        // remarks): completing a deliverable raises an invoice request
        // through the service, never the UI. Set here, once both services
        // exist, rather than as a constructor dependency either way round
        // — `InvoicingService` already depends on `IDeliverableService`
        // (`MarkInvoicedAsync`), so the reverse dependency at construction
        // time would be circular.
        if (deliverableService is Tempest.Core.Deliverables.DeliverableService concreteDeliverableService)
        {
            concreteDeliverableService.SetCompletionHook(
                (completionId, token) => invoicingService.RaiseFromCompletionAsync(completionId, token));
        }

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

        // `TD-85`. Each discipline declares how its own Kinds come back
        // after a restart, using the same named Kind constants it already
        // owns (`ADR-0105`) — registered here, alongside the discipline
        // registration it belongs to, so a discipline can never be wired
        // for creation but silently forgotten for recovery.
        var rehydrators = (IEngineeringObjectRehydratorRegistry)services.GetService(typeof(IEngineeringObjectRehydratorRegistry));
        MechanicalObjectFactoryRegistry.RegisterRehydrators(rehydrators, domainContext);
        DocumentObjectFactoryRegistry.RegisterRehydrators(rehydrators, domainContext);
        CalculationObjectFactoryRegistry.RegisterRehydrators(rehydrators, domainContext);
        VerificationActivityFactoryRegistry.RegisterRehydrators(rehydrators, domainContext);
        ManufacturingObjectFactoryRegistry.RegisterRehydrators(rehydrators, domainContext);
        rehydrators.Register<Tempest.Core.Evidence.Evidence>(Tempest.Core.Evidence.Evidence.CanonicalKind, domainContext);

        // `ADR-0150` (`WP 19.0A`) — the fortieth and forty-first Kinds with
        // a production rehydrator from the day they shipped, the Evidence
        // path repeated for each of the two new disciplines registered
        // above.
        rehydrators.Register<Tempest.Core.Timesheets.TimesheetEntry>(Tempest.Core.Timesheets.TimesheetEntry.CanonicalKind, domainContext);
        rehydrators.Register<Tempest.Core.Deliverables.DeliverableCompletion>(Tempest.Core.Deliverables.DeliverableCompletion.CanonicalKind, domainContext);

        // `ADR-0151` (`WP 19.1A`) — the same shape once more, for the
        // request Kind this Work Package adds.
        rehydrators.Register<Tempest.Core.Invoicing.InvoiceRequest>(Tempest.Core.Invoicing.InvoiceRequest.CanonicalKind, domainContext);

        // The canonical Kinds that are durable and rehydratable but have no
        // discipline workspace yet. Twelve of them were registered only by
        // `Tempest.Samples` and nine by nothing at all, so the product's
        // ability to reload a Risk, a Task or a Hazard was either an
        // accident of the sample harness shipping (`TD-75`) or simply
        // absent. Registered here, in production, on the same one
        // rehydration boundary (`TD-85`).
        CanonicalObjectKinds.RegisterRehydrators(rehydrators, domainContext);

        return calculationTemplateRegistry;
    }

    /// <summary>
    /// Reconstructs every engineering object persisted by a previous run,
    /// and every relationship between them, into the live repositories
    /// (`TD-85`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called after <see cref="RegisterEngineeringDisciplines"/> —
    /// which is what tells the platform how each Kind comes back — and
    /// before anything reads the object repository, so a user never sees
    /// an empty workspace that then fills in underneath them.
    /// </para>
    /// <para>
    /// This is the step that makes persistence real rather than
    /// theoretical: without it the documents survive a restart but the
    /// engineering work does not (`ADR-0077`'s own disclosed gap).
    /// </para>
    /// <para>
    /// Also self-heals the search index (`WP 18.1B`): once rehydration has
    /// finished, <c>IEngineeringObjectStateStore.RebuildIndexAsync</c> runs
    /// — a no-op unless the index is empty while durable state is not, so
    /// a fresh database is indexed once and a healthy one is never
    /// redundantly rewalked.
    /// </para>
    /// <para>
    /// Also populates the five shipped reference libraries (`WP 18.0B-R1`,
    /// `TD-163`) — Standards, Materials, Constants, Fasteners, Bearings —
    /// each only if that library is still holding no record at all
    /// (<see cref="ReferenceSeedService.ApplyIfEmptyAsync{TDefinition}"/>).
    /// A library a person has already populated, edited or seeded is never
    /// touched again, so a re-launch adds nothing and the shipped corpus
    /// never overrides a value a person corrected. Every record this seeds
    /// lands <see cref="ReferenceValidationState.Draft"/>, carrying its
    /// dataset's own <see cref="SourceCitation"/> — nothing is verified or
    /// released by starting the application.
    /// </para>
    /// </remarks>
    /// <returns>A full account of what was recovered, and of anything that could not be.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="host"/>'s own <see cref="ITempestHost.Services"/> is not yet resolvable.</exception>
    public static async Task<EngineeringRehydrationResult> RehydrateEngineeringObjectsAsync(ITempestHost host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        var services = host.Services ?? throw new InvalidOperationException("The Host must be running (ITempestHost.Services resolvable) before engineering objects can be rehydrated.");

        await SeedEmptyReferenceLibrariesAsync(services, cancellationToken).ConfigureAwait(false);

        var rehydrationService = (EngineeringObjectRehydrationService)services.GetService(typeof(EngineeringObjectRehydrationService));

        var result = await rehydrationService.RehydrateAsync(cancellationToken).ConfigureAwait(false);

        var stateStore = (IEngineeringObjectStateStore)services.GetService(typeof(IEngineeringObjectStateStore));
        await stateStore.RebuildIndexAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Populates each of the five shipped reference libraries from its own
    /// seed dataset, but only where that library is still empty
    /// (`WP 18.0B-R1`, `TD-163`) — the gap a backlog audit found: `WP 18.0A`
    /// gave all 41 seeded records a structured source citation, but only
    /// Materials ever reached a shipped call site
    /// (<c>BracketCalculationWorkbench.PopulateMaterialLibraryAsync</c>),
    /// leaving Fasteners, Bearings, Standards and Constants — 35 of the 41
    /// records — permanently empty in the shipped product.
    /// </summary>
    /// <param name="services">The running Host's own resolvable services.</param>
    /// <param name="cancellationToken">Cancels the seeding.</param>
    private static async Task SeedEmptyReferenceLibrariesAsync(ITempestServiceProvider services, CancellationToken cancellationToken)
    {
        var seeder = (ReferenceSeedService)services.GetService(typeof(ReferenceSeedService));

        // Citation order: Standards first, because the other libraries
        // cite it — the same order `SeedHarness.SeedEverythingAsync`
        // already establishes for the test-only equivalent of this pass.
        await seeder.ApplyIfEmptyAsync(
            (IStandardCatalog)services.GetService(typeof(IStandardCatalog)), StandardSeed.Instance, cancellationToken)
            .ConfigureAwait(false);
        await seeder.ApplyIfEmptyAsync(
            (IMaterialCatalog)services.GetService(typeof(IMaterialCatalog)), MaterialSeed.Instance, cancellationToken)
            .ConfigureAwait(false);
        await seeder.ApplyIfEmptyAsync(
            (IConstantCatalog)services.GetService(typeof(IConstantCatalog)), ConstantSeed.Instance, cancellationToken)
            .ConfigureAwait(false);
        await seeder.ApplyIfEmptyAsync(
            (IFastenerCatalog)services.GetService(typeof(IFastenerCatalog)), FastenerSeed.Instance, cancellationToken)
            .ConfigureAwait(false);
        await seeder.ApplyIfEmptyAsync(
            (IBearingCatalog)services.GetService(typeof(IBearingCatalog)), BearingSeed.Instance, cancellationToken)
            .ConfigureAwait(false);
    }
}
