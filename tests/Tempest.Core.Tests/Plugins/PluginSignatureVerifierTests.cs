using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Tempest.Core.Plugins;

namespace Tempest.Core.Tests.Plugins;

// ADR-0112, "Payload and verification": PluginSignatureVerifier's own pure
// cryptography, exercised end-to-end against a REAL, self-signed
// X509Certificate2 built at test time (CertificateRequest, RSA-PSS/SHA-256)
// - both the successful round trip and every tamper-detection path
// (mutated assembly bytes, mutated manifest, wrong certificate, expired
// certificate, malformed Base64 Value). No mocking of any cryptographic
// primitive anywhere in this file.
public class PluginSignatureVerifierTests
{
    // ------------------------------------------------------------------
    // Successful round trip
    // ------------------------------------------------------------------

    [Fact]
    public void Verify_ValidSignatureAgainstMatchingCertificateAndPayload_ReturnsTrue()
    {
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();
        var payload = BuildSamplePayload();

        var (base64Signature, _) = SignPayload(payload, certificate);

        Assert.True(PluginSignatureVerifier.Verify(payload, base64Signature, certificate));
    }

    [Fact]
    public void ComputeManifestHash_SameLogicalDto_ProducesByteIdenticalHash_RegardlessOfInstance()
    {
        var dtoA = PluginSigningTestHelper.BuildDto("test.a", "A.dll");
        var dtoB = PluginSigningTestHelper.BuildDto("test.a", "A.dll");

        var hashA = PluginSignatureVerifier.ComputeManifestHash(dtoA);
        var hashB = PluginSignatureVerifier.ComputeManifestHash(dtoB);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void ComputeManifestHash_DifferentContent_ProducesDifferentHash()
    {
        var dtoA = PluginSigningTestHelper.BuildDto("test.a", "A.dll");
        var dtoB = PluginSigningTestHelper.BuildDto("test.b", "A.dll");

        var hashA = PluginSignatureVerifier.ComputeManifestHash(dtoA);
        var hashB = PluginSignatureVerifier.ComputeManifestHash(dtoB);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void ComputeAssemblyHash_SameBytes_ProducesSameHash()
    {
        var bytes = "some assembly bytes"u8.ToArray();

        var hashA = PluginSignatureVerifier.ComputeAssemblyHash(bytes);
        var hashB = PluginSignatureVerifier.ComputeAssemblyHash(bytes);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void BuildPayload_ConcatenatesManifestHashFirstThenAssemblyHash()
    {
        var manifestHash = SHA256.HashData("manifest"u8.ToArray());
        var assemblyHash = SHA256.HashData("assembly"u8.ToArray());

        var payload = PluginSignatureVerifier.BuildPayload(manifestHash, assemblyHash);

        Assert.Equal(64, payload.Length);
        Assert.Equal(manifestHash, payload[..32]);
        Assert.Equal(assemblyHash, payload[32..]);
    }

    // ------------------------------------------------------------------
    // Tamper detection
    // ------------------------------------------------------------------

    [Fact]
    public void Verify_MutatedAssemblyBytes_ReturnsFalse()
    {
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();

        var manifestHash = SHA256.HashData("manifest-content"u8.ToArray());
        var originalAssemblyHash = PluginSignatureVerifier.ComputeAssemblyHash("original assembly bytes"u8.ToArray());
        var tamperedAssemblyHash = PluginSignatureVerifier.ComputeAssemblyHash("tampered assembly bytes!"u8.ToArray());

        var signedPayload = PluginSignatureVerifier.BuildPayload(manifestHash, originalAssemblyHash);
        var (base64Signature, _) = SignPayload(signedPayload, certificate);

        var recomputedPayload = PluginSignatureVerifier.BuildPayload(manifestHash, tamperedAssemblyHash);

        Assert.False(PluginSignatureVerifier.Verify(recomputedPayload, base64Signature, certificate));
    }

    [Fact]
    public void Verify_MutatedManifestHash_ReturnsFalse()
    {
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();

        var originalManifestHash = SHA256.HashData("original manifest"u8.ToArray());
        var tamperedManifestHash = SHA256.HashData("tampered manifest!!"u8.ToArray());
        var assemblyHash = SHA256.HashData("assembly"u8.ToArray());

        var signedPayload = PluginSignatureVerifier.BuildPayload(originalManifestHash, assemblyHash);
        var (base64Signature, _) = SignPayload(signedPayload, certificate);

        var recomputedPayload = PluginSignatureVerifier.BuildPayload(tamperedManifestHash, assemblyHash);

        Assert.False(PluginSignatureVerifier.Verify(recomputedPayload, base64Signature, certificate));
    }

    [Fact]
    public void Verify_SignatureFromADifferentCertificate_ReturnsFalse()
    {
        using var signingCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Signer");
        using var otherCertificate = PluginSigningTestHelper.CreateSelfSignedCertificate("CN=Impostor");

        var payload = BuildSamplePayload();
        var (base64Signature, _) = SignPayload(payload, signingCertificate);

        Assert.False(PluginSignatureVerifier.Verify(payload, base64Signature, otherCertificate));
    }

    [Fact]
    public void Verify_ExpiredCertificate_ReturnsFalse()
    {
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate(
            notBefore: DateTimeOffset.UtcNow.AddYears(-2),
            notAfter: DateTimeOffset.UtcNow.AddDays(-1));

        var payload = BuildSamplePayload();
        var (base64Signature, _) = SignPayload(payload, certificate);

        Assert.False(PluginSignatureVerifier.Verify(payload, base64Signature, certificate));
    }

    [Fact]
    public void Verify_NotYetValidCertificate_ReturnsFalse()
    {
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate(
            notBefore: DateTimeOffset.UtcNow.AddDays(30),
            notAfter: DateTimeOffset.UtcNow.AddYears(1));

        var payload = BuildSamplePayload();
        var (base64Signature, _) = SignPayload(payload, certificate);

        Assert.False(PluginSignatureVerifier.Verify(payload, base64Signature, certificate));
    }

    [Fact]
    public void Verify_MalformedBase64Value_ReturnsFalse_DoesNotThrow()
    {
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();
        var payload = BuildSamplePayload();

        var exception = Record.Exception(() => PluginSignatureVerifier.Verify(payload, "not-valid-base64!!!", certificate));

        Assert.Null(exception);
        Assert.False(PluginSignatureVerifier.Verify(payload, "not-valid-base64!!!", certificate));
    }

    [Fact]
    public void Verify_EmptyValue_ReturnsFalse_DoesNotThrow()
    {
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();
        var payload = BuildSamplePayload();

        Assert.False(PluginSignatureVerifier.Verify(payload, string.Empty, certificate));
    }

    [Fact]
    public void Verify_ValidBase64ButNotAValidSignature_ReturnsFalse()
    {
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();
        var payload = BuildSamplePayload();

        var garbageButValidBase64 = Convert.ToBase64String("this is not a real signature"u8.ToArray());

        Assert.False(PluginSignatureVerifier.Verify(payload, garbageButValidBase64, certificate));
    }

    // ------------------------------------------------------------------
    // Full manifest+assembly signing round trip, via the production
    // ComputeManifestHash/ComputeAssemblyHash/BuildPayload pipeline exactly
    // as PluginManifestDiscoveryService's own VerifySignature calls it.
    // ------------------------------------------------------------------

    [Fact]
    public void FullPipeline_SignedManifestAndAssembly_VerifiesAgainstRecomputedPayload()
    {
        using var temp = new TempDirectory();
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();

        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Signed.dll", "test.signed", "Signed Plugin", "1.0.0");

        var dto = PluginSigningTestHelper.BuildDto("test.signed", "Signed.dll");
        var envelopeJson = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(dto, assemblyPath, certificate);
        var envelope = PluginSignatureEnvelope.TryParse(envelopeJson)!;

        var payload = PluginSigningTestHelper.ComputePayload(dto, assemblyPath);

        Assert.True(PluginSignatureVerifier.Verify(payload, envelope.Value, certificate));
    }

    [Fact]
    public void FullPipeline_TamperedAssemblyAfterSigning_FailsVerification()
    {
        using var temp = new TempDirectory();
        using var certificate = PluginSigningTestHelper.CreateSelfSignedCertificate();

        var assemblyPath = DynamicPluginAssemblyBuilder.BuildValidPluginAssembly(
            temp.Path, "Signed.dll", "test.signed", "Signed Plugin", "1.0.0");

        var dto = PluginSigningTestHelper.BuildDto("test.signed", "Signed.dll");
        var envelopeJson = PluginSigningTestHelper.ComputeValidSignatureEnvelopeJson(dto, assemblyPath, certificate);
        var envelope = PluginSignatureEnvelope.TryParse(envelopeJson)!;

        // Tamper with the assembly bytes on disk after signing.
        File.AppendAllText(assemblyPath, "tampered");

        var payload = PluginSigningTestHelper.ComputePayload(dto, assemblyPath);

        Assert.False(PluginSignatureVerifier.Verify(payload, envelope.Value, certificate));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static byte[] BuildSamplePayload()
    {
        var manifestHash = SHA256.HashData("sample-manifest"u8.ToArray());
        var assemblyHash = SHA256.HashData("sample-assembly"u8.ToArray());
        return PluginSignatureVerifier.BuildPayload(manifestHash, assemblyHash);
    }

    private static (string Base64Signature, byte[] SignatureBytes) SignPayload(byte[] payload, X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPrivateKey()!;
        var signatureBytes = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        return (Convert.ToBase64String(signatureBytes), signatureBytes);
    }
}
