using Tempest.Core.Notifications;
using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

// Registration validation: proves the Notification Framework is wired into
// the real, unmodified TempestHost exactly as Service Registration
// Matrix.md specifies - INotificationDispatcher resolvable, ordinary
// singleton semantics, and a real publish/subscribe round trip through the
// container-resolved instance.
[Collection("Console output capture")]
public class NotificationHostRegistrationTests
{
    private static async Task RunAgainstRunningHostAsync(Func<ITempestHost, Task> body)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes).Build();
        var originalOut = Console.Out;

        try
        {
            Console.SetOut(new StringWriter());

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            await body(host);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public Task Host_RegistersINotificationDispatcher_Resolvable() =>
        RunAgainstRunningHostAsync(host =>
        {
            var dispatcher = host.Services!.GetService(typeof(INotificationDispatcher));

            Assert.IsType<NotificationDispatcher>(dispatcher);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_ResolvingINotificationDispatcherTwice_ReturnsTheSameInstance() =>
        RunAgainstRunningHostAsync(host =>
        {
            var first = host.Services!.GetService(typeof(INotificationDispatcher));
            var second = host.Services!.GetService(typeof(INotificationDispatcher));

            Assert.Same(first, second);

            return Task.CompletedTask;
        });

    [Fact]
    public Task Host_NotificationDispatcher_CanRoundTripAPublishThroughTheRealContainerResolvedInstance() =>
        RunAgainstRunningHostAsync(async host =>
        {
            var dispatcher = (INotificationDispatcher)host.Services!.GetService(typeof(INotificationDispatcher));
            IPlatformNotification? received = null;
            var handler = new DelegatingHandler(n => received = n);

            dispatcher.Subscribe(handler);
            await dispatcher.PublishAsync<IPlatformNotification>(new PlatformNotification("Registration", NotificationSeverity.Information, "round trip"));

            Assert.NotNull(received);
            Assert.Equal("round trip", received!.Message);
        });

    private sealed class DelegatingHandler : INotificationHandler<IPlatformNotification>
    {
        private readonly Action<IPlatformNotification> _onHandle;

        public DelegatingHandler(Action<IPlatformNotification> onHandle) => _onHandle = onHandle;

        public Task HandleAsync(IPlatformNotification notification, CancellationToken cancellationToken)
        {
            _onHandle(notification);
            return Task.CompletedTask;
        }
    }
}
