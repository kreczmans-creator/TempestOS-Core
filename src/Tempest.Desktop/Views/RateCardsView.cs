using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Evidence;

namespace Tempest.Desktop.Views;

/// <summary>
/// Business → Rate cards (Product Owner decision, 2026-10-01: rate cards are
/// business data): the consultancy's own graded rates — the
/// <see cref="IRateCardCatalog"/> records a project pins, quote lines price
/// from and timesheet entries are priced against — listed one compact row
/// each (name and release status, runbook F1), with the <em>Add a rate
/// card</em> form (name, grade, hourly rate → Add Rate Card, then Release
/// from its row), and the record opened beside the list in the same
/// <see cref="ReferenceRecordView"/> every reference library uses.
/// </summary>
/// <remarks>
/// Moved, not duplicated: rate cards no longer appear under Engineering →
/// Reference data (<see cref="LibrariesView"/>), exactly as People moved to
/// Business → Staff (<see cref="StaffView"/>, runbook B1).
/// </remarks>
public sealed class RateCardsView : UserControl
{
    private readonly IRateCardCatalog _rateCards;
    private readonly ReferenceReviewService _review;
    private readonly ICurrentPrincipalAccessor? _principals;

    private readonly StackPanel _rows = new() { Spacing = 0 };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    // DEFECT-1 of the overnight real-shell journey (2026-09-16, `WP 21.5C`
    // Linux): nothing in the shipped application could create a rate card
    // — no form, no seed, no command — so on a clean install no project
    // could pin one, no timesheet entry could be priced and no invoice
    // request could be raised. One graded hourly rate in the consultancy's
    // own currency (GBP, the product's base currency — the record can be
    // revised to add grades or change rates), registered as Draft and
    // released through the row's own Release like every other library
    // record.
    private readonly TextBox _newRateCardName = new() { Watermark = "Rate card name", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _newRateCardGrade = new() { Watermark = "Grade (e.g. Engineer)", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _newRateCardHourlyRate = new() { Watermark = "Hourly rate (GBP)", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _addRateCardButton = new() { Content = "Add Rate Card", MinHeight = DesignTokens.MinControlSize };

    private readonly DockPanel _container = new();
    private readonly ScrollViewer _listScroll;
    private readonly ScrollViewer _detailScroll;
    private readonly ReferenceRecordView _detail;
    private string? _openRecordId;

    /// <summary>Raised after an action completes — every Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>The open record's own Revise prompt (see <see cref="LibrariesView.ReviseRecordPrompt"/>); <see langword="null"/> leaves Revise unavailable.</summary>
    public Func<string, string, SourceCitation?, CancellationToken, Task<ReviseReferenceRecordInput?>>? ReviseRecordPrompt
    {
        get => _detail.ReviseRecordPrompt;
        set => _detail.ReviseRecordPrompt = value;
    }

    internal RateCardsView(
        ReferenceLibraryCatalogues catalogues, ReferenceReviewService review, IReferenceCitationIndex citationIndex,
        Action<Guid, string> openObjectRightUp, ICurrentPrincipalAccessor? principals = null)
    {
        ArgumentNullException.ThrowIfNull(catalogues);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(citationIndex);
        ArgumentNullException.ThrowIfNull(openObjectRightUp);

        _rateCards = catalogues.BusinessRateCards;
        _review = review;
        _principals = principals;

        _detail = new ReferenceRecordView(catalogues, review, citationIndex, openObjectRightUp);
        _detail.ActionCompleted += (message, outcome) =>
        {
            _status.Text = message;
            ActionCompleted?.Invoke(message, outcome);
        };
        _detail.RecordChanged += () => _ = RefreshAsync();

        AutomationProperties.SetName(_newRateCardName, "Rate card name");
        AutomationProperties.SetName(_newRateCardGrade, "Rate card grade");
        AutomationProperties.SetName(_newRateCardHourlyRate, "Rate card hourly rate");
        AutomationProperties.SetName(_addRateCardButton, "Add Rate Card");
        ToolTip.SetTip(_addRateCardButton, "Add Rate Card");
        _addRateCardButton.Classes.Add(ChromeStyles.Primary);
        _addRateCardButton.Click += async (_, _) => await OnAddRateCardAsync().ConfigureAwait(true);

        var form = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var field in new Control[] { _newRateCardName, _newRateCardGrade, _newRateCardHourlyRate, _addRateCardButton })
        {
            field.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceSm);
            form.Children.Add(field);
        }

        var addSection = new StackPanel { Spacing = DesignTokens.SpaceXs, Margin = new Thickness(0, 0, 0, DesignTokens.SpaceLg) };
        addSection.Children.Add(new TextBlock { Text = "Add a rate card", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });
        addSection.Children.Add(new TextBlock
        {
            Text = "One graded hourly rate in GBP to start; release it from its row, then pin it to a project from the project's Details tab. Revise the record to add grades.",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
        });
        addSection.Children.Add(form);

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(new TextBlock { Text = "Rate cards", FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading });
        body.Children.Add(new TextBlock
        {
            Text = "The consultancy's own graded rates — pinned to a project, picked on quote lines and used to price timesheet entries. Only a released card can be picked.",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(_status);
        body.Children.Add(addSection);
        body.Children.Add(_rows);
        _listScroll = new ScrollViewer { Content = body };

        var detailBody = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        detailBody.Children.Add(_detail);
        _detailScroll = new ScrollViewer { Content = detailBody, IsVisible = false };

        DockPanel.SetDock(_listScroll, Dock.Left);
        _container.Children.Add(_listScroll);
        _container.Children.Add(_detailScroll);
        Content = _container;

        AutomationProperties.SetName(this, "Rate cards");
    }

    /// <summary>The provenance every rate card added here is stamped with — the consultancy's own figures, entered by hand, unverified until reviewed (the same shape as <see cref="Tempest.Core.People.PersonProvenance.Default"/>).</summary>
    public static ReferenceProvenance RateCardProvenance { get; } = new(
        SourceOrganisation: "TempestOS",
        SourceDocument: "Entered directly in the Rate cards library.",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "The consultancy's own rates, added by hand; not verified against any external source until reviewed.");

    /// <summary>The record id a rate card's name is registered under.</summary>
    public static string RecordIdFor(string name) =>
        "ratecard-" + new string(name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

    /// <summary>Re-reads the rate-card list.</summary>
    public async Task RefreshAsync()
    {
        var records = await _rateCards.ListAsync().ConfigureAwait(true);

        _rows.Children.Clear();
        _rows.Children.Add(new TextBlock
        {
            Text = $"Rate cards ({records.Count})",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeHeading,
            Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXs),
        });

        if (records.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Text = "No rate cards yet — add the first one above.", Opacity = 0.7 });
            return;
        }

        foreach (var record in records.OrderBy(r => r.Definition.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            var recordId = record.Id;
            var state = record.ValidationState;
            _rows.Children.Add(ReferenceRecordListBuilder.BuildRow(
                recordId, record.Definition.Name, state,
                () => OpenRecordAsync(recordId),
                () => OnReleaseAsync(recordId, state)));
        }
    }

    private async Task OpenRecordAsync(string recordId)
    {
        _openRecordId = recordId;
        _listScroll.Width = 460;
        _detailScroll.IsVisible = true;
        await _detail.LoadAsync(_rateCards.LibraryName, recordId).ConfigureAwait(true);
    }

    /// <summary>Gets the record id currently open beside the list, if any.</summary>
    internal string? OpenRecordId => _openRecordId;

    private async Task OnReleaseAsync(string recordId, ReferenceValidationState state)
    {
        try
        {
            // "Release a Draft record" is one action from the row: a record
            // still Draft is verified first, with a plain statement naming
            // its own recorded provenance, then released — both real,
            // separately audited acts of ReferenceReviewService.
            if (state == ReferenceValidationState.Draft)
                await _review.VerifyAsync(_rateCards, recordId, new ReferenceReviewStatement(SourceConsulted: "the record's own recorded provenance")).ConfigureAwait(true);

            await _review.ReleaseAsync(_rateCards, recordId, "Released from Business → Rate cards.").ConfigureAwait(true);
            await RefreshAsync().ConfigureAwait(true);
            Report($"Released '{recordId}'.", succeeded: true);
        }
        catch (ReferenceReviewException ex)
        {
            await RefreshAsync().ConfigureAwait(true);
            Report(ex.Message, succeeded: false);
        }
    }

    private async Task OnAddRateCardAsync()
    {
        if (string.IsNullOrWhiteSpace(_newRateCardName.Text))
        {
            Report("Enter a rate card name before adding a rate card.", succeeded: false);
            return;
        }

        var grade = NullIfEmpty(_newRateCardGrade.Text) ?? "Engineer";

        if (!decimal.TryParse(_newRateCardHourlyRate.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var hourlyRate) || hourlyRate < 0m)
        {
            Report("Enter the hourly rate as a number of pounds (for example 95 or 95.50) before adding a rate card.", succeeded: false);
            return;
        }

        try
        {
            var name = _newRateCardName.Text.Trim();
            var recordId = RecordIdFor(name);
            var gradeCode = new string(grade.ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
            var today = DateOnly.FromDateTime(DateTime.Today);
            var principal = _principals?.Current?.Identity.Id is { Length: > 0 } id ? id : Environment.UserName;

            var card = new RateCard
            {
                Code = recordId,
                Name = name,
                EffectivePeriod = new EffectivePeriod(today, null),
                Currency = CurrencyCode.Gbp,
                Governance = new BusinessGovernanceFacts { Ownership = new BusinessOwnership(principal, "Principal") },
                Entries =
                [
                    new RateCardEntry(gradeCode, grade, PricingBasis.Hourly, new Money(hourlyRate, CurrencyCode.Gbp), Grade: grade),
                ],
            };

            await _rateCards.RegisterAsync(recordId, card, RateCardProvenance).ConfigureAwait(true);

            _newRateCardName.Text = string.Empty;
            _newRateCardGrade.Text = string.Empty;
            _newRateCardHourlyRate.Text = string.Empty;

            await RefreshAsync().ConfigureAwait(true);
            Report($"Added rate card '{name}' ({grade} at {MoneyDisplay.Format(new Money(hourlyRate, CurrencyCode.Gbp))} per hour). Release it from its row, then pin it to a project.", succeeded: true);

            // The Product Owner guard (`po-comments.md` item 5): Add opens
            // the new record right up.
            await OpenRecordAsync(recordId).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is ArgumentException or DuplicateReferenceRecordException or DuplicateReferenceKeyException)
        {
            Report(ex.Message, succeeded: false);
        }
    }

    private static string? NullIfEmpty(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private void Report(string message, bool succeeded)
    {
        _status.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(succeeded));
    }
}
