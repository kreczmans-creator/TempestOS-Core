namespace Tempest.Core.Modules;

/// <summary>
/// Discovers <see cref="IModule"/> implementations available to the platform.
/// </summary>
public interface IFrameworkDiscoveryService
{
    /// <summary>
    /// Discovers all valid modules and returns them in deterministic, ascending
    /// alphabetical order by <see cref="ModuleDescriptor.Id"/>.
    /// </summary>
    /// <returns>The discovered modules, ordered by ID.</returns>
    /// <exception cref="DuplicateModuleIdException">
    /// Thrown when two or more discovered modules share the same ID.
    /// </exception>
    /// <exception cref="ModuleDiscoveryException">
    /// Thrown when a discovered module's metadata is invalid.
    /// </exception>
    IReadOnlyList<ModuleDescriptor> DiscoverModules();
}
