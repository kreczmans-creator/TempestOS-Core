using Tempest.App.Shell;

namespace Tempest.Core.Tests.Shell;

// Proves PlaceholderPage in isolation - no Shell, Host, or Navigation
// involved - mirroring how NavigationItemTests proves NavigationItem alone
// before any higher-level integration test exercises it.
public class PlaceholderPageTests
{
    [Fact]
    public void Render_WritesTitleRuleAndMessage()
    {
        var page = new PlaceholderPage("Example", "An example message.");
        var writer = new StringWriter();

        page.Render(writer);

        var output = writer.ToString();
        Assert.Contains("Example", output);
        Assert.Contains("An example message.", output);
    }

    [Fact]
    public void Render_NullWriter_ThrowsArgumentNullException()
    {
        var page = new PlaceholderPage("Example", "An example message.");

        Assert.Throws<ArgumentNullException>(() => page.Render(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_InvalidTitle_ThrowsArgumentException(string? title) =>
        Assert.Throws<ArgumentException>(() => new PlaceholderPage(title!, "message"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_InvalidMessage_ThrowsArgumentException(string? message) =>
        Assert.Throws<ArgumentException>(() => new PlaceholderPage("title", message!));

    [Fact]
    public void Constructor_ValidArguments_ExposesThemAsProperties()
    {
        var page = new PlaceholderPage("Title", "Message");

        Assert.Equal("Title", page.Title);
        Assert.Equal("Message", page.Message);
    }
}
