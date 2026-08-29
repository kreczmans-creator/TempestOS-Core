using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Project Workspace (`TD-84`) — the second level of the navigation
/// model, and the surface engineering work is entered from.
/// </summary>
/// <remarks>
/// <para>
/// Shows the open project's own real identity, lifecycle and contents,
/// and offers its areas as tabs. "Enter Engineering" goes through
/// <see cref="IShellNavigator.GoToEngineeringAsync"/>, which enters the
/// Engineering Workspace <em>with this project as its scope</em> — that is
/// what makes engineering work belong to a project rather than sit beside
/// it. It is not the only way into Engineering: the standalone workflow
/// (`TD-89`) reaches the same workspace with no project, deliberately.
/// </para>
/// <para>
/// Contents are counted by <see cref="IProjectDirectory.ListProjectContentsAsync"/>
/// over the existing <c>IHasParent</c> edge, so the figure shown is the
/// project's genuine object graph, never a stored counter that can drift
/// (the mistake the retired bootstrap <c>ProjectModel</c> made).
/// </para>
/// </remarks>
public sealed class ProjectWorkspaceView : UserControl
{
    private readonly IProjectContext _projectContext;
    private readonly IProjectDirectory _directory;
    private readonly IShellNavigator _navigator;
    private readonly IProjectDocumentRegister _documents;
    private readonly IProjectRequirementRegister _requirements;
    private readonly IProjectTaskRegister _tasks;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontWeight = FontWeight.Bold };
    private readonly TextBlock _subtitle = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85 };
    private readonly TabControl _areas = new();
    private readonly StackPanel _overview = new() { Spacing = DesignTokens.SpaceSm };
    private readonly Button _enterEngineering = new() { Content = "Enter Engineering →", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _closeProject = new() { Content = "Close Project", MinHeight = DesignTokens.MinControlSize };

    private readonly List<ContentControl> _areaHosts = [];

    // The two areas with real surfaces of their own. Built once and
    // refreshed in place, so the register a user is looking at survives a
    // re-render of the project workspace around it.
    private readonly ProjectDocumentsView _documentsView = new();
    private readonly ProjectRequirementsView _requirementsView = new();
    private readonly ProjectTasksView _tasksView = new();

    private bool _suppressAreaSelection;

    /// <summary>Raised after the user asks to enter Engineering, so the shell can render it.</summary>
    public event Action? EngineeringRequested;

    /// <summary>Raised after the user closes the project.</summary>
    public event Action? ProjectClosed;

    /// <summary>
    /// Raised when the user asks to open one of this project's files,
    /// carrying the owning object and the attachment.
    /// </summary>
    /// <remarks>
    /// The shell decides where a document opens, exactly as it does for
    /// the object editor's own Open button — this view never reaches into
    /// the workspace layout itself.
    /// </remarks>
    public event Action<Guid, Guid>? OpenAttachmentRequested;

    /// <summary>Raised when the user asks to create a task in this project.</summary>
    public event Action? CreateTaskRequested;

    /// <summary>Raised when the user asks to assign a task to themselves.</summary>
    public event Action<Guid>? AssignTaskToMeRequested;

    /// <summary>Raised when the user asks to move a task to a work state.</summary>
    public event Action<Guid, TaskWorkState>? TaskWorkStateChangeRequested;

    /// <summary>Raised when the user asks to edit a task.</summary>
    public event Action<Guid>? EditTaskRequested;

    /// <summary>Raised when the user asks to set or change a task's due date.</summary>
    public event Action<Guid>? TaskDueDateChangeRequested;

    /// <summary>The Tasks surface, so the shell can drive and inspect it.</summary>
    public ProjectTasksView TasksView => _tasksView;

    /// <summary>Initialises a new instance of the <see cref="ProjectWorkspaceView"/> class.</summary>
    public ProjectWorkspaceView(
        IProjectContext projectContext,
        IProjectDirectory directory,
        IShellNavigator navigator,
        IProjectDocumentRegister documents,
        IProjectRequirementRegister requirements,
        IProjectTaskRegister tasks)
    {
        ArgumentNullException.ThrowIfNull(projectContext);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(tasks);

        _projectContext = projectContext;
        _directory = directory;
        _navigator = navigator;
        _documents = documents;
        _requirements = requirements;
        _tasks = tasks;

        _documentsView.OpenAttachmentRequested += (ownerId, attachmentId) =>
            OpenAttachmentRequested?.Invoke(ownerId, attachmentId);

        _requirementsView.EngineeringRequested += async () =>
        {
            await _navigator.GoToEngineeringAsync().ConfigureAwait(true);
            EngineeringRequested?.Invoke();
        };

        // The Tasks surface raises intent and performs nothing. The shell
        // holds IProjectTaskService and does the work, exactly as it does
        // for opening a document — a view that mutated the domain directly
        // would be a second place task rules could live.
        _tasksView.CreateRequested += () => CreateTaskRequested?.Invoke();
        _tasksView.AssignToMeRequested += taskId => AssignTaskToMeRequested?.Invoke(taskId);
        _tasksView.WorkStateChangeRequested += (taskId, target) => TaskWorkStateChangeRequested?.Invoke(taskId, target);
        _tasksView.EditRequested += taskId => EditTaskRequested?.Invoke(taskId);
        _tasksView.DueDateChangeRequested += taskId => TaskDueDateChangeRequested?.Invoke(taskId);

        // The tab strip is the product's designed area set, declared once
        // in `ProjectAreas`. An area with no capability behind it is still
        // present and still navigable — it opens a real, project-aware
        // surface that says what is missing (`DeclaredCapabilityView`).
        foreach (var descriptor in ProjectAreas.All)
            _areas.Items.Add(new TabItem { Header = descriptor.Title, Tag = descriptor.Area, Content = BuildAreaContent(descriptor) });

        AutomationProperties.SetName(_areas, "Project areas");
        _areas.SelectionChanged += async (_, _) =>
        {
            if (_suppressAreaSelection || _areas.SelectedItem is not TabItem { Tag: ProjectArea area })
                return;

            if (!_projectContext.HasProject)
                return;

            await _navigator.GoToProjectAreaAsync(area).ConfigureAwait(true);
            if (area == ProjectArea.Engineering)
                EngineeringRequested?.Invoke();
        };

        _enterEngineering.Click += async (_, _) =>
        {
            await _navigator.GoToEngineeringAsync().ConfigureAwait(true);
            EngineeringRequested?.Invoke();
        };

        _closeProject.Click += async (_, _) =>
        {
            await _navigator.CloseProjectAsync().ConfigureAwait(true);
            ProjectClosed?.Invoke();
        };

        var header = new StackPanel { Spacing = DesignTokens.SpaceXs };
        header.Children.Add(_title);
        header.Children.Add(_subtitle);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
        actions.Children.Add(_enterEngineering);
        actions.Children.Add(_closeProject);
        header.Children.Add(actions);

        var root = new StackPanel { Spacing = DesignTokens.SpaceMd, Margin = DesignTokens.PanelPadding };
        root.Children.Add(header);
        root.Children.Add(_areas);
        Content = root;
    }

    /// <summary>Notes that a file is now open in the viewer, so its row says where it went.</summary>
    public void MarkDocumentOpened(Guid attachmentId) => _documentsView.MarkOpened(attachmentId);

    /// <summary>Re-reads the open project and its contents.</summary>
    public async Task RefreshAsync()
    {
        await _projectContext.RefreshAsync().ConfigureAwait(true);

        if (_projectContext.Current is not { } project)
        {
            _title.Text = "No project open";
            _subtitle.Text = "Open a project from the Projects module to begin.";
            _overview.Children.Clear();
            _documentsView.Show([], null);
            _requirementsView.Show([], null);
            _tasksView.Show([], [], null);
            _enterEngineering.IsEnabled = false;
            _closeProject.IsEnabled = false;
            return;
        }

        _title.Text = project.Label;
        _subtitle.Text = $"Lifecycle: {project.Status}";
        _enterEngineering.IsEnabled = true;
        _closeProject.IsEnabled = true;

        var contents = await _directory.ListProjectContentsAsync(project.Id).ConfigureAwait(true);
        _documentsView.Show(await _documents.ListAsync(project.Id).ConfigureAwait(true), project.Label);
        _requirementsView.Show(await _requirements.ListAsync(project.Id).ConfigureAwait(true), project.Label);
        _tasksView.Show(
            await _tasks.ListAsync(project.Id).ConfigureAwait(true),
            await _tasks.ListBoardAsync(project.Id).ConfigureAwait(true),
            project.Label);
        _overview.Children.Clear();
        _overview.Children.Add(new TextBlock { Text = $"Engineering objects in this project: {contents.Count}" });
        _overview.Children.Add(new TextBlock
        {
            Text = contents.Count == 0
                ? "This project has no engineering objects yet — enter Engineering to create some."
                : "Open Engineering to work on them.",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap,
        });

        RefreshAreaSurfaces();
        SyncSelectedArea();
    }

    /// <summary>Selects the tab matching the navigator's own current project area, without re-raising navigation.</summary>
    public void SyncSelectedArea()
    {
        var area = _navigator.Current.ProjectArea ?? ProjectArea.Overview;
        var tab = _areas.Items.OfType<TabItem>().FirstOrDefault(t => Equals(t.Tag, area));
        if (tab is null)
            return;

        _suppressAreaSelection = true;
        _areas.SelectedItem = tab;
        _suppressAreaSelection = false;
    }

    private Control BuildAreaContent(ProjectAreaDescriptor descriptor)
    {
        // Three areas have live content of their own; every other area
        // renders from its own declaration, so a view can never claim a
        // capability the application state does not.
        if (descriptor.Area == ProjectArea.Overview)
            return _overview;

        if (descriptor.Area == ProjectArea.Documents)
            return _documentsView;

        if (descriptor.Area == ProjectArea.Requirements)
            return _requirementsView;

        if (descriptor.Area == ProjectArea.Tasks)
            return _tasksView;

        var host = new ContentControl { Tag = descriptor.Area };
        _areaHosts.Add(host);
        host.Content = new DeclaredCapabilityView(descriptor, _projectContext.Current?.Label);
        return host;
    }

    /// <summary>Re-renders every declared area's own surface so it names the currently open project.</summary>
    private void RefreshAreaSurfaces()
    {
        var label = _projectContext.Current?.Label;

        foreach (var host in _areaHosts)
        {
            if (host.Tag is ProjectArea area)
                host.Content = new DeclaredCapabilityView(ProjectAreas.For(area), label);
        }
    }
}
