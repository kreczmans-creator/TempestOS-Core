using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Tempest.Core.Plugins;

/// <summary>
/// Pure cryptography for plugin signature verification (ADR-0112) — no file
/// I/O of its own. The caller (Plugin Discovery, Phase 3.1) reads manifest
/// and assembly bytes from disk and calls in.
/// </summary>
/// <remarks>
/// Uses only <c>System.Security.Cryptography</c> and <c>System.Text.Json</c>
/// — both already part of .NET. Zero new NuGet dependency, per ADR-0005's
/// reuse-first mandate.
/// </remarks>
public static class PluginSignatureVerifier
{
    /// <summary>
    /// Computes <c>hash1</c>: <c>SHA-256(canonical UTF-8 manifest JSON,
    /// Signature field omitted, System.Text.Json default member-declaration
    /// ordering, no indentation)</c> (ADR-0112, "Payload and verification").
    /// </summary>
    /// <param name="manifestDtoWithSignatureFieldNulled">
    /// The already-parsed manifest DTO, with its own <c>Signature</c>
    /// property set to <see langword="null"/> before calling this method.
    /// Re-serialises FROM this parsed DTO (never re-hashes raw file bytes)
    /// so a signer's and a verifier's own independent re-serialisation of
    /// the identical logical manifest content always produce byte-identical
    /// output, regardless of incidental formatting differences in the file
    /// actually on disk. The caller (Plugin Discovery) supplies this DTO —
    /// this method never reads a file itself.
    /// </param>
    /// <returns>The 32-byte SHA-256 hash.</returns>
    /// <remarks>
    /// Uses the object's own runtime <see cref="object.GetType"/> (not a
    /// generic type parameter) so this method has no compile-time
    /// dependency on the sibling Runtime Integration agent's own manifest
    /// DTO type, avoiding a cross-agent compile-order coupling.
    /// </remarks>
    public static byte[] ComputeManifestHash(object manifestDtoWithSignatureFieldNulled)
    {
        ArgumentNullException.ThrowIfNull(manifestDtoWithSignatureFieldNulled);

        var json = JsonSerializer.SerializeToUtf8Bytes(
            manifestDtoWithSignatureFieldNulled,
            manifestDtoWithSignatureFieldNulled.GetType(),
            new JsonSerializerOptions { WriteIndented = false });

        return SHA256.HashData(json);
    }

    /// <summary>
    /// Computes <c>hash2</c>: <c>SHA-256(raw bytes of the declared assembly
    /// file)</c> (ADR-0112, "Payload and verification").
    /// </summary>
    /// <param name="assemblyBytes">The declared assembly file's raw bytes.</param>
    /// <returns>The 32-byte SHA-256 hash.</returns>
    public static byte[] ComputeAssemblyHash(byte[] assemblyBytes)
    {
        ArgumentNullException.ThrowIfNull(assemblyBytes);

        return SHA256.HashData(assemblyBytes);
    }

    /// <summary>
    /// Builds the 64-byte verification payload: <c>payload = hash1 ++
    /// hash2</c>, manifest hash first (ADR-0112, "Payload and
    /// verification").
    /// </summary>
    /// <param name="manifestHash">The 32-byte manifest hash (<c>hash1</c>).</param>
    /// <param name="assemblyHash">The 32-byte assembly hash (<c>hash2</c>).</param>
    /// <returns>The 64-byte concatenated payload, manifest hash first.</returns>
    public static byte[] BuildPayload(byte[] manifestHash, byte[] assemblyHash)
    {
        ArgumentNullException.ThrowIfNull(manifestHash);
        ArgumentNullException.ThrowIfNull(assemblyHash);

        var payload = new byte[manifestHash.Length + assemblyHash.Length];
        Buffer.BlockCopy(manifestHash, 0, payload, 0, manifestHash.Length);
        Buffer.BlockCopy(assemblyHash, 0, payload, manifestHash.Length, assemblyHash.Length);
        return payload;
    }

    /// <summary>
    /// Verifies <paramref name="base64SignatureValue"/> against
    /// <paramref name="payload"/> and <paramref name="certificate"/>'s
    /// public key: <c>RSA.VerifyData(payload, signatureBytes,
    /// HashAlgorithmName.SHA256, RSASignaturePadding.Pss)</c>, and confirms
    /// the certificate's validity window covers "now" (ADR-0112, "Payload
    /// and verification": "confirm <c>NotBefore</c>/<c>NotAfter</c> covers
    /// 'now'"). <see cref="X509Certificate2.NotBefore"/>/
    /// <see cref="X509Certificate2.NotAfter"/> are local-time
    /// <see cref="DateTime"/> values — <see cref="DateTime.Now"/> is used,
    /// not <see cref="DateTime.UtcNow"/>, to match.
    /// </summary>
    /// <param name="payload">The 64-byte payload built by <see cref="BuildPayload"/>.</param>
    /// <param name="base64SignatureValue">
    /// The envelope's own Base64-encoded <c>Value</c> field.
    /// </param>
    /// <param name="certificate">
    /// The trust-store-matched publisher certificate to verify against.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the signature verifies against the
    /// certificate's public key and the certificate's validity window
    /// covers "now"; <see langword="false"/> for any exception during
    /// verification (a malformed Base64 <c>Value</c>, an unusable public
    /// key) — never throws.
    /// </returns>
    public static bool Verify(byte[] payload, string base64SignatureValue, X509Certificate2 certificate)
    {
        // Deliberately no ArgumentNullException guards here — every failure
        // mode, including a null argument, is caught by the outer catch
        // below and reported as `false`, per this method's own "never
        // throws" contract (ADR-0112).
        try
        {
            byte[] signatureBytes;
            try
            {
                signatureBytes = Convert.FromBase64String(base64SignatureValue);
            }
            catch (FormatException)
            {
                return false;
            }

            var now = DateTime.Now;
            if (now < certificate.NotBefore || now > certificate.NotAfter)
                return false;

            using var rsa = certificate.GetRSAPublicKey();
            if (rsa is null)
                return false;

            return rsa.VerifyData(payload, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch
        {
            return false;
        }
    }
}
