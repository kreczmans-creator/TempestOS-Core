namespace Tempest.Core.Configuration;

/// <summary>
/// An <see cref="IConfigurationSource"/> backed by an in-memory collection of
/// key/value pairs supplied directly by the caller.
/// </summary>
/// <remarks>
/// This is the only <see cref="IConfigurationSource"/> implementation WP 2.5
/// introduces. It accepts entries as a plain sequence rather than a dictionary
/// specifically so a caller can supply the same key more than once — the sequence
/// is stored and yielded as-is, and it is <see cref="ConfigurationBuilder.Build"/>,
/// not this class, that rejects a duplicate key within one source.
/// </remarks>
public sealed class MemoryConfigurationSource : IConfigurationSource
{
    private readonly IReadOnlyList<KeyValuePair<string, string>> _entries;

    /// <summary>
    /// Initialises a new instance of the <see cref="MemoryConfigurationSource"/> class.
    /// </summary>
    /// <param name="entries">The key/value pairs this source contributes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    public MemoryConfigurationSource(IEnumerable<KeyValuePair<string, string>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        _entries = entries.ToList();
    }

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> Load() => _entries;
}
