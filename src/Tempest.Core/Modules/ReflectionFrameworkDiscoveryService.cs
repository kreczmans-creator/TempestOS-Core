using System.Reflection;
using Tempest.Core.Logging;

namespace Tempest.Core.Modules;

/// <summary>
/// Discovers <see cref="IModule"/> implementations by scanning assemblies with reflection.
/// </summary>
/// <remarks>
/// <para>
/// Discovery ignores interfaces, abstract classes, open generic type definitions, and any
/// type that does not implement <see cref="IModule"/>. Discovered modules are returned in
/// ascending, ordinal alphabetical order by <see cref="ModuleDescriptor.Id"/>.
/// </para>
/// <para>
/// Each remaining candidate type is checked for <see cref="ModuleMetadataAttribute"/> first
/// (ADR-0027). If present, metadata is read directly from the attribute and the type is
/// never instantiated by discovery at all. If absent, discovery falls back to its original
/// behaviour, unchanged: the type is instantiated via its public parameterless constructor
/// purely to read its <see cref="IModule"/> instance properties, then the instance is
/// discarded. See <c>Module Dependency Injection Architecture.md</c> for the complete design
/// this enables.
/// </para>
/// </remarks>
public class ReflectionFrameworkDiscoveryService : IFrameworkDiscoveryService
{
    private readonly IEnumerable<Assembly> _assemblies;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ReflectionFrameworkDiscoveryService"/>
    /// class that scans all assemblies currently loaded into the application domain.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record discovery progress via the logging
    /// abstraction. May be <see langword="null"/> if logging is not required.
    /// </param>
    public ReflectionFrameworkDiscoveryService(ILogger? logger = null)
        : this(AppDomain.CurrentDomain.GetAssemblies(), logger)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="ReflectionFrameworkDiscoveryService"/>
    /// class that scans a specific set of assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for <see cref="IModule"/> implementations.</param>
    /// <param name="logger">
    /// An optional logger used to record discovery progress via the logging
    /// abstraction. May be <see langword="null"/> if logging is not required.
    /// </param>
    public ReflectionFrameworkDiscoveryService(IEnumerable<Assembly> assemblies, ILogger? logger = null)
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

            var descriptor = CreateDescriptor(type);

            if (descriptorsById.ContainsKey(descriptor.Id))
            {
                _logger?.Information($"Duplicate module ID detected during discovery: '{descriptor.Id}'.");
                throw new DuplicateModuleIdException(descriptor.Id);
            }

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

    /// <summary>
    /// Builds a <see cref="ModuleDescriptor"/> for a candidate type — from its
    /// <see cref="ModuleMetadataAttribute"/> if present (ADR-0027, no instantiation), or
    /// otherwise by instantiating it via its public parameterless constructor and reading
    /// its <see cref="IModule"/> instance properties, exactly as discovery has always done.
    /// </summary>
    /// <remarks>
    /// <see cref="Activator.CreateInstance(Type)"/> throws an unhelpful, undocumented
    /// <see cref="MissingMethodException"/> for a type with no public parameterless
    /// constructor — exactly the shape a module author who forgets
    /// <see cref="ModuleMetadataAttribute"/> after adding a constructor dependency would hit
    /// (<c>Building a Module.md</c>'s own long-documented, but previously unenforced, "one
    /// constraint you still need to know about"). This is checked explicitly first (WP 5.3)
    /// so the failure is a clear <see cref="ModuleDiscoveryException"/> naming the actual fix,
    /// not a raw runtime exception with no actionable guidance.
    /// </remarks>
    private static ModuleDescriptor CreateDescriptor(Type type)
    {
        var metadataAttribute = type.GetCustomAttribute<ModuleMetadataAttribute>();

        if (metadataAttribute is not null)
        {
            ValidateMetadata(metadataAttribute.Id, metadataAttribute.Name, metadataAttribute.Version, type);
            return new ModuleDescriptor(metadataAttribute.Id, metadataAttribute.Name, metadataAttribute.Version, type);
        }

        if (type.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new ModuleDiscoveryException(
                $"Module type '{type.FullName}' has no public parameterless constructor and no " +
                $"[ModuleMetadataAttribute]. Discovery cannot construct it to read Id/Name/Version. " +
                "Add [ModuleMetadataAttribute(id, name, version)] to declare metadata without " +
                "construction, freeing the constructor to take dependencies (see 'Building a " +
                "Module.md'), or add a public parameterless constructor.");
        }

        var module = (IModule)Activator.CreateInstance(type)!;

        ValidateMetadata(module.Id, module.Name, module.Version, type);

        return new ModuleDescriptor(module.Id, module.Name, module.Version, type);
    }

    private static void ValidateMetadata(string? id, string? name, string? version, Type type)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ModuleDiscoveryException(
                $"Module type '{type.FullName}' has a null, empty, or whitespace Id.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ModuleDiscoveryException(
                $"Module type '{type.FullName}' has a null, empty, or whitespace Name.");
        }

        if (string.IsNullOrWhiteSpace(version))
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
