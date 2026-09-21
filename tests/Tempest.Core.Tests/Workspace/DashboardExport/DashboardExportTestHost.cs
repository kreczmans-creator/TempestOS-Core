using Tempest.Workspace;
using Tempest.Core.BusinessGovernance.Contracts;
using Tempest.Core.BusinessGovernance.Quotations;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Navigation;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Workspace.DashboardExport;

/// <summary>
/// Builds a real host/manager pair with no discipline modules registered —
/// the Dashboard Export adapters query <see cref="EngineeringDomainContext"/>,
/// <see cref="IRequirementsService"/>, <see cref="IRequirementValidationService"/>,
/// <see cref="IVerificationService"/>, <see cref="IIssuedContractCatalog"/>
/// and <see cref="IQuotationCatalog"/> directly, never through a
/// Workspace discipline registration, so none is needed here. Mirrors
/// <c>Tempest.Core.Tests.Evidence.EvidenceTestHost</c>'s own identical
/// shape and reasoning. The <see cref="WorkspaceManager"/> is started too,
/// so <see cref="Cockpit"/> can hand a test the desktop's own
/// <see cref="EngineeringCockpit"/> as the oracle the schema-v2 sections
/// are checked against.
/// </summary>
internal static class DashboardExportTestHost
{
    public const string PrincipalId = "dashboard-export-test-principal-01";

    public static async Task<(ITempestHost Host, WorkspaceManager Manager)> StartAsync(string persistenceRoot)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, persistenceRoot),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        await manager.StartAsync();

        SignIn(host);

        return (host, manager);
    }

    public static EngineeringDomainContext Domain(ITempestHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    public static IEvidenceService Evidence(ITempestHost host) =>
        (IEvidenceService)host.Services!.GetService(typeof(IEvidenceService));

    public static IRequirementsService Requirements(ITempestHost host) =>
        (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));

    public static IRequirementValidationService RequirementValidation(ITempestHost host) =>
        (IRequirementValidationService)host.Services!.GetService(typeof(IRequirementValidationService));

    public static IVerificationService Verification(ITempestHost host) =>
        (IVerificationService)host.Services!.GetService(typeof(IVerificationService));

    public static IIssuedContractCatalog Contracts(ITempestHost host) =>
        (IIssuedContractCatalog)host.Services!.GetService(typeof(IIssuedContractCatalog));

    public static IQuotationCatalog Quotations(ITempestHost host) =>
        (IQuotationCatalog)host.Services!.GetService(typeof(IQuotationCatalog));

    public static ICurrentPrincipalAccessor Principals(ITempestHost host) =>
        (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));

    public static INavigationProvider NavigationProvider(ITempestHost host) =>
        (INavigationProvider)host.Services!.GetService(typeof(INavigationProvider));

    public static ICommandRegistry CommandRegistry(ITempestHost host) =>
        (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

    /// <summary>
    /// The desktop's own <see cref="EngineeringCockpit"/> — the one
    /// <see cref="WorkspaceManager.StartAsync"/> built for this session,
    /// reached exactly as <c>EngineeringCockpitTests</c> reaches it and
    /// primed exactly as <c>CockpitView.RefreshAsync</c> primes it — the
    /// oracle every schema-v2 assertion compares the export against.
    /// </summary>
    public static async Task<EngineeringCockpit> Cockpit(WorkspaceManager manager)
    {
        var cockpit = ((Tempest.Workspace.Workspace)manager.Current!).Cockpit;
        await cockpit.PrimeAsync();
        return cockpit;
    }

    /// <summary>Signs in <paramref name="id"/> with a local session's own broad permission set — mirrors <c>EvidenceTestHost.SignIn</c>.</summary>
    public static void SignIn(ITempestHost host, string id = PrincipalId)
    {
        var accessor = (CurrentPrincipalAccessor)Principals(host);
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity(id, id), ApplicationPermissions.LocalSession));
    }

    public static async Task<Portfolio> CreatePortfolioAsync(ITempestHost host, string identifier, string name)
    {
        var domain = Domain(host);
        var factory = new EngineeringObjectFactory<Portfolio>(
            "Portfolio", domain, (doc, rev) => new Portfolio(doc, rev, domain, identifier, name, EngineeringObjectMetadata.Empty));
        return (Portfolio)await factory.CreateAsync($"{name} — test portfolio.");
    }

    public static async Task<Programme> CreateProgrammeAsync(ITempestHost host, string identifier, string name, Guid? portfolioId = null)
    {
        var domain = Domain(host);
        var factory = new EngineeringObjectFactory<Programme>(
            "Programme", domain, (doc, rev) => new Programme(doc, rev, domain, identifier, name, EngineeringObjectMetadata.Empty, portfolioId));
        return (Programme)await factory.CreateAsync($"{name} — test programme.");
    }

    public static async Task<Project> CreateProjectAsync(ITempestHost host, string identifier, string name, Guid? programmeId = null)
    {
        var domain = Domain(host);
        var factory = new EngineeringObjectFactory<Project>(
            "Project", domain, (doc, rev) => new Project(doc, rev, domain, identifier, name, EngineeringObjectMetadata.Empty, programmeId));
        return (Project)await factory.CreateAsync($"{name} — test project.");
    }

    public static async Task<Part> CreatePartAsync(ITempestHost host, string identifier, string name)
    {
        var domain = Domain(host);
        var factory = new EngineeringObjectFactory<Part>(
            "Part", domain, (doc, rev) => new Part(doc, rev, domain, identifier, name, EngineeringObjectMetadata.Empty, materialId: null));
        return (Part)await factory.CreateAsync($"{name} — test part.");
    }

    public static async Task<Assembly> CreateAssemblyAsync(ITempestHost host, string identifier, string name)
    {
        var domain = Domain(host);
        var factory = new EngineeringObjectFactory<Assembly>(
            "Assembly", domain, (doc, rev) => new Assembly(doc, rev, domain, identifier, name, EngineeringObjectMetadata.Empty));
        return (Assembly)await factory.CreateAsync($"{name} — test assembly.");
    }

    /// <summary>A standalone <c>"Task"</c> — the Kind <c>EngineeringCockpit.OverdueActions</c> reads — mirroring <c>ProjectTaskTests.CreateStandaloneTaskAsync</c>.</summary>
    public static async Task<EngineeringTask> CreateTaskAsync(ITempestHost host, string identifier, string name)
    {
        var domain = Domain(host);
        var factory = new EngineeringObjectFactory<EngineeringTask>(
            CanonicalObjectKinds.Task, domain, (doc, rev) => new EngineeringTask(doc, rev, domain, identifier, name, EngineeringObjectMetadata.Empty));
        return (EngineeringTask)await factory.CreateAsync($"{name} — test task.");
    }
}
