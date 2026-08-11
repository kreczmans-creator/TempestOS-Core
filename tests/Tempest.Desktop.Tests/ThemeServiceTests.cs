using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Tempest.Core.Settings;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates Theme Switching (`WP 10.0B`'s own "Demonstrate" list)
/// against a real <see cref="ISettingsProvider"/> resolved from a running
/// <see cref="WorkspaceHost"/> — the identical Settings Platform Service
/// every other persisted Workspace value already uses (`ADR-0064`).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ThemeServiceTests
{
    [AvaloniaFact]
    public async Task ToggleAsync_SwitchesBetweenLightAndDark_AndPersistsTheChoice()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settingsProvider = (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider));
            var theme = new ThemeService(settingsProvider);

            // Reads whatever this shared persistence store already holds
            // (WorkspacePersistenceCollection serialises this test against
            // its siblings, but does not fix which one of them runs first)
            // rather than assuming Light — this test proves toggling itself
            // works, independent of prior test ordering.
            await theme.LoadAsync();
            var initial = theme.Current;
            var toggled = initial == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;

            await theme.ToggleAsync();
            Assert.Equal(toggled, theme.Current);
            Assert.Equal(toggled, Application.Current!.RequestedThemeVariant);

            var persisted = await settingsProvider.GetValueAsync(ThemeService.SettingKey);
            Assert.Equal(toggled == ThemeVariant.Dark ? nameof(ThemeVariant.Dark) : nameof(ThemeVariant.Light), persisted);

            await theme.ToggleAsync();
            Assert.Equal(initial, theme.Current);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task LoadAsync_OnAFreshHost_ReadsThePreviouslyPersistedChoiceBack()
    {
        ThemeVariant expected;

        // Both hosts below deliberately share one isolated persistence root
        // (WP 10.1B, TD-37) - this test's own point is that the second,
        // independent host reads back what the first durably wrote, which
        // requires the same store, not two different ones.
        var persistenceRootPath = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var firstHost = new WorkspaceHost(persistenceRootPath);
        try
        {
            await firstHost.StartAsync();
            var settingsProvider = (ISettingsProvider)firstHost.Services!.GetService(typeof(ISettingsProvider));
            var theme = new ThemeService(settingsProvider);

            // Reads whatever this shared persistence store already holds
            // (see ToggleAsync_...'s own identical reasoning) rather than
            // assuming Light — one toggle flips it to the opposite of
            // whatever that starting value was, which is what the second,
            // independent host below must read back.
            await theme.LoadAsync();
            await theme.ToggleAsync();
            expected = theme.Current;
        }
        finally
        {
            await firstHost.ShutdownAsync();
            await firstHost.DisposeAsync();
        }

        var secondHost = new WorkspaceHost(persistenceRootPath);
        try
        {
            await secondHost.StartAsync();
            var settingsProvider = (ISettingsProvider)secondHost.Services!.GetService(typeof(ISettingsProvider));
            var theme = new ThemeService(settingsProvider);

            await theme.LoadAsync();
            Assert.Equal(expected, theme.Current);
        }
        finally
        {
            await secondHost.ShutdownAsync();
            await secondHost.DisposeAsync();
        }
    }
}
