namespace Tempest.Core.DependencyInjection;

/// <summary>
/// Convenience resolution methods for <see cref="ITempestServiceProvider"/>.
/// </summary>
public static class ServiceProviderExtensions
{
    /// <summary>
    /// Resolves an instance of <typeparamref name="T"/>.
    /// </summary>
    public static T GetService<T>(this ITempestServiceProvider provider)
        where T : class =>
        (T)provider.GetService(typeof(T));
}
