using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Mechanical;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Configuration;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Projects;
using Tempest.Core.Runtime;
using Tempest.Core.Settings;
using Tempest.Core.Timesheets;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// Builds a real host/manager pair with the Engineering Disciplines
/// registered — the identical convention <c>Evidence.EvidenceTestHost</c>
/// already establishes, for the project commercial core, Timesheets and
/// Deliverables' own tests (`WP 19.0A`, `ADR-0150`).
/// </summary>
internal static class ProjectCommercialTestHost
{
    public const string PrincipalId = "commercial-test-principal-01";

    public static async Task<(ITempestHost Host, WorkspaceManager Manager)> StartAsync(string persistenceRoot)
    {
        var host = new TempestHostBuilder([typeof(MechanicalWorkspaceExplorerModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, persistenceRoot),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        await manager.StartAsync();

        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);

        return (host, manager);
    }

    public static EngineeringDomainContext Domain(ITempestHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    public static ICurrentPrincipalAccessor Principals(ITempestHost host) =>
        (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));

    public static ISettingsProvider Settings(ITempestHost host) =>
        (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider));

    public static IProjectCommercialService ProjectCommercial(ITempestHost host) =>
        (IProjectCommercialService)host.Services!.GetService(typeof(IProjectCommercialService));

    public static IRateCardCatalog RateCards(ITempestHost host) =>
        (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));

    public static IOrganisationCatalog Organisations(ITempestHost host) =>
        (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));

    public static ITimesheetService Timesheets(ITempestHost host) =>
        (ITimesheetService)host.Services!.GetService(typeof(ITimesheetService));

    public static IDeliverableService Deliverables(ITempestHost host) =>
        (IDeliverableService)host.Services!.GetService(typeof(IDeliverableService));

    public static IWorkingPatternProvider WorkingPatterns(ITempestHost host) =>
        (IWorkingPatternProvider)host.Services!.GetService(typeof(IWorkingPatternProvider));

    /// <summary>Signs in <paramref name="id"/> with a local session's own broad permission set.</summary>
    public static void SignIn(ITempestHost host, string id = PrincipalId)
    {
        var accessor = (CurrentPrincipalAccessor)Principals(host);
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity(id, id), ApplicationPermissions.LocalSession));
    }

    public static async Task<Guid> CreateProjectAsync(ITempestHost host, string identifier = "COM-PRJ", string name = "Commercial Test Project")
    {
        var domain = Domain(host);
        var factory = new EngineeringObjectFactory<Project>(
            MechanicalObjectFactoryRegistry.Project, domain,
            (doc, rev) => new Project(doc, rev, domain, identifier, name, EngineeringObjectMetadata.Empty));
        var project = await factory.CreateAsync("Commercial test project.");
        return project.Id;
    }
}
