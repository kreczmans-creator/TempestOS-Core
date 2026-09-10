using System.Text;
using Tempest.Core.Configuration;
using Tempest.Core.Logging;

namespace Tempest.Core.Secrets;

/// <summary>
/// The non-Windows <see cref="ISecretStore"/> fallback: each secret is
/// written, in plain text, to its own file under
/// <c>&lt;persistence root&gt;/secrets/</c> — never the persistence
/// database, but with no OS-level encryption either (`WP 19.1A`,
/// `ADR-0151`).
/// </summary>
/// <remarks>
/// <para>
/// <b>Disclosed, not hidden.</b> This is a genuinely weaker guarantee than
/// <see cref="WindowsDpapiSecretStore"/>'s own DPAPI-encrypted file: a file
/// system permission is all that stands between a secret and anybody who
/// can read this process's own files. It exists so a connector still works
/// at all on a platform TempestOS's own Windows-first desktop scope does
/// not otherwise reach, and every construction logs a
/// <see cref="ILogger.Warning"/> once, naming exactly what it is, so the
/// gap is visible in the running log rather than assumed away.
/// </para>
/// <para>
/// The documented, temporary state of affairs: a real platform keychain
/// binding (macOS Keychain, the Secret Service API on Linux) is future
/// work this Work Package does not build, not a decision that plain files
/// are an acceptable permanent answer.
/// </para>
/// </remarks>
public sealed class FileSecretStore : ISecretStore
{
    private readonly string _secretsDirectory;

    /// <summary>Initialises a new instance of the <see cref="FileSecretStore"/> class, resolving the secrets directory from <paramref name="configuration"/>.</summary>
    public FileSecretStore(IConfigurationProvider configuration, ILogger? logger = null)
        : this(SecretFileNaming.ResolveSecretsDirectory(configuration), logger)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="FileSecretStore"/> class over an explicit secrets directory.</summary>
    /// <remarks>Internal test seam — lets a test point this store at a temporary directory directly, mirroring every other host-fixture's own explicit-root convention.</remarks>
    internal FileSecretStore(string secretsDirectory, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretsDirectory);

        _secretsDirectory = secretsDirectory;

        logger?.Warning(
            "Secrets are stored as plain, unencrypted files under "
            + $"'{_secretsDirectory}' on this platform: no DPAPI or platform keychain binding exists yet "
            + "(WP 19.1A, ADR-0151). A file-system permission is the only protection a connector token has here.");
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key);

        return File.Exists(path)
            ? Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false))
            : null;
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        Directory.CreateDirectory(_secretsDirectory);

        await File.WriteAllBytesAsync(PathFor(key), Encoding.UTF8.GetBytes(value), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key);

        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private string PathFor(string key) => Path.Combine(_secretsDirectory, SecretFileNaming.FileNameFor(key));
}
