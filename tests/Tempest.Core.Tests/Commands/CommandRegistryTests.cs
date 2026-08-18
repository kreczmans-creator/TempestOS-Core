using Tempest.Core.Commands;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Logging;
using Tempest.Core.Tests.Events;

namespace Tempest.Core.Tests.Commands;

// Proves ADR-0036/ADR-0037 against the real CommandRegistry implementation -
// imperative descriptor registration, duplicate rejection, deterministic
// Items ordering, CanExecute stored-not-evaluated, and InvokeAsync resolving
// a default command instance and dispatching it through the shared
// CommandHandlerTable - the identical handler set CommandDispatcher itself
// populates.
public class CommandRegistryTests
{
    private static (CommandRegistry Registry, CommandDispatcher Dispatcher) CreateRegistryAndDispatcher(ILogger? logger = null)
    {
        var table = new CommandHandlerTable();
        return (new CommandRegistry(table, logger), new CommandDispatcher(table, logger));
    }

    // ------------------------------------------------------------------
    // Registration
    // ------------------------------------------------------------------

    [Fact]
    public void RegisterDescriptor_ThenItems_ContainsTheRegisteredDescriptor()
    {
        var (registry, _) = CreateRegistryAndDispatcher();
        var descriptor = new CommandDescriptor("sample.one", "Sample One");

        registry.RegisterDescriptor(descriptor);

        Assert.Same(descriptor, Assert.Single(registry.Items));
    }

