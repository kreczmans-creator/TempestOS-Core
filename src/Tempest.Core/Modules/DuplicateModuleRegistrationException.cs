namespace Tempest.Core.Modules;

/// <summary>
/// Thrown when <see cref="IRuntimeModuleManager.Register"/> is called with a descriptor
/// whose <see cref="ModuleDescriptor.Id"/> is already registered.
/// </summary>
public sealed class DuplicateModuleRegistrationException : ModuleRegistrationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateModuleRegistrationException"/> class.
    /// </summary>
    /// <param name="moduleId">The module ID that was already registered.</param>
    public DuplicateModuleRegistrationException(string moduleId)
        : base($"Module '{moduleId}' is already registered.")
    {
        ModuleId = moduleId;
    }

    /// <summary>
    /// Gets the module ID that was already registered.
    /// </summary>
    public string ModuleId { get; }
}
