using System.Collections.ObjectModel;

namespace Tempest.Core.Configuration;

/// <summary>
/// The concrete <see cref="IConfigurationProvider"/> implementation.
/// </summary>
/// <remarks>
/// Instances are produced only by <see cref="ConfigurationBuilder.Build"/> — the
/// constructor is <see langword="internal"/>. Configuration values are stored as an
/// immutable, case-insensitive dictionary; once built, a <see cref="ConfigurationProvider"/>
/// can never be changed, and defensively copies the values it is given so that
/// nothing the builder does afterward can affect an already-built provider.
/// </remarks>
public sealed class ConfigurationProvider : IConfigurationProvider
{
    private readonly IReadOnlyDictionary<string, string> _values;

    internal ConfigurationProvider(IReadOnlyDictionary<string, string> values)
    {
        _values = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public string Get(string key)
    {
        if (TryGetValue(key, out var value))
            return value!;

        throw new ConfigurationKeyNotFoundException(key);
    }

    /// <inheritdoc />
    public bool TryGetValue(string key, out string? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        return _values.TryGetValue(key, out value);
    }

    /// <inheritdoc />
    public bool ContainsKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return _values.ContainsKey(key);
    }

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> GetAll() => _values;
}
