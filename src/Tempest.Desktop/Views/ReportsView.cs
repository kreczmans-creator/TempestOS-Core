using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Evidence;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Reports area (`WP 19.2B`): every issued evidence sheet — the
/// project, reference, revision, issue date and client every
/// <see cref="Evidence.Issue"/> already carries — and every project
/// document, across open projects, filterable to one project.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a report generator.</b> This surface reads what already exists
/// — Evidence issuance (<see cref="EvidenceWorkspaceView"/>) and a
/// project's own documents (<see cref="IProjectDocumentRegister"/>) — it
/// does not define, run or export a new kind of report; "Export" opens the
/// issue sheet's own attached file exactly as "Open" opens the Evidence
/// record it belongs to, through the same `TD-80` viewer every other
/// attachment in the shell uses.
/// </para>
/// <para>
/// <b>Renders from the domain, refreshed by the change feed.</b> Every
/// list is read fresh, across every live project or the one the filter
/// names, every time <see cref="IWorkspaceChanges.Changed"/> fires — the
/// same "load when you land here, reload on the change feed" discipline
/// <see cref="InvoicingView"/> and <see cref="TimesheetWeekView"/> already
/// established. There is no manual refresh call site anywhere else in
/// this class.
/// </para>
/// </remarks>
public sealed class ReportsView : UserControl
{
    private const string AllProjectsFilter = "All projects";

    private readonly EngineeringDomainContext _domainContext;
    private readonly IProjectDirectory _projectDirectory;
    private readonly IProjectDocumentRegister _documents;
    private readonly Action<Guid, string> _openObject;
    private readonly Action<Guid, Guid> _openAttachment;

    private readonly ComboBox _projectFilter = new() { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 220 };
    private readonly TextBlock _sheetsStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly StackPanel _sheetsList = new() { Spacing = DesignTokens.SpaceXs };
    private readonly TextBlock _documentsStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly StackPanel _documentsList = new() { Spacing = DesignTokens.SpaceXs };

    private IReadOnlyList<ProjectSummary> _projects = [];
    private IWorkspaceChanges? _workspaceChanges;
    private bool _suppressFilterSelection;

