using Tempest.Core.Configuration;

namespace Tempest.Core.Tests.Configuration;

public class MemoryConfigurationSourceTests
{
    [Fact]
    public void Load_ReturnsSuppliedEntries()
    {
        var source = new MemoryConfigurationSource(new[]
        {
            new KeyValuePair<string, string>("A", "1"),
            new KeyValuePair<string, string>("B", "2"),
        });

        var entries = source.Load().ToList();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, entry => entry.Key == "A" && entry.Value == "1");
        Assert.Contains(entries, entry => entry.Key == "B" && entry.Value == "2");
    }

    [Fact]
    public void Load_CanReturnTheSameKeyMoreThanOnce()
    {
        var source = new MemoryConfigurationSource(new[]
        {
            new KeyValuePair<string, string>("A", "1"),
            new KeyValuePair<string, string>("A", "2"),
        });

        var entries = source.Load().ToList();

        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenEntriesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new MemoryConfigurationSource(null!));
    }
}
