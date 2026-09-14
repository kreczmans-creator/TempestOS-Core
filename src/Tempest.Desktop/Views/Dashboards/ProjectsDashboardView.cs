using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Workspace.Projects;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Views.Dashboards;

/// <summary>
/// The Projects dashboard (`WP 19.7B`, Product Owner comment item 6,
/// sheet 3): four status tiles, three reason-carrying lists, and a simple
/// Gantt of every open project's own schedule — the tree's own "Dashboard
/// + Reports" node in <see cref="ProjectsAreaView"/>.
/// </summary>
/// <remarks>
/// One read, on entry and on <see cref="Tempest.Core.Events.IWorkspaceChanges"/>
/// (via the parent <see cref="ProjectsAreaView"/>'s own subscription):
/// <see cref="IProjectStatusReadModel"/>. Every list row opens the project
/// right up through <see cref="OpenProjectRequestedAsync"/> — the identical
/// event <see cref="ProjectsAreaView"/>'s own tree leaves already raise, so
/// the shell's existing wiring (<c>MainWindowComposer.Wire</c>) needs no
/// change of its own.
/// </remarks>
public sealed class ProjectsDashboardView : UserControl
{
    private readonly IProjectStatusReadModel _readModel;

    private readonly WrapPanel _tiles = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _blockedList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _atRiskList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _readyList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly ScrollViewer _ganttScroll = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private readonly ContentControl _ganttHost = new();

    /// <summary>Raised when the user opens a project from one of this dashboard's own lists — the shell opens it, exactly as <see cref="ProjectsAreaView.OpenProjectRequestedAsync"/> already does.</summary>
    public event Func<Guid, Task>? OpenProjectRequestedAsync;

    /// <summary>Initialises a new instance of the <see cref="ProjectsDashboardView"/> class.</summary>
    public ProjectsDashboardView(IProjectStatusReadModel readModel)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        _readModel = readModel;

        _ganttScroll.Content = _ganttHost;

        var page = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceXl };
        page.Children.Add(PageHeading.Label("PROJECTS"));
        page.Children.Add(PageHeading.Title("Dashboard"));
        page.Children.Add(Section("Status", _tiles));
        page.Children.Add(Section("Blocked projects", _blockedList));
        page.Children.Add(Section("At risk", _atRiskList));
        page.Children.Add(Section("Ready to invoice", _readyList));
        page.Children.Add(Section("Schedule", _ganttScroll));

        AutomationProperties.SetName(this, "Projects dashboard");
        Content = new ScrollViewer { Content = page };
    }

    /// <summary>Re-reads <see cref="IProjectStatusReadModel"/> and rebuilds every region.</summary>
    public async Task RefreshAsync()
    {
        var snapshot = await _readModel.ReadAsync().ConfigureAwait(true);
        Render(snapshot);
    }

    private void Render(ProjectStatusSnapshot snapshot)
    {
        RenderTiles(snapshot);

        RenderList(_blockedList, snapshot.Projects.Where(p => p.Status == ProjectHealthStatus.Blocked).ToList(), "No blocked projects.");
        RenderList(_atRiskList, snapshot.Projects.Where(p => p.Status == ProjectHealthStatus.AtRisk).ToList(), "Nothing at risk.");
        RenderList(_readyList, snapshot.Projects.Where(p => p.Status == ProjectHealthStatus.ReadyToInvoice).ToList(), "Nothing ready to invoice.");

        RenderGantt(snapshot);
    }

    private void RenderTiles(ProjectStatusSnapshot snapshot)
    {
        _tiles.Children.Clear();

        var active = snapshot.Projects.Count;
        var atRisk = snapshot.Counts.GetValueOrDefault(ProjectHealthStatus.AtRisk);
        var onHold = snapshot.Counts.GetValueOrDefault(ProjectHealthStatus.OnHold);
        var readyToInvoice = snapshot.Counts.GetValueOrDefault(ProjectHealthStatus.ReadyToInvoice);

        _tiles.Children.Add(Tile("Active", active));
        _tiles.Children.Add(Tile("At risk", atRisk));
        _tiles.Children.Add(Tile("On hold", onHold));
        _tiles.Children.Add(Tile("Ready to invoice", readyToInvoice));
    }

    private static Control Tile(string label, int count)
    {
        var tile = new Border { MinWidth = 150, Padding = DesignTokens.CardPadding, Margin = new Thickness(0, 0, DesignTokens.SpaceMd, DesignTokens.SpaceMd), CornerRadius = new CornerRadius(DesignTokens.PanelCornerRadius) };
        ThemeReactiveBrush.Bind(tile, Border.BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);
        ThemeReactiveBrush.Bind(tile, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        tile.BorderThickness = new Thickness(1);

        var content = new StackPanel { Spacing = DesignTokens.SpaceXs };
        content.Children.Add(new TextBlock { Text = count.ToString(CultureInfo.InvariantCulture), FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeDisplay, FontWeight = DesignTokens.WeightHeading });
        content.Children.Add(new TextBlock { Text = label, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.75 });
        tile.Child = content;

        AutomationProperties.SetName(tile, $"{label}: {count}");
        return tile;
    }

    private void RenderList(StackPanel host, IReadOnlyList<ProjectStatusRow> rows, string emptyText)
    {
        host.Children.Clear();

        if (rows.Count == 0)
        {
            host.Children.Add(Muted(emptyText));
            return;
        }

        foreach (var row in rows)
            host.Children.Add(Row(row));
    }

    private Control Row(ProjectStatusRow row)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock { Text = row.ProjectName, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody });
        text.Children.Add(new TextBlock { Text = row.Reason, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.75, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var open = new Button { Content = "Open", MinHeight = DesignTokens.ControlSizeSmall, VerticalAlignment = VerticalAlignment.Top };
        open.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(open, $"Open {row.ProjectName}");
        open.Click += (_, _) => _ = (OpenProjectRequestedAsync?.Invoke(row.ProjectId) ?? Task.CompletedTask);
        Grid.SetColumn(open, 1);
        grid.Children.Add(open);

        return grid;
    }

    private void RenderGantt(ProjectStatusSnapshot snapshot)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var axisStart = today.AddDays(-84);
        var axisEnd = today.AddDays(84);

        var rows = snapshot.Projects
            .OrderBy(p => p.StartDate ?? DateOnly.MaxValue)
            .Select(p => new DashboardChart.GanttRow(
                p.ProjectName, p.StartDate, p.TargetDate,
                $"quoted {(p.QuotedHours.HasValue ? p.QuotedHours.Value.ToString("0.#", CultureInfo.InvariantCulture) : "—")}h / recorded {p.RecordedHours.ToString("0.#", CultureInfo.InvariantCulture)}h",
                BrandPalette.AccentBrushKey))
            .ToList();

        if (rows.Count == 0)
        {
            _ganttHost.Content = Muted("No open projects to schedule.");
            return;
        }

        _ganttHost.Content = DashboardChart.Gantt(rows, axisStart, axisEnd, today);
    }

    private static TextBlock Muted(string text) => new() { Text = text, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.7 };

    private static StackPanel Section(string title, Control content)
    {
        var section = new StackPanel { Spacing = DesignTokens.SpaceSm };
        var heading = new TextBlock { Text = title, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2 };
        ThemeReactiveBrush.Bind(heading, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        section.Children.Add(heading);
        section.Children.Add(content);
        return section;
    }
}
