using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Identity;

public class PlatformIdentityAndPrincipalTests
{
    // ----------------------------------------------------------------
    // PlatformIdentity
    // ----------------------------------------------------------------

    [Fact]
    public void PlatformIdentity_Constructor_ValidArguments_SetsProperties()
    {
        var identity = new PlatformIdentity("local.user", "Local User");

        Assert.Equal("local.user", identity.Id);
        Assert.Equal("Local User", identity.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PlatformIdentity_Constructor_NullEmptyOrWhitespaceId_ThrowsArgumentException(string? id)
    {
        Assert.Throws<ArgumentException>(() => new PlatformIdentity(id!, "Display"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PlatformIdentity_Constructor_NullEmptyOrWhitespaceDisplayName_ThrowsArgumentException(string? displayName)
    {
        Assert.Throws<ArgumentException>(() => new PlatformIdentity("local.user", displayName!));
    }

    // ----------------------------------------------------------------
    // PlatformPrincipal
    // ----------------------------------------------------------------

    [Fact]
    public void PlatformPrincipal_Constructor_ValidArguments_SetsProperties()
    {
        var identity = new PlatformIdentity("local.user", "Local User");
        var permissions = new List<Permission> { new("reports.generate") };

        var principal = new PlatformPrincipal(identity, permissions);

        Assert.Same(identity, principal.Identity);
        Assert.Equal(permissions, principal.Permissions);
    }

    [Fact]
    public void PlatformPrincipal_Constructor_EmptyPermissions_IsAllowed()
    {
        var identity = new PlatformIdentity("local.user", "Local User");

        var principal = new PlatformPrincipal(identity, []);

        Assert.Empty(principal.Permissions);
    }

    [Fact]
    public void PlatformPrincipal_Constructor_NullIdentity_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new PlatformPrincipal(null!, []));
    }

    [Fact]
    public void PlatformPrincipal_Constructor_NullPermissions_ThrowsArgumentNullException()
    {
        var identity = new PlatformIdentity("local.user", "Local User");

        Assert.Throws<ArgumentNullException>(() => new PlatformPrincipal(identity, null!));
    }
}
