using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Tempest.Companion.Branding;
using Tempest.Companion.Client;
using Tempest.Companion.Contracts;
using Tempest.Companion.Services;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Views;

/// <summary>The Companion's primary sections — one per bottom-navigation item.</summary>
public enum CompanionSection
{
    /// <summary>The Cockpit — the default landing section (`ADR-0069`, reinterpreted for mobile).</summary>
    Cockpit,

    /// <summary>Project awareness and drill-down.</summary>
    Projects,

    /// <summary>Everything requiring attention, including quick actions.</summary>
    Attention,

    /// <summary>Recent Workspace activity.</summary>
    Activity,

    /// <summary>Notifications, settings, identity.</summary>
    More,
}

/// <summary>
/// The Companion's single-view shell — branded app bar (mark, wordmark,
/// live/offline pill, palette and refresh affordances), the active page,
/// and thumb-reach bottom navigation. The mobile re-expression of the
/// desktop's navigation concepts (`WP 14.0A`): the Cockpit is still the
/// default landing surface (`ADR-0069`), the Command Palette is still a
/// first-class global entry point (`ADR-0070`) — only the physical
/// navigation (bottom tabs instead of ribbon/docking) changes for the
/// form factor. Cross-collaborator wiring is direct delegates, exactly
/// <c>ADR-0104</c>'s desktop rule: no mobile-local mediator, dispatcher,
/// or event bus.
/// </summary>
public sealed class CompanionShellView : UserControl
{
    private readonly CompanionDataService _data;
    private readonly ContentControl _pageHost = new();
    private readonly TextBlock _pageTitle;
    private readonly StatusPill _statusPill = new();
    private readonly CommandPaletteOverlay _palette = new();
    private readonly Dictionary<CompanionSection, CompanionPage> _pages = [];
    private readonly Dictionary<CompanionSection, Button> _navButtons = [];
    private ProjectDetailPage? _detailPage;
    private ProjectListDto? _lastProjects;

    /// <summary>Raised when the user saves connection settings — the host persists them and rebuilds the connected stack.</summary>
    public event Action<CompanionClientSettings>? SettingsSaved;

    /// <summary>Raised when the user confirms clearing local data — the host clears the snapshot cache.</summary>
    public event Action? ClearLocalDataRequested;

    /// <summary>Initialises a new instance of the <see cref="CompanionShellView"/> class.</summary>
    /// <param name="data">The Companion data service.</param>
    /// <param name="settings">The current connection settings, rendered into the More page.</param>
    public CompanionShellView(CompanionDataService data, CompanionClientSettings settings)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(settings);

        _data = data;
        _data.ConnectionStateChanged += connected =>
            Dispatcher.UIThread.Post(() => _statusPill.Update(connected));

        _pageTitle = new TextBlock
        {
            FontFamily = CompanionTokens.TitleFont,
            FontSize = CompanionTokens.FontSizeCaption,
            LetterSpacing = 1.5,
            Opacity = 0.85,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _pages[CompanionSection.Cockpit] = new CockpitPage(data);
        _pages[CompanionSection.Projects] = new ProjectsPage(data, OpenProject);
        _pages[CompanionSection.Attention] = new AttentionPage(data);
        _pages[CompanionSection.Activity] = new ActivityPage(data);
        _pages[CompanionSection.More] = new MorePage(
            data,
            settings,
            edited => SettingsSaved?.Invoke(edited),
            () => ClearLocalDataRequested?.Invoke());

        _palette.SetEntriesSource(BuildPaletteEntries);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions($"{CompanionTokens.AppBarHeight},*,{CompanionTokens.NavBarHeight}"),
        };

        var appBar = BuildAppBar();
        root.Children.Add(appBar);

        var contentLayer = new Panel();
        contentLayer.Children.Add(_pageHost);
        contentLayer.Children.Add(_palette);
        Grid.SetRow(contentLayer, 1);
        root.Children.Add(contentLayer);

