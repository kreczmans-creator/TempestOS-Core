using Tempest.Core.BackgroundServices;
using Tempest.Core.Modules;
using Tempest.Core.Runtime;

namespace Tempest.Core.Diagnostics;

/// <summary>
/// A read-only projection over the Runtime Host's own lifecycle state —
/// the Host's current <see cref="Runtime.HostState"/>, every module's
/// current <see cref="ModuleLifecycleStatus"/>, and every hosted
/// service's current <see cref="HostedServiceStatus"/> — for a consumer
/// that needs to observe platform health without gaining any authority
/// over it.
/// </summary>
/// <remarks>
/// <para>
/// A Platform Service (this Work Package's own ADR), DI-public like
/// <see cref="Events.IEventBus"/>, <see cref="Navigation.INavigationProvider"/>,
/// and <see cref="Commands.ICommandDispatcher"/> — resolved via ordinary
/// constructor injection, never a Host-owned collaborator. Unlike those
/// three, it carries no imperative registration surface of its own — a
/// module contributes nothing to it; it only reports what
/// <see cref="Modules.IModuleLifecycleManager"/> and
/// <see cref="BackgroundServices.IHostedServiceManager"/> — both
/// themselves Host-owned and never DI-public (ADR-0017) — already know.
/// </para>
/// <para>
/// Every member here is a live, point-in-time read: it reflects the
/// Runtime Host's own current state at the moment it is queried, not a
/// value captured once at construction. <see cref="HostedServices"/>
/// in particular can legitimately report empty before the Host reaches
/// its own "Hosted Services Started" phase — this is an honest reflection
/// of what has happened so far, not an error condition.
/// </para>
/// </remarks>
public interface IDiagnosticsProvider
{
    /// <summary>
    /// Gets the Runtime Host's own current lifecycle state.
    /// </summary>
    HostState HostState { get; }

    /// <summary>
    /// Gets the current lifecycle status of every module the Runtime Host
    /// is tracking, in the same deterministic order
    /// <see cref="IModuleLifecycleManager.Modules"/> itself reports.
    /// </summary>
    IReadOnlyCollection<ModuleLifecycleStatus> Modules { get; }

    /// <summary>
    /// Gets the current lifecycle status of every hosted service the
    /// Runtime Host is tracking, in the same deterministic order
    /// <see cref="IHostedServiceManager.Services"/> itself reports.
    /// Empty before the Host has constructed its own hosted service
    /// orchestrator (Host Lifecycle phase 8.1) — not an error.
    /// </summary>
    IReadOnlyCollection<HostedServiceStatus> HostedServices { get; }

    /// <summary>
    /// Gets the Plugin Registry's own entry for every plugin candidate this
    /// run attempted, in the same order <see cref="Plugins.IPluginRegistry.Entries"/>
    /// itself reports.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="Modules"/>/<see cref="HostedServices"/>, this
    /// property's collection is never actually empty because it has not
    /// yet been observed: <see cref="IDiagnosticsProvider"/> itself is not
    /// constructed until Platform Services Registered (Host Lifecycle
    /// phase 6), which — per ADR-0026's own frozen phase table — always
    /// occurs after Plugin Discovery (phase 3.1) and Plugin Loading
    /// (phase 3.2) have already fully completed. By the time any caller
    /// can reach this property at all, the Plugin Registry is therefore
    /// already fully populated; an empty collection here reflects a run
    /// with zero plugin candidates, not one that has not reached this
    /// phase yet.
    /// </remarks>
    IReadOnlyCollection<Plugins.PluginRegistryEntry> Plugins { get; }
}
