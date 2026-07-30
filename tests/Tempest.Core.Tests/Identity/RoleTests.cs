using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Identity;

public class RoleTests
{
    [Fact]
    public void Constructor_ValidNameAndPermissions_SetsProperties()
    {
        var permissions = new List<Permission> { new("reports.generate"), new("settings.write") };

        var role = new Role("Admin", permissions);

        Assert.Equal("Admin", role.Name);
        Assert.Equal(permissions, role.Permissions);
    }

    [Fact]
    public void Constructor_EmptyPermissions_IsAllowed()
    {
        var role = new Role("Guest", []);

        Assert.Empty(role.Permissions);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullEmptyOrWhitespaceName_ThrowsArgumentException(string? name)
    {
        Assert.Throws<ArgumentException>(() => new Role(name!, []));
    }

    [Fact]
    public void Constructor_NullPermissions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Role("Admin", null!));
    }
}
