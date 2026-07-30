using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Identity;

public class PermissionTests
{
    // ----------------------------------------------------------------
    // Construction / validation
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_ValidKey_SetsKey()
    {
        var permission = new Permission("reports.generate");

        Assert.Equal("reports.generate", permission.Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullEmptyOrWhitespaceKey_ThrowsArgumentException(string? key)
    {
        Assert.Throws<ArgumentException>(() => new Permission(key!));
    }

    // ----------------------------------------------------------------
    // Value equality (record semantics)
    // ----------------------------------------------------------------

    [Fact]
    public void Equals_SameKey_AreEqual()
    {
        var first = new Permission("settings.write");
        var second = new Permission("settings.write");

        Assert.Equal(first, second);
        Assert.True(first == second);
    }

    [Fact]
    public void Equals_DifferentKey_AreNotEqual()
    {
        var first = new Permission("settings.write");
        var second = new Permission("settings.read");

        Assert.NotEqual(first, second);
        Assert.False(first == second);
    }

    [Fact]
    public void GetHashCode_SameKey_ProducesSameHashCode()
    {
        var first = new Permission("audit.query");
        var second = new Permission("audit.query");

        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
