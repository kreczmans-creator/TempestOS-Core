using Tempest.Core.DependencyInjection;
using Tempest.Core.Invoicing.Xero.Sync.Purchasing;

namespace Tempest.Core.Invoicing.Xero;

// `v0.24.0` X5 (design §11): the purchase-order and expense-bill planners
// and push handlers. B1's `XeroServiceRegistration.cs` declares the
// `RegisterPurchasing` hook; this file's part implements it.
internal static partial class XeroServiceRegistration
{
    /// <summary>X5's <c>RegisterPurchasing</c> hook (declared by B1's <c>XeroServiceRegistration.cs</c>): <see cref="AddXeroPurchasing"/>.</summary>
    static partial void RegisterPurchasing(IServiceCollection services, XeroServiceContext context) => AddXeroPurchasing(services);

    /// <summary>
    /// X5's registrations, each service type once (`ADR-0122`):
    /// <see cref="IXeroPurchaseOrderSource"/> (<see cref="DomainXeroPurchaseOrderSource"/>),
    /// <see cref="IXeroExpenseSource"/> (<see cref="DomainXeroExpenseSource"/>),
    /// and the concrete <see cref="XeroPurchasingSyncState"/>, <see cref="XeroPurchasingCreateLog"/>,
    /// <see cref="XeroGeneralExpensesContact"/>, <see cref="XeroPurchaseOrderPlanner"/>,
    /// <see cref="XeroExpenseBillPlanner"/>, <see cref="XeroPurchaseOrderPushHandler"/>,
    /// <see cref="XeroExpenseBillPushHandler"/>, <see cref="XeroPurchaseOrderAttachmentHandler"/>
    /// and <see cref="XeroExpenseBillAttachmentHandler"/>. Planners and
    /// handlers are registered as their own types, not as
    /// <c>IXeroSyncPlanner</c>/<c>IXeroPushHandler</c>: the container takes one
    /// registration per service type, and X3/X4 have planners and handlers
    /// too — the engine (X6) collects them by type. Their optional
    /// <c>IXeroDocumentFileSource</c> is X6's; until it is registered nothing
    /// uploads.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    internal static void AddXeroPurchasing(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Singleton<IXeroPurchaseOrderSource, DomainXeroPurchaseOrderSource>();
        services.Singleton<IXeroExpenseSource, DomainXeroExpenseSource>();
        services.Singleton<XeroPurchasingSyncState>();
        services.Singleton<XeroPurchasingCreateLog>();
        services.Singleton<XeroGeneralExpensesContact>();
        services.Singleton<XeroPurchaseOrderPlanner>();
        services.Singleton<XeroExpenseBillPlanner>();
        services.Singleton<XeroPurchaseOrderPushHandler>();
        services.Singleton<XeroExpenseBillPushHandler>();
        services.Singleton<XeroPurchaseOrderAttachmentHandler>();
        services.Singleton<XeroExpenseBillAttachmentHandler>();
    }
}
