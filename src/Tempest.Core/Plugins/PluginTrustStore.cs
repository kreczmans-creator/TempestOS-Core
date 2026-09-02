using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Tempest.Core.Logging;

namespace Tempest.Core.Plugins;

/// <summary>
/// The concrete, file-backed <see cref="IPluginTrustStore"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// A fixed convention, not configurable (ADR-0112, mirroring
/// <c>Plugin Manifest Architecture.md</c>'s own plugins-root precedent): a
/// <c>TrustedPublishers</c> folder, relative to <see cref="AppContext.BaseDirectory"/>
/// by default. Every <c>*.cer</c> file in the folder is read once, at
/// construction, as a public-only <see cref="X509Certificate2"/> (no private
/// key expected). "Every" is meant literally and identically on every
/// platform: the extension is matched case-insensitively by this type, not
/// by handing a <c>"*.cer"</c> search pattern to the file system, whose own
/// case rules would otherwise decide whether <c>Acme.CER</c> is a trusted
/// publisher — yes on Windows, no on Linux.
/// </para>
/// <para>
/// An absent <c>TrustedPublishers</c> folder is a valid, empty store — zero
/// trusted publishers, not an error — mirroring
/// <see cref="PluginManifestDiscoveryService"/>'s own "absent plugins
/// directory is a valid steady state" precedent. A file that fails to parse
/// as a certificate is skipped and logged at <see cref="LogLevel.Warning"/>,
/// not thrown — an operator's own trust-store hygiene problem, not a
/// plugin-scoped failure this store needs to isolate through ADR-0025's
/// machinery.
/// </para>
/// <para>
/// Thumbprint comparison (<see cref="FindByThumbprint"/>,
/// <see cref="IsFirstPartyThumbprint"/>) is case-insensitive ordinal —
/// <see cref="X509Certificate2.Thumbprint"/> is already uppercase hex, but
/// the input passed to either method is not assumed to be.
/// </para>
/// <para>
/// TempestOS's own first-party publisher certificate is identified by a
/// fixed filename convention: the file literally named
/// <see cref="FirstPartyCertificateFileName"/> inside the
/// <c>TrustedPublishers</c> folder, if present. It is otherwise a perfectly
/// ordinary trust-store entry, just also flagged as the first-party one. If
/// no such file exists, <see cref="IsFirstPartyThumbprint"/> always returns
/// <see langword="false"/> (no First-Party tier is reachable) — a valid, if
/// unusual, state; this is not thrown as an error.
/// </para>
/// </remarks>
public sealed class PluginTrustStore : IPluginTrustStore
{
    /// <summary>
    /// The fixed folder name, relative to <see cref="AppContext.BaseDirectory"/>,
    /// containing trusted publisher <c>.cer</c> files.
    /// </summary>
    internal const string TrustedPublishersFolderName = "TrustedPublishers";

    /// <summary>
    /// The fixed file name, inside the trusted publishers folder, identifying
    /// TempestOS's own first-party publisher certificate.
    /// </summary>
    internal const string FirstPartyCertificateFileName = "TempestOS.cer";

    /// <summary>The file extension every trust-store certificate carries, matched case-insensitively.</summary>
    private const string CertificateFileExtension = ".cer";

    private readonly Dictionary<string, X509Certificate2> _certificatesByThumbprint =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string? _firstPartyThumbprint;

    /// <summary>
    /// Initialises a new instance of the <see cref="PluginTrustStore"/>
    /// class that reads the conventional <c>TrustedPublishers</c> folder
    /// (relative to <see cref="AppContext.BaseDirectory"/>).
    /// </summary>
    /// <param name="logger">
    /// An optional logger used to record a certificate file that fails to
    /// parse. May be <see langword="null"/> if logging is not required.
    /// </param>
    public PluginTrustStore(ILogger? logger = null)
        : this(Path.Combine(AppContext.BaseDirectory, TrustedPublishersFolderName), logger)
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="PluginTrustStore"/>
    /// class that reads a specific trusted publishers folder. A test seam,
    /// mirroring <see cref="PluginManifestDiscoveryService"/>'s own
    /// <c>internal (string pluginsRootPath, ...)</c> constructor.
    /// </summary>
    /// <param name="trustedPublishersFolderPath">
    /// The directory containing trusted publisher <c>.cer</c> files.
    /// </param>
    /// <param name="logger">
    /// An optional logger used to record a certificate file that fails to
    /// parse. May be <see langword="null"/> if logging is not required.
    /// </param>
    internal PluginTrustStore(string trustedPublishersFolderPath, ILogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedPublishersFolderPath);

        if (!Directory.Exists(trustedPublishersFolderPath))
        {
            logger?.Information(
                $"No trusted publishers folder found at '{trustedPublishersFolderPath}'. Plugin trust store contains 0 entries.");

            return;
        }

        // Enumerated unfiltered, then matched on the extension here, rather
        // than passed to Directory.GetFiles as a "*.cer" search pattern:
        // that pattern is matched with the *file system's* case rules, so on
        // Windows it finds "Acme.CER" and on Linux it does not. A trusted
        // publisher's certificate would then be silently absent from the
        // store on one platform and present on the other — the store would
        // report a genuinely trusted publisher as untrusted, with no error
        // and nothing logged, which is the worst shape a trust decision can
        // take. `Path.GetExtension` + OrdinalIgnoreCase makes the rule this
        // type's own, and identical everywhere.
        foreach (var filePath in Directory.GetFiles(trustedPublishersFolderPath)
                     .Where(path => string.Equals(Path.GetExtension(path), CertificateFileExtension, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            X509Certificate2 certificate;
            try
            {
                // X509CertificateLoader, not the X509Certificate2(string) constructor
                // (obsolete, SYSLIB0057, .NET 9+) - same public-only, no-private-key-
                // expected load, non-obsolete API.
                certificate = X509CertificateLoader.LoadCertificateFromFile(filePath);
            }
            catch (Exception ex) when (ex is CryptographicException or IOException)
            {
                logger?.Warning($"Trusted publisher certificate file '{filePath}' could not be parsed and was skipped.", ex);
                continue;
            }

            _certificatesByThumbprint[certificate.Thumbprint] = certificate;

            if (string.Equals(Path.GetFileName(filePath), FirstPartyCertificateFileName, StringComparison.OrdinalIgnoreCase))
                _firstPartyThumbprint = certificate.Thumbprint;
        }
    }

    /// <inheritdoc />
    public X509Certificate2? FindByThumbprint(string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);

        return _certificatesByThumbprint.TryGetValue(thumbprint, out var certificate) ? certificate : null;
    }

    /// <inheritdoc />
    public bool IsFirstPartyThumbprint(string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);

        return _firstPartyThumbprint is not null &&
               string.Equals(_firstPartyThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase);
    }
}
