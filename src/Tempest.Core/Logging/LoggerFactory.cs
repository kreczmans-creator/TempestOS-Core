using Tempest.Core.Configuration;

namespace Tempest.Core.Logging;

/// <summary>
/// The concrete <see cref="ILoggerFactory"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Reads the runtime's minimum log level from configuration once, at
/// construction, under the key named by <see cref="MinimumLevelConfigurationKey"/>.
/// If the key is missing, the minimum level defaults to
/// <see cref="LogLevel.Information"/>; if the key is present but its value is
/// not a valid <see cref="LogLevel"/> name, construction throws
/// <see cref="ConfigurationException"/> — this is a startup-time failure, not a
/// per-message one.
/// </para>
/// <para>
/// Every logger this factory creates shares the same minimum level and the
/// same sink; only the category differs between them.
/// </para>
/// </remarks>
public sealed class LoggerFactory : ILoggerFactory
{
    /// <summary>
    /// The configuration key the minimum log level is read from
    /// (<c>Runtime:Logging:MinimumLevel</c>).
    /// </summary>
    public const string MinimumLevelConfigurationKey = "Runtime:Logging:MinimumLevel";

    private readonly LogLevel _minimumLevel;
    private readonly ILogSink _sink;

    /// <summary>
    /// Initialises a new instance of the <see cref="LoggerFactory"/> class,
    /// reading the minimum log level from <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">The configuration to read the minimum log level from.</param>
    /// <param name="sink">The sink every logger created by this factory writes to.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="configuration"/> or <paramref name="sink"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ConfigurationException">
    /// <see cref="MinimumLevelConfigurationKey"/> is present but its value is not a
    /// valid <see cref="LogLevel"/> name.
    /// </exception>
    public LoggerFactory(IConfigurationProvider configuration, ILogSink sink)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(sink);

        _minimumLevel = ResolveMinimumLevel(configuration);
        _sink = sink;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new Logger(category, _minimumLevel, _sink);
    }

    private static LogLevel ResolveMinimumLevel(IConfigurationProvider configuration)
    {
        if (!configuration.TryGetValue(MinimumLevelConfigurationKey, out var value))
            return LogLevel.Information;

        if (!Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level))
        {
            throw new ConfigurationException(
                $"Configuration value '{value}' for key '{MinimumLevelConfigurationKey}' " +
                "is not a valid LogLevel.");
        }

        return level;
    }
}
