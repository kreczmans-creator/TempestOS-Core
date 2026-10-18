using Tempest.Core.DependencyInjection;
using Tempest.Core.Invoicing.Xero.Sync.Invoices;

namespace Tempest.Core.Invoicing.Xero;

// `v0.24.0` X4 (design §11): the invoice drafts seam, its push handlers and
// its planner. B1's `XeroServiceRegistration.cs` declares the
// `RegisterInvoices` hook; this file's part implements it.
internal static partial class XeroServiceRegistration
{
    /// <summary>X4's <c>RegisterInvoices</c> hook (declared by B1's <c>XeroServiceRegistration.cs</c>): <see cref="AddXeroInvoices"/>.</summary>
    static partial void RegisterInvoices(IServiceCollection services, XeroServiceContext context) => AddXeroInvoices(services, context.Connector);

    /// <summary>
    /// X4's registration: the composed <see cref="XeroConnector"/> under its
    /// own type (<c>TempestHost</c> registers it as <see cref="IInvoicingConnector"/>
    /// and <see cref="IAccountsConnector"/> only); one
    /// <see cref="XeroInvoiceDrafts"/>, also answering
    /// <see cref="IInvoiceDraftSync"/> (so <see cref="InvoicingService"/>
    /// creates numbered drafts by <c>ContactID</c>) through a forwarding
    /// singleton over the same instance; the invoice push handlers and the
    /// planner for the X6 engine. Each service type is registered once
    /// (`ADR-0122`).
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="connector">The connector <see cref="Compose"/> built.</param>
    internal static void AddXeroInvoices(IServiceCollection services, XeroConnector connector)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connector);

        services.AddInstance(connector);
        services.Singleton<XeroInvoiceDrafts>();
        services.Singleton<IInvoiceDraftSync, XeroInvoiceDraftsForwarder>();
        services.Singleton<XeroInvoicePushHandler>();
        services.Singleton<XeroInvoiceAttachmentPushHandler>();
        services.Singleton<XeroInvoicePlanner>();
    }
}
