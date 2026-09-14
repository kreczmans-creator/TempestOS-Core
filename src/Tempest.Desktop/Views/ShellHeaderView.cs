using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Desktop.Branding;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The brand header — the one fixed strip across the top of every
/// module, and the shell's own answer to the first three questions a
/// user asks of any screen: <em>where am I</em> (the module and, inside
/// one, the area), <em>what project am I in</em> (the open project, named
/// once, in one place), and <em>what can I do here</em> (one search field
/// that reaches every registered command, and the theme switch).
/// </summary>
/// <remarks>
/// <para>
/// Carries the TEMPEST OS lockup — the brand pack's own artwork
/// (<see cref="TempestLockupControl"/>, recovered from the Companion
/// brand alignment, `WP 14.1A`), never a redrawn approximation — on the
/// sunken instrument surface the design system reserves for chrome bars.
/// </para>
/// <para>
/// A view over shell state, never a second source of it: every value
/// shown arrives through <see cref="SetContext"/> from
/// <c>MainWindow</c>'s own <c>RenderCurrentModuleAsync</c>, which reads
/// the real <c>IShellNavigator</c>/<c>IProjectContext</c> — the header
/// cannot disagree with the rail or the status bar about where the user
/// is, because all three are told by the same call.
/// </para>
/// </remarks>
public sealed class ShellHeaderView : UserControl
{
    private readonly TextBlock _module = new()
    {
        FontFamily = DesignTokens.TitleFont,
        FontSize = DesignTokens.FontSizeTitle,
        FontWeight = DesignTokens.WeightHeading,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly TextBlock _projectLabel = new()
    {
        FontSize = DesignTokens.FontSizeBody,
        FontWeight = FontWeight.Medium,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        MaxWidth = 320,
    };

    // A Button, not a Border — `WP-Z4` Productisation Phase 1 (P0):
    // clicking back into the open project used to have no affordance
    // anywhere once inside Engineering (`ReturnToProjectAsync` existed on
    // `IShellNavigator` with zero call sites in Desktop). This chip is the
    // one place the shell already names the open project, so it is the
    // natural "how do I get back" target — a bespoke chip look (like the
    // Search field below), not one of the three ChromeStyles treatments,
    // so its Background/BorderBrush stay direct properties exactly as the
    // Border they replace already used.
    private readonly Button _projectChip = new()
    {
        CornerRadius = new CornerRadius(DesignTokens.ControlCornerRadius),
        BorderThickness = new Thickness(1),
        Padding = new Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceXs + 1),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private readonly TextBlock _detail = new()
    {
        FontSize = DesignTokens.FontSizeBody,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private readonly Button _search = new() { MinHeight = DesignTokens.ControlSizeSmall, MinWidth = 220 };
    private readonly Button _theme = new() { MinHeight = DesignTokens.ControlSizeSmall, MinWidth = DesignTokens.ControlSizeSmall };
    private readonly Button _notifications = new() { MinHeight = DesignTokens.ControlSizeSmall, MinWidth = DesignTokens.ControlSizeSmall };
    private readonly TextBlock _notificationsBadge = new() { FontSize = DesignTokens.FontSizeLabel, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
    private readonly ListBox _notificationsList = new() { MaxHeight = 320, MinWidth = 280 };
    private readonly Popup _notificationsFlyout = new() { Placement = PlacementMode.BottomEdgeAlignedRight, IsLightDismissEnabled = true };
    private readonly TextBlock _principal = new() { FontSize = DesignTokens.FontSizeCaption, VerticalAlignment = VerticalAlignment.Center };

    // A Button, not a Border — `WP 19.7A`, scope item 2: "the signed-in
    // principal's name and role, which opens Settings" — the identical
    // "this is the one place the shell already names it, so it is the
    // natural target" reasoning `_projectChip`'s own remarks give.
    private readonly Button _principalChip = new()
    {
        CornerRadius = new CornerRadius(DesignTokens.ControlCornerRadius),
        Padding = new Thickness(DesignTokens.SpaceSm, DesignTokens.SpaceXs),
        VerticalAlignment = VerticalAlignment.Center,
        IsVisible = false,
    };

    /// <summary>Raised when the user asks for the global search / command palette.</summary>
    public event Action? SearchRequested;

    /// <summary>Raised when the user asks to switch theme.</summary>
    public event Action? ThemeToggleRequested;

    /// <summary>Raised when the user clicks the principal chip, asking for Settings (`WP 19.7A`).</summary>
    public event Action? SettingsRequested;

    /// <summary>
    /// Raised when the user clicks the current-project chip (`WP-Z4`
    /// Productisation Phase 1, P0) — the one existing affordance back into
    /// the open project's own workspace from anywhere else in the shell,
    /// most notably Engineering, which previously had no way back at all
    /// (<c>IShellNavigator.ReturnToProjectAsync</c> existed with zero
    /// Desktop call sites). Only raised while a project is open — see
    /// <see cref="SetContext"/>, which disables the chip otherwise.
    /// </summary>
    public event Action? ReturnToProjectRequested;

    /// <summary>Initialises a new instance of the <see cref="ShellHeaderView"/> class.</summary>
    public ShellHeaderView()
    {
        Height = DesignTokens.HeaderHeight;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, BrandPalette.SunkenBackgroundBrushKey);

        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
            Margin = new Thickness(DesignTokens.SpaceXl, 0, DesignTokens.SpaceLg, 0),
        };

        // ---- The lockup ------------------------------------------------
        var lockup = new TempestLockupControl { Height = 20, VerticalAlignment = VerticalAlignment.Center };
        ThemeReactiveBrush.Bind(lockup, TempestLockupControl.WordmarkBrushProperty, BrandPalette.HeadingTextBrushKey);
        AutomationProperties.SetName(lockup, "TempestOS");
        Grid.SetColumn(lockup, 0);
        root.Children.Add(lockup);

        var divider = new Border { Width = 1, Height = 22, Margin = new Thickness(DesignTokens.SpaceXl, 0), VerticalAlignment = VerticalAlignment.Center };
        ThemeReactiveBrush.Bind(divider, Border.BackgroundProperty, BrandPalette.HairlineStrongBrushKey);
        Grid.SetColumn(divider, 1);
        root.Children.Add(divider);

        // ---- Where am I / what project ---------------------------------
        var context = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, VerticalAlignment = VerticalAlignment.Center };
        ThemeReactiveBrush.Bind(_module, TextBlock.ForegroundProperty, BrandPalette.HeadingTextBrushKey);
        context.Children.Add(_module);

        var chipRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm, VerticalAlignment = VerticalAlignment.Center };
        var folder = IconGeometry.Build(IconGeometry.Folder, 13);
        chipRow.Children.Add(folder);
        chipRow.Children.Add(_projectLabel);
        _projectChip.Content = chipRow;
        ThemeReactiveBrush.Bind(_projectChip, Button.BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);
        ThemeReactiveBrush.Bind(_projectChip, Button.BorderBrushProperty, BrandPalette.HairlineStrongBrushKey);
        AutomationProperties.SetName(_projectChip, "Return to project");
        _projectChip.Click += (_, _) => ReturnToProjectRequested?.Invoke();
        context.Children.Add(_projectChip);

