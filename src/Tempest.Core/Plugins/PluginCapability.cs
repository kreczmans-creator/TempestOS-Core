namespace Tempest.Core.Plugins;

/// <summary>
/// The reserved <c>plugin.*</c> capability key constants and builders a
/// plugin's manifest <c>RequestedCapabilities</c> declares against.
/// </summary>
/// <remarks>
/// No new permission type is introduced — a plugin capability *is* a
/// <see cref="Identity.Permission"/>, constructed from one of these keys
/// (ADR-0111, "Capability keys reuse <c>Permission</c> directly"). This
/// type exists only to give the closed, v1 key set named, discoverable,
/// typo-proof constants/builders — <c>Permission</c>'s own shape (a plain,
/// unvalidated string) is unchanged.
/// <para>
/// Reserved capability keys (`Plugin Trust &amp; Isolation Architecture.md`,
/// "Capability Model" — v1, closed set; extending it later is purely
/// additive):
/// </para>
/// <para>
/// | Key | Grants |<br/>
/// |---|---|<br/>
/// | <c>plugin.navigation.register</c> | May call <c>NavigationService.Register</c>. |<br/>
/// | <c>plugin.commands.register</c> | May call the Command Framework's registration path (<c>CommandHandlerTable.Register</c>/<c>CommandRegistry.RegisterDescriptor</c>). |<br/>
/// | <c>plugin.di.register</c> | May contribute a DI service registration, if/when manifest v2 introduces that mechanism. |<br/>
/// | <c>plugin.events.publish:&lt;FullTypeName&gt;</c> | May call <c>IEventBus.PublishAsync&lt;TEvent&gt;</c>/<c>Publish</c> for the named event type. One key per event type — no wildcard in v1. |<br/>
/// | <c>plugin.services.resolve:&lt;FullTypeName&gt;</c> | Declares the plugin's module constructor is permitted to depend on the named service type, beyond the fixed always-allowed baseline (<c>ILogger</c>, <c>IConfigurationProvider</c>, <c>IDiagnosticsProvider</c>). |<br/>
/// | <c>plugin.identity.establish</c> | May call <c>IIdentityService.EstablishCurrentPrincipal</c>. |
/// </para>
/// </remarks>
public static class PluginCapability
{
    /// <summary>The capability key granting <c>NavigationService.Register</c>.</summary>
    public const string Navigation = "plugin.navigation.register";

    /// <summary>
    /// The capability key granting the Command Framework's registration path
    /// (<c>CommandHandlerTable.Register</c>/<c>CommandRegistry.RegisterDescriptor</c>).
    /// </summary>
    public const string Commands = "plugin.commands.register";

    /// <summary>
    /// The capability key granting a DI service registration contribution,
    /// if/when manifest v2 introduces that mechanism.
    /// </summary>
    public const string DiRegister = "plugin.di.register";

    /// <summary>The capability key granting <c>IIdentityService.EstablishCurrentPrincipal</c>.</summary>
    public const string IdentityEstablish = "plugin.identity.establish";

    /// <summary>
    /// Builds the capability key granting
    /// <c>IEventBus.PublishAsync</c>/<c>Publish</c> for a specific event
    /// type. One key per event type — no wildcard in v1.
    /// </summary>
    /// <param name="fullEventTypeName">The event type's full CLR type name.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="fullEventTypeName"/> is <see langword="null"/>, empty,
    /// or whitespace.
    /// </exception>
    public static string EventPublish(string fullEventTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullEventTypeName);

        return $"plugin.events.publish:{fullEventTypeName}";
    }

    /// <summary>
    /// Builds the capability key declaring a plugin module constructor is
    /// permitted to depend on a specific service type, beyond the fixed
    /// always-allowed baseline (<c>ILogger</c>, <c>IConfigurationProvider</c>,
    /// <c>IDiagnosticsProvider</c>).
    /// </summary>
    /// <param name="fullServiceTypeName">The service type's full CLR type name.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="fullServiceTypeName"/> is <see langword="null"/>,
    /// empty, or whitespace.
    /// </exception>
    public static string ServiceResolve(string fullServiceTypeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullServiceTypeName);

        return $"plugin.services.resolve:{fullServiceTypeName}";
    }
}
