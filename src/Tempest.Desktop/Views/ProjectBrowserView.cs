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
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption };
    private readonly Button _openButton = new() { Content = "Open Project", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _newButton = new() { Content = "New Project…", MinHeight = DesignTokens.ControlSizeMedium };

    private IReadOnlyList<ProjectSummary> _current = [];

    /// <summary>The catalogue's own empty state — shown in place of an empty list, with the one action that fills it.</summary>
    private readonly EmptyStateView _empty = new("▣", "No projects yet", "Engineering work happens inside a project. Create the first one to give requirements, calculations, documents and verification a home.") { IsVisible = false };

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

        var root = new StackPanel { Spacing = DesignTokens.SpaceLg, Margin = DesignTokens.PagePadding, MaxWidth = 960, HorizontalAlignment = HorizontalAlignment.Left };
        root.Children.Add(PageHeading.Label("PROJECTS"));
        root.Children.Add(PageHeading.Title("Projects"));
        root.Children.Add(PageHeading.Lead("Every project is a real engineering object — open one to work inside it, or create the next one."));

        _projects.Background = Brushes.Transparent;
        _projects.BorderThickness = new Thickness(0);
        _empty.SetAction("Create your first project", () => _ = CreateAsync());
        var listBody = new Panel { MinHeight = 240 };
        listBody.Children.Add(_projects);
        listBody.Children.Add(_empty);
        var list = new Border
        {
            Child = listBody,
            CornerRadius = new CornerRadius(DesignTokens.PanelCornerRadius),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(DesignTokens.SpaceSm),
            Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0),
        };
        ThemeReactiveBrush.Bind(list, Border.BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);
        ThemeReactiveBrush.Bind(list, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        root.Children.Add(list);

        _openButton.Classes.Add(ChromeStyles.Primary);
        _newButton.Classes.Add(ChromeStyles.Subtle);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        buttons.Children.Add(_openButton);
        buttons.Children.Add(_newButton);
        root.Children.Add(buttons);
        ThemeReactiveBrush.Bind(_status, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
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

        _empty.IsVisible = _current.Count == 0;
        _projects.IsVisible = _current.Count > 0;
        _openButton.IsEnabled = _current.Count > 0;

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

        // "Create your first project" (and every subsequent New Project…)
        // used to leave the user back on the now-populated, but still
        // unopened, list — a dead end the empty state's own instruction
        // ("Create the first one...") never actually resolved. The
        // identifier generated above is exactly the one the newly created
        // project carries, so it is found in the just-refreshed list
        // without a second directory capability — reusing OpenSelectedAsync's
        // own OpenProjectAsync path, never a second "current project"
        // notion of this view's own.
        var created = _current.FirstOrDefault(p => string.Equals(p.Identifier, identifier, StringComparison.OrdinalIgnoreCase));
        if (created is null)
            return;

        await _navigator.OpenProjectAsync(created.Id).ConfigureAwait(true);
        _status.Text = $"Opened {created.Label}.";
        ProjectOpened?.Invoke();
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
