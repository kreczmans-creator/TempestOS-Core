using System.Reflection;
using Tempest.Core.Logging;

namespace Tempest.Core.Versioning;

/// <summary>
/// The concrete <see cref="IPlatformVersionProvider"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Resolves <see cref="Version"/> exactly once, in the constructor, from the
/// executing assembly's own build-time metadata — never from a hand-typed
/// constant. Because the result is computed once and the type exposes no
/// way to mutate it afterward, no locking is needed for thread safety, the
/// same reasoning <see cref="Logger"/> already applies.
/// </para>
/// <para>
/// <b>Missing metadata behaviour.</b> If the executing assembly has no
/// <see cref="AssemblyInformationalVersionAttribute"/> at all,
/// <see cref="PlatformVersion.InformationalVersion"/> is <see langword="null"/>
/// and <see cref="PlatformVersion.SemanticVersion"/> falls back to
/// <see cref="PlatformVersion.AssemblyVersion"/>'s own
/// <c>Major.Minor.Build</c> values. If the assembly has no version metadata
/// whatsoever, <see cref="PlatformVersion.AssemblyVersion"/> itself falls
/// back to <c>0.0.0.0</c> rather than the constructor throwing — a platform
/// that cannot determine its own version is a diagnostic fact worth
/// reporting, not a startup failure.
/// </para>
/// <para>
/// This provider depends on nothing beyond the executing assembly's own,
/// already-loaded metadata — no configuration, no other platform service.
/// It is a leaf: other platform services may depend on it; it must never
/// depend on them (see <c>docs/architecture/Platform Version.md</c>).
/// </para>
/// </remarks>
public sealed class PlatformVersionProvider : IPlatformVersionProvider
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PlatformVersionProvider"/>
    /// class, resolving its version from the assembly this class itself is
    /// compiled into.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record the resolved version via the
    /// logging abstraction. May be <see langword="null"/> if logging is not
    /// required.
    /// </param>
    public PlatformVersionProvider(ILogger? logger = null)
        : this(typeof(PlatformVersionProvider).Assembly, logger)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="PlatformVersionProvider"/>
    /// class, resolving its version from a specific assembly.
    /// </summary>
    /// <param name="assembly">The assembly to resolve version metadata from.</param>
    /// <param name="logger">
    /// An optional logger used to record the resolved version via the
    /// logging abstraction. May be <see langword="null"/> if logging is not
    /// required.
    /// </param>
    /// <remarks>
    /// Internal test seam — mirrors <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s
    /// own two-constructor pattern, so version resolution (including its
    /// missing-metadata fallback behaviour) can be exercised deterministically
    /// against a controlled assembly in tests, without depending on this
    /// solution's own, incidental build metadata.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="assembly"/> is <see langword="null"/>.</exception>
    internal PlatformVersionProvider(Assembly assembly, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        Version = Resolve(assembly);

        logger?.Information($"Platform version resolved: {Version.SemanticVersion}");
    }

    /// <inheritdoc />
    public PlatformVersion Version { get; }

    private static PlatformVersion Resolve(Assembly assembly)
    {
        var assemblyVersion = assembly.GetName().Version ?? new Version(0, 0, 0, 0);

        var rawInformationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        var informationalVersion = string.IsNullOrWhiteSpace(rawInformationalVersion)
            ? null
            : rawInformationalVersion;

        var semanticVersion = informationalVersion ??
            $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{Math.Max(assemblyVersion.Build, 0)}";

        return new PlatformVersion(semanticVersion, assemblyVersion, informationalVersion);
    }
}
