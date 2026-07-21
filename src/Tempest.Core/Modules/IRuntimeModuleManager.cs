namespace Tempest.Core.Modules;

/// <summary>
/// The single authoritative runtime catalogue of every module known to TempestOS.
/// </summary>
/// <remarks>
/// An <see cref="IRuntimeModuleManager"/> owns runtime metadata only: it registers
/// already-discovered modules and provides lookup over them. It does not perform
/// reflection or assembly scanning, instantiate module implementations, inject
/// dependencies, or execute lifecycle events. Those responsibilities belong to later
/// stages of the module pipeline:
/// discovery → registration (this type) → lifecycle → dependency injection → runtime.
/// </remarks>
public interface IRuntimeModuleManager
{
    /// <summary>
    /// Gets all currently registered modules, in registration order.
    /// </summary>
    /// <remarks>
    /// The returned collection is a read-only snapshot taken at the time of the call;
    /// it cannot be used to mutate the manager's internal state, and later
    /// registrations do not retroactively affect a collection already returned.
    /// </remarks>
    IReadOnlyCollection<RuntimeModule> Modules { get; }

    /// <summary>
    /// Registers a discovered module with the runtime module manager.
    /// </summary>
    /// <param name="descriptor">The module's descriptor, typically produced by discovery.</param>
    /// <returns>The <see cref="RuntimeModule"/> created for <paramref name="descriptor"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="descriptor"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="descriptor"/> has a null, empty, or whitespace <see cref="ModuleDescriptor.Id"/>.
    /// </exception>
    /// <exception cref="DuplicateModuleRegistrationException">
    /// A module with the same <see cref="ModuleDescriptor.Id"/> is already registered.
    /// </exception>
    RuntimeModule Register(ModuleDescriptor descriptor);

    /// <summary>
    /// Gets the registered module with the given ID.
    /// </summary>
    /// <param name="moduleId">The module ID to look up.</param>
    /// <returns>The matching <see cref="RuntimeModule"/>.</returns>
    /// <exception cref="ModuleNotRegisteredException">
    /// No module with <paramref name="moduleId"/> is registered.
    /// </exception>
    RuntimeModule Get(string moduleId);

    /// <summary>
    /// Attempts to get the registered module with the given ID.
    /// </summary>
    /// <param name="moduleId">The module ID to look up.</param>
    /// <param name="module">
    /// When this method returns <see langword="true"/>, the matching
    /// <see cref="RuntimeModule"/>; otherwise, the default value for the type.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if a module with <paramref name="moduleId"/> is registered;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool TryGet(string moduleId, out RuntimeModule module);

    /// <summary>
    /// Determines whether a module with the given ID is registered.
    /// </summary>
    /// <param name="moduleId">The module ID to check.</param>
    /// <returns>
    /// <see langword="true"/> if a module with <paramref name="moduleId"/> is registered;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    bool IsRegistered(string moduleId);

    /// <summary>
    /// Gets all currently registered modules, in registration order.
    /// </summary>
    /// <returns>A read-only snapshot of all registered modules.</returns>
    IReadOnlyCollection<RuntimeModule> GetAll();
}
