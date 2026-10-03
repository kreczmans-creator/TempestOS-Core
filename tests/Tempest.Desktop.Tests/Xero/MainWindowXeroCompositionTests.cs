using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Secrets;
using Tempest.Desktop.Composition;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` review-board fix m21: the real <see cref="MainWindow"/>
/// composed over a real <see cref="WorkspaceHost"/> whose invoicing connector
/// is Xero (no network: no token is held, so nothing is ever sent) — every
/// Xero part the composer looks up is found, never silently missing:
/// Settings → Xero, the contact link section, the badges on quotes,
/// invoices and purchase orders (with their prompts and link actions), and
/// <see cref="XeroSettingsSection.KeepDocumentIdentityCurrent"/> putting the
/// cached Xero company details on every document from start-up.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class MainWindowXeroCompositionTests
{
    private static readonly IReadOnlyList<string> XeroArgs = ["--Invoicing:Connector=Xero", "--Invoicing:Xero:ClientId=test-client-id"];

    [AvaloniaFact]
    public async Task TheMainWindow_ComposedWithXero_WiresEveryXeroPart_AndKeepsDocumentsOnXerosCompanyDetails()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath(), commandLineArgs: XeroArgs);
        MainWindow? window = null;
        try
        {
            await host.StartAsync();

            // A Demo Company reading cached earlier, for the connected organisation.
            var reading = XeroTestReadings.Demo();
            await ((ISecretStore)host.Services!.GetService(typeof(ISecretStore))).SetAsync(XeroContactLinker.TenantIdSecretKey, reading.TenantId);
            await ((IXeroSettingsCache)host.Services.GetService(typeof(IXeroSettingsCache))).SaveAsync(reading);

            window = new MainWindow(host);

            // Settings → Xero is composed (and kept in Settings).
            var settingsView = GetPrivateField<SettingsView>(window, "_settingsView");
            var xeroSettings = GetPrivateField<XeroSettingsSection?>(settingsView, "_xeroSettings");
            Assert.NotNull(xeroSettings);

            // KeepDocumentIdentityCurrent: documents print Xero's company details from start-up.
            var session = GetPrivateField<DesktopSessionState>(window, "_session");
            await UntilAsync(() => session.OrganisationIdentity.XeroCompanyDetails is not null);
            Assert.Equal("Demo Company (UK) Limited", session.OrganisationIdentity.ToIdentity().LegalName);

            // Business: every view's Xero part is there.
            var business = GetPrivateField<BusinessAreaView>(window, "_businessAreaView");
            var invoices = GetPrivateField<InvoicingView>(business, "_invoices");
            var quotes = GetPrivateField<QuotesView>(business, "_quotes");
            var orders = GetPrivateField<PurchaseOrdersView>(business, "_purchaseOrders");
            var contacts = GetPrivateField<CustomersSuppliersView>(business, "_customersSuppliers");

            var badges = Assert.IsType<XeroSyncServiceBadgeSource>(invoices.XeroBadges);
            Assert.Same(badges, quotes.XeroBadges);
            Assert.Same(badges, orders.XeroBadges);
            Assert.NotNull(badges.Prompts);       // Unlink / link by number / the m14 warning can ask
            Assert.True(badges.CanCheckNow);      // M3
            Assert.True(badges.CanLinkByNumber);  // M5/m15: the link actions were resolved
            Assert.NotNull(contacts.XeroContacts); // U2's contact link section
            Assert.NotNull(host.Services.GetService(typeof(XeroDocumentLinkActions)));

            // A badge over the real engine reads local state only: nothing was ever sent.
            var status = await badges.GetStatusAsync(XeroDocumentRef.For(XeroDocumentKind.Quote, Guid.NewGuid()));
            Assert.Equal(XeroSyncBadge.NotSent, status.Status.Badge);
        }
        finally
        {
            window?.Close();
            Dispatcher.UIThread.RunJobs();
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TheMainWindow_WithTheFakeConnector_ComposesNoXeroPart()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        MainWindow? window = null;
        try
        {
            await host.StartAsync();
            window = new MainWindow(host);

            Assert.Null(GetPrivateField<XeroSettingsSection?>(GetPrivateField<SettingsView>(window, "_settingsView"), "_xeroSettings"));
            var business = GetPrivateField<BusinessAreaView>(window, "_businessAreaView");
            Assert.Null(GetPrivateField<InvoicingView>(business, "_invoices").XeroBadges);
            Assert.Null(GetPrivateField<CustomersSuppliersView>(business, "_customersSuppliers").XeroContacts);
        }
        finally
        {
            window?.Close();
            Dispatcher.UIThread.RunJobs();
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = Deadline(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(5);
        }

        Assert.True(condition(), "The condition was not met in time.");
    }
}
