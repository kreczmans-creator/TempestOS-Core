using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

public class ClockModuleTests
{
    // ----------------------------------------------------------------
    // Module metadata correctness
    // ----------------------------------------------------------------

    [Fact]
    public void Constructor_SetsExpectedMetadata()
    {
        var module = new ClockModule();

        Assert.Equal("tempest.samples.clock", module.Id);
        Assert.Equal("System Clock", module.Name);
        Assert.Equal("1.0.0", module.Version);
    }

    [Fact]
    public void Constructor_TimestampsAndStateAreInitiallyUnset()
    {
        var module = new ClockModule();

        Assert.Null(module.InitialisedAt);
        Assert.Null(module.StartedAt);
        Assert.Null(module.StoppedAt);
        Assert.False(module.IsRunning);
        Assert.Null(module.Uptime);
    }

    // ----------------------------------------------------------------
    // Timestamp recording - each lifecycle method individually
    // ----------------------------------------------------------------

    [Fact]
    public async Task InitialiseAsync_RecordsInitialisedAt()
    {
        var module = new ClockModule();

        await module.InitialiseAsync(CancellationToken.None);

        Assert.NotNull(module.InitialisedAt);
        Assert.False(module.IsRunning);
    }

    [Fact]
    public async Task StartAsync_RecordsStartedAt_AndSetsIsRunning()
    {
        var module = new ClockModule();

        await module.StartAsync(CancellationToken.None);

        Assert.NotNull(module.StartedAt);
        Assert.True(module.IsRunning);
    }

    [Fact]
    public async Task StopAsync_RecordsStoppedAt_AndClearsIsRunning()
    {
        var module = new ClockModule();
        await module.StartAsync(CancellationToken.None);

        await module.StopAsync(CancellationToken.None);

        Assert.NotNull(module.StoppedAt);
        Assert.False(module.IsRunning);
    }

    // ----------------------------------------------------------------
    // Lifecycle ordering
    // ----------------------------------------------------------------

    [Fact]
    public async Task FullLifecycle_RecordsTimestampsInNonDecreasingOrder()
    {
        var module = new ClockModule();

        await module.InitialiseAsync(CancellationToken.None);
        await module.StartAsync(CancellationToken.None);
        await module.StopAsync(CancellationToken.None);

        Assert.True(module.InitialisedAt <= module.StartedAt);
        Assert.True(module.StartedAt <= module.StoppedAt);
    }

    // ----------------------------------------------------------------
    // Uptime
    // ----------------------------------------------------------------

    [Fact]
    public void Uptime_BeforeStart_IsNull()
    {
        var module = new ClockModule();

        Assert.Null(module.Uptime);
    }

    [Fact]
    public async Task Uptime_WhileRunning_IsNonNegative()
    {
        var module = new ClockModule();

        await module.StartAsync(CancellationToken.None);

        Assert.NotNull(module.Uptime);
        Assert.True(module.Uptime >= TimeSpan.Zero);
    }

    [Fact]
    public async Task Uptime_AfterStop_IsNull()
    {
        var module = new ClockModule();
        await module.StartAsync(CancellationToken.None);

        await module.StopAsync(CancellationToken.None);

        Assert.Null(module.Uptime);
    }
}
