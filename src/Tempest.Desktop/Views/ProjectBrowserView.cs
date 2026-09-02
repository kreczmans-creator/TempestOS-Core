using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Projects;
using Tempest.App.Shell;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Projects module (`TD-84`) — list, open and create real projects.
/// </summary>
/// <remarks>
/// Every row is a real <see cref="ProjectSummary"/> read from
/// <see cref="IProjectDirectory"/>, which reads the real
/// <c>IProject</c> engineering objects. Opening one goes through
/// <see cref="IShellNavigator.OpenProjectAsync"/>, so the project context
/// and the shell location move together — this view never sets a "current
/// project" of its own.
/// </remarks>
public sealed class ProjectBrowserView : UserControl
{
    private readonly IProjectDirectory _directory;
    private readonly IShellNavigator _navigator;
    private readonly Func<string, string, Task<bool>> _promptForNewProject;

    private readonly ListBox _projects = new() { MinHeight = 240 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85 };
    private readonly Button _openButton = new() { Content = "Open Project", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _newButton = new() { Content = "New Project…", MinHeight = DesignTokens.MinControlSize };

    private IReadOnlyList<ProjectSummary> _current = [];

    /// <summary>Raised after a project is opened, so the shell can render its workspace.</summary>
    public event Action? ProjectOpened;

    /// <summary>Initialises a new instance of the <see cref="ProjectBrowserView"/> class.</summary>
    /// <param name="directory">The project catalogue this view lists.</param>
    /// <param name="navigator">The shell navigator every open goes through.</param>
    /// <param name="promptForNewProject">Collects an identifier and name for a new project; returns <see langword="false"/> if the user cancelled.</param>
    public ProjectBrowserView(IProjectDirectory directory, IShellNavigator navigator, Func<string, string, Task<bool>> promptForNewProject)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(promptForNewProject);

        _directory = directory;
        _navigator = navigator;
        _promptForNewProject = promptForNewProject;

        AutomationProperties.SetName(_projects, "Projects");

        var root = new StackPanel { Spacing = DesignTokens.SpaceMd, Margin = DesignTokens.PanelPadding };
        root.Children.Add(new TextBlock { Text = "Projects", FontSize = DesignTokens.FontSizeTitle, FontWeight = FontWeight.Bold });
        root.Children.Add(new TextBlock
        {
            Text = "Every project is a real engineering object — open one to work inside it.",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap,
        });
        root.Children.Add(_projects);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        buttons.Children.Add(_openButton);
        buttons.Children.Add(_newButton);
        root.Children.Add(buttons);
        root.Children.Add(_status);

        _openButton.Click += async (_, _) => await OpenSelectedAsync().ConfigureAwait(true);
        _projects.DoubleTapped += async (_, _) => await OpenSelectedAsync().ConfigureAwait(true);
        _newButton.Click += async (_, _) => await CreateAsync().ConfigureAwait(true);

        Content = root;
    }

    /// <summary>Re-reads every project from the directory.</summary>
    public async Task RefreshAsync()
    {
        _current = await _directory.ListAsync().ConfigureAwait(true);
        _projects.ItemsSource = _current.Select(p => $"{p.Label}  —  {p.Status}").ToList();

        _status.Text = _current.Count == 0
            ? "No projects yet. Create one to begin — engineering work happens inside a project."
            : $"{_current.Count} project(s).";
    }

    private async Task OpenSelectedAsync()
    {
        if (_projects.SelectedIndex < 0 || _projects.SelectedIndex >= _current.Count)
        {
            _status.Text = "Select a project to open first.";
            return;
        }

        var project = _current[_projects.SelectedIndex];
        await _navigator.OpenProjectAsync(project.Id).ConfigureAwait(true);
        _status.Text = $"Opened {project.Label}.";
        ProjectOpened?.Invoke();
    }

    private async Task CreateAsync()
    {
        var identifier = await NextIdentifierAsync().ConfigureAwait(true);
        if (!await _promptForNewProject(identifier, string.Empty).ConfigureAwait(true))
            return;

        await RefreshAsync().ConfigureAwait(true);
    }

    /// <summary>Suggests the next free <c>P-NNNN</c> identifier, continuing whatever the catalogue already uses.</summary>
    public async Task<string> NextIdentifierAsync()
    {
        var projects = await _directory.ListAsync().ConfigureAwait(true);

        var highest = projects
            .Select(p => p.Identifier)
            .Where(id => id is not null && id.StartsWith("P-", StringComparison.OrdinalIgnoreCase))
            .Select(id => int.TryParse(id!.AsSpan(2), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"P-{highest + 1:D4}";
    }
}
