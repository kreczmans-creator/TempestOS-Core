using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Secrets;

namespace Tempest.Core.Tests.Invoicing.Xero.Contacts;

/// <summary>`v0.24.0` X2's <c>RegisterContacts</c> hook: the linker resolves from the container, as the interface (U2) and as the concrete type (X3–X5), each registered once (`ADR-0122`).</summary>
public sealed class XeroContactsRegistrationTests
{
    [Fact]
    public async Task TheLinker_ResolvesAsTheInterfaceAndTheConcreteType_OverOneInstance()
    {
        using var kit = await ContactLinkerTestKit.CreateAsync(new XeroContactLinkerOptions { WriteContactNumberWhenEmpty = false });
        await kit.AddOrganisationAsync();
        var contactId = kit.Simulator.SeedContact("Acme Engineering Ltd");

        var services = new ServiceCollection();
        services.AddInstance(kit.Api);
        services.AddInstance<IXeroLinkStore>(kit.Links);
        services.AddInstance<IOrganisationCatalog>(kit.Organisations);
        services.AddInstance<ISecretStore>(kit.SecretStore);
        XeroServiceRegistration.AddXeroContacts(services);
        var provider = new TempestServiceProvider(services);

        var linker = (IXeroContactLinker)provider.GetService(typeof(IXeroContactLinker));
        var concrete = (XeroContactLinker)provider.GetService(typeof(XeroContactLinker));

        // Q7 defaults on in the container: linking fills the empty ContactNumber.
        var linked = await linker.LinkExistingAsync("ACME1", contactId);
        Assert.Equal(ConnectorOutcome.Ok, linked.Outcome);
        Assert.Equal("ACME1", linked.Value!.XeroNumber);
        Assert.Equal(contactId, (await concrete.ResolveForPushAsync(ContactLinkerTestKit.TenantId, "ACME1")).ContactId);
        Assert.Same(concrete, provider.GetService(typeof(XeroContactLinker)));
        kit.AssertNoViolations();

        Assert.ThrowsAny<ServiceRegistrationException>(() => XeroServiceRegistration.AddXeroContacts(services));
    }
}
