using Tempest.Core.Events;
using Tempest.Core.Settings;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-60` closure tests — every Desktop-side state loader documents
/// "never an exception" for its load path, yet previously threw a raw
/// <see cref="System.Text.Json.JsonException"/> for a corrupted stored
/// value (e.g. a torn write), which bricked startup permanently because
/// these loads run in the composition root. Each loader must degrade to
/// its documented first-run defaults instead.
/// </summary>
public sealed class CorruptedStateLoadTests
{
    private const string CorruptJson = "{\"ToastDurationSeconds\":4.5,\"Conf"; // a torn write's classic shape

    private static ISettingsProvider NewProvider() => new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());

    [Fact]
    public async Task UserSettings_LoadAsync_CorruptedStoredValue_FallsBackToDefaults_NeverThrows()
    {
        var provider = NewProvider();
        var settings = new UserSettings(provider);
        await provider.SetValueAsync(UserSettings.SettingKey, CorruptJson);

        var exception = await Record.ExceptionAsync(() => settings.LoadAsync());

        Assert.Null(exception);
        Assert.True(settings.ConfirmBeforeDelete);
    }

    [Fact]
    public async Task WindowUiState_LoadAsync_CorruptedStoredValue_FallsBackToDefaults_NeverThrows()
    {
        var provider = NewProvider();
        var state = new WindowUiState(provider);
        await provider.SetValueAsync(WindowUiState.SettingKey, CorruptJson);

        Assert.Null(await Record.ExceptionAsync(() => state.LoadAsync()));
    }

    [Fact]
    public async Task RecentObjectsState_LoadAsync_CorruptedStoredValue_LeavesListEmpty_NeverThrows()
    {
        var provider = NewProvider();
        var state = new RecentObjectsState(provider);
        await provider.SetValueAsync(RecentObjectsState.SettingKey, CorruptJson);

        Assert.Null(await Record.ExceptionAsync(() => state.LoadAsync()));
        Assert.Empty(state.Entries);
    }

    [Fact]
    public async Task FavouriteObjectsState_LoadAsync_CorruptedStoredValue_LeavesListEmpty_NeverThrows()
    {
        var provider = NewProvider();
        var state = new FavouriteObjectsState(provider);
        await provider.SetValueAsync(FavouriteObjectsState.SettingKey, CorruptJson);

        Assert.Null(await Record.ExceptionAsync(() => state.LoadAsync()));
        Assert.Empty(state.Entries);
    }

    [Fact]
    public async Task DesktopPanelUiState_LoadAsync_CorruptedStoredValue_FallsBackToDefaults_NeverThrows()
    {
        var provider = NewProvider();
        var state = new DesktopPanelUiState(provider);
        await provider.SetValueAsync(DesktopPanelUiState.SettingKey, CorruptJson);

        Assert.Null(await Record.ExceptionAsync(() => state.LoadAsync()));
        Assert.True(state.ExplorerPinned);
    }
}
