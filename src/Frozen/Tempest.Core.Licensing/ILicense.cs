namespace Tempest.Core.Licensing;

/// <summary>A single, validated license. Immutable.</summary>
public interface ILicense
{
    /// <summary>The name of the party this license was issued to.</summary>
    string LicenseeName { get; }

    /// <summary>
    /// The date and time this license expires, or <see langword="null"/>
    /// if it never expires.
    /// </summary>
    DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// The capability keys this license enables. Empty if none are
    /// enabled beyond whatever the platform grants by default.
    /// </summary>
    IReadOnlyList<string> EnabledCapabilities { get; }
}
