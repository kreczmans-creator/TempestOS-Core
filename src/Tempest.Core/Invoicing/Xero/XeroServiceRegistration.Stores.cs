using Tempest.Core.DependencyInjection;
using Tempest.Core.Invoicing.Xero.Sync;

namespace Tempest.Core.Invoicing.Xero;

// `v0.24.0` B2 (design §11): the link store, the outbox and the invoice-link
// import. B1's `XeroServiceRegistration.cs` declares the `RegisterStores`
// hook; this file's part implements it.
internal static partial class XeroServiceRegistration
{
    /// <summary>B2's <c>RegisterStores</c> hook (declared by B1's <c>XeroServiceRegistration.cs</c>): <see cref="AddXeroStores"/>.</summary>
    static partial void RegisterStores(IServiceCollection services, XeroServiceContext context) => AddXeroStores(services);

    /// <summary>
    /// B2's registrations: <see cref="IXeroLinkStore"/>
    /// (<see cref="PersistenceXeroLinkStore"/>), <see cref="IXeroOutbox"/> and
    /// the drain's <see cref="IXeroOutboxDrain"/> (both
    /// <see cref="PersistenceXeroOutbox"/>), and
    /// <see cref="XeroInvoiceLinkImporter"/>. Each service type is registered
    /// once (`ADR-0122`). The two outbox registrations construct two
    /// instances over the one <c>IPersistenceStore</c>; they share one write
    /// gate per backing store, so they behave as one queue.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    internal static void AddXeroStores(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Singleton<IXeroLinkStore, PersistenceXeroLinkStore>();
        services.Singleton<IXeroOutbox, PersistenceXeroOutbox>();
        services.Singleton<IXeroOutboxDrain, PersistenceXeroOutbox>();
        services.Singleton<XeroInvoiceLinkImporter>();
    }
}