    [Fact]
    public void RegisterDescriptor_NullDescriptor_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => CreateRegistryAndDispatcher().Registry.RegisterDescriptor(null!));

    [Fact]
    public void RegisterDescriptor_DuplicateId_ThrowsDuplicateCommandIdException()
    {
        var (registry, _) = CreateRegistryAndDispatcher();
        registry.RegisterDescriptor(new CommandDescriptor("sample.one", "Sample One"));

        var exception = Assert.Throws<DuplicateCommandIdException>(
            () => registry.RegisterDescriptor(new CommandDescriptor("sample.one", "Sample One Again")));

        Assert.Equal("sample.one", exception.Id);
    }

    [Fact]
    public void RegisterDescriptor_DuplicateId_DoesNotReplaceTheOriginalDescriptor()
    {
        var (registry, _) = CreateRegistryAndDispatcher();
        var original = new CommandDescriptor("sample.one", "Sample One");
        registry.RegisterDescriptor(original);

        Assert.ThrowsAny<DuplicateCommandIdException>(
            () => registry.RegisterDescriptor(new CommandDescriptor("sample.one", "Replacement")));

        Assert.Same(original, Assert.Single(registry.Items));
    }

    // ------------------------------------------------------------------
    // Ordering: Category (nulls first), then Id
    // ------------------------------------------------------------------

    [Fact]
    public void Items_UncategorisedDescriptors_AreOrderedById()
    {
        var (registry, _) = CreateRegistryAndDispatcher();
        registry.RegisterDescriptor(new CommandDescriptor("charlie", "Charlie"));
        registry.RegisterDescriptor(new CommandDescriptor("alpha", "Alpha"));
        registry.RegisterDescriptor(new CommandDescriptor("bravo", "Bravo"));

        Assert.Equal(["alpha", "bravo", "charlie"], registry.Items.Select(d => d.Id));
    }

    [Fact]
    public void Items_CategorisedAndUncategorisedDescriptors_UncategorisedSortFirst()
    {
        var (registry, _) = CreateRegistryAndDispatcher();
        registry.RegisterDescriptor(new CommandDescriptor("categorised", "Categorised", category: "File"));
        registry.RegisterDescriptor(new CommandDescriptor("uncategorised", "Uncategorised"));

        Assert.Equal(["uncategorised", "categorised"], registry.Items.Select(d => d.Id));
    }

    [Fact]
    public void Items_MultipleCategories_AreOrderedAlphabeticallyByCategory()
    {
        var (registry, _) = CreateRegistryAndDispatcher();
        registry.RegisterDescriptor(new CommandDescriptor("z-item", "Z", category: "Zeta"));
        registry.RegisterDescriptor(new CommandDescriptor("a-item", "A", category: "Alpha"));

        Assert.Equal(["a-item", "z-item"], registry.Items.Select(d => d.Id));
    }

    [Fact]
    public void Items_RegistrationOrder_DoesNotAffectDeterministicOrdering()
    {
        var (registry, _) = CreateRegistryAndDispatcher();
        registry.RegisterDescriptor(new CommandDescriptor("second", "Second"));
        registry.RegisterDescriptor(new CommandDescriptor("first", "First"));
        registry.RegisterDescriptor(new CommandDescriptor("third", "Third"));

        Assert.Equal(["first", "second", "third"], registry.Items.Select(d => d.Id));
    }

    // ------------------------------------------------------------------
    // CanExecute: stored, never evaluated or filtered by the registry
    // ------------------------------------------------------------------

    [Fact]
    public void Items_IncludesDescriptorsRegardlessOfCanExecuteValue()
    {
        var (registry, _) = CreateRegistryAndDispatcher();
        registry.RegisterDescriptor(new CommandDescriptor("disabled", "Disabled", canExecute: () => false));
        registry.RegisterDescriptor(new CommandDescriptor("enabled", "Enabled", canExecute: () => true));

        Assert.Equal(2, registry.Items.Count);
    }

    [Fact]
    public void Items_NeverInvokesTheCanExecutePredicate()
    {
        var invoked = false;
        var (registry, _) = CreateRegistryAndDispatcher();
        registry.RegisterDescriptor(new CommandDescriptor("sample", "Sample", canExecute: () => { invoked = true; return true; }));

        _ = registry.Items;

        Assert.False(invoked);
    }

    // ------------------------------------------------------------------
    // InvokeAsync: resolves the default instance and dispatches it through
    // the shared handler table.
    // ------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_RegisteredId_DispatchesTheDefaultInstanceToItsHandler()
    {
        var (registry, dispatcher) = CreateRegistryAndDispatcher();
        var handler = new RecordingCommandHandler<RecordedCommandA>();
        dispatcher.RegisterHandler(handler);
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.a", "Sample A", createDefault: () => new RecordedCommandA("from-registry")));

        await registry.InvokeAsync("sample.a", CancellationToken.None);

        var received = Assert.Single(handler.Received);
        Assert.Equal("from-registry", received.Payload);
    }

    [Fact]
    public async Task InvokeAsync_HandlerSucceeds_ReturnsTheHandlersResult()
    {
        var (registry, dispatcher) = CreateRegistryAndDispatcher();
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>(
            (_, _) => Task.FromResult(CommandResult.Success("ok"))));
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.a", "Sample A", createDefault: () => new RecordedCommandA()));

        var result = await registry.InvokeAsync("sample.a", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("ok", result.Message);
    }

    [Fact]
    public async Task InvokeAsync_HandlerThrows_PropagatesUncaught()
    {
        var (registry, dispatcher) = CreateRegistryAndDispatcher();
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>(
            (_, _) => throw new InvalidOperationException("boom")));
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.a", "Sample A", createDefault: () => new RecordedCommandA()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.InvokeAsync("sample.a", CancellationToken.None));
    }

    [Fact]
    public async Task InvokeAsync_UnknownId_ThrowsCommandNotFoundException()
    {
        var (registry, _) = CreateRegistryAndDispatcher();

        var exception = await Assert.ThrowsAsync<CommandNotFoundException>(
            () => registry.InvokeAsync("does-not-exist", CancellationToken.None));

        Assert.Equal("does-not-exist", exception.Id);
    }

    [Fact]
    public async Task InvokeAsync_DescriptorWithNoCreateDefaultFactory_ThrowsCommandException()
    {
        var (registry, _) = CreateRegistryAndDispatcher();
        registry.RegisterDescriptor(new CommandDescriptor("sample.a", "Sample A"));

        await Assert.ThrowsAsync<CommandException>(
            () => registry.InvokeAsync("sample.a", CancellationToken.None));
    }

    [Fact]
    public async Task InvokeAsync_NoHandlerRegisteredForTheDefaultCommandType_ThrowsCommandHandlerNotRegisteredException()
    {
        var (registry, _) = CreateRegistryAndDispatcher();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.a", "Sample A", createDefault: () => new RecordedCommandA()));

        await Assert.ThrowsAsync<CommandHandlerNotRegisteredException>(
            () => registry.InvokeAsync("sample.a", CancellationToken.None));
    }

    [Fact]
    public async Task InvokeAsync_NullId_ThrowsArgumentNullException() =>
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => CreateRegistryAndDispatcher().Registry.InvokeAsync(null!, CancellationToken.None));

    [Fact]
    public async Task InvokeAsync_DoesNotReCheckCanExecuteBeforeDispatching()
    {
        var (registry, dispatcher) = CreateRegistryAndDispatcher();
        var handler = new RecordingCommandHandler<RecordedCommandA>();
        dispatcher.RegisterHandler(handler);
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.a", "Sample A", canExecute: () => false, createDefault: () => new RecordedCommandA()));

        // CanExecute reports false, but InvokeAsync dispatches anyway - the
        // caller's own decision to invoke is trusted, per Command Framework
        // Architecture.md's "Command Availability" section.
        await registry.InvokeAsync("sample.a", CancellationToken.None);

        Assert.Single(handler.Received);
    }

    // ------------------------------------------------------------------
    // Repeated execution / determinism
    // ------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_RepeatedInvocation_EachCallConstructsAFreshCommandInstance()
    {
        var (registry, dispatcher) = CreateRegistryAndDispatcher();
        var handler = new RecordingCommandHandler<RecordedCommandA>();
        dispatcher.RegisterHandler(handler);
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.a", "Sample A", createDefault: () => new RecordedCommandA()));

        for (var i = 0; i < 3; i++)
            await registry.InvokeAsync("sample.a", CancellationToken.None);

        Assert.Equal(3, handler.Received.Count);
        Assert.Equal(3, handler.Received.Select(c => c).Distinct().Count());
    }

    // ------------------------------------------------------------------
    // Logging
    // ------------------------------------------------------------------

    [Fact]
    public void RegisterDescriptor_LogsAtInformationLevel()
    {
        var logger = new RecordingLevelLogger();
        var (registry, _) = CreateRegistryAndDispatcher(logger);

        registry.RegisterDescriptor(new CommandDescriptor("sample.a", "Sample A"));

        Assert.True(logger.HasEntryAt(LogLevel.Information, "descriptor registered: 'sample.a'"));
    }

    [Fact]
    public async Task InvokeAsync_LogsAtInformationLevel()
    {
        var logger = new RecordingLevelLogger();
        var (registry, dispatcher) = CreateRegistryAndDispatcher(logger);
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>());
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.a", "Sample A", createDefault: () => new RecordedCommandA()));

        await registry.InvokeAsync("sample.a", CancellationToken.None);

        Assert.True(logger.HasEntryAt(LogLevel.Information, "Invoking command 'sample.a'"));
    }

    // ------------------------------------------------------------------
    // Shared state: CommandRegistry.InvokeAsync dispatches through the
    // identical handler set CommandDispatcher.RegisterHandler populates -
    // proving the CommandHandlerTable sharing design actually works, not
    // merely that each class works in isolation.
    // ------------------------------------------------------------------

    [Fact]
    public async Task InvokeAsync_DispatchesThroughTheSameHandlerTableCommandDispatcherPopulates()
    {
        var table = new CommandHandlerTable();
        var dispatcher = new CommandDispatcher(table);
        var registry = new CommandRegistry(table);
        var handler = new RecordingCommandHandler<RecordedCommandA>();

        // Registered via the dispatcher's own public API...
        dispatcher.RegisterHandler(handler);

        // ...resolved and dispatched via the registry's own public API.
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.a", "Sample A", createDefault: () => new RecordedCommandA("shared")));
        await registry.InvokeAsync("sample.a", CancellationToken.None);

        var received = Assert.Single(handler.Received);
        Assert.Equal("shared", received.Payload);
    }

    // ------------------------------------------------------------------
    // Platform Service registration (ADR-0036: an ordinary singleton, no
    // Composition Root treatment needed for the public contracts) - proves
    // the DI-resolved ICommandDispatcher and ICommandRegistry share state
    // through the same container-resolved CommandHandlerTable.
    // ------------------------------------------------------------------

    [Fact]
    public void ServiceCollection_SingletonRegistration_ResolvesICommandRegistryToCommandRegistry()
    {
        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandRegistry, CommandRegistry>();
        var provider = new TempestServiceProvider(services);

        var resolved = provider.GetService(typeof(ICommandRegistry));

        Assert.IsType<CommandRegistry>(resolved);
    }

    [Fact]
    public async Task ServiceCollection_SingletonRegistration_DispatcherAndRegistryShareTheSameHandlerTable()
    {
        var services = new ServiceCollection();
        var currentComponentAccessor = new Tempest.Core.Identity.CurrentComponentAccessor();
        services.AddInstance<Tempest.Core.Identity.ICurrentComponentAccessor>(currentComponentAccessor);
        services.AddInstance(currentComponentAccessor);
        services.AddInstance<Tempest.Core.Identity.IPermissionEvaluator>(new Tempest.Core.Identity.PermissionEvaluator());
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<CommandHandlerTable>();
        services.Singleton<ICommandDispatcher, CommandDispatcher>();
        services.Singleton<ICommandRegistry, CommandRegistry>();
        var provider = new TempestServiceProvider(services);

        var dispatcher = (ICommandDispatcher)provider.GetService(typeof(ICommandDispatcher));
        var registry = (ICommandRegistry)provider.GetService(typeof(ICommandRegistry));
        var handler = new RecordingCommandHandler<RecordedCommandA>();

        dispatcher.RegisterHandler(handler);
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.a", "Sample A", createDefault: () => new RecordedCommandA("container")));

        await registry.InvokeAsync("sample.a", CancellationToken.None);

        var received = Assert.Single(handler.Received);
        Assert.Equal("container", received.Payload);
    }
}
