namespace Tempest.Core.Plugins;

/// <summary>
/// The write side of the small, Host-owned registry recording every
/// discovered <see cref="Modules.IModule"/> or <see cref="BackgroundServices.IHostedService"/>
/// <see cref="Type"/> that belonged to a plugin
/// <see cref="PluginAssemblyLoader.LoadPlugins"/> denied trust.
/// </summary>
/// <remarks>
/// <para>
/// <b>WP 13.9.4 trust-denial execution boundary remediation.</b> Before this
/// registry existed, <see cref="PluginTrustDeniedException"/> isolated a
/// denied plugin only from <see cref="PluginAssemblyLoader.LoadPlugins"/>'s
/// own returned list and <see cref="PluginRegistryState.Loaded"/> — nothing
/// stopped the plugin's already-loaded assembly (ADR-0015: that step cannot
/// be undone) from being separately, redundantly rediscovered by Module
/// Discovery and Hosted Service Discovery (both deliberately plugin-unaware,
/// ADR-0110) and fully lifecycle-run, indistinguishable from first-party
/// code, since a denied module's ambient component principal is always
/// <see langword="null"/> and <c>null</c> is treated as First-Party
/// (<see cref="PluginTrustPermission.IsFirstParty"/>).
/// </para>
/// <para>
/// <b>Corrected during the same WP 13.9.4 remediation, before this registry
/// was ever committed.</b> Originally named (and scoped to)
/// <c>IPluginDeniedModuleTypeRecorder</c>, keyed only on
/// <see cref="Modules.IModule"/> implementers — closing Module Registration
/// alone left a second, sibling, entirely independent discovery pipeline
/// wide open: a denied plugin's assembly could still contribute an
/// <see cref="BackgroundServices.IHostedService"/> implementer, discovered
/// by <see cref="BackgroundServices.HostedServiceDiscoveryService"/> and
/// started by <see cref="BackgroundServices.IHostedServiceManager"/> with
/// zero trust awareness of any kind — including a single type implementing
/// <i>both</i> interfaces, correctly excluded from Module Registration yet
/// still fully started via the hosted-service path. Renamed and broadened
/// so one registry, keyed on <see cref="Type"/> alone, covers every
/// platform-facing execution entry point a denied plugin's own transitive
/// scan (<c>DiscoverModuleTypes</c>) can discover, not only
/// <see cref="Modules.IModule"/>.
/// </para>
/// <para>
/// Used only by <see cref="PluginAssemblyLoader"/>, which records every
/// <see cref="Modules.IModule"/> and <see cref="BackgroundServices.IHostedService"/>
/// type its own fixed-point transitive scan (<c>DiscoverModuleTypes</c>)
/// found for a plugin denied by <i>either</i> static trust check —
/// capability eligibility or constructor conformance — regardless of which
/// specific type triggered the denial: the whole plugin is isolated, so
/// every type reachable from it is recorded, exactly mirroring
/// <see cref="IPluginComponentPrincipalRecorder"/>'s own all-discovered-types
/// recording shape for the passing case. Kept separate from
/// <see cref="IPluginDeniedTypeRegistry"/> (the read side) so that nothing
/// outside <c>Tempest.Core.Plugins</c> is ever handed a reference capable
/// of mutating this registry — mirroring
/// <see cref="IPluginComponentPrincipalRecorder"/>'s own identical rationale.
/// </para>
/// </remarks>
public interface IPluginDeniedTypeRecorder
{
    /// <summary>
    /// Records <paramref name="type"/> as belonging to a plugin denied
    /// trust — it must never reach Module Registration or Hosted Service
    /// Registration.
    /// </summary>
    /// <param name="type">The discovered type to record as denied.</param>
    void Record(Type type);
}
