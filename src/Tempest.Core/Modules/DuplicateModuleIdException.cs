namespace Tempest.Core.Modules;

/// <summary>
/// Thrown when framework discovery finds two or more modules sharing the same
/// <see cref="IModule.Id"/>.
/// </summary>
public sealed class DuplicateModuleIdException : ModuleDiscoveryException
{
    /// <summary>
    /// Initialises a new instance of the <see cref="DuplicateModuleIdException"/> class.
    /// </summary>
    /// <param name="moduleId">The module ID that was found more than once.</param>
    public DuplicateModuleIdException(string moduleId)
        : base($"Duplicate module ID detected during discovery: '{moduleId}'.")
    {
        ModuleId = moduleId;
    }

    /// <summary>
    /// Gets the module ID that was found more than once.
    /// </summary>
    public string ModuleId { get; }
}
