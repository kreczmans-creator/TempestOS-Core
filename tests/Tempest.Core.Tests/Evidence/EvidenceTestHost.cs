using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Materials;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Evidence;

/// <summary>
/// Builds a real host/manager pair with the Engineering Disciplines
/// registered — the identical convention <c>RestartProofTests</c> and
/// <c>Workspace.CommandDescriptorBindingTests</c> already establish for
/// this assembly — for the Evidence discipline's own tests (`WP 18.0A`).
/// </summary>
internal static class EvidenceTestHost
{
    public const string PrincipalId = "evidence-test-principal-01";
    public const string SecondPrincipalId = "evidence-test-principal-02";

    public static async Task<(ITempestHost Host, WorkspaceManager Manager)> StartAsync(string persistenceRoot)
    {
        var host = new TempestHostBuilder([typeof(MechanicalWorkspaceExplorerModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRoot),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        await manager.StartAsync();

        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);

        return (host, manager);
    }

    /// <summary>The identical convention, with <c>Evidence:IndependentCheck</c> set from configuration.</summary>
    public static async Task<(ITempestHost Host, WorkspaceManager Manager)> StartAsync(string persistenceRoot, bool independentCheck)
    {
        var host = new TempestHostBuilder([typeof(MechanicalWorkspaceExplorerModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, persistenceRoot),
                new KeyValuePair<string, string>(EvidenceService.IndependentCheckConfigurationKey, independentCheck ? "true" : "false"),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        await manager.StartAsync();

        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(manager, host);

        return (host, manager);
    }

    public static IEvidenceService Service(ITempestHost host) =>
        (IEvidenceService)host.Services!.GetService(typeof(IEvidenceService));

    public static IMaterialCatalog Materials(ITempestHost host) =>
        (IMaterialCatalog)host.Services!.GetService(typeof(IMaterialCatalog));

    public static EngineeringDomainContext Domain(ITempestHost host) =>
        (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

    public static ICurrentPrincipalAccessor Principals(ITempestHost host) =>
        (ICurrentPrincipalAccessor)host.Services!.GetService(typeof(ICurrentPrincipalAccessor));

    /// <summary>Signs in <paramref name="id"/> with a local session's own broad permission set.</summary>
    public static void SignIn(ITempestHost host, string id = PrincipalId)
    {
        var accessor = (CurrentPrincipalAccessor)Principals(host);
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity(id, id), ApplicationPermissions.LocalSession));
    }

    public static async Task<Guid> CreateProjectAsync(ITempestHost host)
    {
        var domain = Domain(host);
        var factory = new EngineeringObjectFactory<Project>(
            MechanicalObjectFactoryRegistry.Project, domain,
            (doc, rev) => new Project(doc, rev, domain, "EVD-PRJ", "Evidence Test Project", EngineeringObjectMetadata.Empty));
        var project = await factory.CreateAsync("Evidence test project.");
        return project.Id;
    }
}
