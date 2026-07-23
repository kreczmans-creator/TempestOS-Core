using Tempest.Core.Configuration;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

public class TempestHostBuilderTests
{
    [Fact]
    public void Build_ReturnsHostInCreatedState()
    {
        var builder = new TempestHostBuilder(Type.EmptyTypes);

        var host = builder.Build();

        Assert.Equal(HostState.Created, host.State);
    }

    [Fact]
    public void AddConfigurationSource_ReturnsSameBuilder_ToAllowChaining()
    {
        var builder = new TempestHostBuilder(Type.EmptyTypes);
        var source = new MemoryConfigurationSource([]);

        var result = builder.AddConfigurationSource(source);

        Assert.Same(builder, result);
    }

    [Fact]
    public void AddConfigurationSource_ThrowsArgumentNullException_WhenSourceIsNull()
    {
        var builder = new TempestHostBuilder(Type.EmptyTypes);

        Assert.Throws<ArgumentNullException>(() => builder.AddConfigurationSource(null!));
    }

    [Fact]
    public void Build_CalledTwice_ThrowsInvalidOperationException()
    {
        var builder = new TempestHostBuilder(Type.EmptyTypes);
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void AddConfigurationSource_AfterBuild_ThrowsInvalidOperationException()
    {
        var builder = new TempestHostBuilder(Type.EmptyTypes);
        builder.Build();

        Assert.Throws<InvalidOperationException>(() =>
            builder.AddConfigurationSource(new MemoryConfigurationSource([])));
    }

    [Fact]
    public void Build_ProducesADistinctHostEachTimeADifferentBuilderIsUsed()
    {
        var first = new TempestHostBuilder(Type.EmptyTypes).Build();
        var second = new TempestHostBuilder(Type.EmptyTypes).Build();

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task RunAsync_UsesEveryConfigurationSourceAddedToTheBuilder()
    {
        // Configuration.Build() throwing on a bad Runtime:Logging:MinimumLevel
        // value proves the source added via AddConfigurationSource actually
        // reached the host's own ConfigurationBuilder.
        var builder = new TempestHostBuilder(Type.EmptyTypes);
        builder.AddConfigurationSource(new MemoryConfigurationSource(
        [
            new KeyValuePair<string, string>("Runtime:Logging:MinimumLevel", "NotARealLevel"),
        ]));

        await using var host = builder.Build();

        await Assert.ThrowsAsync<ConfigurationException>(() => host.RunAsync());
    }
}
