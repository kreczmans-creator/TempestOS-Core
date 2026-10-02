using Tempest.Core.Audit;
using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Invoicing.OAuth;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Secrets;
using Tempest.Core.Settings;
using MicrosoftLogging = Microsoft.Extensions.Logging;

namespace Tempest.Core.Invoicing.Xero;

/// <summary>
/// What <c>TempestHost</c> built for Xero before the container exists, and
/// every <c>Register{Area}</c> hook of <see cref="XeroServiceRegistration"/>
/// receives.
/// </summary>
/// <param name="Api">The typed client (`v0.24.0` B1) over <see cref="ApiClient"/>.</param>
/// <param name="Connector">The `WP 19.1A` connector, over the same <see cref="ApiClient"/> — already registered as <see cref="IInvoicingConnector"/> and <see cref="IAccountsConnector"/> by <c>TempestHost</c>.</param>
/// <param name="Authoriser">The Xero OAuth authoriser (its own token <see cref="HttpClient"/>, outside the API pipeline).</param>
/// <param name="ApiClient">The one Xero API <see cref="HttpClient"/>: <see cref="SafetyHandler"/> → <see cref="RateLimiter"/> → logging → network.</param>
/// <param name="SafetyHandler">The D3/D4/D7 handler at the top of <see cref="ApiClient"/>'s pipeline.</param>
/// <param name="RateLimiter">The client-side rate limiter below it.</param>
/// <param name="Configuration">The host's configuration.</param>
/// <param name="SecretStore">The host's secret store (tokens and the granted-scope record).</param>
/// <param name="Time">The clock Xero services use.</param>
internal sealed record XeroServiceContext(
    XeroAccountingApi Api,
    XeroConnector Connector,
    OAuthAuthoriser Authoriser,
    HttpClient ApiClient,
    XeroWriteSafetyHandler SafetyHandler,
    XeroRateLimiter RateLimiter,
    IConfigurationProvider Configuration,
    ISecretStore SecretStore,
    TimeProvider Time);

/// <summary>
/// Composes the Xero clients and registers every `v0.24.0` Xero service
/// (`ADR-0162`; <c>docs/releases/v0.24.0/Xero Technical Design.md</c> §11).
/// Called by <c>TempestHost</c> only when <c>Invoicing:Connector</c> is
/// <c>Xero</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Conflict-free registration.</b> Each build task implements exactly one
/// <c>Register{Area}</c> hook below, in its own
/// <c>XeroServiceRegistration.{Area}.cs</c>; a hook not yet implemented is
/// a partial method with no body, which the compiler removes. No task
/// registers a service another task registers (`ADR-0122`: a second
/// registration is an error, not a replacement).
/// </para>
/// </remarks>
internal static partial class XeroServiceRegistration
{
    /// <summary>The Xero API root every Xero call is relative to.</summary>
    internal static readonly Uri ApiBaseAddress = new("https://api.xero.com/api.xro/2.0/");

    /// <summary>
    /// The Xero OAuth profile: Xero's identity endpoints, the exact scopes of
    /// <see cref="XeroScopes.Required"/> (X0), and the <c>/connections</c>
    /// endpoint the tenant id is read from.
    /// </summary>
    internal static OAuthProviderProfile OAuthProfile { get; } = new(
        Provider: "Xero",
        AuthorizationEndpoint: new Uri("https://login.xero.com/identity/connect/authorize"),
        TokenEndpoint: new Uri("https://identity.xero.com/connect/token"),
        Scopes: XeroScopes.Required,
        TenantResolutionEndpoint: new Uri("https://api.xero.com/connections"));

    /// <summary>
    /// Builds the Xero clients: the OAuth authoriser over its own token
    /// client, and one API <see cref="HttpClient"/> whose pipeline is
    /// <see cref="XeroWriteSafetyHandler"/> → <see cref="XeroRateLimiter"/> →
    /// <see cref="InvoicingHttpLoggingHandler"/> → network, shared by
    /// <see cref="XeroAccountingApi"/> and <see cref="XeroConnector"/> — so no
    /// Xero caller can reach Xero except through the safety handler.
    /// </summary>
    /// <param name="configuration">The host's configuration.</param>
    /// <param name="secretStore">The host's secret store.</param>
    /// <param name="loggerFactory">The host's logger factory.</param>
    /// <param name="services">The host's container once built (<see langword="null"/> before); the safety handler resolves the X1 settings reader, Settings and the audit recorder through it, per write.</param>
    /// <param name="timeProvider">The clock; <see langword="null"/> for the system clock.</param>
    internal static XeroServiceContext Compose(
        IConfigurationProvider configuration,
        ISecretStore secretStore,
        MicrosoftLogging.ILoggerFactory loggerFactory,
        Func<ITempestServiceProvider?> services,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(services);

        var time = timeProvider ?? TimeProvider.System;

        // The token endpoint (`identity.xero.com`) and `/connections` are not
        // the Accounting API: they go through their own client, so the
        // safety handler's write allow-list never meets a token exchange.
        var tokenClient = new HttpClient(new InvoicingHttpLoggingHandler(loggerFactory.CreateLogger("Tempest.Core.Invoicing.Xero.OAuth")));
        var authoriser = new OAuthAuthoriser(OAuthProfile, configuration, secretStore, new SystemBrowserLauncher(), tokenClient, time);

        var rateLimiter = new XeroRateLimiter(time)
        {
            InnerHandler = new InvoicingHttpLoggingHandler(loggerFactory.CreateLogger("Tempest.Core.Invoicing.Xero")),
        };

        var safetyHandler = new XeroWriteSafetyHandler(
            settingsReader: () => TryResolve<IXeroSettingsReader>(services()),
            allowLiveOrganisation: cancellationToken => ReadAllowLiveOrganisationAsync(TryResolve<ISettingsProvider>(services()), cancellationToken),
            auditRecorder: () => TryResolve<IAuditRecorder>(services()),
            logger: loggerFactory.CreateLogger("Tempest.Core.Invoicing.Xero.Safety"),
            timeProvider: time)
        {
            InnerHandler = rateLimiter,
        };

        var apiClient = new HttpClient(safetyHandler) { BaseAddress = ApiBaseAddress };

        return new XeroServiceContext(
            new XeroAccountingApi(apiClient, authoriser, time),
            new XeroConnector(apiClient, authoriser, configuration),
            authoriser,
            apiClient,
            safetyHandler,
            rateLimiter,
            configuration,
            secretStore,
            time);
    }

