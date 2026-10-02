using Tempest.Core.DependencyInjection;
using Tempest.Core.Invoicing.Xero.Settings;

namespace Tempest.Core.Invoicing.Xero;

// `v0.24.0` X1 (design §8, §11): the settings reader and its cache, the
// tax-type resolver and the account-code map. B1's
// `XeroServiceRegistration.cs` declares the `RegisterSettings` hook; this
// file's part implements it.
internal static partial class XeroServiceRegistration
{
    /// <summary>X1's <c>RegisterSettings</c> hook (declared by B1's <c>XeroServiceRegistration.cs</c>): <see cref="AddXeroSettings"/>.</summary>
    static partial void RegisterSettings(IServiceCollection services, XeroServiceContext context) => AddXeroSettings(services);

    /// <summary>
    /// X1's registrations, each service type once (`ADR-0122`):
    /// <see cref="IXeroSettingsCache"/> (<see cref="FileXeroSettingsCache"/>),
    /// <see cref="IXeroSettingsReader"/> (<see cref="XeroSettingsReader"/> —
    /// also what <see cref="Api.XeroWriteSafetyHandler"/> resolves for D7),
    /// <see cref="XeroTaxTypeResolver"/> and <see cref="XeroAccountCodeMap"/>.
    /// Their constructor dependencies — the typed client B1 registers, the
    /// host's configuration, secret store, audit recorder and Settings — are
    /// resolved by the container.
    /// </summary>
    /// <param name="services">The host's service collection.</param>
    internal static void AddXeroSettings(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Singleton<IXeroSettingsCache, FileXeroSettingsCache>();
        services.Singleton<IXeroSettingsReader, XeroSettingsReader>();
        services.Singleton<XeroTaxTypeResolver>();
        services.Singleton<XeroAccountCodeMap>();
    }
}
