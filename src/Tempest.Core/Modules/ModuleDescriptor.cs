namespace Tempest.Core.Modules;

/// <summary>
/// Describes a module found by an <see cref="IFrameworkDiscoveryService"/>.
/// </summary>
/// <remarks>
/// A descriptor is an immutable snapshot of an <see cref="IModule"/> implementation's
/// metadata and its underlying <see cref="System.Type"/>, captured at discovery time.
/// </remarks>
public sealed class ModuleDescriptor
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleDescriptor"/> class.
    /// </summary>
    /// <param name="id">The module's unique identifier.</param>
    /// <param name="name">The module's human-readable name.</param>
    /// <param name="version">The module's version string.</param>
    /// <param name="moduleType">The concrete <see cref="System.Type"/> implementing <see cref="IModule"/>.</param>
    public ModuleDescriptor(string id, string name, string version, Type moduleType)
    {
        Id = id;
        Name = name;
        Version = version;
        ModuleType = moduleType;
    }

    /// <summary>
    /// Gets the module's unique identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the module's human-readable name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the module's version string.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// Gets the concrete <see cref="System.Type"/> that implements <see cref="IModule"/>.
    /// </summary>
    public Type ModuleType { get; }
}
