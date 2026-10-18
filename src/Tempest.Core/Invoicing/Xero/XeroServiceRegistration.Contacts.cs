using Tempest.Core.DependencyInjection;
using Tempest.Core.Invoicing.Xero.Contacts;

namespace Tempest.Core.Invoicing.Xero;

// `v0.24.0` X2 (design §11): the contact linker. B1's
// `XeroServiceRegistration.cs` declares the `RegisterContacts` hook; this
// file's part implements it.
internal static partial class XeroServiceRegistration
{
    /// <summary>X2's <c>RegisterContacts</c> hook (declared by B1's <c>XeroServiceRegistration.cs</c>): <see cref="AddXeroContacts"/>.</summary>
    static partial void RegisterContacts(IServiceCollection services, XeroServiceContext context) => AddXeroContacts(services);

    /// <summary>
    /// X2's registration: one <see cref="XeroContactLinker"/> serving both
    /// <see cref="IXeroContactLinker"/> (U2's link prompt) and the concrete
    /// type (X3–X5's <see cref="XeroContactLinker.ResolveForPushAsync"/>).
    /// The concrete type is registered as a singleton and the interface is
    /// answered by a forwarding singleton over it, so both resolve the same
    /// instance (its write gate keeps a create from racing a link). Each
    /// service type is registered once (`ADR-0122`).
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    internal static void AddXeroContacts(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Singleton<XeroContactLinker>();
        services.Singleton<IXeroContactLinker, XeroContactLinkerForwarder>();
    }
}
