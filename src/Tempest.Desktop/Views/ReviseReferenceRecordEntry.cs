using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>Collects one revision of a reference record — its own definition, an optional source citation and a change summary (`WP 18.2B`, closing a gap `WP 18.2A` disclosed).</summary>
/// <param name="DefinitionJson">The revised definition, as JSON — the same serialised shape <see cref="ReviseReferenceRecordEntry"/> pre-filled from the current record.</param>
/// <param name="ChangeSummary">Why the values changed. <see langword="null"/> if left blank.</param>
/// <param name="Source">The revised source citation, or <see langword="null"/> to withdraw it (both Publisher and Work left blank).</param>
public sealed record ReviseReferenceRecordInput(string DefinitionJson, string? ChangeSummary, SourceCitation? Source);

/// <summary>
/// The Libraries tab's own Revise entry dialog (`WP 18.2B`, closing a gap
/// `WP 18.2A` disclosed): the record's own definition fields, editable as
/// JSON — the identical raw-JSON idiom
/// <see cref="Editors.ObjectEditorView"/>'s own Calculation "Execute"
/// section already uses for a template's typed input, chosen here for the
/// same reason: five reference libraries carry five unrelated definition
/// shapes (<c>MaterialDefinition</c>, <c>FastenerDefinition</c>,
/// <c>BearingDefinition</c>, <c>StandardDefinition</c>,
/// <c>ConstantDefinition</c>), and one dialog that edits all five without
/// duplicating a bespoke, typed form per library is worth the plainer
/// control. An optional structured source citation and change summary sit
/// beside it. Initially hidden, shares the Dialog Framework's own
/// established panel styling and real modal behaviour (mirrors
/// <see cref="DeclaredFigureEntry"/>).
/// </summary>
public sealed class ReviseReferenceRecordEntry : Border
{
    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBlock _recordLabel = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, Margin = new Thickness(0, 0, 0, DesignTokens.SpaceSm) };
    private readonly TextBox _definitionJson = new() { AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 160, FontFamily = "Consolas,monospace" };
    private readonly TextBox _publisher = new() { Watermark = "Source publisher (optional)", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _work = new() { Watermark = "Source work (optional)", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _page = new() { Watermark = "Page (optional)", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _changeSummary = new() { Watermark = "Change summary (optional)", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBlock _validation = new() { FontSize = DesignTokens.FontSizeCaption, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };
    private readonly Button _reviseButton = new() { Content = "Revise", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<ReviseReferenceRecordInput?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="ReviseReferenceRecordEntry"/> class, initially hidden.</summary>
    public ReviseReferenceRecordEntry()
    {
        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 460;
        MaxWidth = 560;
        MaxHeight = 560;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Revise reference record";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_reviseButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_recordLabel);
        body.Children.Add(new ScrollViewer { Content = _definitionJson, MaxHeight = 220 });
        body.Children.Add(_publisher);
        body.Children.Add(_work);
        body.Children.Add(_page);
        body.Children.Add(_changeSummary);
        body.Children.Add(_validation);
        body.Children.Add(buttons);
        Child = body;

        _reviseButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);

        _reviseButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>
    /// Shows this dialog, pre-filled from <paramref name="recordLabel"/>'s
    /// own current <paramref name="definitionJson"/> and
    /// <paramref name="currentSource"/>, and returns what was entered, or
    /// <see langword="null"/> if the user cancelled.
    /// </summary>
    public Task<ReviseReferenceRecordInput?> PromptAsync(
        string recordLabel, string definitionJson, SourceCitation? currentSource, CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _recordLabel.Text = recordLabel;
        _definitionJson.Text = definitionJson;
        _publisher.Text = currentSource?.Publisher ?? string.Empty;
        _work.Text = currentSource?.Work ?? string.Empty;
        _page.Text = currentSource?.Page ?? string.Empty;
        _changeSummary.Text = string.Empty;
        _validation.IsVisible = false;
        IsVisible = true;
        _definitionJson.Focus();

        _pending = new TaskCompletionSource<ReviseReferenceRecordInput?>();
        return _pending.Task;
    }

    private void TryComplete()
    {
        var definitionJson = _definitionJson.Text;
        if (string.IsNullOrWhiteSpace(definitionJson))
        {
            ShowValidationError("The definition must not be blank.");
            return;
        }

        var publisher = _publisher.Text?.Trim();
        var work = _work.Text?.Trim();

        SourceCitation? source;
        if (string.IsNullOrWhiteSpace(publisher) && string.IsNullOrWhiteSpace(work))
        {
            source = null;
        }
        else if (string.IsNullOrWhiteSpace(publisher) || string.IsNullOrWhiteSpace(work))
        {
            ShowValidationError("A source citation needs both a publisher and a work — clear both to withdraw it.");
            return;
        }
        else
        {
            source = new SourceCitation(publisher, work, Page: string.IsNullOrWhiteSpace(_page.Text) ? null : _page.Text!.Trim());
        }

        var changeSummary = string.IsNullOrWhiteSpace(_changeSummary.Text) ? null : _changeSummary.Text!.Trim();

        Complete(new ReviseReferenceRecordInput(definitionJson!, changeSummary, source));
    }

    private void ShowValidationError(string message)
    {
        _validation.Text = message;
        _validation.IsVisible = true;
    }

    private void Complete(ReviseReferenceRecordInput? input)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(input);
    }
}
