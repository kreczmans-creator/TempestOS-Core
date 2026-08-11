using Tempest.Core.Commands;
using Tempest.Core.Input;

namespace Tempest.Core.Tests.Input;

// Proves ADR-0100 (external controller integration is a provider
// abstraction, not a vendor SDK) against the real InputBindingRouter
// implementation — including a real IExternalControllerProvider-shaped
// test double (StubExternalControllerProvider), driven through the
// identical router a real Keyboard/Stream Deck/MIDI provider would use.
public class InputBindingRouterTests
{
    private static (CommandRegistry Registry, InputBindingRouter Router, RecordingCommandHandler Handler) CreateHarness()
    {
        var table = new CommandHandlerTable();
        var registry = new CommandRegistry(table);
        var dispatcher = new CommandDispatcher(table);
        var handler = new RecordingCommandHandler();

        dispatcher.RegisterHandler(handler);
        registry.RegisterDescriptor(new CommandDescriptor("sample.recorded", "Recorded", createDefault: () => new RecordedCommand()));

        return (registry, new InputBindingRouter(registry), handler);
    }

    [Fact]
    public void Register_AddsToProviders()
    {
        var (_, router, _) = CreateHarness();
        var provider = new StubExternalControllerProvider();

        router.Register(provider);

        Assert.Same(provider, Assert.Single(router.Providers));
    }

    [Fact]
    public void Register_Twice_SameProvider_IsIdempotent()
    {
        var (_, router, _) = CreateHarness();
        var provider = new StubExternalControllerProvider();

        router.Register(provider);
        router.Register(provider);

        Assert.Single(router.Providers);
    }

    [Fact]
    public void Unregister_RemovesFromProviders()
    {
        var (_, router, _) = CreateHarness();
        var provider = new StubExternalControllerProvider();
        router.Register(provider);

        router.Unregister(provider);

        Assert.Empty(router.Providers);
    }

    [Fact]
    public async Task StubExternalControllerProvider_SimulatePress_InvokesTheBoundCommandThroughTheRouter()
    {
        var (_, router, handler) = CreateHarness();
        var controller = new StubExternalControllerProvider();
        router.Register(controller);

        controller.SimulatePress("sample.recorded");

        // RouteAsync is fire-and-forget from the provider's own
        // synchronous event (mirrors a real hardware callback) — poll
        // briefly rather than assume synchronous completion.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!handler.WasInvoked && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.True(handler.WasInvoked);
    }

    [Fact]
    public async Task UnregisteredProvider_SimulatePress_DoesNotInvokeAnything()
    {
        var (_, router, handler) = CreateHarness();
        var registered = new StubExternalControllerProvider();
        var unregistered = new StubExternalControllerProvider();
        router.Register(registered);
        router.Unregister(registered);

        unregistered.SimulatePress("sample.recorded");
        await Task.Delay(50);

        Assert.False(handler.WasInvoked);
    }

    [Fact]
    public async Task ARequestForAnUnknownCommandId_IsIsolated_NeverThrowsBackIntoTheProvider()
    {
        var (_, router, _) = CreateHarness();
        var controller = new StubExternalControllerProvider();
        router.Register(controller);

        // CommandRequested is a plain event — a throwing subscriber would
        // otherwise propagate back into the provider's own synchronous
        // raise; this must not throw at all (isolated inside RouteAsync).
        var exception = Record.Exception(() => controller.SimulatePress("does.not.exist"));

        Assert.Null(exception);
        await Task.Delay(50);
    }

    private sealed class RecordedCommand : ICommand;

    private sealed class RecordingCommandHandler : ICommandHandler<RecordedCommand>
    {
        public bool WasInvoked { get; private set; }

        public Task<CommandResult> HandleAsync(RecordedCommand command, CancellationToken cancellationToken)
        {
            WasInvoked = true;
            return Task.FromResult(CommandResult.Success());
        }
    }
}
