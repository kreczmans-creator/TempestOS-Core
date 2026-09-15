using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Mechanical;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Projects;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.PurchaseOrders;

/// <summary>
/// Builds a real host/manager pair with the Engineering Disciplines
/// registered — the identical convention <c>Quotations.QuotationTestHost</c>
/// already establishes, for PurchaseOrder's own tests (`WP 21.3B`).
/// </summary>
internal static class PurchaseOrderTestHost
{
    public const string PrincipalId = "po-test-principal-01";

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

    public static IRateCardCatalog RateCards(ITempestHost host) =>
        (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));

    public static IProjectCommercialService ProjectCommercial(ITempestHost host) =>
        (IProjectCommercialService)host.Services!.GetService(typeof(IProjectCommercialService));

    public static IPurchaseOrderService PurchaseOrders(ITempestHost host) =>
        (IPurchaseOrderService)host.Services!.GetService(typeof(IPurchaseOrderService));

    public static IExpenseService Expenses(ITempestHost host) =>
        (IExpenseService)host.Services!.GetService(typeof(IExpenseService));

    /// <summary>Signs in <paramref name="id"/> with a local session's own broad permission set.</summary>
    public static void SignIn(ITempestHost host, string id = PrincipalId)
    {
        var accessor = (CurrentPrincipalAccessor)Principals(host);
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity(id, id), ApplicationPermissions.LocalSession));
    }

    public static async Task<Guid> CreateProjectAsync(ITempestHost host, string identifier = "PO-PRJ", string name = "Purchase Order Test Project")
    {
        var domain = Domain(host);
        var factory = new EngineeringObjectFactory<Project>(
            MechanicalObjectFactoryRegistry.Project, domain,
            (doc, rev) => new Project(doc, rev, domain, identifier, name, EngineeringObjectMetadata.Empty));
        var project = await factory.CreateAsync("Purchase order test project.");
        return project.Id;
    }
}
