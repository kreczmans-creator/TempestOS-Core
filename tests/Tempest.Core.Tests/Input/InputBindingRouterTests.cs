using Tempest.Core.Commands;
using Tempest.Core.Input;
using Tempest.Core.Tests.Commands;

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

    // ==================================================================
    // WP-A2 — the canonical path: Evaluate then InvokeAsync(id, context,
    // prompt, ct). Until then this router used the obsolete Id-only
    // overload, which throws for every one of the 74 production discipline
    // commands; the throw became a log line and the key looked dead.
    // ==================================================================

    private sealed record BoundCommand(Guid ObjectId, string Value) : ICommand;

    /// <summary>A harness carrying a real binding, so a bound gesture exercises a real production-shaped command.</summary>
    private static (CommandRegistry Registry, InputBindingRouter Router, RecordingCommandHandler<BoundCommand> Handler)
        CreateBoundHarness()
    {
        var table = new CommandHandlerTable();
        var registry = new CommandRegistry(table);
        var dispatcher = new CommandDispatcher(table);
        var handler = new RecordingCommandHandler<BoundCommand>();
        dispatcher.RegisterHandler(handler);

        // Needs a selected object; no parameters.
        registry.RegisterDescriptor(new CommandDescriptor("bound.needs-selection", "Needs A Selection")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new BoundCommand(context.Primary!.ObjectId, string.Empty)),
        });

        // Needs a selected object and one collected value.
        registry.RegisterDescriptor(new CommandDescriptor("bound.needs-value", "Needs A Value")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, values) => new BoundCommand(context.Primary!.ObjectId, values["note"]),
                [new CommandParameter("note", "Note")]),
        });

        // Declares itself unavailable, with a reason.
        registry.RegisterDescriptor(new CommandDescriptor("bound.unavailable", "Needs A Picker")
        {
            Binding = CommandBinding.Unavailable("this platform has no object picker yet."),
        });

        return (registry, new InputBindingRouter(registry), handler);
    }

    private static async Task<bool> SettledAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        return condition();
    }

    /// <summary>
    /// A selection-scoped command reaches its handler with the supplied
    /// context — the whole point of `WP-A2`. On the obsolete path this threw
    /// <see cref="CommandException"/> and was swallowed into a log line.
    /// </summary>
    [Fact]
    public async Task ABoundCommandNeedingASelection_ReachesItsHandler_WithTheSuppliedContext()
    {
        var (_, router, handler) = CreateBoundHarness();
        var selected = Guid.NewGuid();
        router.ContextSource = () => CommandContext.For(selected, "Part");

        var controller = new StubExternalControllerProvider();
        router.Register(controller);
        controller.SimulatePress("bound.needs-selection");

        Assert.True(await SettledAsync(() => handler.Received.Count == 1));
        Assert.Equal(selected, handler.Received[0].ObjectId);
    }

    /// <summary>
    /// With no context source the router supplies <see cref="CommandContext.Empty"/>
    /// — never a fabricated selection — so the command is refused by
    /// <c>Evaluate</c> and its handler is never reached.
    /// </summary>
    [Fact]
    public async Task WithNoContextSource_ASelectionScopedCommand_IsRefused_AndNeverDispatched()
    {
        var (_, router, handler) = CreateBoundHarness();
        var controller = new StubExternalControllerProvider();
        router.Register(controller);

        controller.SimulatePress("bound.needs-selection");
        await Task.Delay(100);

        Assert.Empty(handler.Received);
    }

    /// <summary>A parameterised command collects its value through the supplied prompt, and the collected value reaches the command.</summary>
    [Fact]
    public async Task AParameterisedCommand_CollectsItsValue_ThroughTheSuppliedPrompt()
    {
        var (_, router, handler) = CreateBoundHarness();
        router.ContextSource = () => CommandContext.For(Guid.NewGuid(), "Part");
        router.ParameterPrompt = (_, _, _, _) =>
            Task.FromResult<IReadOnlyDictionary<string, string>?>(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["note"] = "typed by a person" });

        var controller = new StubExternalControllerProvider();
        router.Register(controller);
        controller.SimulatePress("bound.needs-value");

        Assert.True(await SettledAsync(() => handler.Received.Count == 1));
        Assert.Equal("typed by a person", handler.Received[0].Value);
    }

    /// <summary>
    /// With nothing able to ask, a parameterised command is reported as
    /// needing input rather than run without asking. No value is invented.
    /// </summary>
    [Fact]
    public async Task WithNoPrompt_AParameterisedCommand_IsNotRunWithoutAsking()
    {
        var (_, router, handler) = CreateBoundHarness();
        router.ContextSource = () => CommandContext.For(Guid.NewGuid(), "Part");

        var controller = new StubExternalControllerProvider();
        router.Register(controller);
        controller.SimulatePress("bound.needs-value");
        await Task.Delay(100);

        Assert.Empty(handler.Received);
    }

    /// <summary>A user who declines the prompt cancels the command, and nothing is dispatched.</summary>
    [Fact]
    public async Task DecliningThePrompt_CancelsTheCommand_AndDispatchesNothing()
    {
        var (_, router, handler) = CreateBoundHarness();
        router.ContextSource = () => CommandContext.For(Guid.NewGuid(), "Part");
        router.ParameterPrompt = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(null);

        var controller = new StubExternalControllerProvider();
        router.Register(controller);
        controller.SimulatePress("bound.needs-value");
        await Task.Delay(100);

        Assert.Empty(handler.Received);
    }

    /// <summary>
    /// A command that declares itself unavailable is refused with its own
    /// reason — the `ADR-0070` contract — rather than throwing.
    /// </summary>
    [Fact]
    public async Task ADeclaredUnavailableCommand_IsRefused_NotThrown()
    {
        var (registry, router, handler) = CreateBoundHarness();
        router.ContextSource = () => CommandContext.For(Guid.NewGuid(), "Part");

        var controller = new StubExternalControllerProvider();
        router.Register(controller);

        var exception = Record.Exception(() => controller.SimulatePress("bound.unavailable"));
        await Task.Delay(100);

        Assert.Null(exception);
        Assert.Empty(handler.Received);
        Assert.Equal(
            "this platform has no object picker yet.",
            registry.Evaluate("bound.unavailable", CommandContext.For(Guid.NewGuid(), "Part")).Reason);
    }

    /// <summary>
    /// The <c>Evaluate</c> gate is not decoration: an Id nobody registered is
    /// reported as unavailable, rather than reaching
    /// <c>InvokeAsync</c> and being caught as a thrown
    /// <see cref="CommandNotFoundException"/>.
    /// </summary>
    /// <remarks>
    /// Found by mutation: removing the gate left every other assertion here
    /// green, because <c>InvokeAsync</c> re-evaluates internally and returns
    /// <see cref="CommandOutcome.Unavailable"/> for a command that merely
    /// declines. The one case the gate genuinely changes is an unregistered
    /// Id — the difference between a clean refusal and a caught exception —
    /// so that is what this asserts.
    /// </remarks>
    [Fact]
    public async Task AnUnregisteredCommandId_IsRefusedByTheGate_NotCaughtAsAThrownException()
    {
        var table = new CommandHandlerTable();
        var registry = new CommandRegistry(table);
        var logger = new Logging.RecordingLogger();
        var router = new InputBindingRouter(registry, logger);

        var controller = new StubExternalControllerProvider();
        router.Register(controller);
        controller.SimulatePress("nobody.registered.this");

        await SettledAsync(() => logger.Messages.Any(m => m.Contains("nobody.registered.this", StringComparison.Ordinal)));

        var reported = logger.Messages.Single(m => m.Contains("nobody.registered.this", StringComparison.Ordinal));
        Assert.Contains("is not available", reported, StringComparison.Ordinal);
        Assert.DoesNotContain("which failed", reported, StringComparison.Ordinal);
    }

    /// <summary>
    /// The router no longer uses the obsolete Id-only overload. Asserted at
    /// source because both overloads are legitimate API and no runtime
    /// observation distinguishes a deliberate legacy caller from an
    /// accidental one — the same reasoning
    /// <c>IdOnlyInvocationGuardTests</c> records, applied to the file that
    /// guard no longer allow-lists.
    /// </summary>
    [Fact]
    public void TheRouter_UsesTheCanonicalPath_NotTheIdOnlyOverload()
    {
        var source = File.ReadAllText(Path.Combine(
            Templates.RepositoryPaths.RepositoryRoot, "src", "Tempest.Core", "Input", "InputBindingRouter.cs"));

        var code = string.Join("\n", source.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal)
                           && !line.StartsWith("///", StringComparison.Ordinal)
                           && !line.StartsWith('*')));

        Assert.Contains("_commandRegistry.Evaluate(commandId, context)", code, StringComparison.Ordinal);
        Assert.Contains(".InvokeAsync(commandId, context, ParameterPrompt)", code, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeAsync(commandId)", code, StringComparison.Ordinal);
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
