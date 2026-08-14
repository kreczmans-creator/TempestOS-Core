using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Tempest.Core.Plugins;

namespace Tempest.Core.Tests.Plugins;

/// <summary>
/// Builds real, self-signed <see cref="X509Certificate2"/> test certificates
/// and real, cryptographically valid ADR-0112 plugin signature envelopes at
/// test time — no mocking of any cryptographic primitive. Mirrors the exact
/// production computation (<see cref="PluginSignatureVerifier"/>,
/// <see cref="PluginManifestDiscoveryService.AssignTrustTier"/> via its
/// private <c>VerifySignature</c> method) so a test-constructed signature
/// round-trips through the real verification path unless a test deliberately
/// tampers with one input.
/// </summary>
internal static class PluginSigningTestHelper
{
    /// <summary>
    /// Creates a real, self-signed RSA certificate with an attached, usable
    /// private key (exportable), suitable both for signing in-test and for
    /// exporting a public-only <c>.cer</c> copy into a trust store folder.
    /// </summary>
    public static X509Certificate2 CreateSelfSignedCertificate(
        string subjectName = "CN=Test Publisher",
        DateTimeOffset? notBefore = null,
        DateTimeOffset? notAfter = null,
        int keySizeBits = 2048)
    {
        using var rsa = RSA.Create(keySizeBits);
        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var effectiveNotBefore = notBefore ?? DateTimeOffset.UtcNow.AddDays(-1);
        var effectiveNotAfter = notAfter ?? DateTimeOffset.UtcNow.AddYears(1);

        using var created = request.CreateSelfSigned(effectiveNotBefore, effectiveNotAfter);

        // Re-import via PFX bytes to obtain a certificate whose private key is
        // independently persisted/exportable, rather than tied to the `rsa`
        // instance's own lifetime (which is disposed at the end of this
        // method) - mirrors the standard .NET pattern for using a
        // CreateSelfSigned result beyond its originating scope.
        return X509CertificateLoader.LoadPkcs12(
            created.Export(X509ContentType.Pfx), password: null, X509KeyStorageFlags.Exportable);
    }

    /// <summary>
    /// Writes <paramref name="certificate"/>'s public-only bytes (no private
    /// key) as a <c>.cer</c> file into <paramref name="trustedPublishersFolder"/>
    /// under <paramref name="fileName"/>, creating the folder if needed.
    /// </summary>
    public static void WriteToTrustStore(string trustedPublishersFolder, X509Certificate2 certificate, string fileName)
    {
        Directory.CreateDirectory(trustedPublishersFolder);
        File.WriteAllBytes(Path.Combine(trustedPublishersFolder, fileName), certificate.Export(X509ContentType.Cert));
    }

    /// <summary>
    /// Builds a fully-populated, valid <see cref="PluginManifestDto"/> for a
    /// candidate plugin - the same shape <see cref="PluginManifestDiscoveryService"/>
    /// itself parses a manifest file into.
    /// </summary>
    public static PluginManifestDto BuildDto(
        string id,
        string assemblyFileName,
        string name = "Signed Plugin",
        string version = "1.0.0",
        string minimumPlatformVersion = "0.1.0",
        IReadOnlyList<string>? requestedCapabilities = null,
        string? publisher = "Test Publisher Ltd.",
        string? signature = null) =>
        new()
        {
            Id = id,
            Name = name,
            Version = version,
            MinimumPlatformVersion = minimumPlatformVersion,
            AssemblyFileName = assemblyFileName,
            RequestedCapabilities = requestedCapabilities,
            Publisher = publisher,
            Signature = signature,
        };

