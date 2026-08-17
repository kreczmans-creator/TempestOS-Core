using Tempest.Core.Logging;
using Tempest.Core.Plugins;
using Tempest.Core.Versioning;

namespace Tempest.Core.Tests.Plugins;

// ADR-0112's own full tier-assignment table, exercised end-to-end through
// PluginManifestDiscoveryService against a REAL PluginTrustStore of real,
// self-signed X509Certificate2 certificates:
//
// | Outcome                                                       | Tier             |
// |----------------------------------------------------------------|------------------|
// | No Signature, AllowUnsignedLoad true                           | UnsignedLocal    |
// | No Signature, AllowUnsignedLoad false (default)                | Rejected (cat 16)|
// | Signature verifies, matched cert is TempestOS's own            | FirstParty       |
// | Signature verifies, matched cert is any other trusted entry    | VerifiedSigned   |
// | Signature present but fails to verify (any reason)             | Rejected (cat 15)|
// WP 13.9.1: see PluginAssemblyLoaderTests.cs's own comment on this same
// [Collection] attribute.
[Collection("Console output capture")]
public class PluginTrustTierAssignmentTests
{
    private static readonly IPlatformVersionProvider DefaultVersionProvider =
        new FakePlatformVersionProvider(new Version(1, 0, 0));

    // ------------------------------------------------------------------
    // No Signature field
    // ------------------------------------------------------------------

