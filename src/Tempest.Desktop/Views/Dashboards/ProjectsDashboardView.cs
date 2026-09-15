using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.EngineeringDomain;
using Tempest.Workspace.Projects;
using Tempest.Desktop.Documents;
using Tempest.Desktop.Documents.ProgressReports;
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
    private readonly IProjectGovernanceRegister? _governance;
    private readonly DocumentExporter? _documentExporter;
    private readonly ProgressReportDocumentRenderer? _progressReportRenderer;
    private readonly Func<string>? _applicationVersionText;

    private readonly WrapPanel _tiles = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _blockedList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _atRiskList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _readyList = new() { Spacing = DesignTokens.SpaceXs };
    // `WP 21.2A`, scope item 3: every project the three reason-carrying
    // lists above never show — On track, On hold, and (a pre-existing gap
    // this Work Package does not otherwise touch) Overdue, which none of
    // the three ever listed either — so every open project gets an Export
    // progress report exactly once across this whole page, never twice.
    // A project shown twice would carry two "Open {name}"/"Export progress
    // report {name}" buttons with the identical accessible name, breaking
    // both `AutomationNameCoverageTests`' own uniqueness expectations and
    // `DashboardsTests`' own `.Single(...)` lookups by name (found by
    // running that suite after first wiring this section as "every
    // project" — fixed by this complement instead).
    private readonly StackPanel _otherProjectsList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly ScrollViewer _ganttScroll = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private readonly ContentControl _ganttHost = new();
    private readonly TextBlock _exportStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private IReadOnlyList<ProjectStatusRow> _currentProjects = [];

    /// <summary>Raised when the user opens a project from one of this dashboard's own lists — the shell opens it, exactly as <see cref="ProjectsAreaView.OpenProjectRequestedAsync"/> already does.</summary>
    public event Func<Guid, Task>? OpenProjectRequestedAsync;

    /// <summary>Initialises a new instance of the <see cref="ProjectsDashboardView"/> class.</summary>
    /// <param name="governance">Reads a project's own live risks for the exported progress report's own Risks section (`WP 21.2A`, scope item 2). <see langword="null"/> renders that section as unavailable rather than omitting Export entirely.</param>
    /// <param name="documentExporter">Saves the rendered progress report through the file picker (`WP 21.2A`, scope item 3). <see langword="null"/> leaves Export progress report unavailable.</param>
    /// <param name="progressReportRenderer">Renders the progress report document. <see langword="null"/> leaves Export progress report unavailable.</param>
    /// <param name="applicationVersionText">The running application's own version text, for the document's own footer. <see langword="null"/> leaves Export progress report unavailable.</param>
    public ProjectsDashboardView(
        IProjectStatusReadModel readModel, IProjectGovernanceRegister? governance = null, DocumentExporter? documentExporter = null,
        ProgressReportDocumentRenderer? progressReportRenderer = null, Func<string>? applicationVersionText = null)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        _readModel = readModel;
        _governance = governance;
        _documentExporter = documentExporter;
        _progressReportRenderer = progressReportRenderer;
        _applicationVersionText = applicationVersionText;

        _ganttScroll.Content = _ganttHost;

        var page = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceXl };
        page.Children.Add(PageHeading.Label("PROJECTS"));
        page.Children.Add(PageHeading.Title("Dashboard"));
        page.Children.Add(_exportStatus);
        page.Children.Add(Section("Status", _tiles));
        page.Children.Add(Section("Blocked projects", _blockedList));
        page.Children.Add(Section("At risk", _atRiskList));
        page.Children.Add(Section("Ready to invoice", _readyList));
        page.Children.Add(Section("Schedule", _ganttScroll));
        // `WP 21.2A`, scope item 3: every project the three lists above
        // never show, each with its own Export progress report — this
        // button belongs wherever a project is, not only where it is
        // Blocked/At risk/Ready to invoice. See `_otherProjectsList`'s own
        // remarks for why this is a complement, not a fourth full list.
        page.Children.Add(Section("Other projects (On track / On hold)", _otherProjectsList));

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
        _currentProjects = snapshot.Projects;

        RenderTiles(snapshot);

        RenderList(_blockedList, snapshot.Projects.Where(p => p.Status == ProjectHealthStatus.Blocked).ToList(), "No blocked projects.");
        RenderList(_atRiskList, snapshot.Projects.Where(p => p.Status == ProjectHealthStatus.AtRisk).ToList(), "Nothing at risk.");
        RenderList(_readyList, snapshot.Projects.Where(p => p.Status == ProjectHealthStatus.ReadyToInvoice).ToList(), "Nothing ready to invoice.");
        RenderList(
            _otherProjectsList,
            snapshot.Projects.Where(p => p.Status is not (ProjectHealthStatus.Blocked or ProjectHealthStatus.AtRisk or ProjectHealthStatus.ReadyToInvoice)).ToList(),
            "No other open projects.");

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
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };

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

        // `WP 21.2A`, scope item 3.
        var export = new Button { Content = "Export progress report", MinHeight = DesignTokens.ControlSizeSmall, VerticalAlignment = VerticalAlignment.Top };
        export.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(export, $"Export progress report {row.ProjectName}");
        var exportAvailable = _documentExporter is not null && _progressReportRenderer is not null && _applicationVersionText is not null;
        export.IsEnabled = exportAvailable;
        if (!exportAvailable)
            ToolTip.SetTip(export, "Export is unavailable here.");
        export.Click += async (_, _) => await OnExportProgressReportAsync(row.ProjectId).ConfigureAwait(true);
        Grid.SetColumn(export, 2);
        grid.Children.Add(export);

        return grid;
    }

    /// <summary>Renders this project's own progress report and saves it through <see cref="DocumentExporter"/> (`WP 21.2A`, scope item 3) — built from the identical <see cref="ProjectStatusRow"/> this dashboard already reads, plus this project's own live risks (<see cref="IProjectGovernanceRegister.ListRisksAsync"/>) read only at export time, not on every render.</summary>
    private async Task OnExportProgressReportAsync(Guid projectId)
    {
        if (_documentExporter is null || _progressReportRenderer is null || _applicationVersionText is null)
        {
            _exportStatus.Text = "Export is unavailable here.";
            return;
        }

        if (_currentProjects.FirstOrDefault(p => p.ProjectId == projectId) is not { } row)
        {
            _exportStatus.Text = "That project could not be found.";
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var milestones = row.Milestones
            .Select(m => new ProgressReportMilestoneRow(m.Title, m.TargetDate, ProgressReportDocumentModel.RagFor(m.TargetDate, today)))
            .OrderBy(m => m.TargetDate)
            .ToList();
        var lookAhead = milestones.Where(m => m.TargetDate >= today && m.TargetDate <= today.AddDays(28)).ToList();

        IReadOnlyList<ProgressReportRiskRow> risks = [];
        var deliverablesNote = "Deliverable-level progress is not available from the project status summary this report renders from.";
        if (_governance is not null)
        {
            var liveRisks = await _governance.ListRisksAsync(projectId).ConfigureAwait(true);
            risks =
            [
                .. liveRisks
                    .Where(r => r.IsLive)
                    .Select(r => new ProgressReportRiskRow(r.DisplayName, r.Status.ToString(), r.Likelihood, r.Severity)),
            ];
        }

        var model = new ProgressReportDocumentModel(
            // `ProjectStatusRow` carries no business identifier of its own
            // (only `ProjectId`/`ProjectName`) — the project's own name
            // stands in for both fields rather than leaving the header
            // band's own eyebrow blank.
            ProjectCode: row.ProjectName,
            ProjectName: row.ProjectName,
            AsOfDate: today,
            HealthStatus: row.Status.ToString(),
            HealthReason: row.Reason,
            Milestones: milestones,
            LookAheadMilestones: lookAhead,
            QuotedHours: row.QuotedHours,
            RecordedHours: row.RecordedHours,
            Risks: risks,
            DeliverablesNote: deliverablesNote,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            ApplicationVersionText: _applicationVersionText());

        var result = await _documentExporter.ExportAsync(_progressReportRenderer, model, row.ProjectName, cancellationToken: CancellationToken.None).ConfigureAwait(true);
        _exportStatus.Text = result.Message;
    }

    private void RenderGantt(ProjectStatusSnapshot snapshot)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var axisStart = today.AddDays(-84);
        var axisEnd = today.AddDays(84);

        // Only a project that actually carries both dates gets a row — a
        // Gantt with no start/end means nothing to draw, never a row with
        // an honest-but-cluttering "no dates" label for every project that
        // has not been scheduled yet.
        var rows = snapshot.Projects
            .Where(p => p.StartDate is not null && p.TargetDate is not null)
            .OrderBy(p => p.StartDate)
            .Select(p => new DashboardChart.GanttRow(
                p.ProjectName, p.StartDate, p.TargetDate,
                $"quoted {(p.QuotedHours.HasValue ? p.QuotedHours.Value.ToString("0.#", CultureInfo.InvariantCulture) : "—")}h / recorded {p.RecordedHours.ToString("0.#", CultureInfo.InvariantCulture)}h",
                BrandPalette.AccentBrushKey))
            .ToList();

        if (rows.Count == 0)
        {
            _ganttHost.Content = Muted(snapshot.Projects.Count == 0
                ? "No open projects."
                : "No open project carries both a start and a target date yet.");
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
