using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Evidence workspace's own citation picker (`WP 18.2A`, §4): lists
/// <b>released records only</b> across the five governed reference
/// libraries, with a text filter — an unreleased record is never offered
/// here at all, which is the surface half of the refusal
/// <c>CiteEvidenceCommand</c>'s own handler already enforces the substrate
/// half of (`ADR-0148`, decision 10). Initially hidden, shares the Dialog
/// Framework's own established panel styling and real modal behaviour
/// (mirrors <see cref="InputDialog"/>).
/// </summary>
public sealed class CitationPicker : Border
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<EvidenceLibraryRow>>> _source;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBox _filter = new() { Watermark = "Filter…", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceSm) };
    private readonly ListBox _list = new() { MaxHeight = 320 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0) };
    private readonly Button _citeButton = new() { Content = "Cite", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private IReadOnlyList<EvidenceLibraryRow> _released = [];
    private TaskCompletionSource<EvidenceLibraryRow?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="CitationPicker"/> class, initially hidden.</summary>
    /// <param name="source">Reads every record across the five governed libraries, fresh, every time this picker opens — never cached, so a record released elsewhere in the session appears here with no restart.</param>
    public CitationPicker(Func<CancellationToken, Task<IReadOnlyList<EvidenceLibraryRow>>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;

        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 480;
        MaxWidth = 620;
        MaxHeight = 520;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Cite a released reference record";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_citeButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_filter);
        body.Children.Add(_list);
        body.Children.Add(_status);
        body.Children.Add(buttons);
        Child = body;

        _citeButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_filter, "Filter…");
        AutomationProperties.SetName(_list, "Released reference records");
        AutomationProperties.SetName(_citeButton, "Cite");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        ToolTip.SetTip(_citeButton, "Cite");
        ToolTip.SetTip(_cancelButton, "Cancel");

        _filter.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) ApplyFilter(); };
        _list.DoubleTapped += (_, _) => TryComplete();
        _citeButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        // Real modal behaviour (`WP 16.5A`, `TD-65`).
        DialogModality.Install(this);
    }

    /// <summary>
    /// Shows this picker, freshly reading every released record across the
    /// five libraries, and returns the one the user chose, or
    /// <see langword="null"/> if they cancelled.
    /// </summary>
    public async Task<EvidenceLibraryRow?> PickAsync(CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _filter.Text = string.Empty;
        _status.Text = "Loading…";
        IsVisible = true;

        var all = await _source(cancellationToken).ConfigureAwait(true);
        _released = [.. all.Where(r => r.IsCitable)];
        _status.Text = _released.Count == 0 ? "No released records are available to cite." : string.Empty;
        ApplyFilter();
        _filter.Focus();

        _pending = new TaskCompletionSource<EvidenceLibraryRow?>();
        return await _pending.Task.ConfigureAwait(true);
    }

    private void ApplyFilter()
    {
        var text = _filter.Text ?? string.Empty;

        var matches = string.IsNullOrWhiteSpace(text)
            ? _released
            : [.. _released.Where(r =>
                r.RecordId.Contains(text, StringComparison.OrdinalIgnoreCase)
                || r.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || r.Library.Contains(text, StringComparison.OrdinalIgnoreCase))];

        _list.ItemsSource = matches.Select(r => new ListBoxItem
        {
            Content = $"{r.Library} — {r.RecordId} — {r.DisplayName} (rev {r.RevisionNumber}) — {r.Source?.ToString() ?? "(no source citation)"}",
            Tag = r,
        }).ToList();
    }

    private void TryComplete()
    {
        if (_list.SelectedItem is ListBoxItem { Tag: EvidenceLibraryRow row })
            Complete(row);
    }

    private void Complete(EvidenceLibraryRow? row)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(row);
    }
}
