using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Tempest.Core.Configuration;

namespace Tempest.Core.Secrets;

/// <summary>
/// The Windows <see cref="ISecretStore"/>: each secret is encrypted with
/// the Windows Data Protection API, scoped to the signed-in Windows user,
/// and written to its own file under <c>&lt;persistence root&gt;/secrets/</c> —
/// never the persistence database (`WP 19.1A`, `ADR-0151`).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ProtectedData.Protect"/> ties the ciphertext to the current
/// Windows user account (<see cref="DataProtectionScope.CurrentUser"/>): a
/// copy of the secrets directory read by anybody else, on this machine or
/// another, decrypts to nothing. This is the same trade-off every desktop
/// credential manager makes — the secret is exactly as available as the
/// Windows account that wrote it.
/// </para>
/// <para>
/// <see cref="Tempest.Core.Runtime.TempestHost"/> selects this
/// implementation only inside an <c>OperatingSystem.IsWindows()</c> guard
/// — the pattern <c>Tempest.Core.Identity.SessionPrincipalSource</c>'s own
/// <c>SafeWindowsSid</c> already establishes — and selects
/// <see cref="FileSecretStore"/> everywhere else.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsDpapiSecretStore : ISecretStore
{
    private readonly string _secretsDirectory;

    /// <summary>Initialises a new instance of the <see cref="WindowsDpapiSecretStore"/> class, resolving the secrets directory from <paramref name="configuration"/>.</summary>
    public WindowsDpapiSecretStore(IConfigurationProvider configuration)
        : this(SecretFileNaming.ResolveSecretsDirectory(configuration))
    {
    }

    /// <summary>Initialises a new instance of the <see cref="WindowsDpapiSecretStore"/> class over an explicit secrets directory.</summary>
    /// <remarks>Internal test seam — lets a test point this store at a temporary directory directly, mirroring every other host-fixture's own explicit-root convention.</remarks>
    internal WindowsDpapiSecretStore(string secretsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secretsDirectory);

        _secretsDirectory = secretsDirectory;
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = PathFor(key);

        if (!File.Exists(path))
            return null;

        var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        Directory.CreateDirectory(_secretsDirectory);

        var plainBytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

        await File.WriteAllBytesAsync(PathFor(key), protectedBytes, cancellationToken).ConfigureAwait(false);
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
