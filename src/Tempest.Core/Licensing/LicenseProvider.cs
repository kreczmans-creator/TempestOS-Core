namespace Tempest.Core.Licensing;

/// <summary>
/// The concrete <see cref="ILicenseProvider"/> implementation.
/// </summary>
/// <remarks>
/// Constructed directly by the Host from an already-validated
/// <see cref="ILicense"/> (never resolved from a license file itself —
/// that is <see cref="ILicenseValidator"/>'s own job) and registered via
/// <c>AddInstance</c>, exactly like <see cref="Versioning.IPlatformVersionProvider"/>
/// and <see cref="Diagnostics.IDiagnosticsProvider"/> before it.
/// </remarks>
public sealed class LicenseProvider : ILicenseProvider
{
    private readonly HashSet<string> _enabledCapabilities;

    /// <summary>
    /// Initialises a new instance of the <see cref="LicenseProvider"/> class.
    /// </summary>
    /// <param name="currentLicense">The already-validated license this provider exposes.</param>
    public LicenseProvider(ILicense currentLicense)
    {
        ArgumentNullException.ThrowIfNull(currentLicense);

        CurrentLicense = currentLicense;
        _enabledCapabilities = new HashSet<string>(currentLicense.EnabledCapabilities, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public ILicense CurrentLicense { get; }

    /// <inheritdoc />
    public bool HasCapability(string capability)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capability);

        return _enabledCapabilities.Contains(capability);
    }
}
