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
    private bool _built;

    /// <summary>
    /// Initialises a new instance of the <see cref="TempestHostBuilder"/> class.
    /// The resulting host discovers modules from every assembly currently
    /// loaded into the application domain.
    /// </summary>
    public TempestHostBuilder()
        : this(discoveryCandidateTypesOverride: null)
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
    /// </remarks>
    internal TempestHostBuilder(IEnumerable<Type>? discoveryCandidateTypesOverride)
    {
        _discoveryCandidateTypesOverride = discoveryCandidateTypesOverride;
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

        return new TempestHost(_configurationSources, _discoveryCandidateTypesOverride);
    }

    private void ThrowIfAlreadyBuilt()
    {
        if (_built)
            throw new InvalidOperationException("This builder has already built a host and cannot be reused.");
    }
}
