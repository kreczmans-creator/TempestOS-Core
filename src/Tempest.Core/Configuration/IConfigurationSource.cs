namespace Tempest.Core.Configuration;

/// <summary>
/// Supplies a set of configuration key/value pairs to a <see cref="ConfigurationBuilder"/>.
/// </summary>
/// <remarks>
/// WP 2.5 implements exactly one source, <see cref="MemoryConfigurationSource"/>.
/// Future work packages may introduce additional sources (JSON, environment
/// variables, command line, database) behind this same contract, without requiring
/// any change to <see cref="ConfigurationBuilder"/> or <see cref="IConfigurationProvider"/>.
/// </remarks>
public interface IConfigurationSource
{
    /// <summary>
    /// Loads this source's configuration entries.
    /// </summary>
    /// <returns>
    /// The entries this source contributes. May contain the same key more than
    /// once — <see cref="ConfigurationBuilder.Build"/> treats that as a defect in
    /// this source and rejects it; it does not silently deduplicate.
    /// </returns>
    IEnumerable<KeyValuePair<string, string>> Load();
}