    /// <summary>Registers every Xero service: B1's own, then each build task's hook.</summary>
    /// <param name="services">The host's service collection.</param>
    /// <param name="context">What <see cref="Compose"/> built.</param>
    internal static void Register(IServiceCollection services, XeroServiceContext context)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(context);

        RegisterFoundation(services, context);
        RegisterStores(services, context);
        RegisterSettings(services, context);
        RegisterContacts(services, context);
        RegisterQuotes(services, context);
        RegisterInvoices(services, context);
        RegisterPurchasing(services, context);
        RegisterSync(services, context);
    }

    /// <summary>B1: the typed client and the rate limiter, as the instances <see cref="Compose"/> built.</summary>
    private static void RegisterFoundation(IServiceCollection services, XeroServiceContext context)
    {
        services.AddInstance(context.Api);
        services.AddInstance(context.RateLimiter);
    }

    /// <summary>B2: link store, outbox, idempotency keys, invoice-link import (<c>XeroServiceRegistration.Stores.cs</c>).</summary>
    static partial void RegisterStores(IServiceCollection services, XeroServiceContext context);

    /// <summary>X1: settings reader and cache, tax-type and account-code maps (<c>XeroServiceRegistration.Settings.cs</c>).</summary>
    static partial void RegisterSettings(IServiceCollection services, XeroServiceContext context);

    /// <summary>X2: contact linker and matcher (<c>XeroServiceRegistration.Contacts.cs</c>).</summary>
    static partial void RegisterContacts(IServiceCollection services, XeroServiceContext context);

    /// <summary>X3: quote planner and push handlers (<c>XeroServiceRegistration.Quotes.cs</c>).</summary>
    static partial void RegisterQuotes(IServiceCollection services, XeroServiceContext context);

    /// <summary>X4: invoice planner and push handlers (<c>XeroServiceRegistration.Invoices.cs</c>).</summary>
    static partial void RegisterInvoices(IServiceCollection services, XeroServiceContext context);

    /// <summary>X5: purchase-order and expense-bill planners and push handlers (<c>XeroServiceRegistration.Purchasing.cs</c>).</summary>
    static partial void RegisterPurchasing(IServiceCollection services, XeroServiceContext context);

    /// <summary>X6: the sync engine, change observer, read-back and hosted service (<c>XeroServiceRegistration.Sync.cs</c>).</summary>
    static partial void RegisterSync(IServiceCollection services, XeroServiceContext context);

    /// <summary>Resolves <typeparamref name="T"/> from <paramref name="provider"/>, or <see langword="null"/> when there is no container yet or nothing is registered for it.</summary>
    internal static T? TryResolve<T>(ITempestServiceProvider? provider)
        where T : class
    {
        if (provider is null)
            return null;

        try
        {
            return provider.GetService(typeof(T)) as T;
        }
        catch (ServiceNotRegisteredException)
        {
            return null;
        }
    }

    /// <summary>
    /// Registers every Xero Settings definition B1 owns —
    /// <see cref="XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey"/> —
    /// and X1's tax-type and account-code choices (with their Q10 defaults) —
    /// once the container is built, so Settings lists them (live organisation
    /// default off) before the first write or line resolution reads them. Idempotent
    /// (<see cref="XeroWriteSafetyHandler.EnsureAllowLiveOrganisationDefinition"/>):
    /// the Settings UI may call that too.
    /// </summary>
    /// <param name="provider">The host's built container.</param>
    internal static void RegisterSettingDefinitions(ITempestServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (TryResolve<ISettingsProvider>(provider) is { } settings)
        {
            XeroWriteSafetyHandler.EnsureAllowLiveOrganisationDefinition(settings);

            // X1's tax-type and account-code choices, so Settings lists them before anything resolves a code.
            XeroTaxTypeResolver.EnsureDefinitions(settings);
            XeroAccountCodeMap.EnsureDefinitions(settings);
        }
    }

    /// <summary>
    /// Reads <see cref="XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey"/>
    /// (default <c>false</c>). Its definition is registered at start-up
    /// (<see cref="RegisterSettingDefinitions"/>); it is ensured again here
    /// for a provider that start-up never saw. Anything but a parsable
    /// <c>true</c> is off.
    /// </summary>
    /// <param name="settings">The settings provider; <see langword="null"/> reads as off.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    internal static async Task<bool> ReadAllowLiveOrganisationAsync(ISettingsProvider? settings, CancellationToken cancellationToken)
    {
        if (settings is null)
            return false;

        XeroWriteSafetyHandler.EnsureAllowLiveOrganisationDefinition(settings);

        var value = await settings.GetValueAsync(XeroWriteSafetyHandler.AllowLiveOrganisationSettingKey, cancellationToken).ConfigureAwait(false);
        return bool.TryParse(value, out var allowed) && allowed;
    }
}
