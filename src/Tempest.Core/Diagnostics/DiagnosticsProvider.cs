using Tempest.Core.BackgroundServices;
using Tempest.Core.Modules;
using Tempest.Core.Runtime;

namespace Tempest.Core.Diagnostics;

/// <summary>
/// The concrete <see cref="IDiagnosticsProvider"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Constructed and registered directly by <c>TempestHost</c>
/// (<see cref="DependencyInjection.IServiceCollection.AddInstance{TService}"/>,
/// the Composition Root pattern, ADR-0009) — never a container-constructed
/// singleton — because two of its collaborators
/// (<see cref="IModuleLifecycleManager"/>, <see cref="IHostedServiceManager"/>)
/// are themselves Host-owned and never added to the container (ADR-0017),
/// and neither exists yet at the point in <c>Host Lifecycle.md</c>'s phase
/// table where Platform Services are registered — both are constructed
/// only afterwards. Those two dependencies are therefore supplied as a
/// <see cref="Func{T}"/> accessor rather than a direct reference, so this
/// class can be constructed and registered early, and simply reports
/// "not yet available" (a live <see langword="null"/> or empty
/// collection) for whichever collaborator the Host has not constructed
/// yet, exactly as <c>ITempestHost.Services</c> itself is
/// <see langword="null"/> before Dependency Injection Built (ADR-0034) —
/// the same "not yet available is a normal, honestly-reported state, not
/// an error" discipline, applied a second time. Its Plugin Registry
/// collaborator (<see cref="Plugins.IPluginRegistry"/>) is, by contrast,
/// supplied as a direct reference: the Plugin Registry is constructed
/// during Plugin Discovery (phase 3.1), well before Platform Services
/// Registered (phase 6), so it is always already available by the time
/// this class is constructed — a deferred accessor would add indirection
/// with no corresponding "not yet constructed" state to guard against.
/// </para>
/// </remarks>
public sealed class DiagnosticsProvider : IDiagnosticsProvider
{
    private readonly Func<HostState> _hostStateAccessor;
    private readonly Func<IModuleLifecycleManager?> _lifecycleManagerAccessor;
    private readonly Func<IHostedServiceManager?> _hostedServiceManagerAccessor;
    private readonly Plugins.IPluginRegistry _pluginRegistry;

    /// <summary>
    /// Initialises a new instance of the <see cref="DiagnosticsProvider"/> class.
    /// </summary>
    /// <param name="hostStateAccessor">Returns the Runtime Host's own current lifecycle state.</param>
    /// <param name="lifecycleManagerAccessor">
    /// Returns the Runtime Host's own module lifecycle manager, or
    /// <see langword="null"/> if it has not been constructed yet.
    /// </param>
    /// <param name="hostedServiceManagerAccessor">
    /// Returns the Runtime Host's own hosted service manager, or
    /// <see langword="null"/> if it has not been constructed yet.
    /// </param>
    /// <param name="pluginRegistry">
    /// The Runtime Host's own Plugin Registry. Unlike
    /// <paramref name="lifecycleManagerAccessor"/>/<paramref name="hostedServiceManagerAccessor"/>,
    /// this is supplied as a direct reference, not a deferred
    /// <see cref="Func{T}"/> accessor: the Plugin Registry is constructed
    /// during Plugin Discovery (Host Lifecycle phase 3.1), well before
    /// <see cref="DiagnosticsProvider"/> itself is built (phase 6), so it
    /// is always already available at construction time.
    /// </param>
    /// <exception cref="ArgumentNullException">Any parameter is <see langword="null"/>.</exception>
    public DiagnosticsProvider(
        Func<HostState> hostStateAccessor,
        Func<IModuleLifecycleManager?> lifecycleManagerAccessor,
        Func<IHostedServiceManager?> hostedServiceManagerAccessor,
        Plugins.IPluginRegistry pluginRegistry)
    {
        ArgumentNullException.ThrowIfNull(hostStateAccessor);
        ArgumentNullException.ThrowIfNull(lifecycleManagerAccessor);
        ArgumentNullException.ThrowIfNull(hostedServiceManagerAccessor);
        ArgumentNullException.ThrowIfNull(pluginRegistry);

        _hostStateAccessor = hostStateAccessor;
        _lifecycleManagerAccessor = lifecycleManagerAccessor;
        _hostedServiceManagerAccessor = hostedServiceManagerAccessor;
        _pluginRegistry = pluginRegistry;
    }

    /// <inheritdoc />
    public HostState HostState => _hostStateAccessor();

    /// <inheritdoc />
    public IReadOnlyCollection<ModuleLifecycleStatus> Modules =>
        _lifecycleManagerAccessor()?.Modules ?? Array.Empty<ModuleLifecycleStatus>();

    /// <inheritdoc />
    public IReadOnlyCollection<HostedServiceStatus> HostedServices =>
        _hostedServiceManagerAccessor()?.Services ?? Array.Empty<HostedServiceStatus>();

    /// <inheritdoc />
    public IReadOnlyCollection<Plugins.PluginRegistryEntry> Plugins => _pluginRegistry.Entries;
}
