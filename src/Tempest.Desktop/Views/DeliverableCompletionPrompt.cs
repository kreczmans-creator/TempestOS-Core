using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Workspace.Projects;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>Complete's own collected values (`WP 19.0A`, `ADR-0150`).</summary>
/// <param name="CompletedOn">The day the deliverable was completed.</param>
/// <param name="FixedPriceValue">The fixed price this deliverable is billed at. <see langword="null"/> where it is billed at time instead.</param>
/// <param name="IssuedEvidenceIds">The Evidence records, each Issued, this completion is supported by.</param>
/// <param name="DocumentIds">The documents this completion is supported by.</param>
public sealed record DeliverableCompletionInput(
    DateOnly CompletedOn, Money? FixedPriceValue, IReadOnlyList<Guid> IssuedEvidenceIds, IReadOnlyList<Guid> DocumentIds);

/// <summary>
/// The project Deliverables view's own Complete dialog (`WP 19.0A`,
/// `ADR-0150`): a completion date (defaulting to today), an optional
/// fixed-price value, a multi-select over the project's own Issued
/// evidence, and a multi-select over the project's own documents.
/// Initially hidden, shares the Dialog Framework's own established panel
/// styling and real modal behaviour (mirrors <see cref="CheckEntry"/>).
/// </summary>
public sealed class DeliverableCompletionPrompt : Border
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly IProjectDocumentRegister _documents;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly DatePicker _completedOn = new() { MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _fixedPrice = new() { Watermark = "Fixed price (\"amount currency\", blank for time-billed)", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBlock _evidenceLabel = new() { Text = "Issued evidence", FontSize = DesignTokens.FontSizeBody, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly ListBox _evidence = new() { SelectionMode = SelectionMode.Multiple, MaxHeight = 140 };
    private readonly TextBlock _documentsLabel = new() { Text = "Documents", FontSize = DesignTokens.FontSizeBody, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly ListBox _documentList = new() { SelectionMode = SelectionMode.Multiple, MaxHeight = 140 };
    private readonly TextBlock _validation = new() { FontSize = DesignTokens.FontSizeCaption, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };
    private readonly Button _completeButton = new() { Content = "Complete", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private TaskCompletionSource<DeliverableCompletionInput?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="DeliverableCompletionPrompt"/> class, initially hidden.</summary>
    public DeliverableCompletionPrompt(EngineeringDomainContext domainContext, IProjectDocumentRegister documents)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(documents);
        _domainContext = domainContext;
        _documents = documents;

        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 420;
        MaxWidth = 520;
        MaxHeight = 640;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Complete deliverable";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_completeButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_completedOn);
        body.Children.Add(_fixedPrice);
        body.Children.Add(_evidenceLabel);
        body.Children.Add(_evidence);
        body.Children.Add(_documentsLabel);
        body.Children.Add(_documentList);
        body.Children.Add(_validation);
        body.Children.Add(buttons);
        Child = new ScrollViewer { Content = body };

        _completeButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_fixedPrice, "Fixed price");
        AutomationProperties.SetName(_evidence, "Issued evidence");
        AutomationProperties.SetName(_documentList, "Documents");
        AutomationProperties.SetName(_completeButton, "Complete");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        ToolTip.SetTip(_completeButton, "Complete");
        ToolTip.SetTip(_cancelButton, "Cancel");

        _completeButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>
    /// Shows this dialog over <paramref name="projectId"/>'s own Issued
    /// evidence and documents, and returns the completion entered, or
    /// <see langword="null"/> if the user cancelled.
    /// </summary>
    public async Task<DeliverableCompletionInput?> PromptAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _completedOn.SelectedDate = DateTimeOffset.Now;
        _fixedPrice.Text = string.Empty;
        _validation.IsVisible = false;

        var everyEvidence = await _domainContext.Repository.ListByKindAsync(Evidence.CanonicalKind, cancellationToken).ConfigureAwait(true);
        var issued = everyEvidence
            .OfType<Evidence>()
            .Where(e => e is not IDeletable { IsDeleted: true } && e.ParentId == projectId && e.Status == EvidenceStatus.Issued)
            .OrderBy(e => e.DisplayName, StringComparer.Ordinal)
            .ToList();
        _evidence.ItemsSource = issued.Select(e => new ListBoxItem { Content = e.DisplayName, Tag = e.Id }).ToList();

        var documents = await _documents.ListAsync(projectId, cancellationToken).ConfigureAwait(true);
        _documentList.ItemsSource = documents
            .OrderBy(d => d.DisplayName, StringComparer.Ordinal)
            .Select(d => new ListBoxItem { Content = d.DisplayName, Tag = d.ObjectId })
            .ToList();

        IsVisible = true;

        _pending = new TaskCompletionSource<DeliverableCompletionInput?>();
        return await _pending.Task.ConfigureAwait(true);
    }

    private void TryComplete()
    {
        if (_completedOn.SelectedDate is not { } completedOn)
        {
            ShowValidationError("A completion date is required.");
            return;
        }

        Money? fixedPrice = null;
        var priceText = _fixedPrice.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(priceText))
        {
            if (!TryParseMoney(priceText, out var parsed))
            {
                ShowValidationError("Fixed price must be \"<amount> <currency>\" (e.g. \"5000 GBP\"), or blank for time-billed.");
                return;
            }

            fixedPrice = parsed;
        }

        var evidenceIds = _evidence.SelectedItems!.Cast<ListBoxItem>().Select(i => (Guid)i.Tag!).ToList();
        var documentIds = _documentList.SelectedItems!.Cast<ListBoxItem>().Select(i => (Guid)i.Tag!).ToList();

        Complete(new DeliverableCompletionInput(DateOnly.FromDateTime(completedOn.Date), fixedPrice, evidenceIds, documentIds));
    }

    private static bool TryParseMoney(string value, out Money money)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2 && decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            try
            {
                money = new Money(amount, new CurrencyCode(parts[1]));
                return true;
            }
            catch (ArgumentException)
            {
                // Falls through to the failure return below.
            }
        }

        money = default;
        return false;
    }

    private void ShowValidationError(string message)
    {
        _validation.Text = message;
        _validation.IsVisible = true;
    }

    private void Complete(DeliverableCompletionInput? input)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(input);
    }
}
