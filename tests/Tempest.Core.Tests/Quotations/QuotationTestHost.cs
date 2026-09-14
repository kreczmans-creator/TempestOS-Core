using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Mechanical;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Configuration;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Invoicing;
using Tempest.Core.Persistence;
using Tempest.Core.Projects;
using Tempest.Core.Quotations;
using Tempest.Core.Requirements;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Quotations;

/// <summary>
/// Builds a real host/manager pair with the Engineering Disciplines
/// registered — the identical convention <c>Invoicing.InvoicingTestHost</c>
/// already establishes, for Quotation's own tests (`WP 19.5A`, `ADR-0152`).
/// </summary>
internal static class QuotationTestHost
{
    public const string PrincipalId = "quotation-test-principal-01";

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

    public static IOrganisationCatalog Organisations(ITempestHost host) =>
        (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));

    public static IRateCardCatalog RateCards(ITempestHost host) =>
        (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));

    public static IProjectCommercialService ProjectCommercial(ITempestHost host) =>
        (IProjectCommercialService)host.Services!.GetService(typeof(IProjectCommercialService));

    public static IDeliverableService Deliverables(ITempestHost host) =>
        (IDeliverableService)host.Services!.GetService(typeof(IDeliverableService));

    public static IRequirementsService Requirements(ITempestHost host) =>
        (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));

    public static IQuotationService Quotations(ITempestHost host) =>
        (IQuotationService)host.Services!.GetService(typeof(IQuotationService));

    public static IInvoicingService Invoicing(ITempestHost host) =>
        (IInvoicingService)host.Services!.GetService(typeof(IInvoicingService));

    /// <summary>The one live request whose lines carry <paramref name="completionId"/> — the request completing raised through the completion hook, exactly as <c>Invoicing.InvoicingTestHost.RequestRaisedByCompletionAsync</c> reads it.</summary>
    public static async Task<InvoiceRequest> RequestRaisedByCompletionAsync(ITempestHost host, Guid completionId)
    {
        var requests = await Domain(host).Repository.ListByKindAsync(InvoiceRequest.CanonicalKind);
        return requests.OfType<InvoiceRequest>().Single(r => r.Lines.Any(l => l.SourceId == completionId));
    }

    /// <summary>Signs in <paramref name="id"/> with a local session's own broad permission set.</summary>
    public static void SignIn(ITempestHost host, string id = PrincipalId)
    {
        var accessor = (CurrentPrincipalAccessor)Principals(host);
        accessor.SetCurrent(new PlatformPrincipal(new PlatformIdentity(id, id), ApplicationPermissions.LocalSession));
    }

    public static async Task<Guid> CreateProjectAsync(ITempestHost host, string identifier = "QUO-PRJ", string name = "Quotation Test Project")
    {
        var domain = Domain(host);
        var factory = new EngineeringObjectFactory<Project>(
            MechanicalObjectFactoryRegistry.Project, domain,
            (doc, rev) => new Project(doc, rev, domain, identifier, name, EngineeringObjectMetadata.Empty));
        var project = await factory.CreateAsync("Quotation test project.");
        return project.Id;
    }
}