    /// <summary>The change feed this view reloads its own issued-sheet and document lists from (`WP 19.2B`).</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges;
        set
        {
            if (ReferenceEquals(_workspaceChanges, value))
                return;

            if (_workspaceChanges is not null)
                _workspaceChanges.Changed -= OnWorkspaceChanged;

            _workspaceChanges = value;

            if (_workspaceChanges is not null)
                _workspaceChanges.Changed += OnWorkspaceChanged;
        }
    }

    /// <summary>Initialises a new instance of the <see cref="ReportsView"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own real object graph — every issued Evidence record is read from it.</param>
    /// <param name="projectDirectory">Every live project, for the project filter and for resolving a project document's own project.</param>
    /// <param name="documents">Every project's own documents, resolved transitively through project membership — the same register <see cref="ProjectWorkspaceView"/>'s own Documents tab reads.</param>
    /// <param name="openObject">Opens an Evidence record's own editor — the same delegate every other rail area's "open right up" already uses.</param>
    /// <param name="openAttachment">Opens a file in the `TD-80` viewer, given its owning object and the attachment itself — used for both a sheet's own "Export" and a document's own "Open".</param>
    public ReportsView(
        EngineeringDomainContext domainContext, IProjectDirectory projectDirectory, IProjectDocumentRegister documents,
        Action<Guid, string> openObject, Action<Guid, Guid> openAttachment)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(projectDirectory);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(openObject);
        ArgumentNullException.ThrowIfNull(openAttachment);

        _domainContext = domainContext;
        _projectDirectory = projectDirectory;
        _documents = documents;
        _openObject = openObject;
        _openAttachment = openAttachment;

        this.DetachedFromVisualTree += (_, _) => WorkspaceChanges = null;

        _projectFilter.Items.Add(new ComboBoxItem { Content = AllProjectsFilter, Tag = null });
        _projectFilter.SelectedIndex = 0;
        AutomationProperties.SetName(_projectFilter, "Filter by project");
        ToolTip.SetTip(_projectFilter, "Filter the sheets and documents below to one project, or show every live project.");
        _projectFilter.SelectionChanged += (_, _) =>
        {
            if (!_suppressFilterSelection)
                ApplyFilter();
        };

        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm, VerticalAlignment = VerticalAlignment.Center };
        filterRow.Children.Add(new TextBlock { Text = "Project", VerticalAlignment = VerticalAlignment.Center, FontSize = DesignTokens.FontSizeBody });
        filterRow.Children.Add(_projectFilter);

        var sheetsSection = BuildSection("Issued evidence sheets", _sheetsStatus, _sheetsList);
        var documentsSection = BuildSection("Project documents", _documentsStatus, _documentsList);

        var body = new StackPanel { Margin = DesignTokens.PagePadding, Spacing = DesignTokens.SpaceLg };
        body.Children.Add(PageHeading.Label("REPORTS"));
        body.Children.Add(PageHeading.Title("Reports"));
        body.Children.Add(PageHeading.Lead("Every issued evidence sheet and every project document, across every live project — filter to one below."));
        body.Children.Add(filterRow);
        body.Children.Add(sheetsSection);
        body.Children.Add(documentsSection);

        AutomationProperties.SetName(this, "Reports");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Reloads the project filter, every issued evidence sheet, and every project document — filtered to the currently selected project, or every live project.</summary>
    public async Task RefreshAsync()
    {
        var selectedProjectId = SelectedProjectId();

        _projects = (await _projectDirectory.ListAsync().ConfigureAwait(true))
            .OrderBy(p => p.Label, StringComparer.Ordinal)
            .ToList();

        _suppressFilterSelection = true;
        _projectFilter.Items.Clear();
        _projectFilter.Items.Add(new ComboBoxItem { Content = AllProjectsFilter, Tag = null });
        foreach (var project in _projects)
            _projectFilter.Items.Add(new ComboBoxItem { Content = project.Label, Tag = project.Id });

        _projectFilter.SelectedItem = _projectFilter.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => Equals(i.Tag, selectedProjectId)) ?? _projectFilter.Items.OfType<ComboBoxItem>().First();
        _suppressFilterSelection = false;

        await RefreshSheetsAsync().ConfigureAwait(true);
        await RefreshDocumentsAsync().ConfigureAwait(true);
    }

    private Guid? SelectedProjectId() => (_projectFilter.SelectedItem as ComboBoxItem)?.Tag as Guid?;

    /// <summary>Re-filters the already-loaded lists to the newly selected project — the filter's own selection has meaning without a further domain read.</summary>
    private void ApplyFilter() => _ = RefreshAsync();

    private async Task RefreshSheetsAsync()
    {
        var selectedProjectId = SelectedProjectId();

        var everyEvidence = await _domainContext.Repository.ListByKindAsync(Evidence.CanonicalKind).ConfigureAwait(true);
        var sheets = everyEvidence
            .OfType<Evidence>()
            .Where(e => e is not IDeletable { IsDeleted: true } && e.Issue?.IssueSheetAttachmentId is not null)
            .Where(e => selectedProjectId is null || e.ParentId == selectedProjectId)
            .Select(e => (Evidence: e, Project: _projects.FirstOrDefault(p => p.Id == e.ParentId)))
            .OrderByDescending(row => row.Evidence.Issue!.DateUtc)
            .ThenBy(row => row.Evidence.Issue!.IssueReference, StringComparer.Ordinal)
            .ToList();

        _sheetsStatus.Text = sheets.Count == 0
            ? (selectedProjectId is null ? "No issued evidence sheets yet." : "No issued evidence sheets for this project yet.")
            : $"{sheets.Count} issued sheet(s).";

        _sheetsList.Children.Clear();
        foreach (var (evidence, project) in sheets)
            _sheetsList.Children.Add(BuildSheetRow(evidence, project));
    }

    private async Task RefreshDocumentsAsync()
    {
        var selectedProjectId = SelectedProjectId();
        var projects = selectedProjectId is { } id
            ? _projects.Where(p => p.Id == id).ToList()
            : _projects;

        var rows = new List<(ProjectSummary Project, ProjectDocumentEntry Entry, ProjectDocumentAttachment Attachment)>();
        foreach (var project in projects)
        {
            var entries = await _documents.ListAsync(project.Id).ConfigureAwait(true);
            foreach (var entry in entries.Where(e => e.HasFiles))
            foreach (var attachment in entry.Attachments)
                rows.Add((project, entry, attachment));
        }

        rows = rows
            .OrderBy(r => r.Project.Label, StringComparer.Ordinal)
            .ThenBy(r => r.Entry.DisplayName, StringComparer.Ordinal)
            .ToList();

        _documentsStatus.Text = rows.Count == 0
            ? (selectedProjectId is null ? "No project documents yet." : "No documents for this project yet.")
            : $"{rows.Count} document(s) across {projects.Count} project(s).";

        _documentsList.Children.Clear();
        foreach (var row in rows)
            _documentsList.Children.Add(BuildDocumentRow(row.Project, row.Entry, row.Attachment));
    }

    private Control BuildSheetRow(Evidence evidence, ProjectSummary? project)
    {
        var issue = evidence.Issue!;
        var projectLabel = project?.Label ?? "(project unknown)";

        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock
        {
            Text = $"{projectLabel} — {issue.IssueReference} rev {issue.Revision} — {evidence.DisplayName}",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        text.Children.Add(new TextBlock
        {
            Text = $"Issued {issue.DateUtc:yyyy-MM-dd} — Client {issue.Client}",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm, VerticalAlignment = VerticalAlignment.Center };

        var open = new Button { Content = "Open", MinHeight = DesignTokens.MinControlSize };
        open.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(open, $"Open {issue.IssueReference} rev {issue.Revision}");
        open.Click += (_, _) => _openObject(evidence.Id, Evidence.CanonicalKind);
        actions.Children.Add(open);

        var export = new Button { Content = "Export", MinHeight = DesignTokens.MinControlSize };
        export.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(export, $"Export {issue.IssueReference} rev {issue.Revision}");
        export.Click += (_, _) => _openAttachment(evidence.Id, issue.IssueSheetAttachmentId!.Value);
        actions.Children.Add(export);

        return BuildRow(text, actions);
    }

    private Control BuildDocumentRow(ProjectSummary project, ProjectDocumentEntry entry, ProjectDocumentAttachment attachment)
    {
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(new TextBlock
        {
            Text = $"{project.Label} — {entry.DisplayName} — {attachment.FileName}",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        text.Children.Add(new TextBlock
        {
            Text = $"{attachment.ContentType} — {attachment.SizeInBytes:N0} bytes",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm, VerticalAlignment = VerticalAlignment.Center };

        var open = new Button { Content = "Open", MinHeight = DesignTokens.MinControlSize };
        open.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(open, $"Open {attachment.FileName}");
        open.Click += (_, _) => _openAttachment(entry.ObjectId, attachment.Id);
        actions.Children.Add(open);

        return BuildRow(text, actions);
    }

    private static Control BuildRow(Control text, Control actions)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(text);
        grid.Children.Add(actions);
        return grid;
    }

    private static Control BuildSection(string title, TextBlock status, StackPanel list)
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceSm };
        panel.Children.Add(new TextBlock { Text = title, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });
        panel.Children.Add(status);
        panel.Children.Add(list);

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = panel,
        };
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }

    private void OnWorkspaceChanged(WorkspaceChange change) =>
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await RefreshAsync().ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Best-effort background refresh — a surface the user is
                // not currently looking at re-reads correctly the next
                // time it is entered regardless (`OnEnter` in the area
                // registry), mirroring every sibling rail view's own
                // identical "the next real entry is the backstop" shape.
            }
        });
}
