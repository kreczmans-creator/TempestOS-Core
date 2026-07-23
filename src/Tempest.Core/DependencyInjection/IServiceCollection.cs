namespace Tempest.Core.DependencyInjection;

/// <summary>
/// A registry of service registrations, built up before an
/// <see cref="ITempestServiceProvider"/> is constructed from it.
/// </summary>
/// <remarks>
/// The core contract is deliberately minimal — a single <see cref="Add"/> method
/// keyed on runtime <see cref="Type"/> values — so that types discovered by
/// reflection (see <c>ModuleServiceCollectionExtensions.AddDiscoveredModules</c>)
/// can be registered without a compile-time generic argument. The familiar
/// <c>Singleton&lt;T&gt;()</c>, <c>Singleton&lt;TService, TImplementation&gt;()</c>,
/// <c>Transient&lt;T&gt;()</c>, and <c>Transient&lt;TService, TImplementation&gt;()</c>
/// forms are provided as extension methods in <see cref="ServiceCollectionExtensions"/>,
/// implemented in terms of this one method.
/// </remarks>
public interface IServiceCollection
{
    /// <summary>
    /// Gets every registration added so far.
    /// </summary>
    IReadOnlyList<ServiceDescriptor> Descriptors { get; }

    /// <summary>
    /// Registers <paramref name="implementationType"/> to satisfy requests for
    /// <paramref name="serviceType"/>, with the given lifetime.
    /// </summary>
    /// <param name="serviceType">The type consumers will ask the container to resolve.</param>
    /// <param name="implementationType">
    /// The concrete type to construct. Must be assignable to <paramref name="serviceType"/>.
    /// </param>
    /// <param name="lifetime">How long the constructed instance should be kept alive.</param>
    /// <returns>This collection, to allow chaining.</returns>
    /// <remarks>
    /// Registering the same <paramref name="serviceType"/> more than once replaces the
    /// previous registration; the most recent registration wins.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="implementationType"/> is not assignable to <paramref name="serviceType"/>.
    /// </exception>
    IServiceCollection Add(Type serviceType, Type implementationType, ServiceLifetime lifetime);

    /// <summary>
    /// Registers an already-constructed instance to satisfy requests for
    /// <paramref name="serviceType"/>.
    /// </summary>
    /// <param name="serviceType">The type consumers will ask the container to resolve.</param>
    /// <param name="instance">
    /// The instance to hand out. Must be assignable to <paramref name="serviceType"/>.
    /// </param>
    /// <returns>This collection, to allow chaining.</returns>
    /// <remarks>
    /// <para>
    /// For registrations the container can construct itself via reflection, prefer
    /// <see cref="Add"/> (or the <c>Singleton</c>/<c>Transient</c> extension methods).
    /// <see cref="AddInstance"/> exists for the opposite case: a value that has
    /// already been built by something other than the container — most notably,
    /// configuration (see <c>ConfigurationBuilder.Build</c>), which requires
    /// runtime-supplied sources the container has no way to construct on its own.
    /// </para>
    /// <para>
    /// An instance registration is always effectively a singleton: there is exactly
    /// one instance, and every resolution of <paramref name="serviceType"/> returns
    /// it. Registering the same <paramref name="serviceType"/> more than once
    /// (whether via <see cref="Add"/> or <see cref="AddInstance"/>) replaces the
    /// previous registration; the most recent registration wins.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="instance"/> is not assignable to <paramref name="serviceType"/>.
    /// </exception>
    IServiceCollection AddInstance(Type serviceType, object instance);
}
