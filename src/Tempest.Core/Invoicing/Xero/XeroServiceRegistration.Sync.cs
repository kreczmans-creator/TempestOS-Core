using Tempest.Core.DependencyInjection;
using Tempest.Core.Invoicing.Xero.Sync;

namespace Tempest.Core.Invoicing.Xero;

// `v0.24.0` X6 (design §6, §11): the sync engine, its change observer,
// read-back and file source. B1's `XeroServiceRegistration.cs` declares the
// `RegisterSync` hook; this file's part implements it.
internal static partial class XeroServiceRegistration
{
    /// <summary>X6's <c>RegisterSync</c> hook (declared by B1's <c>XeroServiceRegistration.cs</c>): <see cref="AddXeroSync"/>.</summary>
    static partial void RegisterSync(IServiceCollection services, XeroServiceContext context) => AddXeroSync(services);

    /// <summary>
    /// X6's registrations, each service type once (`ADR-0122`):
    /// <see cref="IXeroDocumentFileSource"/> (<see cref="AttachmentXeroDocumentFileSource"/> —
    /// from now on X3–X5's planners and handlers are built with it, so issued
    /// PDFs and receipts are uploaded); <see cref="IXeroConnectionState"/>
    /// (<see cref="XeroConnectorConnectionState"/> over X4's
    /// <see cref="XeroConnector"/>); <see cref="XeroChangeObserver"/>;
    /// <see cref="XeroReadBack"/>; <see cref="XeroSyncParts"/> (every
    /// planner and handler, by its concrete type); and one
    /// <see cref="XeroSyncService"/>, also answering <see cref="IXeroSyncService"/>
    /// through a forwarding singleton over the same instance.
    /// </summary>
    /// <remarks>
    /// The background loop, <see cref="XeroSyncHostedService"/>, is
    /// deliberately <b>not</b> registered here: like every other
    /// <c>IHostedService</c> it is found and registered by the platform's
    /// reflection-based hosted-service discovery (registering it again would
    /// be a duplicate registration), and it resolves the
    /// <see cref="XeroSyncService"/> registered here.
    /// </remarks>
    /// <param name="services">The host's service collection.</param>
    internal static void AddXeroSync(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Singleton<IXeroDocumentFileSource, AttachmentXeroDocumentFileSource>();
        services.Singleton<IXeroConnectionState, XeroConnectorConnectionState>();
        services.Singleton<XeroChangeObserver>();
        services.Singleton<XeroReadBack>();
        services.Singleton<XeroSyncParts>();
        services.Singleton<XeroSyncService>();
        services.Singleton<IXeroSyncService, XeroSyncServiceForwarder>();
    }
}