    [Fact]
    public void NoSignature_AllowUnsignedLoadTrue_AssignsUnsignedLocal()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "unsigned-plugin");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(folder, "Plugin.dll", "test.unsigned", "Unsigned", "1.0.0");
        WriteManifest(folder, PluginSigningTestHelper.BuildDto("test.unsigned", Path.GetFileName(assemblyPath)));

        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, allowUnsignedLoad: true);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal(PluginTrustTier.UnsignedLocal, manifest.TrustTier);
    }

    [Fact]
    public void NoSignature_AllowUnsignedLoadFalse_IsIsolated_LoggedAsWarning_Category16()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "unsigned-plugin");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(folder, "Plugin.dll", "test.unsigned", "Unsigned", "1.0.0");
        WriteManifest(folder, PluginSigningTestHelper.BuildDto("test.unsigned", Path.GetFileName(assemblyPath)));

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger, allowUnsignedLoad: false);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("test.unsigned", StringComparison.Ordinal));
    }

    [Fact]
    public void NoSignature_AllowUnsignedLoadDefaultsToFalse_IsIsolated()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "unsigned-plugin");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(folder, "Plugin.dll", "test.unsigned", "Unsigned", "1.0.0");
        WriteManifest(folder, PluginSigningTestHelper.BuildDto("test.unsigned", Path.GetFileName(assemblyPath)));

        // allowUnsignedLoad not specified - the safe, fail-closed default.
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
    }

    // ------------------------------------------------------------------
    // Signature verifies against a trusted, first-party certificate
    // ------------------------------------------------------------------

    [Fact]
    public void SignatureVerifies_AgainstFirstPartyCertificate_AssignsFirstParty()
    {
        using var temp = new TempDirectory();
        var pluginsRoot = Path.Combine(temp.Path, "plugins");
        var trustFolder = Path.Combine(temp.Path, "trust");

        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=TempestOS");
        PluginSigningTestHelper.WriteToTrustStore(trustFolder, certificate, "TempestOS.cer");

        var folder = CreateCandidateFolder(pluginsRoot, "signed-plugin");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(folder, "Plugin.dll", "test.firstparty", "First Party", "1.0.0");

        var dto = PluginSigningTestHelper.BuildDto("test.firstparty", Path.GetFileName(assemblyPath));
        var signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(dto, assemblyPath, certificate);
        dto.Signature = signature;
        WriteManifest(folder, dto);

        var trustStore = new PluginTrustStore(trustFolder);
        var service = new PluginManifestDiscoveryService(pluginsRoot, DefaultVersionProvider, trustStore: trustStore);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal(PluginTrustTier.FirstParty, manifest.TrustTier);
    }

    // ------------------------------------------------------------------
    // Signature verifies against a trusted, but not first-party, certificate
    // ------------------------------------------------------------------

    [Fact]
    public void SignatureVerifies_AgainstTrustedNonFirstPartyCertificate_AssignsVerifiedSigned()
    {
        using var temp = new TempDirectory();
        var pluginsRoot = Path.Combine(temp.Path, "plugins");
        var trustFolder = Path.Combine(temp.Path, "trust");

        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Acme Plugins Ltd.");
        PluginSigningTestHelper.WriteToTrustStore(trustFolder, certificate, "Acme.cer");

        var folder = CreateCandidateFolder(pluginsRoot, "signed-plugin");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(folder, "Plugin.dll", "test.verified", "Verified", "1.0.0");

        var dto = PluginSigningTestHelper.BuildDto("test.verified", Path.GetFileName(assemblyPath));
        var signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(dto, assemblyPath, certificate);
        dto.Signature = signature;
        WriteManifest(folder, dto);

        var trustStore = new PluginTrustStore(trustFolder);
        var service = new PluginManifestDiscoveryService(pluginsRoot, DefaultVersionProvider, trustStore: trustStore);

        var result = service.DiscoverManifests();

        var manifest = Assert.Single(result);
        Assert.Equal(PluginTrustTier.VerifiedSigned, manifest.TrustTier);
    }

    // ------------------------------------------------------------------
    // Signature present but fails to verify - category 15, never downgraded
    // to UnsignedLocal even when AllowUnsignedLoad is true.
    // ------------------------------------------------------------------

    [Fact]
    public void SignatureThumbprint_NotInTrustStore_IsIsolated_Category15_NeverDowngradedToUnsignedLocal()
    {
        using var temp = new TempDirectory();
        var pluginsRoot = Path.Combine(temp.Path, "plugins");
        var trustFolder = Path.Combine(temp.Path, "trust"); // deliberately left empty

        using var untrustedCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Unknown Publisher");

        var folder = CreateCandidateFolder(pluginsRoot, "signed-plugin");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(folder, "Plugin.dll", "test.untrusted", "Untrusted", "1.0.0");

        var dto = PluginSigningTestHelper.BuildDto("test.untrusted", Path.GetFileName(assemblyPath));
        var signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(dto, assemblyPath, untrustedCertificate);
        dto.Signature = signature;
        WriteManifest(folder, dto);

        Directory.CreateDirectory(trustFolder);
        var trustStore = new PluginTrustStore(trustFolder);
        var logger = new RecordingLevelLogger();

        // AllowUnsignedLoad true is deliberate here: proves a broken
        // signature is never treated as equivalent to "no signature at all",
        // even when unsigned loading would otherwise be permitted.
        var service = new PluginManifestDiscoveryService(
            pluginsRoot, DefaultVersionProvider, logger, trustStore: trustStore, allowUnsignedLoad: true);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("test.untrusted", StringComparison.Ordinal));
    }

    [Fact]
    public void SignatureFailsToParse_IsIsolated_Category15()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "malformed-signature-plugin");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(folder, "Plugin.dll", "test.malformed-sig", "Malformed Sig", "1.0.0");

        var dto = PluginSigningTestHelper.BuildDto("test.malformed-sig", Path.GetFileName(assemblyPath));
        dto.Signature = "{ not a valid envelope";
        WriteManifest(folder, dto);

        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger, allowUnsignedLoad: true);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("test.malformed-sig", StringComparison.Ordinal));
    }

    [Fact]
    public void SignatureVerification_TamperedAssemblyAfterSigning_IsIsolated_Category15()
    {
        using var temp = new TempDirectory();
        var pluginsRoot = Path.Combine(temp.Path, "plugins");
        var trustFolder = Path.Combine(temp.Path, "trust");

        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Acme Plugins Ltd.");
        PluginSigningTestHelper.WriteToTrustStore(trustFolder, certificate, "Acme.cer");

        var folder = CreateCandidateFolder(pluginsRoot, "tampered-plugin");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(folder, "Plugin.dll", "test.tampered", "Tampered", "1.0.0");

        var dto = PluginSigningTestHelper.BuildDto("test.tampered", Path.GetFileName(assemblyPath));
        var signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(dto, assemblyPath, certificate);
        dto.Signature = signature;
        WriteManifest(folder, dto);

        // Tamper with the assembly bytes after signing but before discovery
        // reads them.
        File.AppendAllText(assemblyPath, "tampered-bytes");

        var trustStore = new PluginTrustStore(trustFolder);
        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(pluginsRoot, DefaultVersionProvider, logger, trustStore: trustStore);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("test.tampered", StringComparison.Ordinal));
    }

    [Fact]
    public void SignatureVerification_TamperedManifestAfterSigning_IsIsolated_Category15()
    {
        using var temp = new TempDirectory();
        var pluginsRoot = Path.Combine(temp.Path, "plugins");
        var trustFolder = Path.Combine(temp.Path, "trust");

        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Acme Plugins Ltd.");
        PluginSigningTestHelper.WriteToTrustStore(trustFolder, certificate, "Acme.cer");

        var folder = CreateCandidateFolder(pluginsRoot, "tampered-manifest-plugin");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(folder, "Plugin.dll", "test.tampered-manifest", "Tampered Manifest", "1.0.0");

        var dto = PluginSigningTestHelper.BuildDto("test.tampered-manifest", Path.GetFileName(assemblyPath), publisher: "Original Publisher");
        var signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(dto, assemblyPath, certificate);

        // Mutate a manifest field (Publisher) AFTER signing, but keep the
        // signature computed against the original content - the manifest
        // hash recomputed at Discovery must no longer match.
        dto.Publisher = "Tampered Publisher";
        dto.Signature = signature;
        WriteManifest(folder, dto);

        var trustStore = new PluginTrustStore(trustFolder);
        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(pluginsRoot, DefaultVersionProvider, logger, trustStore: trustStore);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("test.tampered-manifest", StringComparison.Ordinal));
    }

    [Fact]
    public void SignaturePresent_NoTrustStoreConfigured_IsIsolated_Category15()
    {
        using var temp = new TempDirectory();
        var folder = CreateCandidateFolder(temp.Path, "no-store-plugin");
        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(folder, "Plugin.dll", "test.no-store", "No Store", "1.0.0");

        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();
        var dto = PluginSigningTestHelper.BuildDto("test.no-store", Path.GetFileName(assemblyPath));
        var signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(dto, assemblyPath, certificate);
        dto.Signature = signature;
        WriteManifest(folder, dto);

        // trustStore: null - deliberately not configured.
        var logger = new RecordingLevelLogger();
        var service = new PluginManifestDiscoveryService(temp.Path, DefaultVersionProvider, logger, trustStore: null);

        var result = service.DiscoverManifests();

        Assert.Empty(result);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("test.no-store", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // Multiple plugins under different tiers coexist correctly in one
    // discovery pass.
    // ------------------------------------------------------------------

    [Fact]
    public void MixedTierPlugins_EachAssignedItsOwnCorrectTier()
    {
        using var temp = new TempDirectory();
        var pluginsRoot = Path.Combine(temp.Path, "plugins");
        var trustFolder = Path.Combine(temp.Path, "trust");

        using var firstPartyCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=TempestOS");
        using var verifiedCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Acme Plugins Ltd.");
        PluginSigningTestHelper.WriteToTrustStore(trustFolder, firstPartyCertificate, "TempestOS.cer");
        PluginSigningTestHelper.WriteToTrustStore(trustFolder, verifiedCertificate, "Acme.cer");

        var unsignedFolder = CreateCandidateFolder(pluginsRoot, "a-unsigned-plugin");
        var unsignedAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(unsignedFolder, "Plugin.dll", "test.mixed-unsigned", "Unsigned", "1.0.0");
        WriteManifest(unsignedFolder, PluginSigningTestHelper.BuildDto("test.mixed-unsigned", Path.GetFileName(unsignedAssembly)));

        var firstPartyFolder = CreateCandidateFolder(pluginsRoot, "b-firstparty-plugin");
        var firstPartyAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(firstPartyFolder, "Plugin.dll", "test.mixed-firstparty", "First Party", "1.0.0");
        var firstPartyDto = PluginSigningTestHelper.BuildDto("test.mixed-firstparty", Path.GetFileName(firstPartyAssembly));
        firstPartyDto.Signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(firstPartyDto, firstPartyAssembly, firstPartyCertificate);
        WriteManifest(firstPartyFolder, firstPartyDto);

        var verifiedFolder = CreateCandidateFolder(pluginsRoot, "c-verified-plugin");
        var verifiedAssembly = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(verifiedFolder, "Plugin.dll", "test.mixed-verified", "Verified", "1.0.0");
        var verifiedDto = PluginSigningTestHelper.BuildDto("test.mixed-verified", Path.GetFileName(verifiedAssembly));
        verifiedDto.Signature = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(verifiedDto, verifiedAssembly, verifiedCertificate);
        WriteManifest(verifiedFolder, verifiedDto);

        var trustStore = new PluginTrustStore(trustFolder);
        var service = new PluginManifestDiscoveryService(pluginsRoot, DefaultVersionProvider, trustStore: trustStore, allowUnsignedLoad: true);

        var result = service.DiscoverManifests();

        Assert.Equal(3, result.Count);
        Assert.Equal(PluginTrustTier.UnsignedLocal, result.Single(m => m.Id == "test.mixed-unsigned").TrustTier);
        Assert.Equal(PluginTrustTier.FirstParty, result.Single(m => m.Id == "test.mixed-firstparty").TrustTier);
        Assert.Equal(PluginTrustTier.VerifiedSigned, result.Single(m => m.Id == "test.mixed-verified").TrustTier);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string CreateCandidateFolder(string root, string folderName)
    {
        var path = Path.Combine(root, folderName);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteManifest(string candidateFolder, PluginManifestDto dto) =>
        File.WriteAllText(
            Path.Combine(candidateFolder, PluginManifestDiscoveryService.ManifestFileName),
            PluginSigningTestHelper.ToManifestJson(dto));
}
