namespace Tempest.Core.Licensing;

/// <summary>The concrete <see cref="ILicense"/> implementation.</summary>
public sealed class License : ILicense
{
    /// <summary>
    /// Initialises a new instance of the <see cref="License"/> class.
    /// </summary>
    /// <param name="licenseeName">The name of the party this license was issued to.</param>
    /// <param name="expiresAt">The date and time this license expires, or <see langword="null"/> if it never expires.</param>
    /// <param name="enabledCapabilities">The capability keys this license enables.</param>
    public License(string licenseeName, DateTimeOffset? expiresAt, IReadOnlyList<string> enabledCapabilities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseeName);
        ArgumentNullException.ThrowIfNull(enabledCapabilities);

        LicenseeName = licenseeName;
        ExpiresAt = expiresAt;
        EnabledCapabilities = enabledCapabilities;
    }

    /// <inheritdoc />
    public string LicenseeName { get; }

    /// <inheritdoc />
    public DateTimeOffset? ExpiresAt { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> EnabledCapabilities { get; }
}