        ThemeReactiveBrush.Bind(_detail, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        context.Children.Add(_detail);

        Grid.SetColumn(context, 2);
        root.Children.Add(context);

        // ---- What can I do here ----------------------------------------
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, VerticalAlignment = VerticalAlignment.Center };

        var searchContent = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), MinWidth = 200 };
        var searchIcon = IconGeometry.Build(IconGeometry.Search, 14);
        searchIcon.Margin = new Thickness(0, 0, DesignTokens.SpaceMd, 0);
        Grid.SetColumn(searchIcon, 0);
        var searchLabel = new TextBlock { Text = "Search or run a command", FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(searchLabel, 1);
        var shortcut = new TextBlock
        {
            Text = "CTRL K",
            FontFamily = DesignTokens.MonoFont,
            FontSize = DesignTokens.FontSizeLabel,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(DesignTokens.SpaceLg, 0, 0, 0),
            Opacity = 0.7,
        };
        Grid.SetColumn(shortcut, 2);
        searchContent.Children.Add(searchIcon);
        searchContent.Children.Add(searchLabel);
        searchContent.Children.Add(shortcut);
        _search.Content = searchContent;
        _search.Classes.Add(ChromeStyles.Subtle);
        _search.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(_search, "Search or run a command");
        ToolTip.SetTip(_search, "Search every registered command (Ctrl+K)");
        _search.Click += (_, _) => SearchRequested?.Invoke();
        actions.Children.Add(_search);

        _theme.Content = IconGeometry.Build(IconGeometry.Theme, 15);
        _theme.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(_theme, "Switch theme");
        ToolTip.SetTip(_theme, "Switch between the instrument (dark) and paper (light) themes");
        _theme.Click += (_, _) => ThemeToggleRequested?.Invoke();
        actions.Children.Add(_theme);

        // ---- Notifications (`WP 19.7A`, scope item 2) -------------------
        var bellHost = new Panel();
        var bellIcon = IconGeometry.Build(IconGeometry.Bell, 15);
        bellHost.Children.Add(bellIcon);
        _notificationsBadge.FontWeight = FontWeight.Bold;
        var badgeFrame = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(3, 0),
            MinWidth = 12,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -4, -6, 0),
            Child = _notificationsBadge,
        };
        ThemeReactiveBrush.Bind(badgeFrame, Border.BackgroundProperty, BrandPalette.AccentBrushKey);
        bellHost.Children.Add(badgeFrame);
        _notifications.Content = bellHost;
        _notifications.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(_notifications, "Notifications");
        ToolTip.SetTip(_notifications, "No notifications");
        _notifications.Click += (_, _) => _notificationsFlyout.IsOpen = !_notificationsFlyout.IsOpen;
        actions.Children.Add(_notifications);

        AutomationProperties.SetName(_notificationsList, "Notifications list");
        var flyoutFrame = new Border
        {
            CornerRadius = new CornerRadius(DesignTokens.ControlCornerRadius),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(DesignTokens.SpaceSm),
            Child = _notificationsList,
        };
        ThemeReactiveBrush.Bind(flyoutFrame, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(flyoutFrame, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        _notificationsFlyout.Child = flyoutFrame;
        _notificationsFlyout.PlacementTarget = _notifications;

        // ---- The signed-in principal (`WP 19.7A`: name, role, opens Settings) ----
        var user = IconGeometry.Build(IconGeometry.User, 14);
        var principalRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm, VerticalAlignment = VerticalAlignment.Center };
        principalRow.Children.Add(user);
        principalRow.Children.Add(_principal);
        _principalChip.Content = principalRow;
        _principalChip.Margin = new Thickness(DesignTokens.SpaceSm, 0, 0, 0);
        ThemeReactiveBrush.Bind(_principal, TextBlock.ForegroundProperty, BrandPalette.MutedTextBrushKey);
        AutomationProperties.SetName(_principalChip, "Account — opens Settings");
        _principalChip.Click += (_, _) => SettingsRequested?.Invoke();
        actions.Children.Add(_principalChip);

        Grid.SetColumn(actions, 3);
        root.Children.Add(actions);
        root.Children.Add(_notificationsFlyout);

        var frame = new Border { Child = root, BorderThickness = new Thickness(0, 0, 0, 1) };
        ThemeReactiveBrush.Bind(frame, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        Content = frame;

        SetContext("Home", null, null);
    }

    /// <summary>
    /// Sets the location the header states — the module title, the open
    /// project's label (<see langword="null"/> renders an honest "No
    /// project open" rather than hiding the slot, so the absence of a
    /// project is itself visible), and an optional detail (the project
    /// area, or the engineering scope and object count).
    /// </summary>
    public void SetContext(string module, string? projectLabel, string? detail)
    {
        ArgumentNullException.ThrowIfNull(module);

        _module.Text = module;

        var hasProject = !string.IsNullOrWhiteSpace(projectLabel);
        _projectLabel.Text = hasProject ? projectLabel : "No project open";
        _projectChip.Opacity = hasProject ? 1.0 : 0.6;
        _projectChip.IsEnabled = hasProject;
        ToolTip.SetTip(_projectChip, hasProject ? $"Return to {projectLabel}" : "Open a project from the Projects module to work inside it.");

        _detail.Text = string.IsNullOrWhiteSpace(detail) ? string.Empty : $"·  {detail}";
        _detail.IsVisible = !string.IsNullOrWhiteSpace(detail);
    }

    /// <summary>Names the principal this session operates as (`TD-103`) — hidden when none could be established, never a fabricated name.</summary>
    public void SetPrincipal(string? displayName, string? role = null)
    {
        _principal.Text = string.IsNullOrWhiteSpace(role) ? displayName ?? string.Empty : $"{displayName} · {role}";
        _principalChip.IsVisible = !string.IsNullOrWhiteSpace(displayName);
        if (!string.IsNullOrWhiteSpace(displayName))
            ToolTip.SetTip(_principalChip, $"Signed in as {displayName}{(string.IsNullOrWhiteSpace(role) ? string.Empty : $" ({role})")} — click for Settings");
    }

    /// <summary>
    /// Shows the notifications bell's own count, and — while the flyout is
    /// open — the messages themselves (`WP 19.7A`, scope item 2: "a
    /// notifications bell with the count and a flyout listing the platform
    /// notifications the status bar counts today").
    /// </summary>
    public void SetNotifications(IReadOnlyList<string> messages)
    {
        _notificationsBadge.Text = messages.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _notificationsBadge.IsVisible = messages.Count > 0;
        ToolTip.SetTip(_notifications, messages.Count == 0 ? "No notifications" : $"{messages.Count} notification(s)");
        _notificationsList.ItemsSource = messages.Count == 0 ? new[] { "No notifications" } : messages;
    }

    /// <summary>Hides the search field's own long label when the window is narrow, keeping the icon and shortcut.</summary>
    public void SetCompact(bool compact)
    {
        _search.MinWidth = compact ? DesignTokens.ControlSizeSmall : 220;
        if (_search.Content is Grid grid)
        {
            grid.MinWidth = compact ? 0 : 200;
            foreach (var child in grid.Children)
            {
                if (child is TextBlock text && text.Text == "Search or run a command")
                    text.IsVisible = !compact;
            }
        }

        _module.MaxWidth = compact ? 140 : double.PositiveInfinity;
        _projectLabel.MaxWidth = compact ? 160 : 320;
    }
}
