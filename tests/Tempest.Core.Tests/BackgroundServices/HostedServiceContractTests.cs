using Tempest.Core.BackgroundServices;

namespace Tempest.Core.Tests.BackgroundServices;

public class HostedServiceContractTests
{
    [Fact]
    public async Task IHostedService_StartAsync_CanBeInvokedAndAwaited()
    {
        var service = new RecordingHostedService();

        await service.StartAsync(CancellationToken.None);

        Assert.True(service.Started);
    }

    [Fact]
    public async Task IHostedService_StopAsync_CanBeInvokedAndAwaited()
    {
        var service = new RecordingHostedService();

        await service.StopAsync(CancellationToken.None);

        Assert.True(service.Stopped);
    }

    [Fact]
    public async Task IHostedService_ObservesCancellation()
    {
        var service = new RecordingHostedService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.StartAsync(cts.Token));
    }

    [Fact]
    public void ICriticalBackgroundService_IsAHostedService()
    {
        var service = new RecordingCriticalBackgroundService();

        Assert.IsAssignableFrom<IHostedService>(service);
    }

    [Fact]
    public async Task ICriticalBackgroundService_StartAndStop_BehaveLikeAnyHostedService()
    {
        IHostedService service = new RecordingCriticalBackgroundService();

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        var critical = Assert.IsType<RecordingCriticalBackgroundService>(service);
        Assert.True(critical.Started);
        Assert.True(critical.Stopped);
    }

    private sealed class RecordingHostedService : IHostedService
    {
        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingCriticalBackgroundService : ICriticalBackgroundService
    {
        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }
}
