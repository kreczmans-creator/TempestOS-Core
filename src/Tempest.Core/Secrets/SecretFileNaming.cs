using System.Security.Cryptography;
using System.Text;
using Tempest.Core.Configuration;
using Tempest.Core.Persistence;

namespace Tempest.Core.Secrets;

/// <summary>
/// What both <see cref="ISecretStore"/> implementations share: where the
/// secrets directory lives, and how a caller-chosen key becomes a file name
/// (`WP 19.1A`, `ADR-0151`).
/// </summary>
internal static class SecretFileNaming
{
    /// <summary>The directory name under the persistence root every secret file lives beneath.</summary>
    internal const string SecretsDirectoryName = "secrets";

    /// <summary>
    /// The secrets directory for this run: <c>&lt;persistence root&gt;/secrets</c>,
    /// the persistence root resolved the identical way
    /// <see cref="SqlitePersistenceStore"/> itself resolves it, so a secret
    /// and the database it must never appear inside sit beside each other
    /// under the one root an operator already configures.
    /// </summary>
    internal static string ResolveSecretsDirectory(IConfigurationProvider configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var root = configuration.TryGetValue(SqlitePersistenceStore.RootPathConfigurationKey, out var configuredRoot)
            && !string.IsNullOrWhiteSpace(configuredRoot)
                ? configuredRoot
                : SqlitePersistenceStore.DefaultRootPath;

        return Path.Combine(root, SecretsDirectoryName);
    }

    /// <summary>
    /// The file a secret keyed by <paramref name="key"/> lives under —
    /// a SHA-256 hash of the key, never the key itself, so a key carrying
    /// characters a file system cannot name (<c>"Xero:AccessToken"</c>) is
    /// never a problem and a key is never readable from a directory
    /// listing alone.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty, or whitespace.</exception>
    internal static string FileNameFor(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash).ToLowerInvariant() + ".secret";
    }
}
