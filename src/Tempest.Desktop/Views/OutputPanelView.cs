using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.BackgroundServices;
using Tempest.Core.Diagnostics;
using Tempest.Core.Modules;
using Tempest.Desktop.History;
using Tempest.Desktop.Tasks;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Output panel's own content (`WP 10.2B`) — a live
/// <see cref="IDiagnosticsProvider"/> read: Runtime Host State, every
/// tracked module's own lifecycle state, every tracked hosted service's
/// own lifecycle state. Honest, disclosed scope: this is a live
/// module/hosted-service status stream, not a captured log-history feed —
/// <see cref="Tempest.Core.Logging.ILogSink"/> is write-only, with no
/// read-back API anywhere in the platform, and adding one would be a
/// Runtime change, explicitly out of this Work Package's own "No Runtime
/// redesign" scope. Mirrors <c>StatusBarView.SetDiagnostics</c>'s own
/// identical, already-accepted "real read, never fabricated" discipline,
/// applied to a dockable panel instead of a status-bar segment.
/// </summary>
/// <remarks>
/// Extended `WP 10.6A` with two more sections — Background Tasks
/// (<see cref="RefreshBackgroundTasks"/>) and Command History
/// (<see cref="RefreshHistory"/>) — reusing this already-existing,
/// already-docked surface rather than adding a new dock panel/
/// dedicated layout slot for either — it is an ordinary dockable panel (`TD-72`).
/// </remarks>
public sealed class OutputPanelView : UserControl
{
    private readonly TextBlock _hostState = new() { FontSize = DesignTokens.FontSizeBody, FontWeight = DesignTokens.WeightHeading, Margin = DesignTokens.ControlMargin };
    private readonly ItemsControl _modules = new();
    private readonly ItemsControl _hostedServices = new();
    private readonly ItemsControl _backgroundTasks = new();
    private readonly ItemsControl _history = new();

    /// <summary>Initialises a new instance of the <see cref="OutputPanelView"/> class.</summary>
    public OutputPanelView()
    {
        var modulesHeader = new TextBlock { Text = "Modules", FontSize = DesignTokens.FontSizeHeading, FontWeight = DesignTokens.WeightHeading, Margin = DesignTokens.SectionMargin };
        var servicesHeader = new TextBlock { Text = "Hosted Services", FontSize = DesignTokens.FontSizeHeading, FontWeight = DesignTokens.WeightHeading, Margin = DesignTokens.SectionMargin };
        var backgroundTasksHeader = new TextBlock { Text = "Background Tasks", FontSize = DesignTokens.FontSizeHeading, FontWeight = DesignTokens.WeightHeading, Margin = DesignTokens.SectionMargin };
        var historyHeader = new TextBlock { Text = "Command History", FontSize = DesignTokens.FontSizeHeading, FontWeight = DesignTokens.WeightHeading, Margin = DesignTokens.SectionMargin };

        var columns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXxl };
        var left = new StackPanel();
        left.Children.Add(modulesHeader);
        left.Children.Add(_modules);
        var right = new StackPanel();
        right.Children.Add(servicesHeader);
        right.Children.Add(_hostedServices);
        var tasksColumn = new StackPanel();
        tasksColumn.Children.Add(backgroundTasksHeader);
        tasksColumn.Children.Add(_backgroundTasks);
        var historyColumn = new StackPanel();
        historyColumn.Children.Add(historyHeader);
        historyColumn.Children.Add(_history);
        columns.Children.Add(left);
        columns.Children.Add(right);
        columns.Children.Add(tasksColumn);
        columns.Children.Add(historyColumn);

        var root = new ScrollViewer
        {
            Padding = DesignTokens.PanelPadding,
            Content = new StackPanel { Children = { _hostState, columns } },
        };
        Content = root;
    }

    /// <summary>Refreshes every segment from a real, current <see cref="IDiagnosticsProvider"/> read — never a cached or assumed value.</summary>
    public void Refresh(IDiagnosticsProvider diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        _hostState.Text = $"Host state: {diagnostics.HostState}";

        _modules.ItemsSource = diagnostics.Modules
            .Select(m => $"{StateGlyph(m.State)} {m.Descriptor.Name} — {m.State}")
            .ToList();

        _hostedServices.ItemsSource = diagnostics.HostedServices
            .Select(s => $"{StateGlyph(s.State)} {s.ServiceType.Name} — {s.State}")
            .ToList();
    }

    /// <summary>Refreshes the Background Tasks section from a real <see cref="IBackgroundTaskRunner"/> read (`WP 10.6A`) — coarse state only, see <see cref="IBackgroundTaskRunner"/>'s own remarks for why.</summary>
    public void RefreshBackgroundTasks(IBackgroundTaskRunner backgroundTaskRunner)
    {
        ArgumentNullException.ThrowIfNull(backgroundTaskRunner);

        _backgroundTasks.ItemsSource = backgroundTaskRunner.Tasks
            .Select(t => $"{StateGlyph(t.State)} {t.Title} — {t.State}{(t.OutcomeMessage is null ? string.Empty : $" ({t.OutcomeMessage})")}")
            .ToList();
    }

    /// <summary>Refreshes the Command History section from a real <see cref="CommandHistoryLog"/> read (`WP 10.6A`) — most recent first.</summary>
    public void RefreshHistory(CommandHistoryLog history)
    {
        ArgumentNullException.ThrowIfNull(history);

        _history.ItemsSource = history.Entries
            .Reverse()
            .Select(e => $"{(e.Succeeded ? "✓" : "⚠")} {e.Timestamp:HH:mm:ss} — {e.Description}")
            .ToList();
    }

    private static string StateGlyph(BackgroundTaskState state) => state switch
    {
        BackgroundTaskState.Failed => "⚠",
        BackgroundTaskState.Succeeded => "✓",
        BackgroundTaskState.Cancelled => "⊘",
        _ => "…",
    };

    private static string StateGlyph(ModuleState state) => state switch
    {
        ModuleState.Failed => "⚠",
        ModuleState.Running => "✓",
        _ => "•",
    };

    private static string StateGlyph(HostedServiceState state) => state switch
    {
        HostedServiceState.Failed => "⚠",
        HostedServiceState.Running => "✓",
        _ => "•",
    };
}
