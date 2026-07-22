using Tempest.Core.Configuration;
using Tempest.Core.DependencyInjection;

namespace Tempest.Core.Logging;

/// <summary>
/// Wires the logging framework into the dependency injection container.
/// </summary>
public static class LoggingServiceCollectionExtensions
{
    /// <summary>
    /// The category the default <see cref="ILogger"/> registration is created with.
    /// </summary>
    public const string DefaultLoggerCategory = "Runtime";

    /// <summary>
    /// Builds and registers the logging framework: an <see cref="ILogSink"/>
    /// (<see cref="ConsoleLogSink"/>), an <see cref="ILoggerFactory"/>, and a
    /// default <see cref="ILogger"/> created with category
    /// <see cref="DefaultLoggerCategory"/>.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configuration">
    /// The already-built configuration to read <c>Runtime:Logging:MinimumLevel</c>
    /// from — see <see cref="LoggerFactory"/>.
    /// </param>
    /// <returns><paramref name="services"/>, to allow chaining.</returns>
    /// <remarks>
    /// Like configuration itself (see ADR-0009, <em>Composition Root Owns
    /// Externally-Created Services</em>), the sink, factory, and default logger
    /// are all constructed directly, here, at the composition root — not
    /// resolved via the container's own reflection-based construction — because
    /// producing the default logger requires calling
    /// <see cref="ILoggerFactory.CreateLogger"/>, a method invocation the
    /// container has no way to perform on its own. All three are registered via
    /// <see cref="IServiceCollection.AddInstance"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ConfigurationException">
    /// <c>Runtime:Logging:MinimumLevel</c> is present but is not a valid
    /// <see cref="LogLevel"/> name.
    /// </exception>
    public static IServiceCollection AddLogging(this IServiceCollection services, IConfigurationProvider configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        ILogSink sink = new ConsoleLogSink();
        ILoggerFactory loggerFactory = new LoggerFactory(configuration, sink);
        ILogger defaultLogger = loggerFactory.CreateLogger(DefaultLoggerCategory);

        services.AddInstance(sink);
        services.AddInstance(loggerFactory);
        services.AddInstance(defaultLogger);

        return services;
    }
}