        var navBar = BuildNavBar();
        Grid.SetRow(navBar, 2);
        root.Children.Add(navBar);

        Content = root;

        Navigate(CompanionSection.Cockpit);
    }

    /// <summary>Gets the currently shown section.</summary>
    public CompanionSection CurrentSection { get; private set; } = CompanionSection.Cockpit;

    /// <summary>Gets the active page (test seam).</summary>
    public CompanionPage? ActivePage => _pageHost.Content as CompanionPage;

    /// <summary>Gets the palette overlay (test seam).</summary>
    public CommandPaletteOverlay Palette => _palette;

    /// <summary>Shows <paramref name="section"/> and refreshes its page.</summary>
    public void Navigate(CompanionSection section)
    {
        CurrentSection = section;
        _detailPage = null;

        var page = _pages[section];
        _pageHost.Content = page;
        _pageTitle.Text = page.Title.ToUpperInvariant();

        foreach (var (button, buttonSection) in _navButtons.Select(p => (p.Value, p.Key)))
            StyleNavButton(button, buttonSection == section);

        _ = RefreshActiveAsync(page, section);
    }

    /// <summary>Opens one project's detail page over the Projects section.</summary>
    public void OpenProject(ProjectSummaryDto project)
    {
        ArgumentNullException.ThrowIfNull(project);

        _detailPage = new ProjectDetailPage(project, () => Navigate(CompanionSection.Projects));
        _pageHost.Content = _detailPage;
        _pageTitle.Text = "PROJECT";
    }

    private async Task RefreshActiveAsync(CompanionPage page, CompanionSection section)
    {
        await page.RefreshAsync();

        // Keep the palette's project entries current from whatever the
        // Projects page last fetched - reusing the identical data path,
        // not a second fetch.
        if (section == CompanionSection.Projects)
            _lastProjects = (await _data.GetProjectsAsync()).Data ?? _lastProjects;
    }

    private Control BuildAppBar()
    {
        var app = Avalonia.Application.Current!;
        var chromeForeground = BrandPalette.Brush(app, BrandPalette.ChromeForegroundBrushKey);

        var bar = new Border
        {
            Background = BrandPalette.Brush(app, BrandPalette.ChromeBackgroundBrushKey),
            Padding = new Avalonia.Thickness(CompanionTokens.SpaceXl, 0),
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var mark = new TempestLogoControl
        {
            Width = 26,
            Height = 26,
            Foreground = chromeForeground,
            VerticalAlignment = VerticalAlignment.Center,
        };
        grid.Children.Add(mark);

        var wordmark = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(CompanionTokens.SpaceLg, 0, 0, 0),
        };
        wordmark.Children.Add(new TextBlock
        {
            Text = "TEMPEST OS",
            FontFamily = CompanionTokens.TitleFont,
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            LetterSpacing = 2,
            Foreground = chromeForeground,
        });
        wordmark.Children.Add(new TextBlock
        {
            Text = "COMPANION",
            FontFamily = CompanionTokens.MonoFont,
            FontSize = 9,
            LetterSpacing = 3,
            Foreground = new SolidColorBrush(BrandPalette.ElectricBlue),
        });
        Grid.SetColumn(wordmark, 1);
        grid.Children.Add(wordmark);

        _pageTitle.Foreground = chromeForeground;
        _pageTitle.HorizontalAlignment = HorizontalAlignment.Right;
        _pageTitle.Margin = new Avalonia.Thickness(CompanionTokens.SpaceMd, 0);
        Grid.SetColumn(_pageTitle, 2);
        grid.Children.Add(_pageTitle);

        Grid.SetColumn(_statusPill, 3);
        grid.Children.Add(_statusPill);

        var paletteButton = AppBarButton("⌘", "Open command palette", chromeForeground);
        paletteButton.Click += (_, _) => _palette.Open();
        Grid.SetColumn(paletteButton, 4);
        grid.Children.Add(paletteButton);

        var refreshButton = AppBarButton("↻", "Refresh", chromeForeground);
        refreshButton.Click += (_, _) => _ = (ActivePage ?? _pages[CurrentSection]).RefreshAsync();
        Grid.SetColumn(refreshButton, 5);
        grid.Children.Add(refreshButton);

        bar.Child = grid;
        return bar;
    }

    private static Button AppBarButton(string glyph, string automationName, IBrush foreground)
    {
        var button = new Button
        {
            Content = glyph,
            FontSize = 16,
            Foreground = foreground,
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            MinWidth = CompanionTokens.MinTouchTarget,
            MinHeight = CompanionTokens.MinTouchTarget,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        Avalonia.Automation.AutomationProperties.SetName(button, automationName);
        return button;
    }

    private Control BuildNavBar()
    {
        var app = Avalonia.Application.Current!;

        var bar = new Border
        {
            Background = BrandPalette.Brush(app, BrandPalette.NavBarBackgroundBrushKey),
            BorderThickness = new Avalonia.Thickness(0, 1, 0, 0),
            BorderBrush = BrandPalette.Brush(app, BrandPalette.CardBorderBrushKey),
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*") };

        var items = new (CompanionSection Section, string Glyph, string Label)[]
        {
            (CompanionSection.Cockpit, "▣", "Cockpit"),
            (CompanionSection.Projects, "⬡", "Projects"),
            (CompanionSection.Attention, "⚠", "Attention"),
            (CompanionSection.Activity, "↻", "Activity"),
            (CompanionSection.More, "☰", "More"),
        };

        for (var i = 0; i < items.Length; i++)
        {
            var (section, glyph, label) = items[i];
            var button = NavButton(glyph, label);
            button.Click += (_, _) => Navigate(section);
            _navButtons[section] = button;
            Grid.SetColumn(button, i);
            grid.Children.Add(button);
        }

        bar.Child = grid;
        return bar;
    }

    private static Button NavButton(string glyph, string label)
    {
        var stack = new StackPanel { Spacing = CompanionTokens.SpaceXs, HorizontalAlignment = HorizontalAlignment.Center };
        stack.Children.Add(new TextBlock { Text = glyph, FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontFamily = CompanionTokens.BodyFont,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var button = new Button
        {
            Content = stack,
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = CompanionTokens.MinTouchTarget,
        };
        Avalonia.Automation.AutomationProperties.SetName(button, label);
        return button;
    }

    private void StyleNavButton(Button button, bool selected)
    {
        var app = Avalonia.Application.Current!;
        button.Foreground = BrandPalette.Brush(app, selected ? BrandPalette.NavSelectedBrushKey : BrandPalette.NavUnselectedBrushKey);
        button.FontWeight = selected ? FontWeight.SemiBold : FontWeight.Normal;
    }

    private IReadOnlyList<CommandPaletteOverlay.PaletteEntry> BuildPaletteEntries()
    {
        var entries = new List<CommandPaletteOverlay.PaletteEntry>
        {
            new("Go to Cockpit", "Navigate", () => Navigate(CompanionSection.Cockpit)),
            new("Go to Projects", "Navigate", () => Navigate(CompanionSection.Projects)),
            new("Go to Attention", "Navigate", () => Navigate(CompanionSection.Attention)),
            new("Go to Activity", "Navigate", () => Navigate(CompanionSection.Activity)),
            new("Go to More / Settings", "Navigate", () => Navigate(CompanionSection.More)),
            new("Refresh current screen", "Action", () => _ = (ActivePage ?? _pages[CurrentSection]).RefreshAsync()),
        };

        foreach (var project in _lastProjects?.Projects ?? [])
            entries.Add(new($"Open project: {project.DisplayName}", "Project", () => OpenProject(project)));

        return entries;
    }
}
