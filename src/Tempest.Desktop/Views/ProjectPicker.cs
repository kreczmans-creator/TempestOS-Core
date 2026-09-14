using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Workspace.Projects;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// A project picker (`WP 19.5B`): lists every live project, with a text
/// filter, so a surface that is not itself scoped to an open project (the
/// Quotes area's own New quote action, brief scope item 5: "a New quote
/// action that picks a project and creates a Draft") can still name one.
/// Initially hidden, shares the Dialog Framework's own established panel
/// styling and real modal behaviour (mirrors <see cref="OrganisationPicker"/>).
/// </summary>
public sealed class ProjectPicker : Border
{
    private readonly IProjectDirectory _projects;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBox _filter = new() { Watermark = "Filter…", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceSm) };
    private readonly ListBox _list = new() { MaxHeight = 260 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0) };
    private readonly Button _chooseButton = new() { Content = "Choose", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private IReadOnlyList<ProjectSummary> _candidates = [];
    private TaskCompletionSource<Guid?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="ProjectPicker"/> class, initially hidden.</summary>
    public ProjectPicker(IProjectDirectory projects)
    {
        ArgumentNullException.ThrowIfNull(projects);
        _projects = projects;

        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 460;
        MaxWidth = 580;
        MaxHeight = 560;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Choose a project";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_chooseButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_filter);
        body.Children.Add(_list);
        body.Children.Add(_status);
        body.Children.Add(buttons);
        Child = body;

        _chooseButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_filter, "Filter…");
        AutomationProperties.SetName(_list, "Projects");
        AutomationProperties.SetName(_chooseButton, "Choose");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        ToolTip.SetTip(_chooseButton, "Choose");
        ToolTip.SetTip(_cancelButton, "Cancel");

        _filter.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) ApplyFilter(); };
        _list.DoubleTapped += (_, _) => TryComplete();
        _chooseButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>Shows this picker, freshly reading every live project, and returns the id chosen, or <see langword="null"/> if the user cancelled.</summary>
    public async Task<Guid?> PickAsync(CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _filter.Text = string.Empty;
        _status.Text = "Loading…";
        IsVisible = true;

        await ReloadAsync(cancellationToken).ConfigureAwait(true);
        _filter.Focus();

        _pending = new TaskCompletionSource<Guid?>();
        return await _pending.Task.ConfigureAwait(true);
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        _candidates = await _projects.ListAsync(cancellationToken).ConfigureAwait(true);
        _status.Text = _candidates.Count == 0 ? "No projects yet." : string.Empty;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var text = _filter.Text ?? string.Empty;

        var matches = string.IsNullOrWhiteSpace(text)
            ? _candidates
            : [.. _candidates.Where(c => c.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || (c.Identifier?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))];

        _list.ItemsSource = matches.Select(c => new ListBoxItem { Content = $"{c.DisplayName} ({c.Identifier})", Tag = c.Id }).ToList();
    }

    private void TryComplete()
    {
        if (_list.SelectedItem is ListBoxItem { Tag: Guid projectId })
            Complete(projectId);
    }

    private void Complete(Guid? projectId)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(projectId);
    }
}
