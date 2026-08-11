using Tempest.Core.Commands;
using Tempest.Core.Events;
using Tempest.Core.Macros;
using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Macros;

// Proves ADR-0098 (a macro is realised as a registered Command) against
// the real MacroManager/RunMacroCommandHandler implementations, isolated
// from real file I/O via InMemoryPersistenceStore, mirroring
// SettingsProviderTests' own established isolation shape.
public class MacroManagerTests
{
    private static (CommandRegistry Registry, CommandDispatcher Dispatcher, MacroManager MacroManager, IncrementCommandHandler IncrementHandler) CreateHarness()
    {
        var table = new CommandHandlerTable();
        var registry = new CommandRegistry(table);
        var dispatcher = new CommandDispatcher(table);
        var settingsProvider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        var macroManager = new MacroManager(settingsProvider, registry);

        // Every real registered command in this test harness that a macro
        // can legally reference — mirrors real production usage, where
        // only CreateDefault-eligible commands qualify (ADR-0098's own
        // disclosed limitation). A fresh IncrementCommandHandler instance
        // per harness — never a shared/static counter — so tests running
        // in parallel (xUnit's own default) never contaminate each other.
        var incrementHandler = new IncrementCommandHandler();
        registry.RegisterDescriptor(new CommandDescriptor("sample.increment", "Increment", createDefault: () => new IncrementCommand()));
        dispatcher.RegisterHandler(incrementHandler);

        registry.RegisterDescriptor(new CommandDescriptor("sample.fail", "Always Fails", createDefault: () => new AlwaysFailsCommand()));
        dispatcher.RegisterHandler(new AlwaysFailsCommandHandler());

        dispatcher.RegisterHandler<RunMacroCommand>(new RunMacroCommandHandler(macroManager, registry));

        return (registry, dispatcher, macroManager, incrementHandler);
    }

    [Fact]
    public async Task CreateAsync_RegistersADescriptor_InvokableThroughTheRegistry()
    {
        var (registry, _, macroManager, _) = CreateHarness();

        var macro = await macroManager.CreateAsync("My Macro", ["sample.increment", "sample.increment"]);

        var descriptor = Assert.Single(registry.Items, d => d.Id == $"macro:{macro.Id}");
        Assert.Equal("My Macro", descriptor.DisplayName);
        Assert.Equal("Macros", descriptor.Category);

        var result = await registry.InvokeAsync(descriptor.Id);

        Assert.True(result.Succeeded);
        Assert.Contains("2 step", result.Message);
    }

    [Fact]
    public async Task CreateAsync_UnknownStepId_ThrowsArgumentException()
    {
        var (_, _, macroManager, _) = CreateHarness();

        await Assert.ThrowsAsync<ArgumentException>(() => macroManager.CreateAsync("Bad Macro", ["does.not.exist"]));
    }

    [Fact]
    public async Task CreateAsync_EmptySteps_ThrowsArgumentException() =>
        await Assert.ThrowsAsync<ArgumentException>(() => CreateHarness().MacroManager.CreateAsync("Empty", []));

    [Fact]
    public async Task CreateAsync_BlankName_ThrowsArgumentException() =>
        await Assert.ThrowsAsync<ArgumentException>(() => CreateHarness().MacroManager.CreateAsync("  ", ["sample.increment"]));

    [Fact]
    public async Task ListAsync_AfterCreate_ContainsTheNewMacro()
    {
        var (_, _, macroManager, _) = CreateHarness();
        await macroManager.CreateAsync("Alpha", ["sample.increment"]);

        var macros = await macroManager.ListAsync();

        Assert.Contains(macros, m => m.Name == "Alpha");
    }

    [Fact]
    public async Task RunMacroCommandHandler_StopsAtTheFirstFailingStep()
    {
        var (registry, _, macroManager, incrementHandler) = CreateHarness();
        var macro = await macroManager.CreateAsync("Stops Early", ["sample.increment", "sample.fail", "sample.increment"]);

        var result = await registry.InvokeAsync($"macro:{macro.Id}");

        Assert.False(result.Succeeded);
        Assert.Contains("step 2/3", result.Message);
        Assert.Equal(1, incrementHandler.CallCount);
    }

    [Fact]
    public async Task DeleteAsync_RemovesFromListAsync_ButLeavesTheStaleDescriptorFailingGracefully()
    {
        var (registry, _, macroManager, _) = CreateHarness();
        var macro = await macroManager.CreateAsync("Deleted Soon", ["sample.increment"]);

        await macroManager.DeleteAsync(macro.Id);

        Assert.DoesNotContain(await macroManager.ListAsync(), m => m.Id == macro.Id);

        // ICommandRegistry exposes no removal method (confirmed, frozen) —
        // the stale descriptor still resolves; RunMacroCommandHandler's
        // own disclosed, graceful failure replaces a throw.
        var result = await registry.InvokeAsync($"macro:{macro.Id}");
        Assert.False(result.Succeeded);
        Assert.Contains("no longer exists", result.Message);
    }

    [Fact]
    public async Task LoadAsync_AgainstAFreshManager_RestoresEveryPersistedMacro()
    {
        var settingsProvider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        var table = new CommandHandlerTable();
        var registry = new CommandRegistry(table);
        registry.RegisterDescriptor(new CommandDescriptor("sample.increment", "Increment", createDefault: () => new IncrementCommand()));

        var original = new MacroManager(settingsProvider, registry);
        await original.CreateAsync("Persisted", ["sample.increment"]);

        // A fresh MacroManager instance, sharing the identical
        // ISettingsProvider — the real "survives a restart" scenario.
        var restored = new MacroManager(settingsProvider, new CommandRegistry(table));
        await restored.LoadAsync();

        var macros = await restored.ListAsync();
        Assert.Contains(macros, m => m.Name == "Persisted");
    }

    private sealed class IncrementCommand : ICommand;

    private sealed class IncrementCommandHandler : ICommandHandler<IncrementCommand>
    {
        public int CallCount { get; private set; }

        public Task<CommandResult> HandleAsync(IncrementCommand command, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(CommandResult.Success());
        }
    }

    private sealed class AlwaysFailsCommand : ICommand;

    private sealed class AlwaysFailsCommandHandler : ICommandHandler<AlwaysFailsCommand>
    {
        public Task<CommandResult> HandleAsync(AlwaysFailsCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(CommandResult.Failure("deliberate test failure"));
    }
}
