using Tempest.Core.DependencyInjection;
using Tempest.Core.Invoicing.Xero.Sync.Quotes;

namespace Tempest.Core.Invoicing.Xero;

// `v0.24.0` X3 (design §11): the quote planner and push handlers. B1's
// `XeroServiceRegistration.cs` declares the `RegisterQuotes` hook; this
// file's part implements it.
internal static partial class XeroServiceRegistration
{
    /// <summary>X3's <c>RegisterQuotes</c> hook (declared by B1's <c>XeroServiceRegistration.cs</c>): <see cref="AddXeroQuotes"/>.</summary>
    static partial void RegisterQuotes(IServiceCollection services, XeroServiceContext context) => AddXeroQuotes(services);

    /// <summary>
    /// X3's registrations, each service type once (`ADR-0122`):
    /// <see cref="IXeroQuoteSource"/> (<see cref="DomainXeroQuoteSource"/>),
    /// and the concrete <see cref="XeroQuotePlanner"/>,
    /// <see cref="XeroQuotePushHandler"/> and <see cref="XeroQuoteAttachmentHandler"/>.
    /// The planner and handlers are registered as their own types, not as
    /// <c>IXeroSyncPlanner</c>/<c>IXeroPushHandler</c>: the container takes
    /// one registration per service type, and X4/X5 have planners and
    /// handlers too — the engine (X6) collects them by type. Their
    /// <c>IXeroDocumentFileSource</c> is X6's; until it is registered the
    /// optional parameter is left empty (nothing uploads).
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    internal static void AddXeroQuotes(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Singleton<IXeroQuoteSource, DomainXeroQuoteSource>();
        services.Singleton<XeroQuotePlanner>();
        services.Singleton<XeroQuotePushHandler>();
        services.Singleton<XeroQuoteAttachmentHandler>();
    }
}
