using Tempest.Core.Navigation;

namespace Tempest.Core.Tests.Navigation;

// Proves NavigationItem's own construction and validation - pure data, no
// registry, service, or event involved.
public class NavigationItemTests
{
    [Fact]
    public void Constructor_MinimalArguments_AppliesDefaults()
    {
        var item = new NavigationItem("home", "Home");

        Assert.Equal("home", item.Id);
        Assert.Equal("Home", item.Title);
        Assert.Equal(0, item.Order);
        Assert.Null(item.Icon);
        Assert.Null(item.Group);
        Assert.Null(item.ParentId);
        Assert.Null(item.IsVisible);
    }

    [Fact]
    public void Constructor_AllArguments_AreAllRetained()
    {
        Func<bool> isVisible = () => true;

        var item = new NavigationItem(
            "settings", "Settings", order: 5, icon: "gear", group: "Admin", parentId: "home", isVisible: isVisible);

        Assert.Equal("settings", item.Id);
        Assert.Equal("Settings", item.Title);
        Assert.Equal(5, item.Order);
        Assert.Equal("gear", item.Icon);
        Assert.Equal("Admin", item.Group);
        Assert.Equal("home", item.ParentId);
        Assert.Same(isVisible, item.IsVisible);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidId_ThrowsArgumentException(string? id) =>
        Assert.Throws<ArgumentException>(() => new NavigationItem(id!, "Title"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidTitle_ThrowsArgumentException(string? title) =>
        Assert.Throws<ArgumentException>(() => new NavigationItem("id", title!));
}
