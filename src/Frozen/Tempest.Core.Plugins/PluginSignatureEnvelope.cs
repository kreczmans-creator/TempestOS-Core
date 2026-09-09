using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tempest.Core.Plugins;

/// <summary>
/// The small, self-contained JSON envelope carried inside
/// <see cref="PluginManifest.Signature"/>'s own opaque string value.
/// </summary>
/// <remarks>
/// ADR-0112, "Manifest fields — reconciled against the sibling's already-
/// reserved shape": <c>PluginManifest.Signature</c> is a plain
/// <c>string?</c> (the sibling Plugin Architecture workstream's own manifest
/// v2 shape) whose value is itself this JSON envelope —
/// <c>{"Algorithm":"RSA-SHA256","PublisherCertificateThumbprint":"&lt;SHA-256
/// hex, 64 chars&gt;","Value":"&lt;Base64-encoded signature bytes&gt;"}</c>.
/// Plugin Discovery otherwise treats <c>Signature</c> exactly as the sibling
/// document already states — read, not interpreted; only this type's own
/// <see cref="TryParse"/> parses it.
/// </remarks>
public sealed class PluginSignatureEnvelope
{
    /// <summary>
    /// Initialises a new instance of the <see cref="PluginSignatureEnvelope"/>
    /// class.
    /// </summary>
    /// <param name="algorithm">The signature algorithm — <c>"RSA-SHA256"</c>.</param>
    /// <param name="publisherCertificateThumbprint">
    /// The signing publisher certificate's SHA-256 thumbprint, hex, 64
    /// characters.
    /// </param>
    /// <param name="value">The Base64-encoded signature bytes.</param>
    /// <exception cref="ArgumentException">
    /// Any parameter is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public PluginSignatureEnvelope(string algorithm, string publisherCertificateThumbprint, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(publisherCertificateThumbprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Algorithm = algorithm;
        PublisherCertificateThumbprint = publisherCertificateThumbprint;
        Value = value;
    }

    /// <summary>Gets the signature algorithm — <c>"RSA-SHA256"</c>.</summary>
    public string Algorithm { get; }

    /// <summary>
    /// Gets the signing publisher certificate's SHA-256 thumbprint, hex, 64
    /// characters — matched against <see cref="IPluginTrustStore"/> at
    /// verification.
    /// </summary>
    public string PublisherCertificateThumbprint { get; }

    /// <summary>Gets the Base64-encoded signature bytes.</summary>
    public string Value { get; }

    /// <summary>
    /// Parses <paramref name="rawSignatureFieldValue"/> (<see cref="PluginManifest"/>'s
    /// own raw <c>Signature</c> string) as this envelope. Returns
    /// <see langword="null"/> — never throws — on any parse failure
    /// (malformed JSON, a missing/blank required property): ADR-0112 states
    /// a <c>Signature</c> that fails to parse as the expected JSON envelope
    /// is treated identically to a signature that fails cryptographic
    /// verification (category 15) — the caller (Plugin Discovery, owned by
    /// the sibling Runtime Integration agent) is responsible for raising
    /// that category; this method only reports parse success/failure.
    /// </summary>
    /// <param name="rawSignatureFieldValue">
    /// The raw <c>Signature</c> field value read from a plugin manifest.
    /// </param>
    /// <returns>
    /// The parsed envelope, or <see langword="null"/> if
    /// <paramref name="rawSignatureFieldValue"/> does not parse as a
    /// well-formed envelope with all three fields present and non-blank.
    /// </returns>
    public static PluginSignatureEnvelope? TryParse(string rawSignatureFieldValue)
    {
        if (string.IsNullOrWhiteSpace(rawSignatureFieldValue))
            return null;

        try
        {
            var dto = JsonSerializer.Deserialize<EnvelopeDto>(
                rawSignatureFieldValue,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (dto is null)
                return null;

            if (string.IsNullOrWhiteSpace(dto.Algorithm) ||
                string.IsNullOrWhiteSpace(dto.PublisherCertificateThumbprint) ||
                string.IsNullOrWhiteSpace(dto.Value))
            {
                return null;
            }

            return new PluginSignatureEnvelope(dto.Algorithm, dto.PublisherCertificateThumbprint, dto.Value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The wire-shape DTO <see cref="TryParse"/> deserialises into, kept
    /// private and separate from the immutable, guard-validated public type
    /// above — mirrors this codebase's existing manifest-DTO/domain-type
    /// split (<see cref="PluginManifestDto"/>/<see cref="PluginManifest"/>).
    /// </summary>
    private sealed class EnvelopeDto
    {
        [JsonPropertyName("Algorithm")]
        public string? Algorithm { get; set; }

        [JsonPropertyName("PublisherCertificateThumbprint")]
        public string? PublisherCertificateThumbprint { get; set; }

        [JsonPropertyName("Value")]
        public string? Value { get; set; }
    }
}
