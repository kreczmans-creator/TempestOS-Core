using Tempest.Core.Commands;
using Tempest.Core.Events;
using Tempest.Core.Macros;
using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Macros;

/// <summary>
/// `WP 20.2C` (`ADR-0099`'s own addendum) — a macro step's own recorded
/// values, and the replaying prompt that answers a real invocation from
/// them, isolated from the real Engineering Disciplines the way
/// <see cref="MacroManagerTests"/> already is.
/// </summary>
public sealed class MacroStepRecordingTests
{
    /// <summary>The reason a genuinely unavailable step's own binding declares — mirrors the object-picker set's own shape.</summary>
    private const string UnavailableReason = "Needs an object picker chosen from the object tree.";

    private static (CommandRegistry Registry, CommandDispatcher Dispatcher, MacroManager MacroManager, GreetCommandHandler GreetHandler)
        CreateHarness(CommandParameterPrompt? fallbackPrompt = null)
    {
        var table = new CommandHandlerTable();
        var registry = new CommandRegistry(table);
        var dispatcher = new CommandDispatcher(table);
        var settingsProvider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        var macroManager = new MacroManager(settingsProvider, registry);

        var greetHandler = new GreetCommandHandler();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.greet", "Greet", category: "Samples", description: "Greets someone by name.")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new GreetCommand(values["name"]),
                [new CommandParameter("name", "Name")]),
        });
        dispatcher.RegisterHandler(greetHandler);

        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.unavailable", "Needs A Picker", category: "Samples")
        {
            Binding = CommandBinding.Unavailable(UnavailableReason),
        });

        dispatcher.RegisterHandler<RunMacroCommand>(new RunMacroCommandHandler(macroManager, registry, fallbackPrompt));

        return (registry, dispatcher, macroManager, greetHandler);
    }

    // ==================================================================
    // Recording (CreateAsync)
    // ==================================================================

    [Fact]
    public async Task CreateAsync_WithMacroSteps_PersistsEachStepsOwnRecordedValues()
    {
        var (_, _, macroManager, _) = CreateHarness();

        var macro = await macroManager.CreateAsync(
            "Greets Ada", [new MacroStep("sample.greet", new Dictionary<string, string> { ["name"] = "Ada" })]);

        var step = Assert.Single(macro.Steps);
        Assert.Equal("sample.greet", step.CommandId);
        Assert.Equal("Ada", step.RecordedValues["name"]);
    }

    [Fact]
    public async Task CreateAsync_LegacyStringIdOverload_RecordsNoValues()
    {
        var (_, _, macroManager, _) = CreateHarness();

        var macro = await macroManager.CreateAsync("Legacy", ["sample.greet"]);

        var step = Assert.Single(macro.Steps);
        Assert.Empty(step.RecordedValues);
    }

    [Fact]
    public async Task CreateAsync_AStepForAnUnavailableCommand_IsRefused_NamingTheReason()
    {
        var (_, _, macroManager, _) = CreateHarness();

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => macroManager.CreateAsync("Bad", [new MacroStep("sample.unavailable")]));

        Assert.Contains("sample.unavailable", exception.Message, StringComparison.Ordinal);
        Assert.Contains(UnavailableReason, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_AStepForAnUnavailableCommand_NeverCreatesTheMacro()
    {
        var (registry, _, macroManager, _) = CreateHarness();

        await Assert.ThrowsAsync<ArgumentException>(
            () => macroManager.CreateAsync("Bad", [new MacroStep("sample.unavailable")]));

        Assert.Empty(await macroManager.ListAsync());
        Assert.DoesNotContain(registry.Items, d => d.Category == "Macros");
    }

    // ==================================================================
    // Replay (RunMacroCommandHandler)
    // ==================================================================

    [Fact]
    public async Task RunMacroCommandHandler_EveryValueRecorded_ReplaysSilently_NeverAskingAnyone()
    {
        var asked = 0;
        CommandParameterPrompt fallback = (_, parameters, _, _) =>
        {
            asked++;
            return Task.FromResult<IReadOnlyDictionary<string, string>?>(
                parameters.ToDictionary(p => p.Name, _ => "should-never-be-used", StringComparer.Ordinal));
        };

        var (_, dispatcher, macroManager, greetHandler) = CreateHarness(fallback);
        var macro = await macroManager.CreateAsync(
            "Greets Ada", [new MacroStep("sample.greet", new Dictionary<string, string> { ["name"] = "Ada" })]);

        var result = await dispatcher.DispatchAsync(new RunMacroCommand(macro.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(["Ada"], greetHandler.Greeted);
        Assert.Equal(0, asked);
    }

    [Fact]
    public async Task RunMacroCommandHandler_AMissingRecordedValue_AsksTheFallbackPromptExactlyOnce()
    {
        var askedFor = new List<string>();
        CommandParameterPrompt fallback = (_, parameters, _, _) =>
        {
            askedFor.AddRange(parameters.Select(p => p.Name));
            return Task.FromResult<IReadOnlyDictionary<string, string>?>(
                parameters.ToDictionary(p => p.Name, _ => "Fallback", StringComparer.Ordinal));
        };

        var (_, dispatcher, macroManager, greetHandler) = CreateHarness(fallback);
        // Recorded with no values at all — every declared parameter is missing.
        var macro = await macroManager.CreateAsync("Greets Nobody Recorded", ["sample.greet"]);

        var result = await dispatcher.DispatchAsync(new RunMacroCommand(macro.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(["name"], askedFor);
        Assert.Equal(["Fallback"], greetHandler.Greeted);
    }

    [Fact]
    public async Task RunMacroCommandHandler_AMissingRecordedValue_PartiallyRecorded_OnlyAsksForWhatIsMissing()
    {
        // A binding with two declared parameters, one recorded and one not.
        var table = new CommandHandlerTable();
        var registry = new CommandRegistry(table);
        var dispatcher = new CommandDispatcher(table);
        var settingsProvider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        var macroManager = new MacroManager(settingsProvider, registry);
        var handler = new GreetTwoCommandHandler();

        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.greet-two", "Greet Two", category: "Samples")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new GreetTwoCommand(values["first"], values["second"]),
                [new CommandParameter("first", "First"), new CommandParameter("second", "Second")]),
        });
        dispatcher.RegisterHandler(handler);

        var askedFor = new List<string>();
        CommandParameterPrompt fallback = (_, parameters, _, _) =>
        {
            askedFor.AddRange(parameters.Select(p => p.Name));
            return Task.FromResult<IReadOnlyDictionary<string, string>?>(
                parameters.ToDictionary(p => p.Name, _ => "Asked", StringComparer.Ordinal));
        };
        dispatcher.RegisterHandler<RunMacroCommand>(new RunMacroCommandHandler(macroManager, registry, fallback));

        var macro = await macroManager.CreateAsync(
            "Partly recorded",
            [new MacroStep("sample.greet-two", new Dictionary<string, string> { ["first"] = "Recorded" })]);

        var result = await dispatcher.DispatchAsync(new RunMacroCommand(macro.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(["second"], askedFor);
        Assert.Equal(("Recorded", "Asked"), handler.Greeted.Single());
    }

    [Fact]
    public async Task RunMacroCommandHandler_AMissingRecordedValue_NoFallbackSupplied_FailsHonestly_NeverPrompting()
    {
        // Production shape: RunMacroCommandHandler constructed with no
        // fallback at all — the identical, unchanged failure the registry
        // itself has always reported for a step nothing can ask.
        var (_, dispatcher, macroManager, greetHandler) = CreateHarness(fallbackPrompt: null);
        var macro = await macroManager.CreateAsync("Nothing recorded, no fallback", ["sample.greet"]);

        var result = await dispatcher.DispatchAsync(new RunMacroCommand(macro.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("no input surface was supplied", result.Message, StringComparison.Ordinal);
        Assert.Empty(greetHandler.Greeted);
    }

    [Fact]
    public async Task RunMacroCommandHandler_TheFallbackDeclines_ReportsAFailure_NotASilentSuccess()
    {
        CommandParameterPrompt decline = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(null);

        var (_, dispatcher, macroManager, greetHandler) = CreateHarness(decline);
        var macro = await macroManager.CreateAsync("Declined", ["sample.greet"]);

        var result = await dispatcher.DispatchAsync(new RunMacroCommand(macro.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(greetHandler.Greeted);
    }

    private sealed class GreetCommand : ICommand
    {
        public GreetCommand(string name) => Name = name;
        public string Name { get; }
    }

    private sealed class GreetCommandHandler : ICommandHandler<GreetCommand>
    {
        public List<string> Greeted { get; } = [];

        public Task<CommandResult> HandleAsync(GreetCommand command, CancellationToken cancellationToken)
        {
            Greeted.Add(command.Name);
            return Task.FromResult(CommandResult.Success($"Hello, {command.Name}"));
        }
    }

    private sealed class GreetTwoCommand : ICommand
    {
        public GreetTwoCommand(string first, string second)
        {
            First = first;
            Second = second;
        }

        public string First { get; }
        public string Second { get; }
    }

    private sealed class GreetTwoCommandHandler : ICommandHandler<GreetTwoCommand>
    {
        public List<(string First, string Second)> Greeted { get; } = [];

        public Task<CommandResult> HandleAsync(GreetTwoCommand command, CancellationToken cancellationToken)
        {
            Greeted.Add((command.First, command.Second));
            return Task.FromResult(CommandResult.Success());
        }
    }
}
