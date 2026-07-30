using Tempest.Core.Notifications;

namespace Tempest.Core.Tests.Notifications;

public class PlatformNotificationTests
{
    [Fact]
    public void Constructor_ValidArguments_SetsProperties()
    {
        var occurredAt = DateTimeOffset.UtcNow;

        var notification = new PlatformNotification("Reports", NotificationSeverity.Warning, "Export took longer than expected.", occurredAt);

        Assert.Equal("Reports", notification.Category);
        Assert.Equal(NotificationSeverity.Warning, notification.Severity);
        Assert.Equal("Export took longer than expected.", notification.Message);
        Assert.Equal(occurredAt, notification.OccurredAt);
    }

    [Fact]
    public void Constructor_OccurredAtOmitted_DefaultsToUtcNow()
    {
        var before = DateTimeOffset.UtcNow;

        var notification = new PlatformNotification("Reports", NotificationSeverity.Information, "message");

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(notification.OccurredAt, before, after);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullEmptyOrWhitespaceCategory_ThrowsArgumentException(string? category)
    {
        Assert.Throws<ArgumentException>(() => new PlatformNotification(category!, NotificationSeverity.Information, "message"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullEmptyOrWhitespaceMessage_ThrowsArgumentException(string? message)
    {
        Assert.Throws<ArgumentException>(() => new PlatformNotification("Reports", NotificationSeverity.Information, message!));
    }

    [Theory]
    [InlineData(NotificationSeverity.Information)]
    [InlineData(NotificationSeverity.Success)]
    [InlineData(NotificationSeverity.Warning)]
    [InlineData(NotificationSeverity.Error)]
    public void Constructor_EverySeverityValue_IsAccepted(NotificationSeverity severity)
    {
        var notification = new PlatformNotification("Reports", severity, "message");

        Assert.Equal(severity, notification.Severity);
    }

    [Fact]
    public void PlatformNotification_IsBothINotificationAndIEvent()
    {
        var notification = new PlatformNotification("Reports", NotificationSeverity.Information, "message");

        Assert.IsAssignableFrom<INotification>(notification);
        Assert.IsAssignableFrom<Tempest.Core.Events.IEvent>(notification);
    }
}
