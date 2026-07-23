namespace Tempest.Core.Modules;

/// <summary>
/// A minimal, convenient base implementation of <see cref="IModule"/> and
/// <see cref="IModuleLifecycle"/> for modules that participate in the
/// runtime lifecycle but only need to act during some of its phases.
/// </summary>
/// <remarks>
/// <para>
/// Every lifecycle method is <see langword="virtual"/> and defaults to
/// <see cref="Task.CompletedTask"/>. A module overrides only the phase(s) it
/// actually cares about, rather than writing a trivial
/// <c>=&gt; Task.CompletedTask;</c> override for every phase it doesn't.
/// </para>
/// <para>
/// Extends <see cref="ModuleBase"/> for identity (<see cref="IModule.Id"/>,
/// <see cref="IModule.Name"/>, <see cref="IModule.Version"/>) — see that
/// class's remarks regarding the public parameterless constructor a
/// concrete module still needs for discovery.
/// </para>
/// </remarks>
public abstract class ModuleLifecycleBase : ModuleBase, IModuleLifecycle
{
    /// <summary>
    /// Initialises a new instance of the <see cref="ModuleLifecycleBase"/> class.
    /// </summary>
    /// <param name="id">The module's unique, stable identifier.</param>
    /// <param name="name">The module's human-readable display name.</param>
    /// <param name="version">The module's version string.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="id"/>, <paramref name="name"/>, or
    /// <paramref name="version"/> is <see langword="null"/>, empty, or
    /// whitespace.
    /// </exception>
    protected ModuleLifecycleBase(string id, string name, string version)
        : base(id, name, version)
    {
    }

    /// <inheritdoc />
    /// <remarks>Defaults to a no-op. Override to act during initialisation.</remarks>
    public virtual Task InitialiseAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>Defaults to a no-op. Override to act during startup.</remarks>
    public virtual Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>Defaults to a no-op. Override to act during shutdown.</remarks>
    public virtual Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    /// <remarks>Defaults to a no-op. Override to release resources on disposal.</remarks>
    public virtual Task DisposeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
