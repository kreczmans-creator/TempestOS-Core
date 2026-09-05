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
    /// <param name="allowReplace">
    /// <see langword="true"/> to deliberately replace an existing registration for
    /// <paramref name="serviceType"/> instead of throwing; <see langword="false"/> — the
    /// default — treats a pre-existing registration as a mistake (TD-69). Ignored when
    /// no registration for <paramref name="serviceType"/> exists yet.
    /// </param>
    /// <returns>This collection, to allow chaining.</returns>
    /// <remarks>
    /// Registering the same <paramref name="serviceType"/> a second time without
    /// <paramref name="allowReplace"/> throws <see cref="DuplicateServiceRegistrationException"/>
    /// rather than silently replacing the previous registration — a mistaken
    /// re-registration (of, say, <c>IEventBus</c>) is far more likely than a genuine need
    /// to replace one, and silently swapping the platform implementation with no
    /// exception and no log was exactly TD-69's own defect.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="implementationType"/> is not assignable to <paramref name="serviceType"/>.
    /// </exception>
    /// <exception cref="DuplicateServiceRegistrationException">
    /// A registration for <paramref name="serviceType"/> already exists and
    /// <paramref name="allowReplace"/> is <see langword="false"/>.
    /// </exception>
    IServiceCollection Add(Type serviceType, Type implementationType, ServiceLifetime lifetime, bool allowReplace = false);

    /// <summary>
    /// Registers an already-constructed instance to satisfy requests for
    /// <paramref name="serviceType"/>.
    /// </summary>
    /// <param name="serviceType">The type consumers will ask the container to resolve.</param>
    /// <param name="instance">
    /// The instance to hand out. Must be assignable to <paramref name="serviceType"/>.
    /// </param>
    /// <param name="allowReplace">
    /// <see langword="true"/> to deliberately replace an existing registration for
    /// <paramref name="serviceType"/> instead of throwing; <see langword="false"/> — the
    /// default — treats a pre-existing registration as a mistake (TD-69). Ignored when
    /// no registration for <paramref name="serviceType"/> exists yet.
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
    /// it. Registering the same <paramref name="serviceType"/> a second time (whether
    /// via <see cref="Add"/> or <see cref="AddInstance"/>) without <paramref name="allowReplace"/>
    /// throws <see cref="DuplicateServiceRegistrationException"/> — see <see cref="Add"/>'s
    /// own remarks (TD-69).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="instance"/> is not assignable to <paramref name="serviceType"/>.
    /// </exception>
    /// <exception cref="DuplicateServiceRegistrationException">
    /// A registration for <paramref name="serviceType"/> already exists and
    /// <paramref name="allowReplace"/> is <see langword="false"/>.
    /// </exception>
    IServiceCollection AddInstance(Type serviceType, object instance, bool allowReplace = false);
}
