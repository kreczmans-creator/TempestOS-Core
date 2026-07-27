using System.Reflection;
using Tempest.Core.Logging;

namespace Tempest.Core.BackgroundServices;

/// <summary>
/// The concrete <see cref="IHostedServiceDiscoveryService"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s own reflection-based
/// discovery pattern exactly — filter before instantiating, impose deterministic ordering,
/// isolate per-candidate load failures, expose an <see langword="internal"/> test seam —
/// with one deliberate difference: a candidate is never instantiated. <see cref="IHostedService"/>
/// carries no metadata to read, so there is nothing discovery would need to construct a
/// candidate for at all (ADR-0029). This is not an extension of
/// <see cref="Modules.ReflectionFrameworkDiscoveryService"/> itself — see ADR-0029's own
/// Alternatives Considered (RD-0025) for why a separate, dedicated service exists instead.
/// </para>
/// </remarks>
public sealed class HostedServiceDiscoveryService : IHostedServiceDiscoveryService
{
    private readonly IEnumerable<Assembly> _assemblies;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="HostedServiceDiscoveryService"/>
    /// class that scans all assemblies currently loaded into the application domain.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record discovery progress via the logging
    /// abstraction. May be <see langword="null"/> if logging is not required.
    /// </param>
    public HostedServiceDiscoveryService(ILogger? logger = null)
        : this(AppDomain.CurrentDomain.GetAssemblies(), logger)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="HostedServiceDiscoveryService"/>
    /// class that scans a specific set of assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for <see cref="IHostedService"/> implementations.</param>
    /// <param name="logger">
    /// An optional logger used to record discovery progress via the logging
    /// abstraction. May be <see langword="null"/> if logging is not required.
    /// </param>
    public HostedServiceDiscoveryService(IEnumerable<Assembly> assemblies, ILogger? logger = null)
    {
        _assemblies = assemblies;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<Type> DiscoverHostedServiceTypes()
    {
        var candidateTypes = _assemblies.SelectMany(GetLoadableTypes);

        return DiscoverHostedServiceTypes(candidateTypes);
    }

    /// <summary>
    /// Discovers hosted service types from an explicit set of candidate types.
    /// </summary>
    /// <param name="candidateTypes">
    /// The types to evaluate. Types that are interfaces, abstract classes, open generic
    /// type definitions, or that do not implement <see cref="IHostedService"/> are ignored.
    /// </param>
    /// <returns>The discovered hosted service types, ordered by <see cref="Type.FullName"/>.</returns>
    /// <remarks>
    /// This overload is <see langword="internal"/>. It isolates the core discovery
    /// algorithm — type filtering and ordering — from assembly enumeration, so it can be
    /// exercised deterministically against a controlled set of types in unit tests,
    /// mirroring <see cref="Modules.ReflectionFrameworkDiscoveryService"/>'s own
    /// established seam exactly.
    /// </remarks>
    internal IReadOnlyList<Type> DiscoverHostedServiceTypes(IEnumerable<Type> candidateTypes)
    {
        _logger?.Information("Hosted service discovery started.");

        var discovered = candidateTypes
            .Where(IsValidHostedServiceType)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        foreach (var type in discovered)
            _logger?.Information($"Discovered hosted service '{type.FullName}'.");

        _logger?.Information($"Hosted service discovery completed. {discovered.Count} hosted service(s) found.");

        return discovered;
    }

    private static bool IsValidHostedServiceType(Type type)
    {
        if (!typeof(IHostedService).IsAssignableFrom(type))
            return false;

        if (type.IsInterface || type.IsAbstract || type.IsGenericTypeDefinition)
            return false;

        return true;
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null).Select(type => type!);
        }
    }
}
