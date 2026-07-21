namespace Tempest.Core.Modules;

/// <summary>
/// Defines the contract implemented by all TempestOS framework modules.
/// </summary>
/// <remarks>
/// Implementations are located by <see cref="IFrameworkDiscoveryService"/> via reflection.
/// A discoverable module must be a concrete, non-generic class with a public parameterless
/// constructor; interfaces, abstract classes, and open generic type definitions are ignored.
/// </remarks>
public interface IModule
{
    /// <summary>
    /// Gets the unique, stable identifier for this module.
    /// </summary>
    /// <remarks>
    /// The identifier must be unique across all modules returned by a single discovery
    /// pass. Discovery fails with <see cref="DuplicateModuleIdException"/> if two modules
    /// share the same identifier.
    /// </remarks>
    string Id { get; }

    /// <summary>
    /// Gets the human-readable display name of this module.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the version of this module, expressed as a string (for example, "1.0.0").
    /// </summary>
    string Version { get; }
}
