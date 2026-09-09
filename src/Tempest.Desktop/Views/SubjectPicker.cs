using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Requirements;
using Tempest.Desktop.Theming;
using Tempest.Workspace;
using Tempest.Workspace.Mechanical;

namespace Tempest.Desktop.Views;

/// <summary>
/// Evidence's own subject picker (`WP 18.2A`, §4): an object-reference
/// picker over the project's own structure — Parts, Assemblies,
/// Requirements and Deliverables — the four Kinds a piece of evidence may
/// be tagged to. A tag, never a managed structure (`D-028`): this control
/// only ever hands back an id, and never validates or dereferences it
/// itself. Initially hidden, shares the Dialog Framework's own established
/// panel styling and real modal behaviour (mirrors <see cref="InputDialog"/>).
/// </summary>
public sealed class SubjectPicker : Border
{
    private readonly EngineeringDomainContext _domainContext;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBox _filter = new() { Watermark = "Filter…", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceSm) };
    private readonly ListBox _list = new() { MaxHeight = 320 };
    private readonly Button _chooseButton = new() { Content = "Choose", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _skipButton = new() { Content = "No subject", MinHeight = DesignTokens.ControlSizeMedium };

    private IReadOnlyList<(Guid Id, string Kind, string DisplayName)> _candidates = [];
    private TaskCompletionSource<Guid?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="SubjectPicker"/> class, initially hidden.</summary>
    public SubjectPicker(EngineeringDomainContext domainContext)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        _domainContext = domainContext;

        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 440;
        MaxWidth = 560;
        MaxHeight = 480;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Tag a subject (optional)";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_skipButton);
        buttons.Children.Add(_chooseButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_filter);
        body.Children.Add(_list);
        body.Children.Add(buttons);
        Child = body;

        _chooseButton.Classes.Add(ChromeStyles.Primary);
        _skipButton.Classes.Add(ChromeStyles.Subtle);

        _filter.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) ApplyFilter(); };
        _list.DoubleTapped += (_, _) => TryComplete();
        _chooseButton.Click += (_, _) => TryComplete();
        _skipButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>
    /// Shows this picker over every live Part, Assembly, Requirement and
    /// Deliverable, and returns the one chosen, or <see langword="null"/>
    /// for no subject (skip or cancel — both mean the same thing here:
    /// evidence's own subject is always optional).
    /// </summary>
    public async Task<Guid?> PickAsync(CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _filter.Text = string.Empty;
        _candidates = await LoadCandidatesAsync(cancellationToken).ConfigureAwait(true);
        ApplyFilter();
        IsVisible = true;
        _filter.Focus();

        _pending = new TaskCompletionSource<Guid?>();
        return await _pending.Task.ConfigureAwait(true);
    }

    private async Task<IReadOnlyList<(Guid Id, string Kind, string DisplayName)>> LoadCandidatesAsync(CancellationToken cancellationToken)
    {
        var kinds = new[]
        {
            MechanicalObjectFactoryRegistry.Part,
            MechanicalObjectFactoryRegistry.Assembly,
            RequirementsService.RequirementDocumentKind,
            CanonicalObjectKinds.Deliverable,
        };

        var found = new List<(Guid, string, string)>();

        foreach (var kind in kinds)
        {
            var objects = await _domainContext.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false);
            found.AddRange(objects
                .Where(o => o is not IDeletable { IsDeleted: true })
                .Select(o => (o.Id, o.Kind, (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString())));
        }

        return [.. found.OrderBy(c => c.Item2, StringComparer.Ordinal).ThenBy(c => c.Item3, StringComparer.Ordinal)];
    }

    private void ApplyFilter()
    {
        var text = _filter.Text ?? string.Empty;

        var matches = string.IsNullOrWhiteSpace(text)
            ? _candidates
            : [.. _candidates.Where(c => c.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) || c.Kind.Contains(text, StringComparison.OrdinalIgnoreCase))];

        _list.ItemsSource = matches.Select(c => new ListBoxItem { Content = $"{c.Kind} — {c.DisplayName}", Tag = c.Id }).ToList();
    }

    private void TryComplete()
    {
        if (_list.SelectedItem is ListBoxItem { Tag: Guid id })
            Complete(id);
    }

    private void Complete(Guid? id)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(id);
    }
}
