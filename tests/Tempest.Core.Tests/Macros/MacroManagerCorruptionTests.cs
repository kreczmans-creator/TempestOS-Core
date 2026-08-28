using Tempest.Core.Commands;
using Tempest.Core.Events;
using Tempest.Core.Macros;
using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Macros;

/// <summary>
/// `TD-60` closure tests — a corrupted persisted macro value must
/// degrade to "no persisted macros" on <see cref="MacroManager.LoadAsync"/>,
/// never throw a raw <see cref="System.Text.Json.JsonException"/> during
/// load, and a single corrupted entry must not abort the healthy rest.
/// </summary>
public class MacroManagerCorruptionTests
{
    [Theory]
    [InlineData("{")]
    [InlineData("{{{not json")]
    [InlineData("\"a string, not a list\"")]
    public async Task LoadAsync_CorruptedStoredValue_LoadsNoMacros_NeverThrows(string corruptJson)
    {
        var settingsProvider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        var registry = new CommandRegistry(new CommandHandlerTable());
        var manager = new MacroManager(settingsProvider, registry);
        await settingsProvider.SetValueAsync(MacroManager.SettingKey, corruptJson);

        var exception = await Record.ExceptionAsync(() => manager.LoadAsync());

        Assert.Null(exception);
        Assert.Empty(await manager.ListAsync());
    }

    [Fact]
    public async Task LoadAsync_OneCorruptedEntry_SkipsIt_AndLoadsTheHealthyRest()
    {
        var settingsProvider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        var registry = new CommandRegistry(new CommandHandlerTable());
        registry.RegisterDescriptor(new CommandDescriptor("test.step", "Step", createDefault: null));
        var manager = new MacroManager(settingsProvider, registry);

        var goodId = Guid.NewGuid();
        var corruptId = Guid.NewGuid();
        await settingsProvider.SetValueAsync(
            MacroManager.SettingKey,
            $$"""
            [
              {"Id":"{{corruptId}}","Name":null,"StepCommandIds":null},
              {"Id":"{{goodId}}","Name":"Good","StepCommandIds":["test.step"]}
            ]
            """);

        await manager.LoadAsync();

        var macros = await manager.ListAsync();
        Assert.Single(macros);
        Assert.Equal("Good", macros[0].Name);
    }
}
