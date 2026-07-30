using Tempest.Core.Events;
using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Settings;

public class SettingsChangedEventTests
{
    [Fact]
    public void Constructor_ValidArguments_SetsProperties()
    {
        var @event = new SettingsChangedEvent("sample.key", "old", "new");

        Assert.Equal("sample.key", @event.Key);
        Assert.Equal("old", @event.OldValue);
        Assert.Equal("new", @event.NewValue);
    }

    [Fact]
    public void IsAnIEvent()
    {
        var @event = new SettingsChangedEvent("sample.key", "old", "new");

        Assert.IsAssignableFrom<IEvent>(@event);
        Assert.IsAssignableFrom<ISettingsChangedEvent>(@event);
    }

    [Fact]
    public void Constructor_NullKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsChangedEvent(null!, "old", "new"));
    }

    [Fact]
    public void Constructor_NullOldValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsChangedEvent("sample.key", null!, "new"));
    }

    [Fact]
    public void Constructor_NullNewValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsChangedEvent("sample.key", "old", null!));
    }
}
