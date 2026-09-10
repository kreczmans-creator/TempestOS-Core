using System.Runtime.Versioning;
using System.Text;
using Tempest.Core.Persistence;
using Tempest.Core.Secrets;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// `ISecretStore`'s own acceptance (`WP 19.1A` #4, `ADR-0151`): a stored
/// token round-trips through <see cref="WindowsDpapiSecretStore"/>, and
/// never appears — even as plaintext bytes inside an otherwise-unrelated
/// value — in <c>tempest.db</c>.
/// </summary>
/// <remarks>Windows-only, mirroring <c>WindowsDpapiSecretStore</c>'s own <see cref="SupportedOSPlatformAttribute"/> — this platform's build and CI are Windows (`WP 19.1A`'s own row).</remarks>
[SupportedOSPlatform("windows")]
public sealed class SecretStoreTests
{
    [Fact]
    public async Task WindowsDpapiSecretStore_RoundTrips_GetSetRemove()
    {
        using var temp = new TempDirectory();
        var store = new WindowsDpapiSecretStore(Path.Combine(temp.Path, "secrets"));

        const string key = "Xero:AccessToken";
        const string value = "dpapi-round-trip-marker-7f3a9c";

        Assert.Null(await store.GetAsync(key));

        await store.SetAsync(key, value);
        Assert.Equal(value, await store.GetAsync(key));

        // Overwrite.
        await store.SetAsync(key, value + "-revised");
        Assert.Equal(value + "-revised", await store.GetAsync(key));

        await store.RemoveAsync(key);
        Assert.Null(await store.GetAsync(key));

        // Removing an absent key is a no-op, not a failure.
        await store.RemoveAsync(key);
    }

    [Fact]
    public async Task WindowsDpapiSecretStore_EncryptsOnDisk_TheFileNeverCarriesThePlaintext()
    {
        using var temp = new TempDirectory();
        var secretsDirectory = Path.Combine(temp.Path, "secrets");
        var store = new WindowsDpapiSecretStore(secretsDirectory);

        const string key = "QuickBooksOnline:RefreshToken";
        const string value = "plaintext-must-not-appear-on-disk-b91e4d";

        await store.SetAsync(key, value);

        var files = Directory.GetFiles(secretsDirectory);
        var file = Assert.Single(files);

        var onDisk = await File.ReadAllBytesAsync(file);
        var plaintext = Encoding.UTF8.GetBytes(value);

        Assert.True(onDisk.AsSpan().IndexOf(plaintext) < 0, "DPAPI-protected bytes on disk must not contain the plaintext secret.");

        // And the file name is not the key itself (SecretFileNaming's own
        // hash-not-key rule).
        Assert.DoesNotContain(key, Path.GetFileName(file), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStoredSecret_NeverAppearsInTempestDb()
    {
        using var temp = new TempDirectory();

        const string key = "Xero:AccessToken";
        const string secretValue = "must-never-reach-tempest-db-c48f21a9";

        var store = new WindowsDpapiSecretStore(Path.Combine(temp.Path, "secrets"));
        await store.SetAsync(key, secretValue);

        // Drive a real host over the same persistence root, so
        // tempest.db actually exists with real, committed engineering
        // data to grep.
        var (host, manager) = await InvoicingTestHost.StartAsync(temp.Path);
        InvoicingTestHost.SignIn(host);
        await InvoicingTestHost.CreateProjectAsync(host, "SECRET-STORE-PROBE");
        await manager.ShutdownAsync();
        await host.DisposeAsync();

        var databasePath = Path.Combine(temp.Path, SqlitePersistenceStore.DatabaseFileName);
        Assert.True(File.Exists(databasePath));

        var databaseBytes = await File.ReadAllBytesAsync(databasePath);
        var secretBytes = Encoding.UTF8.GetBytes(secretValue);

        Assert.True(
            databaseBytes.AsSpan().IndexOf(secretBytes) < 0,
            "The secret's own plaintext bytes were found inside tempest.db.");
    }
}
