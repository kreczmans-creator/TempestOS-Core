namespace Tempest.Core.Modules;

/// <summary>
/// Declares a module's <see cref="IModule.Id"/>, <see cref="IModule.Name"/>,
/// and <see cref="IModule.Version"/> on the type itself, so
/// <see cref="IFrameworkDiscoveryService"/> can read them without
/// constructing an instance.
/// </summary>
/// <remarks>
/// <para>
/// Optional. A module without this attribute is discovered exactly as it
/// always has been: instantiated via its public parameterless
/// constructor, its <see cref="IModule"/> instance properties read, then
/// discarded. A module carrying this attribute is never instantiated by
/// discovery at all — its metadata is read directly from the attribute,
/// leaving its own public constructor free to declare whatever
/// dependencies <c>TempestServiceProvider</c> can resolve, since nothing
/// about discovery requires it to be parameterless. This is what makes a
/// constructor-injected module possible — see ADR-0027 and <c>Module
/// Dependency Injection Architecture.md</c> for the complete design.
/// </para>
/// <para>
/// The values supplied here are not cross-checked against the module's own
/// <see cref="IModule.Id"/>/<see cref="IModule.Name"/>/<see cref="IModule.Version"/>
/// once it is eventually constructed — keeping the two in agreement is the
/// module author's own responsibility, structurally the same, accepted
/// risk as a <c>PluginManifest</c>'s declared <c>Version</c> not being
/// cross-checked against a loaded plugin's real <see cref="IModule.Version"/>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ModuleMetadataAttribute : Attribute
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleMetadataAttribute"/> class.
    /// </summary>
    /// <param name="id">The module's unique, stable identifier.</param>
    /// <param name="name">The module's human-readable display name.</param>
    /// <param name="version">The module's version string.</param>
    public ModuleMetadataAttribute(string id, string name, string version)
    {
        Id = id;
        Name = name;
        Version = version;
    }

    /// <summary>
    /// Gets the module's unique, stable identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the module's human-readable display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the module's version string.
    /// </summary>
    public string Version { get; }
}
