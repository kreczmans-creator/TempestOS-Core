using Tempest.Core.Notifications;

namespace Tempest.Core.Tests.Notifications;

public class ExceptionTests
{
    [Fact]
    public void NotificationException_MessageConstructor_SetsMessage()
    {
        var exception = new NotificationException("something went wrong");

        Assert.Equal("something went wrong", exception.Message);
    }

    [Fact]
    public void NotificationException_IsAnException()
    {
        var exception = new NotificationException("message");

        Assert.IsAssignableFrom<Exception>(exception);
    }
}
