using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Shell;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The global navigation rail (`TD-84`) — the first level of the
/// TempestOS navigation model, <c>Module → Project → Workspace</c>.
/// </summary>
/// <remarks>
/// <para>
/// A <b>view over <see cref="IShellNavigator"/></b>, never a second
/// navigation model: it raises intent and renders the navigator's own
/// current location, exactly as <see cref="RibbonView"/> is a view over
/// <c>ICommandRegistry</c>. Which module is highlighted is therefore
/// always the navigator's answer, so the rail cannot disagree with the
/// rest of the shell about where the user is.
/// </para>
/// <para>
/// Only the modules the platform can genuinely serve are shown. Tasks,
/// Commercial, Resources, Knowledge and Administration are deliberately
/// absent rather than present-and-dead — a rail button that opens
/// nothing is exactly the "fake navigation" this Work Package's own
/// controlling instruction forbids (`TD-81` tracks them).
/// </para>
/// </remarks>
public sealed class GlobalNavigationRail : UserControl
{
    private readonly IShellNavigator _navigator;
    private readonly StackPanel _buttons = new() { Spacing = DesignTokens.SpaceXs };
    private readonly List<(Button Button, ShellArea Area)> _moduleButtons = [];

    /// <summary>Raised after the user picks a module, so the shell can render it.</summary>
    public event Action? NavigationRequested;

    /// <summary>Initialises a new instance of the <see cref="GlobalNavigationRail"/> class.</summary>
    /// <param name="navigator">The shell navigator this rail is a view over.</param>
    /// <exception cref="ArgumentNullException"><paramref name="navigator"/> is <see langword="null"/>.</exception>
    public GlobalNavigationRail(IShellNavigator navigator)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        _navigator = navigator;

        Width = 148;
        Padding = new Thickness(DesignTokens.SpaceSm);
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);

        AddModule("⌂", "Home", ShellArea.Home, () => _navigator.GoHomeAsync());
        AddModule("▤", "Projects", ShellArea.Projects, () => _navigator.GoToProjectsAsync());
        AddModule("⚙", "Engineering", ShellArea.Engineering, async () =>
        {
            // Engineering is project-scoped by design: with no project
            // open there is nothing to enter, so the rail routes to the
            // project browser rather than failing.
            if (_navigator.Current.ProjectId is null)
                await _navigator.GoToProjectsAsync().ConfigureAwait(true);
            else
                await _navigator.GoToEngineeringAsync().ConfigureAwait(true);
        });

        Content = _buttons;
        RefreshSelection();
    }

    /// <summary>Re-highlights whichever module the navigator currently reports — called after every shell move.</summary>
    public void RefreshSelection()
    {
        var current = _navigator.Current.Area;

        foreach (var (button, area) in _moduleButtons)
        {
            var isCurrent = area == current
                || (area == ShellArea.Projects && current == ShellArea.ProjectWorkspace);

            button.FontWeight = isCurrent ? FontWeight.Bold : FontWeight.Normal;
            ThemeReactiveBrush.Bind(
                button,
                BackgroundProperty,
                isCurrent ? ApplicationPalette.AccentPanelBackgroundBrushKey : ApplicationPalette.PanelBackgroundBrushKey);
        }
    }

    private void AddModule(string glyph, string label, ShellArea area, Func<Task> navigate)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = DesignTokens.SpaceSm,
            Children =
            {
                new TextBlock { Text = glyph, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center },
            },
        };

        var button = new Button
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            MinHeight = DesignTokens.MinControlSize,
            Tag = area,
        };

        AutomationProperties.SetName(button, label);
        button.Click += async (_, _) =>
        {
            await navigate().ConfigureAwait(true);
            RefreshSelection();
            NavigationRequested?.Invoke();
        };

        _buttons.Children.Add(button);
        _moduleButtons.Add((button, area));
    }
}
