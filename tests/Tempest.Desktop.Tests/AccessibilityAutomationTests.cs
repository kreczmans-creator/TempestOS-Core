using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Core.Commands;
using Tempest.Core.Macros;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 16.5A` (`TD-65`) — an <see cref="AutomationProperties.NameProperty"/>
/// pass over every icon-only button and watermark-only input the prior
/// accessibility review named, plus the live-region announcements for
/// <see cref="StatusBarView"/> and <see cref="ToastNotification"/>. The
/// Digital Thread graph's own search box and expand/collapse chevron are
/// covered alongside the rest of that view's own tests, in
/// <c>DigitalThreadGraphTests.cs</c>.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class AccessibilityAutomationTests
{
    // ------------------------------------------------------------
    // Icon-only buttons
    // ------------------------------------------------------------

    [AvaloniaFact]
    public void ToastNotification_DismissButton_HasAnAutomationNameAndToolTip()
    {
        var toast = new ToastNotification("Something happened.", FeedbackSeverity.Info);
        var dismissButton = toast.GetLogicalDescendants().OfType<Button>().Single();

        Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(dismissButton)));
        Assert.NotNull(ToolTip.GetTip(dismissButton));
    }

    // ------------------------------------------------------------
    // Watermark-only inputs
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task CommandPaletteOverlay_QueryBox_HasAnAutomationNameAndToolTip()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var palette = new CommandPaletteOverlay(registry);
            var panel = (StackPanel)palette.Child!;
            var query = (TextBox)panel.Children[0];

            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(query)));
            Assert.NotNull(ToolTip.GetTip(query));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task MacroManagerDialog_NameBox_HasAnAutomationNameAndToolTip()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var macroManager = (IMacroManager)host.Services!.GetService(typeof(IMacroManager));
            var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var dialog = new MacroManagerDialog(macroManager, commandRegistry, runMacro: _ => Task.FromResult(CommandResult.Success()));

            var nameBox = dialog.GetLogicalDescendants().OfType<TextBox>().Single();

            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(nameBox)));
            Assert.NotNull(ToolTip.GetTip(nameBox));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ProjectExplorerView_FilterBox_HasAnAutomationNameAndToolTip()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var view = new ProjectExplorerView(workspace.ProjectExplorer, host.Manager!);

            var filter = view.GetLogicalDescendants().OfType<TextBox>().Single(t => t.Watermark != null && t.Watermark.StartsWith("Filter"));

            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(filter)));
            Assert.NotNull(ToolTip.GetTip(filter));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // Live regions
    // ------------------------------------------------------------

    /// <summary>
    /// Review board finding #5 (`WP 16.5A-R1`) — the fix. Seven of the
    /// eight segments (Current project, Location, Selected object, Active
    /// area, Host state, Diagnostics, Notifications) change because a
    /// real, discrete backend/navigation event happened, and stay
    /// `Polite`. `Hint` alone is wired to `RibbonView`'s own
    /// `PointerEntered`/`PointerExited`, fired on *every* ribbon button —
    /// before this fix a screen-reader user sweeping the pointer across
    /// the ribbon got one `Polite` announcement per hover-enter/exit, on
    /// top of the button's own accessible name, for every button passed
    /// over. This test replaces the prior version of itself, which
    /// asserted the bug (<c>Polite</c> on all eight, hint included) as if
    /// it were the correct behaviour.
    /// </summary>
    [AvaloniaFact]
    public void StatusBarView_SevenSegmentValues_CarryAPoliteLiveSetting_ButHintDoesNot()
    {
        var statusBar = new StatusBarView();

        // The eight value TextBlocks each already carry their own
        // AutomationProperties.Name ("Current project", "Location", …,
        // set in the constructor) — the UPPERCASE label captions built by
        // the private `Segment` helper do not, so filtering on Name
        // isolates exactly the eight.
        var segmentValues = statusBar.GetLogicalDescendants().OfType<TextBlock>()
            .Where(t => AutomationProperties.GetName(t) is not null)
            .ToList();

        Assert.Equal(8, segmentValues.Count);

        var hint = segmentValues.Single(t => AutomationProperties.GetName(t) == "Hint");
        var others = segmentValues.Where(t => t != hint).ToList();

        Assert.Equal(7, others.Count);
        Assert.All(others, t => Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(t)));

        Assert.NotEqual(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(hint));
        Assert.Equal(AutomationLiveSetting.Off, AutomationProperties.GetLiveSetting(hint));
    }

    [AvaloniaFact]
    public void ToastNotification_CarriesAnAssertiveLiveSetting()
    {
        var toast = new ToastNotification("Something happened.", FeedbackSeverity.Info);

        Assert.Equal(AutomationLiveSetting.Assertive, AutomationProperties.GetLiveSetting(toast));
    }
}
