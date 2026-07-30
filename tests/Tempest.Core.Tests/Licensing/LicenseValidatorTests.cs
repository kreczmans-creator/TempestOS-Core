using Tempest.Core.Licensing;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Licensing;

// Proves the approved Licensing contract against the real LicenseValidator
// implementation - the four documented validation outcomes (valid,
// expired, malformed, missing file), and this Work Package's own
// resolution of Risk Register.md's R5: a missing license file is a
// valid, unrestricted-but-uncapable default, never Host-fatal; a file
// that exists but is broken (unreadable, not JSON, missing its own
// required field, or already expired) is invalid.
public class LicenseValidatorTests
{
    private static string WriteLicenseFile(TempDirectory directory, string json)
    {
        var path = Path.Combine(directory.Path, "license.json");
        File.WriteAllText(path, json);
        return path;
    }

    // ------------------------------------------------------------------
    // Missing file - resolves R5: not invalid, a valid default
    // ------------------------------------------------------------------

    [Fact]
    public void Validate_MissingFile_ReturnsAValidUnlicensedDefault()
    {
        using var directory = new TempDirectory();
        var validator = new LicenseValidator(Path.Combine(directory.Path, "does-not-exist.json"));

        var result = validator.Validate();

        Assert.True(result.IsValid);
        Assert.Null(result.FailureReason);
        Assert.NotNull(result.License);
        Assert.Equal(LicenseValidator.UnlicensedLicenseeName, result.License!.LicenseeName);
        Assert.Null(result.License.ExpiresAt);
        Assert.Empty(result.License.EnabledCapabilities);
    }

    // ------------------------------------------------------------------
    // Valid license
    // ------------------------------------------------------------------

    [Fact]
    public void Validate_ValidLicenseNoExpiry_ReturnsValidResult()
    {
        using var directory = new TempDirectory();
        var path = WriteLicenseFile(directory, """{"LicenseeName":"Acme Corp","EnabledCapabilities":["feature.a","feature.b"]}""");
        var validator = new LicenseValidator(path);

        var result = validator.Validate();

        Assert.True(result.IsValid);
        Assert.Null(result.FailureReason);
        Assert.Equal("Acme Corp", result.License!.LicenseeName);
        Assert.Null(result.License.ExpiresAt);
        Assert.Equal(["feature.a", "feature.b"], result.License.EnabledCapabilities);
    }

    [Fact]
    public void Validate_ValidLicenseWithFutureExpiry_ReturnsValidResult()
    {
        using var directory = new TempDirectory();
        var futureExpiry = DateTimeOffset.UtcNow.AddYears(1).ToString("O");
        var path = WriteLicenseFile(directory, $$"""{"LicenseeName":"Acme Corp","ExpiresAt":"{{futureExpiry}}","EnabledCapabilities":[]}""");
        var validator = new LicenseValidator(path);

        var result = validator.Validate();

        Assert.True(result.IsValid);
        Assert.NotNull(result.License!.ExpiresAt);
    }

    [Fact]
    public void Validate_ValidLicenseWithNoCapabilitiesField_TreatsItAsEmpty()
    {
        using var directory = new TempDirectory();
        var path = WriteLicenseFile(directory, """{"LicenseeName":"Acme Corp"}""");
        var validator = new LicenseValidator(path);

        var result = validator.Validate();

        Assert.True(result.IsValid);
        Assert.Empty(result.License!.EnabledCapabilities);
    }

    // ------------------------------------------------------------------
    // Expired license
    // ------------------------------------------------------------------

    [Fact]
    public void Validate_ExpiredLicense_ReturnsInvalidResult()
    {
        using var directory = new TempDirectory();
        var pastExpiry = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");
        var path = WriteLicenseFile(directory, $$"""{"LicenseeName":"Acme Corp","ExpiresAt":"{{pastExpiry}}"}""");
        var validator = new LicenseValidator(path);

        var result = validator.Validate();

        Assert.False(result.IsValid);
        Assert.Null(result.License);
        Assert.NotNull(result.FailureReason);
        Assert.Contains("expired", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Acme Corp", result.FailureReason);
    }

    // ------------------------------------------------------------------
    // Malformed license
    // ------------------------------------------------------------------

    [Fact]
    public void Validate_NotJson_ReturnsInvalidResult()
    {
        using var directory = new TempDirectory();
        var path = WriteLicenseFile(directory, "definitely not json {{{");
        var validator = new LicenseValidator(path);

        var result = validator.Validate();

        Assert.False(result.IsValid);
        Assert.Null(result.License);
        Assert.Contains("not valid JSON", result.FailureReason);
    }

    [Fact]
    public void Validate_MissingLicenseeName_ReturnsInvalidResult()
    {
        using var directory = new TempDirectory();
        var path = WriteLicenseFile(directory, """{"EnabledCapabilities":["feature.a"]}""");
        var validator = new LicenseValidator(path);

        var result = validator.Validate();

        Assert.False(result.IsValid);
        Assert.Null(result.License);
        Assert.Contains("LicenseeName", result.FailureReason);
    }

    [Fact]
    public void Validate_BlankLicenseeName_ReturnsInvalidResult()
    {
        using var directory = new TempDirectory();
        var path = WriteLicenseFile(directory, """{"LicenseeName":"   "}""");
        var validator = new LicenseValidator(path);

        var result = validator.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("LicenseeName", result.FailureReason);
    }

    [Fact]
    public void Validate_NullJson_ReturnsInvalidResult()
    {
        using var directory = new TempDirectory();
        var path = WriteLicenseFile(directory, "null");
        var validator = new LicenseValidator(path);

        var result = validator.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("deserialized to nothing", result.FailureReason);
    }

    // ------------------------------------------------------------------
    // Argument validation
    // ------------------------------------------------------------------

    [Fact]
    public void Constructor_NullPath_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new LicenseValidator(null!));

    [Fact]
    public void Constructor_BlankPath_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => new LicenseValidator("   "));

    // ------------------------------------------------------------------
    // Default (public, parameterless) constructor
    // ------------------------------------------------------------------

    [Fact]
    public void DefaultConstructor_NoLicenseFileAtBaseDirectory_ReturnsUnlicensedDefault()
    {
        // The test assembly's own output directory has no license.json,
        // so the parameterless constructor - the one production code
        // actually uses - exercises the exact same "missing file" path
        // as the explicit-path tests above.
        var validator = new LicenseValidator();

        var result = validator.Validate();

        Assert.True(result.IsValid);
        Assert.Equal(LicenseValidator.UnlicensedLicenseeName, result.License!.LicenseeName);
    }
}
