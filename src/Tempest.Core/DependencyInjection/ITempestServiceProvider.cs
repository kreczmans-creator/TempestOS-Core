namespace Tempest.Core.DependencyInjection;

/// <summary>
/// Resolves service instances according to registrations recorded in an
/// <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// Named <c>ITempestServiceProvider</c> rather than <c>IServiceProvider</c> to avoid
/// colliding with <see cref="System.IServiceProvider"/>: <c>System</c> is part of this
/// project's implicit usings (see <c>Directory.Build.props</c>), so any file that also
/// referenced <c>Tempest.Core.DependencyInjection</c> would face an ambiguous-reference
/// compiler error between the two types on every unqualified use.
/// </remarks>
public interface ITempestServiceProvider
{
    /// <summary>
    /// Resolves an instance of <paramref name="serviceType"/>, constructing it (and,
    /// recursively, its constructor dependencies) as needed.
    /// </summary>
    /// <param name="serviceType">The service type to resolve.</param>
    /// <returns>An instance satisfying <paramref name="serviceType"/>.</returns>
    /// <exception cref="ServiceNotRegisteredException">
    /// <paramref name="serviceType"/>, or one of its transitive dependencies, has no
    /// registration.
    /// </exception>
    /// <exception cref="CircularServiceDependencyException">
    /// Resolving <paramref name="serviceType"/> would require constructing a type that
    /// is already in the process of being constructed.
    /// </exception>
    /// <exception cref="AmbiguousConstructorException">
    /// The implementation type for <paramref name="serviceType"/>, or one of its
    /// transitive dependencies, declares more than one public constructor.
    /// </exception>
    object GetService(Type serviceType);
}
