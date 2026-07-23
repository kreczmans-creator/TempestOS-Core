using Tempest.Core.Logging;

namespace Tempest.Core.Configuration;

/// <summary>
/// Builds an <see cref="IConfigurationProvider"/> from one or more
/// <see cref="IConfigurationSource"/> instances.
/// </summary>
/// <remarks>
/// <para>
/// Sources are applied in the order they were added via <see cref="AddSource"/>:
/// later sources override earlier sources for any key they have in common. This is
/// expected, legitimate behaviour, distinct from a duplicate key appearing twice
/// within the <em>same</em> source, which <see cref="Build"/> rejects — see
/// <see cref="DuplicateConfigurationKeyException"/>.
/// </para>
/// <para>
/// Hierarchical keys (for example, <c>Runtime:Logging:MinimumLevel</c>) are supported
/// as plain, flat string keys — colons carry no special meaning to
/// <see cref="ConfigurationBuilder"/> or <see cref="ConfigurationProvider"/>, and no
/// section/binding API is provided. Object binding is explicitly out of scope for
/// WP 2.5.
/// </para>
/// </remarks>
public sealed class ConfigurationBuilder
{
    private readonly List<IConfigurationSource> _sources = new();
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ConfigurationBuilder"/> class.
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record source registration, loading, duplicate key
    /// detection, and build completion via the logging abstraction. May be
    /// <see langword="null"/> if logging is not required.
    /// </param>
    public ConfigurationBuilder(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Adds a configuration source. Sources are applied in the order they are added;
    /// a source added later overrides a source added earlier for any key both define.
    /// </summary>
    /// <param name="source">The configuration source to add.</param>
    /// <returns>This builder, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public ConfigurationBuilder AddSource(IConfigurationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _sources.Add(source);

        _logger?.Information($"Configuration source registered: '{source.GetType().Name}'.");

        return this;
    }

    /// <summary>
    /// Loads every added source, in order, and builds an <see cref="IConfigurationProvider"/>
    /// from the merged result.
    /// </summary>
    /// <returns>An <see cref="IConfigurationProvider"/> over the merged configuration.</returns>
    /// <exception cref="InvalidConfigurationEntryException">
    /// A source produced a null or empty key, or a null value.
    /// </exception>
    /// <exception cref="DuplicateConfigurationKeyException">
    /// A source produced the same key more than once.
    /// </exception>
    public IConfigurationProvider Build()
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in _sources)
        {
            _logger?.Information($"Loading configuration source '{source.GetType().Name}'.");

            var seenInSource = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in source.Load())
            {
                ValidateEntry(entry, source);

                if (!seenInSource.Add(entry.Key))
                {
                    _logger?.Information(
                        $"Duplicate key detected in source '{source.GetType().Name}': '{entry.Key}'.");

                    throw new DuplicateConfigurationKeyException(entry.Key, source.GetType());
                }

                merged[entry.Key] = entry.Value;
            }
        }

        _logger?.Information(
            $"Configuration build completed: {merged.Count} key(s) from {_sources.Count} source(s).");

        return new ConfigurationProvider(merged);
    }

    private static void ValidateEntry(KeyValuePair<string, string> entry, IConfigurationSource source)
    {
        if (entry.Key is null)
            throw new InvalidConfigurationEntryException("Configuration key must not be null.", source.GetType());

        if (string.IsNullOrWhiteSpace(entry.Key))
            throw new InvalidConfigurationEntryException("Configuration key must not be empty or whitespace.", source.GetType());

        if (entry.Value is null)
        {
            throw new InvalidConfigurationEntryException(
                $"Configuration value for key '{entry.Key}' must not be null.",
                source.GetType());
        }
    }
}
