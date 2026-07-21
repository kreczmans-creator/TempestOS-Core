namespace Tempest.Core.Modules;

/// <summary>
/// Thrown when <see cref="IRuntimeModuleManager.Get"/> is called with a module ID that
/// has not been registered.
/// </summary>
public sealed class ModuleNotRegisteredException : ModuleRegistrationException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleNotRegisteredException"/> class.
    /// </summary>
    /// <param name="moduleId">The module ID that was not found.</param>
    public ModuleNotRegisteredException(string moduleId)
        : base($"No module is registered with ID '{moduleId}'.")
    {
        ModuleId = moduleId;
    }

    /// <summary>
    /// Gets the module ID that was not found.
    /// </summary>
    public string ModuleId { get; }
}
