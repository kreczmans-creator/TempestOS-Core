namespace Tempest.Core.Modules;

/// <summary>
/// The lifecycle contract implemented by modules that participate in runtime
/// initialisation, startup, shutdown, and disposal.
/// </summary>
/// <remarks>
/// A module implementing <see cref="IModule"/> is not required to also implement
/// <see cref="IModuleLifecycle"/>. Modules that don't are still driven through the
/// full <see cref="ModuleState"/> progression by <see cref="IModuleLifecycleManager"/>,
/// but no instance is constructed for them and no lifecycle method is invoked — they
/// are treated as having no lifecycle behaviour to run.
/// </remarks>
public interface IModuleLifecycle
{
    /// <summary>
    /// Initialises the module. Invoked once, after registration and before
    /// <see cref="StartAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task InitialiseAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts the module. Invoked once, after <see cref="InitialiseAsync"/> has
    /// completed successfully.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the module. Invoked once, while the module is running.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Releases resources held by the module. May be invoked regardless of which
    /// prior lifecycle stage the module reached, but at most once.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task DisposeAsync(CancellationToken cancellationToken);
}
