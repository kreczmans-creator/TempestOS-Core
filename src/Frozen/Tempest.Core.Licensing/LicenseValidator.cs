using System.Text.Json;

namespace Tempest.Core.Licensing;

/// <summary>
/// The concrete <see cref="ILicenseValidator"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a leaf — no constructor dependencies at all, mirroring
/// <see cref="Versioning.PlatformVersionProvider"/>'s own "deliberately a
/// leaf" position (<c>ADR-0050</c>). Reads its own license file from a
/// fixed, documented convention path, never from <c>IConfigurationProvider</c>
/// — Configuration's own value is irrelevant to this type regardless of
/// build-order timing, since Licensing never consumes it.
/// </para>
/// <para>
/// <b>A missing license file is not itself invalid</b> — it produces a
/// valid, default <see cref="License"/> under <see cref="UnlicensedLicenseeName"/>
/// with no enabled capabilities, never expiring. Resolves
/// `Risk Register.md`'s own <c>R5</c> ("License validation being too
/// aggressively Host-fatal"): the absence of a license file is this
/// platform's own normal, unrestricted-but-uncapable default state, not
/// an operator error — an unlicensed Host still starts and runs
/// normally, with every capability-gated feature reporting itself
/// unavailable via <see cref="ILicenseProvider.HasCapability"/>. A
/// license file that exists but cannot be read, is not valid JSON, is
/// missing its own required <see cref="LicenseDto.LicenseeName"/> field,
/// or has already expired, <i>is</i> Host-fatal — someone deliberately
/// supplied it, and it is broken, which is a genuinely different,
/// actionable condition. See <c>ADR-0050</c>.
/// </para>
/// </remarks>
public sealed class LicenseValidator : ILicenseValidator
{
    /// <summary>The license file name expected at the fixed convention path.</summary>
    public const string LicenseFileName = "license.json";

    /// <summary>
    /// The <see cref="ILicense.LicenseeName"/> used when no license file
    /// is present.
    /// </summary>
    public const string UnlicensedLicenseeName = "Unlicensed";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _licenseFilePath;

    /// <summary>
    /// Initialises a new instance of the <see cref="LicenseValidator"/>
    /// class that reads the conventional license file
    /// (<c>license.json</c>, relative to the application's base
    /// directory).
    /// </summary>
    public LicenseValidator()
        : this(Path.Combine(AppContext.BaseDirectory, LicenseFileName))
    {
    }

    /// <summary>
    /// Initialises a new instance of the <see cref="LicenseValidator"/>
    /// class that reads a specific license file.
    /// </summary>
    /// <param name="licenseFilePath">The license file to read.</param>
    /// <remarks>
    /// Internal test seam — mirrors
    /// <see cref="Plugins.PluginManifestDiscoveryService"/>'s own
    /// internal, path-accepting constructor, so validation can be
    /// exercised deterministically against a controlled temporary file
    /// in tests, without changing the public API surface.
    /// </remarks>
    internal LicenseValidator(string licenseFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseFilePath);

        _licenseFilePath = licenseFilePath;
    }

    /// <inheritdoc />
    public LicenseValidationResult Validate()
    {
        if (!File.Exists(_licenseFilePath))
        {
            var unlicensed = new License(UnlicensedLicenseeName, expiresAt: null, enabledCapabilities: []);

            return new LicenseValidationResult(true, unlicensed, null);
        }

        string json;

        try
        {
            json = File.ReadAllText(_licenseFilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new LicenseValidationResult(false, null, $"License file '{_licenseFilePath}' could not be read: {ex.Message}");
        }

        LicenseDto? dto;

        try
        {
            dto = JsonSerializer.Deserialize<LicenseDto>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            return new LicenseValidationResult(false, null, $"License file '{_licenseFilePath}' is not valid JSON: {ex.Message}");
        }

        if (dto is null)
            return new LicenseValidationResult(false, null, $"License file '{_licenseFilePath}' deserialized to nothing.");

        if (string.IsNullOrWhiteSpace(dto.LicenseeName))
        {
            return new LicenseValidationResult(
                false, null, $"License file '{_licenseFilePath}' has a null, empty, or whitespace 'LicenseeName' field.");
        }

        if (dto.ExpiresAt is not null && dto.ExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            return new LicenseValidationResult(
                false, null,
                $"License for '{dto.LicenseeName}' expired on {dto.ExpiresAt:O} (current time: {DateTimeOffset.UtcNow:O}).");
        }

        var license = new License(dto.LicenseeName, dto.ExpiresAt, dto.EnabledCapabilities ?? []);

        return new LicenseValidationResult(true, license, null);
    }
}
