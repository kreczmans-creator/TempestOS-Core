using System.Reflection;
using Tempest.Core.Logging;

namespace Tempest.Core.Modules;

/// <summary>
/// Discovers <see cref="IModule"/> implementations by scanning assemblies with reflection.
/// </summary>
/// <remarks>
/// Discovery ignores interfaces, abstract classes, open generic type definitions, and any
/// type that does not implement <see cref="IModule"/>. Every remaining candidate type is
/// instantiated via its public parameterless constructor so its metadata can be validated.
/// Discovered modules are returned in ascending, ordinal alphabetical order by
/// <see cref="ModuleDescriptor.Id"/>.
/// </remarks>
public class ReflectionFrameworkDiscoveryService : IFrameworkDiscoveryService
{
    private readonly IEnumerable<Assembly> _assemblies;
    private readonly LoggingService? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ReflectionFrameworkDiscoveryService"/>
    /// class that scans all assemblies currently loaded into the application domain.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record discovery progress via the existing TempestOS
    /// logging infrastructure. May be <see langword="null"/> if logging is not required.
    /// </param>
    public ReflectionFrameworkDiscoveryService(LoggingService? logger = null)
        : this(AppDomain.CurrentDomain.GetAssemblies(), logger)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="ReflectionFrameworkDiscoveryService"/>
    /// class that scans a specific set of assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for <see cref="IModule"/> implementations.</param>
    /// <param name="logger">
    /// An optional logger used to record discovery progress via the existing TempestOS
    /// logging infrastructure. May be <see langword="null"/> if logging is not required.
    /// </param>
    public ReflectionFrameworkDiscoveryService(IEnumerable<Assembly> assemblies, LoggingService? logger = null)
    {
        _assemblies = assemblies;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<ModuleDescriptor> DiscoverModules()
    {
        var candidateTypes = _assemblies.SelectMany(GetLoadableTypes);

        return DiscoverModules(candidateTypes);
    }

    /// <summary>
    /// Discovers modules from an explicit set of candidate types.
    /// </summary>
    /// <param name="candidateTypes">
    /// The types to evaluate. Types that are interfaces, abstract classes, open generic
    /// type definitions, or that do not implement <see cref="IModule"/> are ignored.
    /// </param>
    /// <returns>The discovered modules, ordered by ID.</returns>
    /// <remarks>
    /// This overload is <see langword="internal"/>. It isolates the core discovery
    /// algorithm — type filtering, metadata validation, duplicate detection, and
    /// ordering — from assembly enumeration, so it can be exercised deterministically
    /// against a controlled set of types in unit tests.
    /// </remarks>
    internal IReadOnlyList<ModuleDescriptor> DiscoverModules(IEnumerable<Type> candidateTypes)
    {
        _logger?.Information("Framework discovery started.");

        var descriptorsById = new Dictionary<string, ModuleDescriptor>(StringComparer.Ordinal);

        foreach (var type in candidateTypes)
        {
            if (!IsValidModuleType(type))
                continue;

            var module = (IModule)Activator.CreateInstance(type)!;

            ValidateMetadata(module, type);

            if (descriptorsById.ContainsKey(module.Id))
            {
                _logger?.Information($"Duplicate module ID detected during discovery: '{module.Id}'.");
                throw new DuplicateModuleIdException(module.Id);
            }

            var descriptor = new ModuleDescriptor(module.Id, module.Name, module.Version, type);
            descriptorsById.Add(descriptor.Id, descriptor);

            _logger?.Information($"Discovered module '{descriptor.Id}' ({descriptor.Name} v{descriptor.Version}).");
        }

        var ordered = descriptorsById.Values
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .ToList();

        _logger?.Information($"Framework discovery completed. {ordered.Count} module(s) found.");

        return ordered;
    }

    private static bool IsValidModuleType(Type type)
    {
        if (!typeof(IModule).IsAssignableFrom(type))
            return false;

        if (type.IsInterface || type.IsAbstract || type.IsGenericTypeDefinition)
            return false;

        return true;
    }

    private static void ValidateMetadata(IModule module, Type type)
    {
        if (string.IsNullOrWhiteSpace(module.Id))
        {
            throw new ModuleDiscoveryException(
                $"Module type '{type.FullName}' has a null, empty, or whitespace Id.");
        }

        if (string.IsNullOrWhiteSpace(module.Name))
        {
            throw new ModuleDiscoveryException(
                $"Module type '{type.FullName}' has a null, empty, or whitespace Name.");
        }

        if (string.IsNullOrWhiteSpace(module.Version))
        {
            throw new ModuleDiscoveryException(
                $"Module type '{type.FullName}' has a null, empty, or whitespace Version.");
        }
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
