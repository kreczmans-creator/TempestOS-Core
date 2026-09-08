namespace Tempest.Core.Licensing;

/// <summary>
/// The raw, on-disk JSON shape of a license file, before validation.
/// </summary>
/// <remarks>
/// Deliberately separate from <see cref="License"/>: <see cref="LicenseeName"/>
/// is nullable here because deserialized JSON has not yet been validated.
/// <see cref="License"/> is only ever constructed once every required
/// field has been confirmed present and well-formed — see
/// <see cref="LicenseValidator"/>.
/// </remarks>
internal sealed class LicenseDto
{
    /// <summary>Gets or sets the raw, unvalidated licensee name.</summary>
    public string? LicenseeName { get; set; }

    /// <summary>Gets or sets the raw, unvalidated expiry date, or <see langword="null"/> if the license never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Gets or sets the raw, unvalidated enabled capability keys.</summary>
    public List<string>? EnabledCapabilities { get; set; }
}
