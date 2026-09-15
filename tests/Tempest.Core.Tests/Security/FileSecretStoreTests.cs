using System.Text;
using Tempest.Core.Secrets;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Security;

/// <summary>
/// <see cref="FileSecretStore"/>'s own acceptance — the non-Windows
/// <see cref="ISecretStore"/> fallback had zero tests of any kind before
/// `WP 21.5F` (Offensive Security Audit, OSA-04): not its round trip, not
/// its disclosure warning, and not (the actual finding) any check that it
/// restricts the files it writes to the current user rather than relying
/// on whatever default permission happened to apply.
/// </summary>
public sealed class FileSecretStoreTests
{
    [Fact]
    public async Task RoundTrips_GetSetRemove()
    {
        using var temp = new TempDirectory();
        var store = new FileSecretStore(temp.Path);

        Assert.Null(await store.GetAsync("k"));

        await store.SetAsync("k", "v1");
        Assert.Equal("v1", await store.GetAsync("k"));

        await store.SetAsync("k", "v2");
        Assert.Equal("v2", await store.GetAsync("k"));

        await store.RemoveAsync("k");
        Assert.Null(await store.GetAsync("k"));

        // Removing an absent key is a no-op, not a failure.
        await store.RemoveAsync("k");
    }

    [Fact]
    public async Task Construction_LogsAWarning_DisclosingTheUnencryptedState()
    {
        using var temp = new TempDirectory();
        var logger = new Tempest.Core.Tests.ExportImport.RecordingLevelLogger();

        _ = new FileSecretStore(temp.Path, logger);

        Assert.True(logger.HasEntryAt(Tempest.Core.Logging.LogLevel.Warning, "unencrypted"));
        Assert.True(logger.HasEntryAt(Tempest.Core.Logging.LogLevel.Warning, temp.Path));
    }

    [Fact]
    public async Task SetAsync_WritesThePlaintextValue_ItsOwnDisclosedAndDocumentedBehaviour()
    {
        // Unlike WindowsDpapiSecretStore, FileSecretStore's whole contract
        // is "plain, unencrypted files" (its own doc comment) - this pins
        // that documented behaviour rather than treating it as a defect,
        // so a future change that started silently obfuscating (without
        // becoming genuine encryption) would not go unnoticed either.
        using var temp = new TempDirectory();
        var store = new FileSecretStore(temp.Path);

        await store.SetAsync("k", "the-plaintext-value-9f2c");

        var file = Assert.Single(Directory.GetFiles(temp.Path));
        var bytes = await File.ReadAllBytesAsync(file);
        Assert.True(
            bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes("the-plaintext-value-9f2c")) >= 0,
            "The plaintext value's own bytes were not found in the secret file.");
    }

    [Fact]
    public async Task SetAsync_TheFileNameIsAHashOfTheKey_NeverTheKeyItself()
    {
        using var temp = new TempDirectory();
        var store = new FileSecretStore(temp.Path);

        await store.SetAsync("Invoicing:Xero:AccessToken", "v");

        var file = Assert.Single(Directory.GetFiles(temp.Path));
        Assert.DoesNotContain("Xero", System.IO.Path.GetFileName(file), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccessToken", System.IO.Path.GetFileName(file), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SecretFileNaming.FileNameFor("Invoicing:Xero:AccessToken"), System.IO.Path.GetFileName(file));
    }

    [Fact]
    public async Task SetAsync_OnNonWindows_RestrictsTheSecretsDirectoryAndFile_ToTheCurrentUserOnly()
    {
        // `WP 21.5F` Offensive Security Audit, OSA-04: before this fix,
        // neither the secrets directory nor a secret file had any
        // permission restriction this class itself applied - "a file
        // system permission is the only protection a connector token has
        // here" (this class's own doc comment) was aspirational, not
        // enforced. This platform's own CI is Windows-only (`WP 19.1A`'s
        // own row), so this assertion can only run for real on a
        // contributor's own Linux/macOS machine - it still exercises the
        // Windows branch (the method must not throw
        // PlatformNotSupportedException there) either way.
        using var temp = new TempDirectory();
        var store = new FileSecretStore(temp.Path);

        await store.SetAsync("k", "v");

        if (OperatingSystem.IsWindows())
            return;

        var directoryMode = File.GetUnixFileMode(temp.Path);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, directoryMode);

        var file = Assert.Single(Directory.GetFiles(temp.Path));
        var fileMode = File.GetUnixFileMode(file);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, fileMode);
    }
}
