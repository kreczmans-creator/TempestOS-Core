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
/// The Companion's single-view shell, in the Tempest Engineering Design
/// System's instrument idiom (`WP 14.1A`): a sunken navy top bar
/// carrying the supplied TEMPEST OS lockup and a live/offline status
/// readout, the active page over the blueprint-grid ground, and a
/// text-label bottom navigation whose selected item takes the cyan
/// accent and a 2px top rule (the pack's selection-rule pattern). No
/// icon glyphs anywhere — the pack ships no glyph set and bans
/// hand-drawn ones; labels are UPPERCASE Chakra Petch with wide
/// tracking. The Cockpit is still the default landing surface
/// (`ADR-0069`) and the Command Palette a first-class global entry point
/// (`ADR-0070`); wiring stays direct delegates (`ADR-0104`).
/// </summary>
public sealed class CompanionShellView : UserControl
{
    private readonly CompanionDataService _data;
    private readonly ContentControl _pageHost = new();
    private readonly StatusPill _statusPill = new();
    private readonly CommandPaletteOverlay _palette = new();
    private readonly Dictionary<CompanionSection, CompanionPage> _pages = [];
    private readonly Dictionary<CompanionSection, (Button Button, Border Rule)> _navItems = [];
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

        foreach (var (itemSection, item) in _navItems)
            StyleNavItem(item, itemSection == section);

        _ = RefreshActiveAsync(page, section);
    }

    /// <summary>Opens one project's detail page over the Projects section.</summary>
    public void OpenProject(ProjectSummaryDto project)
    {
        ArgumentNullException.ThrowIfNull(project);

        _detailPage = new ProjectDetailPage(project, () => Navigate(CompanionSection.Projects));
        _pageHost.Content = _detailPage;
    }

    private async Task RefreshActiveAsync(CompanionPage page, CompanionSection section)
    {
        await page.RefreshAsync();

        // Keep the palette's project entries current from whatever the
        // Projects page last fetched - the identical data path, not a
        // second fetch.
        if (section == CompanionSection.Projects)
            _lastProjects = (await _data.GetProjectsAsync()).Data ?? _lastProjects;
    }

    private Control BuildAppBar()
    {
        var app = Avalonia.Application.Current!;

        var bar = new Border
        {
            Background = BrandPalette.Brush(app, BrandPalette.SunkenBackgroundBrushKey),
            BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
            BorderBrush = BrandPalette.Brush(app, BrandPalette.CardBorderBrushKey),
            Padding = new Avalonia.Thickness(CompanionTokens.SpaceXl, 0),
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        // The supplied TEMPEST OS lockup, verbatim geometry - paper
        // wordmark on the dark ground, ink on paper (the pack's own
        // light/ink variants).
        var lockup = new TempestLockupControl
        {
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center,
            WordmarkBrush = BrandPalette.Brush(app, BrandPalette.HeadingTextBrushKey),
        };
        Avalonia.Automation.AutomationProperties.SetName(lockup, "TEMPEST OS");
        grid.Children.Add(lockup);

        var surfaceTag = new TextBlock
        {
            Text = "COMPANION",
            FontFamily = CompanionTokens.MonoFont,
            FontSize = 9,
            LetterSpacing = CompanionTokens.WideTracking,
            Foreground = BrandPalette.Brush(app, BrandPalette.SecondaryTextBrushKey),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Avalonia.Thickness(CompanionTokens.SpaceMd, 0, 0, 14),
        };
        Grid.SetColumn(surfaceTag, 1);
        grid.Children.Add(surfaceTag);

        Grid.SetColumn(_statusPill, 3);
        grid.Children.Add(_statusPill);

        var paletteButton = AppBarButton("CMD", "Open command palette");
        paletteButton.Click += (_, _) => _palette.Open();
        paletteButton.Margin = new Avalonia.Thickness(CompanionTokens.SpaceMd, 0, 0, 0);
        Grid.SetColumn(paletteButton, 4);
        grid.Children.Add(paletteButton);

        var refreshButton = AppBarButton("SYNC", "Refresh");
        refreshButton.Click += (_, _) => _ = (ActivePage ?? _pages[CurrentSection]).RefreshAsync();
        Grid.SetColumn(refreshButton, 5);
        grid.Children.Add(refreshButton);

        bar.Child = grid;
        return bar;
    }

    private static Button AppBarButton(string label, string automationName)
    {
        var app = Avalonia.Application.Current!;
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = label,
                FontFamily = CompanionTokens.TitleFont,
                FontSize = CompanionTokens.FontSizeLabel,
                FontWeight = CompanionTokens.WeightLabel,
                LetterSpacing = CompanionTokens.LabelTracking,
            },
            Foreground = BrandPalette.Brush(app, BrandPalette.AccentBrushKey),
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            MinWidth = CompanionTokens.MinTouchTarget,
            MinHeight = CompanionTokens.MinTouchTarget,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(BrandPalette.Paper050, 0.05);
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(BrandPalette.Paper050, 0.09);
        button.Resources["ButtonForegroundPointerOver"] = new SolidColorBrush(BrandPalette.Cyan400);
        Avalonia.Automation.AutomationProperties.SetName(button, automationName);
        return button;
    }

    private Control BuildNavBar()
    {
        var app = Avalonia.Application.Current!;

        var bar = new Border
        {
            Background = BrandPalette.Brush(app, BrandPalette.SunkenBackgroundBrushKey),
            BorderThickness = new Avalonia.Thickness(0, 1, 0, 0),
            BorderBrush = BrandPalette.Brush(app, BrandPalette.CardBorderBrushKey),
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*") };

        var items = new (CompanionSection Section, string Label)[]
        {
            (CompanionSection.Cockpit, "Cockpit"),
            (CompanionSection.Projects, "Projects"),
            (CompanionSection.Attention, "Attention"),
            (CompanionSection.Activity, "Activity"),
            (CompanionSection.More, "More"),
        };

        for (var i = 0; i < items.Length; i++)
        {
            var (section, label) = items[i];

            // The pack's selection-rule pattern: a 2px accent rule on the
            // selected item's leading (top) edge, over the sunken rail.
            var rule = new Border { Height = CompanionTokens.RuleThickness, Background = Brushes.Transparent };
            var button = NavButton(label);
            button.Click += (_, _) => Navigate(section);

            var cell = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
            cell.Children.Add(rule);
            Grid.SetRow(button, 1);
            cell.Children.Add(button);

            _navItems[section] = (button, rule);
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        bar.Child = grid;
        return bar;
    }

    private static Button NavButton(string label)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontFamily = CompanionTokens.TitleFont,
                FontSize = 10,
                FontWeight = CompanionTokens.WeightLabel,
                LetterSpacing = CompanionTokens.LabelTracking,
            },
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = CompanionTokens.MinTouchTarget,
        };
        button.Resources["ButtonBackgroundPointerOver"] = new SolidColorBrush(BrandPalette.Paper050, 0.05);
        button.Resources["ButtonBackgroundPressed"] = new SolidColorBrush(BrandPalette.Paper050, 0.09);
        Avalonia.Automation.AutomationProperties.SetName(button, label);
        return button;
    }

    private void StyleNavItem((Button Button, Border Rule) item, bool selected)
    {
        var app = Avalonia.Application.Current!;
        var accent = BrandPalette.Brush(app, BrandPalette.AccentBrushKey);

        item.Button.Foreground = selected ? accent : BrandPalette.Brush(app, BrandPalette.NavUnselectedBrushKey);
        item.Rule.Background = selected ? accent : Brushes.Transparent;
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
