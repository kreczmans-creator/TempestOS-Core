using Tempest.Core.Configuration;

namespace Tempest.Core.Runtime;

/// <summary>
/// The concrete <see cref="ITempestHostBuilder"/> implementation.
/// </summary>
/// <remarks>
/// The composition root's own entry point, and the only component permitted
/// to construct a <see cref="TempestHost"/> — <see cref="TempestHost"/>'s
/// constructor is <see langword="internal"/>. A builder produces at most one
/// host; calling any member after <see cref="Build"/> has already been called
/// throws <see cref="InvalidOperationException"/>.
/// </remarks>
public sealed class TempestHostBuilder : ITempestHostBuilder
{
    private readonly List<IConfigurationSource> _configurationSources = [];
    private readonly IEnumerable<Type>? _discoveryCandidateTypesOverride;
    private readonly string? _pluginsRootPathOverride;
    private readonly IEnumerable<Type>? _hostedServiceCandidateTypesOverride;
    private bool _built;

    /// <summary>
    /// Initialises a new instance of the <see cref="TempestHostBuilder"/> class.
    /// The resulting host discovers modules from every assembly currently
    /// loaded into the application domain, discovers plugins from the
    /// conventional plugins directory, and discovers hosted services from
    /// every assembly currently loaded into the application domain.
    /// </summary>
    public TempestHostBuilder()
        : this(discoveryCandidateTypesOverride: null, pluginsRootPathOverride: null, hostedServiceCandidateTypesOverride: null)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="TempestHostBuilder"/> class
    /// whose host's discovery phase evaluates a specific, fixed set of
    /// candidate types rather than scanning the application domain's loaded
    /// assemblies.
    /// </summary>
    /// <param name="discoveryCandidateTypesOverride">
    /// The candidate types the resulting host's discovery phase evaluates, or
    /// <see langword="null"/> to scan every assembly currently loaded into
    /// the application domain.
    /// </param>
    /// <remarks>
    /// Internal test seam — mirrors <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s
    /// own internal <c>DiscoverModules(IEnumerable&lt;Type&gt;)</c> seam, so a
    /// host's discovery phase can be exercised deterministically against a
    /// controlled set of types in tests, isolated from every other
    /// <c>IModule</c> fixture defined elsewhere in the test assembly, without
    /// changing the public API surface.
    /// Hosted service discovery is likewise scoped to an empty candidate
    /// list rather than left to scan the application domain — a caller using
    /// this constructor is explicitly asking for an isolated, deterministic
    /// host and should not be surprised by an unrelated <c>IHostedService</c>
    /// fixture defined elsewhere in the test assembly.
    /// </remarks>
    internal TempestHostBuilder(IEnumerable<Type>? discoveryCandidateTypesOverride)
        : this(discoveryCandidateTypesOverride, pluginsRootPathOverride: null, hostedServiceCandidateTypesOverride: Type.EmptyTypes)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="TempestHostBuilder"/> class
    /// whose host's discovery phase evaluates a specific, fixed set of
    /// candidate types, and whose plugin discovery phase scans a specific
    /// plugins root directory.
    /// </summary>
    /// <param name="discoveryCandidateTypesOverride">
    /// The candidate types the resulting host's discovery phase evaluates, or
    /// <see langword="null"/> to scan every assembly currently loaded into
    /// the application domain.
    /// </param>
    /// <param name="pluginsRootPathOverride">
    /// The plugins root directory the resulting host's Plugin Discovery phase
    /// scans, or <see langword="null"/> to use the conventional
    /// <c>Plugins</c> directory relative to the application's base directory.
    /// </param>
    /// <remarks>
    /// Internal test seam — mirrors
    /// <see cref="Plugins.PluginManifestDiscoveryService"/>'s own internal,
    /// plugins-root-accepting constructor, so a host's Plugin Discovery phase
    /// can be exercised deterministically against a controlled temporary
    /// directory in tests, without changing the public API surface.
    /// Hosted service discovery is likewise scoped to an empty candidate
    /// list rather than left to scan the application domain, for the same
    /// isolation reason documented on the single-argument constructor above.
    /// </remarks>
    internal TempestHostBuilder(IEnumerable<Type>? discoveryCandidateTypesOverride, string? pluginsRootPathOverride)
        : this(discoveryCandidateTypesOverride, pluginsRootPathOverride, hostedServiceCandidateTypesOverride: Type.EmptyTypes)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="TempestHostBuilder"/> class
    /// whose host's discovery phase evaluates a specific, fixed set of
    /// candidate types, whose plugin discovery phase scans a specific
    /// plugins root directory, and whose hosted service discovery phase
    /// evaluates a specific, fixed set of candidate types.
    /// </summary>
    /// <param name="discoveryCandidateTypesOverride">
    /// The candidate types the resulting host's module discovery phase
    /// evaluates, or <see langword="null"/> to scan every assembly currently
    /// loaded into the application domain.
    /// </param>
    /// <param name="pluginsRootPathOverride">
    /// The plugins root directory the resulting host's Plugin Discovery phase
    /// scans, or <see langword="null"/> to use the conventional
    /// <c>Plugins</c> directory relative to the application's base directory.
    /// </param>
    /// <param name="hostedServiceCandidateTypesOverride">
    /// The candidate types the resulting host's hosted service discovery
    /// phase evaluates, or <see langword="null"/> to scan every assembly
    /// currently loaded into the application domain.
    /// </param>
    /// <remarks>
    /// Internal test seam — mirrors
    /// <see cref="BackgroundServices.HostedServiceDiscoveryService"/>'s own
    /// internal, explicit-candidate-list overload, so a host's hosted
    /// service discovery phase can be exercised deterministically in tests,
    /// isolated from every other <c>IHostedService</c> fixture defined
    /// elsewhere in the test assembly, without changing the public API
    /// surface.
    /// </remarks>
    internal TempestHostBuilder(
        IEnumerable<Type>? discoveryCandidateTypesOverride,
        string? pluginsRootPathOverride,
        IEnumerable<Type>? hostedServiceCandidateTypesOverride)
    {
        _discoveryCandidateTypesOverride = discoveryCandidateTypesOverride;
        _pluginsRootPathOverride = pluginsRootPathOverride;
        _hostedServiceCandidateTypesOverride = hostedServiceCandidateTypesOverride;
    }

    /// <inheritdoc />
    public ITempestHostBuilder AddConfigurationSource(IConfigurationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ThrowIfAlreadyBuilt();

        _configurationSources.Add(source);

        return this;
    }

    /// <inheritdoc />
    public ITempestHost Build()
    {
        ThrowIfAlreadyBuilt();
        _built = true;

        return new TempestHost(
            _configurationSources,
            _discoveryCandidateTypesOverride,
            _pluginsRootPathOverride,
            _hostedServiceCandidateTypesOverride);
    }

    private void ThrowIfAlreadyBuilt()
    {
        if (_built)
            throw new InvalidOperationException("This builder has already built a host and cannot be reused.");
    }
}
