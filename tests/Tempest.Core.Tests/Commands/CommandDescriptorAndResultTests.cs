using Tempest.Core.Commands;

namespace Tempest.Core.Tests.Commands;

public class CommandDescriptorAndResultTests
{
    // ------------------------------------------------------------------
    // CommandDescriptor construction
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CommandDescriptor_InvalidId_ThrowsArgumentException(string? id) =>
        Assert.Throws<ArgumentException>(() => new CommandDescriptor(id!, "Display Name"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CommandDescriptor_InvalidDisplayName_ThrowsArgumentException(string? displayName) =>
        Assert.Throws<ArgumentException>(() => new CommandDescriptor("sample.a", displayName!));

    [Fact]
    public void CommandDescriptor_OptionalMembers_DefaultToNull()
    {
        var descriptor = new CommandDescriptor("sample.a", "Sample A");

        Assert.Null(descriptor.Category);
        Assert.Null(descriptor.Description);
        Assert.Null(descriptor.Icon);
        Assert.Null(descriptor.CanExecute);
        Assert.Null(descriptor.CreateDefault);
    }

    [Fact]
    public void CommandDescriptor_AllMembers_AreRetainedExactly()
    {
        Func<bool> canExecute = () => true;
        Func<ICommand> createDefault = () => new RecordedCommandA();

        var descriptor = new CommandDescriptor(
            "sample.a", "Sample A", "Category", "Description", "icon-key", canExecute, createDefault);

        Assert.Equal("sample.a", descriptor.Id);
        Assert.Equal("Sample A", descriptor.DisplayName);
        Assert.Equal("Category", descriptor.Category);
        Assert.Equal("Description", descriptor.Description);
        Assert.Equal("icon-key", descriptor.Icon);
        Assert.Same(canExecute, descriptor.CanExecute);
        Assert.Same(createDefault, descriptor.CreateDefault);
    }

    // ------------------------------------------------------------------
    // CommandResult construction
    // ------------------------------------------------------------------

    [Fact]
    public void Success_NoMessage_SucceededTrue_MessageNull()
    {
        var result = CommandResult.Success();

        Assert.True(result.Succeeded);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Success_WithMessage_RetainsTheMessage()
    {
        var result = CommandResult.Success("all good");

        Assert.True(result.Succeeded);
        Assert.Equal("all good", result.Message);
    }

    [Fact]
    public void Failure_RetainsTheMessage_SucceededFalse()
    {
        var result = CommandResult.Failure("went wrong");

        Assert.False(result.Succeeded);
        Assert.Equal("went wrong", result.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Failure_InvalidMessage_ThrowsArgumentException(string? message) =>
        Assert.Throws<ArgumentException>(() => CommandResult.Failure(message!));
}
