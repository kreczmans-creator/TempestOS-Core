using System.Security.Cryptography.X509Certificates;

namespace Tempest.Core.Plugins;

/// <summary>
/// The local trust store of publisher certificates plugin signatures are
/// verified against (ADR-0112, "Trust store and tier assignment").
/// </summary>
public interface IPluginTrustStore
{
    /// <summary>
    /// Looks up a trusted publisher certificate by its SHA-256 thumbprint
    /// (hex, case-insensitive).
    /// </summary>
    /// <param name="thumbprint">The certificate thumbprint to look up.</param>
    /// <returns>
    /// The matching certificate, or <see langword="null"/> if no matching
    /// entry exists.
    /// </returns>
    X509Certificate2? FindByThumbprint(string thumbprint);

    /// <summary>
    /// True if <paramref name="thumbprint"/> matches TempestOS's own
    /// first-party publisher certificate specifically (not merely any
    /// trusted entry) — the ADR-0112 distinction between the FirstParty and
    /// VerifiedSigned tiers.
    /// </summary>
    /// <param name="thumbprint">The certificate thumbprint to check.</param>
    bool IsFirstPartyThumbprint(string thumbprint);
}
