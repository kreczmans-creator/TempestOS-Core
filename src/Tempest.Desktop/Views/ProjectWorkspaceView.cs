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
/// <see cref="IShellNavigator.GoToEngineeringAsync"/>, which is the only
/// route into the Engineering Workspace — that is what makes engineering
/// work belong to a project rather than sit beside it.
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

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontWeight = FontWeight.Bold };
    private readonly TextBlock _subtitle = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85 };
    private readonly TabControl _areas = new();
    private readonly StackPanel _overview = new() { Spacing = DesignTokens.SpaceSm };
    private readonly Button _enterEngineering = new() { Content = "Enter Engineering →", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _closeProject = new() { Content = "Close Project", MinHeight = DesignTokens.MinControlSize };

    private bool _suppressAreaSelection;

    /// <summary>Raised after the user asks to enter Engineering, so the shell can render it.</summary>
    public event Action? EngineeringRequested;

    /// <summary>Raised after the user closes the project.</summary>
    public event Action? ProjectClosed;

    /// <summary>Initialises a new instance of the <see cref="ProjectWorkspaceView"/> class.</summary>
    public ProjectWorkspaceView(IProjectContext projectContext, IProjectDirectory directory, IShellNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(projectContext);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(navigator);

        _projectContext = projectContext;
        _directory = directory;
        _navigator = navigator;

        foreach (var area in Enum.GetValues<ProjectArea>())
            _areas.Items.Add(new TabItem { Header = area.ToString(), Tag = area, Content = BuildAreaContent(area) });

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

    /// <summary>Re-reads the open project and its contents.</summary>
    public async Task RefreshAsync()
    {
        await _projectContext.RefreshAsync().ConfigureAwait(true);

        if (_projectContext.Current is not { } project)
        {
            _title.Text = "No project open";
            _subtitle.Text = "Open a project from the Projects module to begin.";
            _overview.Children.Clear();
            _enterEngineering.IsEnabled = false;
            _closeProject.IsEnabled = false;
            return;
        }

        _title.Text = project.Label;
        _subtitle.Text = $"Lifecycle: {project.Status}";
        _enterEngineering.IsEnabled = true;
        _closeProject.IsEnabled = true;

        var contents = await _directory.ListProjectContentsAsync(project.Id).ConfigureAwait(true);
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

    private Control BuildAreaContent(ProjectArea area) => area switch
    {
        ProjectArea.Overview => _overview,

        // Honest, not decorative: these areas name what the platform can
        // genuinely reach today and where the work continues.
        ProjectArea.Engineering => new EmptyStateView(
            "⚙",
            "Engineering Workspace",
            "Engineering work for this project opens in the Engineering Workspace — use “Enter Engineering”."),

        ProjectArea.Documents => new EmptyStateView(
            "📄",
            "Project documents",
            "Documents and drawings belonging to this project appear in the Engineering Workspace's Documents area. A dedicated project document surface, with a real drawing viewer, is tracked as TD-80."),

        ProjectArea.Requirements => new EmptyStateView(
            "◎",
            "Project requirements",
            "Requirements belonging to this project are managed in the Engineering Workspace's Requirements area."),

        _ => new EmptyStateView("•", area.ToString(), "Not yet available."),
    };
}
