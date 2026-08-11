using Tempest.Core.Events;
using Tempest.Core.Persistence;
using Tempest.Core.Settings;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates "Persistent panel visibility"/"Persistent splitter
/// positions" for this Work Package's own Desktop-local state (Collapse/
/// Auto-Hide/Output — `WP 10.2B`) directly against
/// <see cref="DesktopPanelUiState"/>, over a real, standalone
/// <see cref="SettingsProvider"/> — no <c>WorkspaceHost</c> required, since
/// this class depends on nothing but <see cref="ISettingsProvider"/>
/// (`ADR-0064`'s own already-proven substrate, `WorkspaceState`'s own
/// identical construction pattern).
/// </summary>
public sealed class DesktopPanelUiStateTests
{
    private static ISettingsProvider NewProvider() => new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());

    [Fact]
    public async Task LoadAsync_WithNothingPersistedYet_LeavesEveryPropertyAtItsOwnDocumentedDefault()
    {
        var state = new DesktopPanelUiState(NewProvider());

        await state.LoadAsync();

        Assert.False(state.ExplorerCollapsed);
        Assert.True(state.ExplorerPinned);
        Assert.False(state.InspectorCollapsed);
        Assert.True(state.InspectorPinned);
        Assert.False(state.OutputVisible);
        Assert.Equal(160, state.OutputHeight);
        Assert.False(state.OutputCollapsed);
        Assert.True(state.OutputPinned);
        Assert.Null(state.LastAppliedPreset);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_OnAFreshInstanceOverTheSameProvider_RoundTripsEveryField()
    {
        var provider = NewProvider();
        var saved = new DesktopPanelUiState(provider)
        {
            ExplorerCollapsed = true,
            ExplorerPinned = false,
            InspectorCollapsed = true,
            InspectorPinned = false,
            OutputVisible = true,
            OutputHeight = 222,
            OutputCollapsed = true,
            OutputPinned = false,
            LastAppliedPreset = "Review",
        };

        await saved.SaveAsync();

        var loaded = new DesktopPanelUiState(provider);
        await loaded.LoadAsync();

        Assert.True(loaded.ExplorerCollapsed);
        Assert.False(loaded.ExplorerPinned);
        Assert.True(loaded.InspectorCollapsed);
        Assert.False(loaded.InspectorPinned);
        Assert.True(loaded.OutputVisible);
        Assert.Equal(222, loaded.OutputHeight);
        Assert.True(loaded.OutputCollapsed);
        Assert.False(loaded.OutputPinned);
        Assert.Equal("Review", loaded.LastAppliedPreset);
    }

    [Fact]
    public void ConstructingTwice_OverTheSameProvider_NeverThrows_MirroringWorkspaceStatesOwnRestartIdempotency()
    {
        var provider = NewProvider();
        _ = new DesktopPanelUiState(provider);

        var exception = Record.Exception(() => new DesktopPanelUiState(provider));

        Assert.Null(exception);
    }
}
