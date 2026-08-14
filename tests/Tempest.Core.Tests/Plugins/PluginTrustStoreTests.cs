using Tempest.Core.Logging;
using Tempest.Core.Plugins;

namespace Tempest.Core.Tests.Plugins;

// ADR-0112, "Trust store and tier assignment": PluginTrustStore reading a
// real TrustedPublishers/ folder of real X509Certificate2 .cer files -
// no folder, empty folder, one ordinary trusted cert, the fixed
// "TempestOS.cer" first-party filename convention, and a malformed .cer
// file (skipped, logged Warning, never thrown).
public class PluginTrustStoreTests
{
    // ------------------------------------------------------------------
    // No folder at all - a valid, empty store
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_NoTrustedPublishersFolder_ProducesEmptyStore()
    {
        using var temp = new TempDirectory();
        var folderPath = Path.Combine(temp.Path, "does-not-exist");

        var store = new PluginTrustStore(folderPath);

        Assert.Null(store.FindByThumbprint("ANYTHING"));
        Assert.False(store.IsFirstPartyThumbprint("ANYTHING"));
    }

    [Fact]
    public void Constructor_NoTrustedPublishersFolder_LogsInformation_DoesNotThrow()
    {
        using var temp = new TempDirectory();
        var folderPath = Path.Combine(temp.Path, "does-not-exist");
        var logger = new RecordingLevelLogger();

        var exception = Record.Exception(() => new PluginTrustStore(folderPath, logger));

        Assert.Null(exception);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information);
    }

    // ------------------------------------------------------------------
    // Empty folder - also a valid, empty store
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_EmptyTrustedPublishersFolder_ProducesEmptyStore()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(temp.Path);

        var store = new PluginTrustStore(temp.Path);

        Assert.Null(store.FindByThumbprint("ANYTHING"));
        Assert.False(store.IsFirstPartyThumbprint("ANYTHING"));
    }

    // ------------------------------------------------------------------
    // One ordinary trusted certificate - found by thumbprint, not first-party
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_OneOrdinaryCertificate_IsFoundByThumbprint_NotFirstParty()
    {
        using var temp = new TempDirectory();
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Acme Plugins Ltd.");
        PluginSigningTestHelper.WriteToTrustStore(temp.Path, certificate, "Acme.cer");

        var store = new PluginTrustStore(temp.Path);

        var found = store.FindByThumbprint(certificate.Thumbprint);
        Assert.NotNull(found);
        Assert.Equal(certificate.Thumbprint, found!.Thumbprint);
        Assert.False(store.IsFirstPartyThumbprint(certificate.Thumbprint));
    }

    [Fact]
    public void FindByThumbprint_ThumbprintComparisonIsCaseInsensitive()
    {
        using var temp = new TempDirectory();
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();
        PluginSigningTestHelper.WriteToTrustStore(temp.Path, certificate, "Acme.cer");

        var store = new PluginTrustStore(temp.Path);

        var found = store.FindByThumbprint(certificate.Thumbprint.ToLowerInvariant());

        Assert.NotNull(found);
    }

    [Fact]
    public void FindByThumbprint_UnknownThumbprint_ReturnsNull()
    {
        using var temp = new TempDirectory();
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();
        PluginSigningTestHelper.WriteToTrustStore(temp.Path, certificate, "Acme.cer");

        var store = new PluginTrustStore(temp.Path);

        Assert.Null(store.FindByThumbprint("0000000000000000000000000000000000000000"));
    }

    // ------------------------------------------------------------------
    // The fixed "TempestOS.cer" first-party filename convention
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_TempestOSCerFile_IsFlaggedFirstParty()
    {
        using var temp = new TempDirectory();
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=TempestOS");
        PluginSigningTestHelper.WriteToTrustStore(temp.Path, certificate, "TempestOS.cer");

        var store = new PluginTrustStore(temp.Path);

        Assert.True(store.IsFirstPartyThumbprint(certificate.Thumbprint));
        Assert.NotNull(store.FindByThumbprint(certificate.Thumbprint));
    }

    [Fact]
    public void Constructor_TempestOSCerFileNameIsCaseInsensitive_StillFlaggedFirstParty()
    {
        using var temp = new TempDirectory();
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=TempestOS");
        PluginSigningTestHelper.WriteToTrustStore(temp.Path, certificate, "tempestos.CER");

        var store = new PluginTrustStore(temp.Path);

        Assert.True(store.IsFirstPartyThumbprint(certificate.Thumbprint));
    }

    [Fact]
    public void Constructor_OrdinaryCertificateAlongsideTempestOSCert_OnlyTempestOSCertIsFirstParty()
    {
        using var temp = new TempDirectory();
        using var firstPartyCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=TempestOS");
        using var ordinaryCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Acme Plugins Ltd.");

        PluginSigningTestHelper.WriteToTrustStore(temp.Path, firstPartyCertificate, "TempestOS.cer");
        PluginSigningTestHelper.WriteToTrustStore(temp.Path, ordinaryCertificate, "Acme.cer");

        var store = new PluginTrustStore(temp.Path);

        Assert.True(store.IsFirstPartyThumbprint(firstPartyCertificate.Thumbprint));
        Assert.False(store.IsFirstPartyThumbprint(ordinaryCertificate.Thumbprint));
        Assert.NotNull(store.FindByThumbprint(firstPartyCertificate.Thumbprint));
        Assert.NotNull(store.FindByThumbprint(ordinaryCertificate.Thumbprint));
    }

    [Fact]
    public void IsFirstPartyThumbprint_NoTempestOSCerFilePresent_AlwaysReturnsFalse()
    {
        using var temp = new TempDirectory();
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Acme Plugins Ltd.");
        PluginSigningTestHelper.WriteToTrustStore(temp.Path, certificate, "Acme.cer");

        var store = new PluginTrustStore(temp.Path);

        Assert.False(store.IsFirstPartyThumbprint(certificate.Thumbprint));
    }

    // ------------------------------------------------------------------
    // Malformed .cer file - skipped, logged Warning, never thrown
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_MalformedCerFile_IsSkipped_LoggedAsWarning_DoesNotThrow()
    {
        using var temp = new TempDirectory();
        File.WriteAllBytes(Path.Combine(temp.Path, "Broken.cer"), "this is not a valid certificate"u8.ToArray());

        var logger = new RecordingLevelLogger();

        var exception = Record.Exception(() => new PluginTrustStore(temp.Path, logger));

        Assert.Null(exception);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("Broken.cer", StringComparison.Ordinal));
    }

    [Fact]
    public void Constructor_MalformedCerFileAlongsideValidOne_ValidOneStillLoads()
    {
        using var temp = new TempDirectory();
        File.WriteAllBytes(Path.Combine(temp.Path, "a-Broken.cer"), "not a certificate"u8.ToArray());

        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();
        PluginSigningTestHelper.WriteToTrustStore(temp.Path, certificate, "b-Valid.cer");

        var store = new PluginTrustStore(temp.Path);

        Assert.NotNull(store.FindByThumbprint(certificate.Thumbprint));
    }

    // ------------------------------------------------------------------
    // Non-.cer files in the folder are ignored entirely
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_NonCerFilesInFolder_AreIgnored()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "readme.txt"), "not a certificate at all");

        var store = new PluginTrustStore(temp.Path);

        Assert.Null(store.FindByThumbprint("ANYTHING"));
    }

    // ------------------------------------------------------------------
    // Argument guards
    // ------------------------------------------------------------------

    [Fact]
    public void FindByThumbprint_NullOrWhitespaceThumbprint_ThrowsArgumentException()
    {
        using var temp = new TempDirectory();
        var store = new PluginTrustStore(temp.Path);

        Assert.Throws<ArgumentException>(() => store.FindByThumbprint(""));
        Assert.Throws<ArgumentException>(() => store.FindByThumbprint("   "));
    }

    [Fact]
    public void IsFirstPartyThumbprint_NullOrWhitespaceThumbprint_ThrowsArgumentException()
    {
        using var temp = new TempDirectory();
        var store = new PluginTrustStore(temp.Path);

        Assert.Throws<ArgumentException>(() => store.IsFirstPartyThumbprint(""));
        Assert.Throws<ArgumentException>(() => store.IsFirstPartyThumbprint("   "));
    }
}