    /// <summary>
    /// Computes and returns a real, valid ADR-0112 <c>Signature</c> envelope
    /// JSON string for <paramref name="dto"/> (whose own <c>Signature</c>
    /// field is ignored/overwritten here) against the assembly bytes at
    /// <paramref name="assemblyPath"/>, signed with
    /// <paramref name="signingCertificate"/>'s own private key — reusing
    /// <see cref="PluginSignatureVerifier"/>'s exact hash/payload computation,
    /// so the result verifies against production's own recomputation
    /// byte-for-byte.
    /// </summary>
    public static string ComputeValidSignatureEnvelopeJson(
        PluginManifestDto dto, string assemblyPath, X509Certificate2 signingCertificate)
    {
        var dtoForHashing = new PluginManifestDto
        {
            Id = dto.Id,
            Name = dto.Name,
            Version = dto.Version,
            MinimumPlatformVersion = dto.MinimumPlatformVersion,
            AssemblyFileName = dto.AssemblyFileName,
            Dependencies = dto.Dependencies,
            RequestedCapabilities = dto.RequestedCapabilities,
            Publisher = dto.Publisher,
            Signature = null,
        };

        var manifestHash = PluginSignatureVerifier.ComputeManifestHash(dtoForHashing);
        var assemblyHash = PluginSignatureVerifier.ComputeAssemblyHash(File.ReadAllBytes(assemblyPath));
        var payload = PluginSignatureVerifier.BuildPayload(manifestHash, assemblyHash);

        return SignPayloadToEnvelopeJson(payload, signingCertificate);
    }

    /// <summary>
    /// Recomputes the exact 64-byte ADR-0112 verification payload for
    /// <paramref name="dto"/> (its own <c>Signature</c> field is ignored) and
    /// the assembly bytes at <paramref name="assemblyPath"/> - the same
    /// computation <see cref="ComputeValidSignatureEnvelopeJson"/> signs and
    /// <c>PluginManifestDiscoveryService</c>'s own verification recomputes,
    /// exposed separately so a caller can recompute against a deliberately
    /// mutated <paramref name="dto"/>/assembly without needing its own
    /// duplicate hashing logic.
    /// </summary>
    public static byte[] ComputePayload(PluginManifestDto dto, string assemblyPath)
    {
        var dtoForHashing = new PluginManifestDto
        {
            Id = dto.Id,
            Name = dto.Name,
            Version = dto.Version,
            MinimumPlatformVersion = dto.MinimumPlatformVersion,
            AssemblyFileName = dto.AssemblyFileName,
            Dependencies = dto.Dependencies,
            RequestedCapabilities = dto.RequestedCapabilities,
            Publisher = dto.Publisher,
            Signature = null,
        };

        var manifestHash = PluginSignatureVerifier.ComputeManifestHash(dtoForHashing);
        var assemblyHash = PluginSignatureVerifier.ComputeAssemblyHash(File.ReadAllBytes(assemblyPath));
        return PluginSignatureVerifier.BuildPayload(manifestHash, assemblyHash);
    }

    /// <summary>
    /// Signs an already-built <paramref name="payload"/> directly (bypassing
    /// manifest/assembly hashing) and returns the resulting envelope JSON -
    /// used by <see cref="PluginSignatureVerifier"/>-level tests that
    /// construct their own 64-byte payload directly.
    /// </summary>
    public static string SignPayloadToEnvelopeJson(byte[] payload, X509Certificate2 signingCertificate)
    {
        using var rsa = signingCertificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Signing certificate has no usable RSA private key.");

        var signatureBytes = rsa.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

        return BuildEnvelopeJson(signingCertificate.Thumbprint, Convert.ToBase64String(signatureBytes));
    }

    /// <summary>
    /// Builds the raw ADR-0112 envelope JSON text directly from already-known
    /// field values - used by malformed/tampered-input tests that need
    /// precise control over the envelope's own shape.
    /// </summary>
    public static string BuildEnvelopeJson(string? thumbprint, string? base64Value, string algorithm = "RSA-SHA256") =>
        JsonSerializer.Serialize(new
        {
            Algorithm = algorithm,
            PublisherCertificateThumbprint = thumbprint,
            Value = base64Value,
        });

    /// <summary>
    /// Serialises <paramref name="dto"/> to the manifest JSON text form
    /// written to disk - plain <see cref="JsonSerializer.Serialize{TValue}(TValue, JsonSerializerOptions?)"/>,
    /// matching what a real plugin author's manifest file would contain.
    /// </summary>
    public static string ToManifestJson(PluginManifestDto dto) =>
        JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
}
