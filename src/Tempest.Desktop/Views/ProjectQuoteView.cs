using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using System.IO;
using Tempest.Core.BusinessGovernance;
using IRateCardCatalog = Tempest.Core.BusinessGovernance.Pricing.IRateCardCatalog;
using PricingBasis = Tempest.Core.BusinessGovernance.Pricing.PricingBasis;
using RateCard = Tempest.Core.BusinessGovernance.Pricing.RateCard;
using RateCardEntry = Tempest.Core.BusinessGovernance.Pricing.RateCardEntry;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Quotations;
using Tempest.Core.ReferenceData;
using Tempest.Core.Requirements;
using Tempest.Desktop;
using Tempest.Desktop.Quotations;
using Tempest.Desktop.Theming;
using Tempest.Workspace;
using Tempest.Workspace.Files;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Quotations;

namespace Tempest.Desktop.Views;

/// <summary>
/// The project workspace's own Quote tab (`WP 19.5B`, `ADR-0152`, Product
/// Owner comment item 4: "a quote is opened with the project; the quote
/// defines the initial requirement set and defines the deliverables"): the
/// open project's own quotation(s) — a list, newest open, when there is
/// more than one — with the selected quotation's own identity, its lines
/// as an editable table while Draft, totals and terms;
/// <b>Send</b>/<b>Accept</b>/<b>Decline</b>/<b>Export</b>; once Accepted,
/// every Deliverable and Requirement the quote created, each opening right
/// up.
/// </summary>
/// <remarks>
/// <para>
/// <b>Renders from the domain, refreshed by the change feed.</b> Mirrors
/// <see cref="ProjectDeliverablesView"/>/<see cref="InvoicingView"/>'s own
/// identical discipline: every quotation is read fresh, through one
/// coherent <see cref="EngineeringDomainContext.Repository"/> read, every
/// time <see cref="IWorkspaceChanges.Changed"/> touches
/// <see cref="Quotation.CanonicalKind"/>, <c>Deliverable</c> or
/// <see cref="RequirementsService.RequirementDocumentKind"/> — there is no
/// manual refresh call site anywhere else in this class.
/// </para>
/// <para>
/// <b>Two dispatch shapes, by convention, not by accident.</b> Create and
/// every line edit (add/update/remove) dispatch directly through
/// <see cref="ICommandDispatcher"/> — <see cref="ProjectDeliverablesView"/>'s
/// own "Complete"/"Raise invoice" convention, needing no separate
/// confirmation for an edit made in place on a Draft quote. Send, Accept
/// and Decline instead go through <see cref="ICommandRegistry.InvokeAsync(string,CommandContext,CommandParameterPrompt?,CancellationToken)"/>
/// with <see cref="ParameterPrompt"/> — <see cref="InvoicingView"/>'s own
/// convention — so each command's own registered <c>confirmationMessage</c>
/// (<see cref="QuotationWorkspaceRegistration.Register"/>) is honoured
/// rather than bypassed: Accept, in particular, creates a Deliverable and a
/// Requirement per line, which deserves the same "are you sure" every other
/// irreversible act in this platform gets.
/// </para>
/// <para>
/// <b>The export attaches directly, like every other attachment in this
/// platform.</b> <see cref="OnSendAsync"/> calls
/// <see cref="IHasAttachments.AttachContentAsync"/> on the quotation
/// itself after <c>quotation.send</c> succeeds — not a second command —
/// mirroring <see cref="Editors.ObjectEditorView"/>'s own established
/// "attach real bytes directly, no business rule to enforce" precedent for
/// this one facet, over the same SkiaSharp path
/// <see cref="QuotationSheetRenderer"/> renders through
/// (`ADR-0152`, Product Owner comment item 9).
/// </para>
/// </remarks>
public sealed class ProjectQuoteView : UserControl
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;
    private readonly Func<Guid?> _currentProjectId;
    private readonly IOrganisationCatalog _organisations;
    private readonly Action<Guid, string> _openObject;
    private readonly IFilePicker _filePicker;
    private readonly QuotationSheetRenderer _sheetRenderer;
    private readonly Func<string> _issuerName;
    private readonly Func<string> _applicationVersionText;
    private readonly TimeProvider _time;

    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly StackPanel _quoteListPanel = new() { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
    private readonly StackPanel _detailPanel = new() { Spacing = DesignTokens.SpaceMd };
    private readonly Button _newQuoteButton = new() { Content = "New Quote", MinHeight = DesignTokens.MinControlSize };

    private List<Quotation> _quotations = [];
    private Guid? _selectedQuotationId;
    private Guid? _editingLineId;
    private Func<Task<bool>>? _pendingLineCommit;
    private bool _isArchived;

    /// <summary>
    /// Whether the open project is archived — Add line/Edit/Remove/Send/
    /// Accept/Decline are disabled, with a tooltip, while this is true
    /// (`WP 19.10H`, `TD-179`). Export stays enabled: it reads and saves a
    /// copy, never writes. New Quote also stays enabled — this Work
    /// Package's brief names only the four above — but
    /// <see cref="QuotationService.CreateAsync"/> already refuses it on an
    /// archived project (`ArchivedAsync`'s own sibling check at the top of
    /// that method), so a click there still ends in an honest refusal
    /// message rather than a silent write.
    /// </summary>
    public const string ArchivedTooltip = "Archived project — read only";

    private readonly WorkspaceChangesSubscription _workspaceChanges;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>
    /// Collects Send/Accept/Decline's own confirmation — see this class's
    /// own remarks. <see langword="null"/> (any test that constructs this
    /// view directly) leaves all three honestly unavailable rather than
    /// run without asking.
    /// </summary>
    public CommandParameterPrompt? ParameterPrompt { get; set; }

    /// <summary>
    /// Where a quote PDF export starts — the project's own Windows
    /// Explorer folder, or its configured quote subfolder (PO decision
    /// 2026-10-01; earlier PO comment: "exports direct to the quote
    /// section there"). <see langword="null"/>, or a locator with nothing
    /// to offer (not Windows, no D: drive, a refusing file system), leaves
    /// the export exactly as it was: the picker opens at its own default.
    /// </summary>
    public ProjectFolderLocator? ProjectFolders { get; set; }

    /// <summary>
    /// Turns the submitter's, approver's and returner's stored identity ids
    /// into names on the review line (colour review board M4).
    /// <see langword="null"/> (a test that constructs this view directly)
    /// still never shows a raw SID or GUID — see <c>PersonLabel</c>.
    /// </summary>
    public Tempest.Core.Identity.IPrincipalDirectory? Principals { get; set; }

    /// <summary>
    /// The global "Second-person sign-off" switch (`ADR-0161`, Product Owner
    /// decision 2026-10-01) — read on every render so the review panel says
    /// whether the author may approve their own quote. <see langword="null"/>
    /// (a test that constructs this view directly) shows no sign-off line
    /// and keeps the second-person wording, the rule's own default before
    /// the switch existed.
    /// </summary>
    public Tempest.Core.Governance.ISignOffPolicy? SignOffPolicy { get; set; }

    /// <summary>The automation name of the review panel's own sign-off line (`ADR-0161`).</summary>
    public const string SignOffStateName = "Second-person sign-off state";

    /// <summary>The sign-off line's own text while second-person sign-off is off (`ADR-0161`).</summary>
    public const string SelfApprovalAllowedText = "Self-approval allowed (second-person sign-off is off).";

    /// <summary>The sign-off line's own text while second-person sign-off is on (`ADR-0161`).</summary>
    public const string SecondPersonRequiredText = "A second person must approve (second-person sign-off is on).";

    /// <summary>Whether the last render found second-person sign-off on — <see langword="true"/> when no policy is composed.</summary>
    private bool _secondPersonRequired = true;

    /// <summary>The automation name of the line form's own rate dropdown (runbook C3).</summary>
    public const string RateChoiceName = "Line rate basis";

    /// <summary>The rate dropdown's own Fixed choice — enables the fixed price box (runbook C3).</summary>
    public const string FixedChoiceLabel = "Fixed";

    /// <summary>
    /// Where the line form's rate dropdown reads the project's own pinned
    /// rate card from (runbook C3). <see langword="null"/> (a test that
    /// constructs this view directly without one) leaves the dropdown
    /// offering Fixed alone, exactly as an unpinned project does.
    /// </summary>
    public IRateCardCatalog? RateCards { get; set; }

    /// <summary>
    /// Where the selected quotation's Xero badge is read from (`v0.24.0` U3,
    /// <see cref="XeroSyncBadgeControl"/>). <see langword="null"/> — Xero is
    /// not the configured connector, or a test that does not thread it
    /// through — shows no badge.
    /// </summary>
    public IXeroBadgeSource? XeroBadges { get; set; }

    /// <summary>The selected quotation's Xero badge as last rendered; <see langword="null"/> when none is shown.</summary>
    internal XeroSyncBadgeControl? XeroBadge { get; private set; }

    /// <summary>The change feed this view reloads its own list from (`WP 18.1A`, `WP 18.9.1`).</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Initialises a new instance of the <see cref="ProjectQuoteView"/> class.</summary>
    public ProjectQuoteView(
        EngineeringDomainContext domainContext, ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry,
        Func<Guid?> currentProjectId, IOrganisationCatalog organisations, Action<Guid, string> openObject,
        IFilePicker filePicker, QuotationSheetRenderer sheetRenderer, Func<string> issuerName, Func<string> applicationVersionText,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(currentProjectId);
        ArgumentNullException.ThrowIfNull(organisations);
        ArgumentNullException.ThrowIfNull(openObject);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(sheetRenderer);
        ArgumentNullException.ThrowIfNull(issuerName);
        ArgumentNullException.ThrowIfNull(applicationVersionText);

        _domainContext = domainContext;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
        _currentProjectId = currentProjectId;
        _organisations = organisations;
        _openObject = openObject;
        _filePicker = filePicker;
        _sheetRenderer = sheetRenderer;
        _issuerName = issuerName;
        _applicationVersionText = applicationVersionText;
        _time = timeProvider ?? TimeProvider.System;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        var heading = new TextBlock
        {
            Text = "Quote",
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
        };

        AutomationProperties.SetName(_newQuoteButton, "New Quote");
        _newQuoteButton.Classes.Add(ChromeStyles.Primary);
        _newQuoteButton.Click += async (_, _) => await OnCreateAsync().ConfigureAwait(true);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        headerRow.Children.Add(heading);
        headerRow.Children.Add(_newQuoteButton);

        AutomationProperties.SetName(_quoteListPanel, "Quotes");

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(headerRow);
        body.Children.Add(_status);
        body.Children.Add(_quoteListPanel);
        body.Children.Add(_detailPanel);

        AutomationProperties.SetName(this, "Quote");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Test-only (`WP 19.7C`, <c>WorkspaceChangesReattachTests</c>): counts every <see cref="RefreshAsync"/> call, proving a reattached view's subscription still reaches <see cref="OnWorkspaceChanged"/>.</summary>
    internal int RefreshCount { get; private set; }

    /// <summary>
    /// Sets whether the open project is archived — see this class's own
    /// remarks on <see cref="ArchivedTooltip"/> for exactly which controls
    /// that disables. Takes effect on the next render (call before
    /// <see cref="RefreshAsync"/>, as <c>ProjectWorkspaceView</c> does) —
    /// this class's own detail panel is rebuilt from scratch on every
    /// render, so no re-render is forced here (`WP 19.10H`, `TD-179`).
    /// </summary>
    public void SetArchived(bool archived) => _isArchived = archived;

    /// <summary>Reloads the open project's own quotation(s) — empty, honestly, when no project is open.</summary>
    public async Task RefreshAsync()
    {
        RefreshCount++;

        var projectId = _currentProjectId();

        if (projectId is not { } id)
        {
            _status.Text = "Open a project to see, and open, its quotation(s).";
            _newQuoteButton.IsEnabled = false;
            _quotations = [];
            _quoteListPanel.Children.Clear();
            _detailPanel.Children.Clear();
            return;
        }

        _newQuoteButton.IsEnabled = true;

        var memberEntries = await _domainContext.Repository.ListChildrenAsync(id, CancellationToken.None).ConfigureAwait(true);
        var members = await _domainContext.Repository.MaterialiseAsync<Quotation>(
            [.. memberEntries.Where(entry => !entry.IsDeleted)], CancellationToken.None).ConfigureAwait(true);
        _quotations = members
            .OrderByDescending(q => q.QuoteDate)
            .ThenByDescending(q => q.DisplayName, StringComparer.Ordinal)
            .ToList();

        if (_selectedQuotationId is null || _quotations.All(q => q.Id != _selectedQuotationId))
            _selectedQuotationId = _quotations.FirstOrDefault()?.Id;

        _status.Text = _quotations.Count == 0
            ? "No quote yet for this project."
            : $"{_quotations.Count} quotation(s), newest first.";

        RenderQuoteList();
        await RenderDetailAsync(id).ConfigureAwait(true);
    }

    /// <summary>Selects a specific quotation by id, if it belongs to the currently loaded project — what <c>QuotesView</c>'s own Open action drives.</summary>
    public async Task SelectQuoteAsync(Guid quotationId)
    {
        _selectedQuotationId = quotationId;
        await RefreshAsync().ConfigureAwait(true);
    }

    private void RenderQuoteList()
    {
        _quoteListPanel.Children.Clear();
        _quoteListPanel.IsVisible = _quotations.Count > 1;

        if (_quotations.Count <= 1)
            return;

        foreach (var quote in _quotations)
        {
            var button = new ToggleButton
            {
                Content = $"{quote.Reference} ({quote.Status})",
                IsChecked = quote.Id == _selectedQuotationId,
                MinHeight = DesignTokens.MinControlSize,
            };
            AutomationProperties.SetName(button, $"Select {quote.Reference}");
            var id = quote.Id;
            button.Click += async (_, _) =>
            {
                _selectedQuotationId = id;
                _editingLineId = null;
                await RefreshAsync().ConfigureAwait(true);
            };
            _quoteListPanel.Children.Add(button);
        }
    }

    private async Task RenderDetailAsync(Guid projectId)
    {
        _detailPanel.Children.Clear();
        XeroBadge = null;

        if (_selectedQuotationId is not { } quoteId || _quotations.FirstOrDefault(q => q.Id == quoteId) is not { } quote)
        {
            _detailPanel.Children.Add(new TextBlock
            {
                Text = "No quote yet. Use New Quote to open one with this project.",
                Opacity = 0.7,
            });
            return;
        }

        var clientName = await ResolveClientNameAsync(quote.ClientOrganisationId).ConfigureAwait(true);
        _secondPersonRequired = SignOffPolicy is null
            || await SignOffPolicy.IsSecondPersonRequiredAsync(CancellationToken.None).ConfigureAwait(true);

        var identity = new StackPanel { Spacing = DesignTokens.SpaceXs };
        identity.Children.Add(new TextBlock
        {
            // `WP 20.10E`: a change order's own kind is shown alongside its
            // status, exactly as `QuotesView`'s own group rows show it.
            Text = quote.QuotationKind == QuotationKind.ChangeOrder
                ? $"{quote.Reference} {QuotationExport.RevisionText(quote)} — {QuotationExport.StatusText(quote)} · change order"
                : $"{quote.Reference} {QuotationExport.RevisionText(quote)} — {QuotationExport.StatusText(quote)}",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
        });
        identity.Children.Add(new TextBlock
        {
            Text = $"Date {quote.QuoteDate:yyyy-MM-dd}  •  Client {clientName}  •  Currency {quote.Currency}  •  Valid {quote.ValidityDays} day(s)",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        identity.Children.Add(new TextBlock
        {
            Text = $"Sent {quote.SentOn?.ToString("yyyy-MM-dd") ?? "(not yet sent)"}  •  Decided {quote.DecidedOn?.ToString("yyyy-MM-dd") ?? "(not yet decided)"}",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
        });
        var reviewState = new TextBlock
        {
            Text = DescribeReviewState(quote),
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };
        AutomationProperties.SetName(reviewState, "Quote review state");
        identity.Children.Add(reviewState);

        // `ADR-0161`: where the quote is reviewed, say whether its author may
        // approve it — the one global switch, read fresh on every render.
        if (SignOffPolicy is not null && quote.Status is QuotationStatus.Draft or QuotationStatus.InReview or QuotationStatus.Approved)
        {
            var signOffState = new TextBlock
            {
                Text = _secondPersonRequired ? SecondPersonRequiredText : SelfApprovalAllowedText,
                FontSize = DesignTokens.FontSizeCaption,
                Opacity = 0.85,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            };
            AutomationProperties.SetName(signOffState, SignOffStateName);
            identity.Children.Add(signOffState);
        }

        // `v0.24.0` U3: the quotation's Xero badge, from local state only.
        if (XeroBadges is { } xero)
        {
            var badge = new XeroSyncBadgeControl(
                xero, XeroDocumentRef.For(XeroDocumentKind.Quote, quote.Id), quote.Reference, offerSendToXero: XeroIssuedPdf.IsIssued(quote));
            badge.ActionCompleted += (message, outcome) => Report(message, outcome.Succeeded);
            identity.Children.Add(badge);
            XeroBadge = badge;
        }

        _detailPanel.Children.Add(identity);

        var rateCard = await LoadRateCardAsync(quote).ConfigureAwait(true);

        _detailPanel.Children.Add(BuildLinesSection(quote, rateCard));

        _pendingLineCommit = null;
        if (LinesEditable(quote))
            _detailPanel.Children.Add(BuildLineEntryForm(quote, rateCard));

        var termsBox = new TextBlock { Text = $"Terms: {quote.Terms ?? "(none)"}", FontSize = DesignTokens.FontSizeBody, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        _detailPanel.Children.Add(termsBox);

        _detailPanel.Children.Add(BuildActionsRow(quote));

        if (quote.Status == QuotationStatus.Accepted)
            _detailPanel.Children.Add(await BuildAcceptedResultsSectionAsync(quote).ConfigureAwait(true));

        if (XeroBadge is { } shown)
            await shown.LoadAsync().ConfigureAwait(true);
    }

    private Control BuildLinesSection(Quotation quote, RateCard? rateCard)
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(new TextBlock { Text = "Lines", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody });

        if (quote.Lines.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "(no lines)", Opacity = 0.6, FontSize = DesignTokens.FontSizeCaption });
        }

        foreach (var line in quote.Lines)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };

            var rateSource = line.RateCardServiceCode is { } code && rateCard?.FindEntry(code) is { } entry ? $" ({entry.ServiceName})" : string.Empty;
            var text = new TextBlock
            {
                Text = line.Basis == QuotationLineBasis.Hourly
                    ? $"{line.Description}  •  {line.Hours:0.##} × {MoneyDisplay.Format(line.Rate!.Value)}{rateSource}  =  {MoneyDisplay.Format(line.Amount)}"
                    : $"{line.Description}  •  {MoneyDisplay.Format(line.Amount)} (fixed)",
                FontSize = DesignTokens.FontSizeBody,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(text, 0);
            row.Children.Add(text);

            if (LinesEditable(quote))
            {
                var edit = new Button { Content = "Edit", MinHeight = DesignTokens.MinControlSize };
                edit.Classes.Add(ChromeStyles.Flat);
                AutomationProperties.SetName(edit, $"Edit {line.Description}");
                var editLine = line;
                edit.Click += (_, _) => BeginEditLine(quote, editLine);
                ApplyArchivedState(edit);
                Grid.SetColumn(edit, 1);
                row.Children.Add(edit);

                var remove = new Button { Content = "Remove", MinHeight = DesignTokens.MinControlSize };
                remove.Classes.Add(ChromeStyles.Flat);
                AutomationProperties.SetName(remove, $"Remove {line.Description}");
                var removeId = line.Id;
                remove.Click += async (_, _) => await OnRemoveLineAsync(quote.Id, removeId).ConfigureAwait(true);
                ApplyArchivedState(remove);
                Grid.SetColumn(remove, 2);
                row.Children.Add(remove);
            }

            panel.Children.Add(row);
        }

        panel.Children.Add(new TextBlock
        {
            Text = $"Total {MoneyDisplay.Format(quote.Total)}",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0),
        });

        return panel;
    }

    /// <summary>
    /// The add/edit line form (runbook C3, PO: "if I'm pinning a rate card,
    /// why am I having to type out the rate on each line? Make the hourly
    /// rate a drop down from whatever rate card is applied to the project
    /// or 'fixed' when I can then use the fixed cost box"). The
    /// <see cref="RateChoiceName"/> dropdown lists the pinned card's own
    /// hourly entries, then <see cref="FixedChoiceLabel"/>: an hourly entry
    /// fills the (read-only) rate and takes hours; Fixed enables the fixed
    /// price box instead. With no card pinned the dropdown offers Fixed
    /// alone, with a hint to pin one on the Details tab — a typed rate is
    /// exactly what the PO asked to be rid of.
    /// </summary>
    private Control BuildLineEntryForm(Quotation quote, RateCard? rateCard)
    {
        var descriptionBox = new TextBox { Watermark = "Description", MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 220 };
        AutomationProperties.SetName(descriptionBox, "Line description");

        var rateChoice = new ComboBox { MinHeight = DesignTokens.ControlSizeMedium, MinWidth = 220 };
        AutomationProperties.SetName(rateChoice, RateChoiceName);

        var hoursBox = new NumericUpDown { Watermark = "Hours", MinHeight = DesignTokens.ControlSizeMedium, FormatString = "0.##", Minimum = 0 };
        AutomationProperties.SetName(hoursBox, "Line hours");

        var rateBox = new NumericUpDown
        {
            Watermark = $"Rate ({quote.Currency})", MinHeight = DesignTokens.ControlSizeMedium, FormatString = "0.##",
            IsReadOnly = true, ShowButtonSpinner = false,
        };
        AutomationProperties.SetName(rateBox, "Line rate");
        ToolTip.SetTip(rateBox, "Taken from the rate card entry chosen in the dropdown.");

        var fixedPriceBox = new NumericUpDown { Watermark = $"Fixed price ({quote.Currency})", MinHeight = DesignTokens.ControlSizeMedium, FormatString = "0.##" };
        AutomationProperties.SetName(fixedPriceBox, "Line fixed price");

        var editingLine = _editingLineId is { } editingId ? quote.Lines.FirstOrDefault(l => l.Id == editingId) : null;

        var options = new List<ComboBoxItem>();
        foreach (var entry in HourlyEntries(rateCard, quote.Currency))
        {
            var label = entry.Grade is { Length: > 0 } grade && !entry.ServiceName.Contains(grade, StringComparison.OrdinalIgnoreCase)
                ? $"{entry.ServiceName} ({grade}) — {MoneyDisplay.Format(entry.Rate)}/h"
                : $"{entry.ServiceName} — {MoneyDisplay.Format(entry.Rate)}/h";
            options.Add(new ComboBoxItem { Content = label, Tag = new RateOption(entry, null) });
        }

        // An hourly line written before runbook C3, or with a rate no
        // longer on the pinned card, keeps its own rate as an option of its
        // own while it is edited — never silently repriced.
        if (editingLine is { Basis: QuotationLineBasis.Hourly, Rate: { } keptRate }
            && (editingLine.RateCardServiceCode is null || rateCard?.FindEntry(editingLine.RateCardServiceCode) is null))
        {
            options.Add(new ComboBoxItem { Content = $"As entered — {MoneyDisplay.Format(keptRate)}/h", Tag = new RateOption(null, keptRate) });
        }

        var fixedItem = new ComboBoxItem { Content = FixedChoiceLabel, Tag = new RateOption(null, null) };
        options.Add(fixedItem);
        rateChoice.ItemsSource = options;

        void ApplyChoice()
        {
            var option = (rateChoice.SelectedItem as ComboBoxItem)?.Tag as RateOption;
            var rate = option?.Entry?.Rate ?? option?.ManualRate;
            var isHourly = rate is not null;

            rateBox.Value = rate?.Amount;
            hoursBox.IsEnabled = isHourly;
            fixedPriceBox.IsEnabled = !isHourly;
            if (isHourly)
                fixedPriceBox.Value = null;
            else
                hoursBox.Value = null;
        }

        rateChoice.SelectionChanged += (_, _) => ApplyChoice();

        var hint = new TextBlock
        {
            Text = "No rate card is pinned to this project, so lines are priced fixed. Pin a rate card on the Details tab to price lines by the hour.",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.8,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            IsVisible = rateCard is null,
        };
        AutomationProperties.SetName(hint, "Rate card hint");

        var lineStatus = new TextBlock { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

        var saveButton = new Button { Content = _editingLineId is null ? "Add line" : "Save line", MinHeight = DesignTokens.MinControlSize };
        saveButton.Classes.Add(ChromeStyles.Primary);
        AutomationProperties.SetName(saveButton, saveButton.Content?.ToString() ?? "Add line");
        ApplyArchivedState(saveButton);

        var cancelButton = new Button { Content = "Cancel edit", MinHeight = DesignTokens.MinControlSize, IsVisible = _editingLineId is not null };
        cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(cancelButton, "Cancel edit");

        if (editingLine is not null)
        {
            descriptionBox.Text = editingLine.Description;
            rateChoice.SelectedItem = editingLine.Basis == QuotationLineBasis.FixedPrice
                ? fixedItem
                : options.FirstOrDefault(o => o.Tag is RateOption { Entry: { } e } && string.Equals(e.ServiceCode, editingLine.RateCardServiceCode, StringComparison.OrdinalIgnoreCase))
                  ?? options.FirstOrDefault(o => o.Tag is RateOption { ManualRate: not null })
                  ?? fixedItem;
            ApplyChoice();
            hoursBox.Value = editingLine.Hours;
            fixedPriceBox.Value = editingLine.FixedPrice?.Amount;
        }
        else
        {
            rateChoice.SelectedItem = options[0];
            ApplyChoice();
        }

        // What Save line does — shared with Save draft, which saves a line
        // still being typed before stamping the draft saved. `true` when
        // there was nothing to save, or it saved.
        async Task<bool> CommitAsync(bool requireLine)
        {
            var description = descriptionBox.Text?.Trim() ?? string.Empty;
            if (description.Length == 0)
            {
                if (!requireLine)
                    return true;

                lineStatus.Text = "A description is required.";
                return false;
            }

            var option = (rateChoice.SelectedItem as ComboBoxItem)?.Tag as RateOption;
            var hours = option is { Entry: not null } or { ManualRate: not null } ? (decimal?)hoursBox.Value : null;
            var rate = option?.ManualRate;
            var serviceCode = option?.Entry?.ServiceCode;
            var fixedPrice = option is { Entry: null, ManualRate: null } && fixedPriceBox.Value is { } f ? new Money(f, quote.Currency) : (Money?)null;

            var succeeded = _editingLineId is { } lineId
                ? await OnUpdateLineAsync(quote.Id, quote.Kind, lineId, description, hours, rate, fixedPrice, serviceCode).ConfigureAwait(true)
                : await OnAddLineAsync(quote.Id, quote.Kind, description, hours, rate, fixedPrice, serviceCode).ConfigureAwait(true);

            if (succeeded)
                _editingLineId = null;

            return succeeded;
        }

        _pendingLineCommit = () => CommitAsync(requireLine: false);
        saveButton.Click += async (_, _) => await CommitAsync(requireLine: true).ConfigureAwait(true);

        cancelButton.Click += async (_, _) =>
        {
            _editingLineId = null;
            await RefreshAsync().ConfigureAwait(true);
        };

        var fieldsRow = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in new Control[] { descriptionBox, rateChoice, hoursBox, rateBox, fixedPriceBox, saveButton, cancelButton })
        {
            control.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceXs);
            fieldsRow.Children.Add(control);
        }

        var form = new StackPanel { Spacing = DesignTokens.SpaceXs };
        if (quote.Status == QuotationStatus.Approved)
        {
            form.Children.Add(new TextBlock
            {
                Text = $"Changing a line starts a new draft; the next approval issues {QuotationReview.LabelFor(quote.RevisionNumber + 1)}.",
                FontSize = DesignTokens.FontSizeCaption,
                Opacity = 0.8,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });
        }

        form.Children.Add(fieldsRow);
        form.Children.Add(hint);
        form.Children.Add(lineStatus);
        return form;
    }

    private void BeginEditLine(Quotation quote, QuotationLine line)
    {
        _editingLineId = line.Id;
        _ = RenderDetailAsync(quote.ParentId ?? Guid.Empty);
    }

    private Control BuildActionsRow(Quotation quote)
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        panel.Children.Add(actions);

        Button AddAction(string content, string automationName, string chrome, Func<Task> onClick, bool archivable = true)
        {
            var button = new Button { Content = content, MinHeight = DesignTokens.MinControlSize };
            button.Classes.Add(chrome);
            AutomationProperties.SetName(button, automationName);
            button.Click += async (_, _) => await onClick().ConfigureAwait(true);
            if (archivable)
                ApplyArchivedState(button);
            actions.Children.Add(button);
            return button;
        }

        switch (quote.Status)
        {
            case QuotationStatus.Draft:
                AddAction("Save draft", $"Save draft {quote.Reference}", ChromeStyles.Flat, () => OnSaveDraftAsync(quote.Id));
                AddAction("Submit for review", $"Submit {quote.Reference} for review", ChromeStyles.Primary, () => OnSubmitForReviewAsync(quote.Id));
                break;

            case QuotationStatus.InReview:
                var commentBox = new TextBox
                {
                    Watermark = "Comment for the author (needed to return it to draft)",
                    MinHeight = DesignTokens.ControlSizeMedium,
                    AcceptsReturn = true,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                };
                AutomationProperties.SetName(commentBox, "Review comment");
                panel.Children.Insert(0, commentBox);

                AddAction("Approve", $"Approve {quote.Reference}", ChromeStyles.Primary, () => OnReviewActAsync(quote.Id, QuotationReviewAct.Approve, null));
                AddAction("Return to draft", $"Return {quote.Reference} to draft", ChromeStyles.Flat, () => OnReviewActAsync(quote.Id, QuotationReviewAct.ReturnToDraft, commentBox.Text));
                break;

            case QuotationStatus.Approved:
                AddAction("Send", $"Send {quote.Reference}", ChromeStyles.Primary, () => OnSendAsync(quote.Id));
                break;

            case QuotationStatus.Sent:
                AddAction("Accept", $"Accept {quote.Reference}", ChromeStyles.Primary, () => OnAcceptAsync(quote.Id));
                AddAction("Decline", $"Decline {quote.Reference}", ChromeStyles.Flat, () => OnDeclineAsync(quote.Id));
                break;
        }

        // Export reads and saves a copy — never disabled by archiving.
        AddAction("Export", $"Export {quote.Reference}", ChromeStyles.Flat, () => OnExportAsync(quote.Id), archivable: false);

        return panel;
    }

    /// <summary>
    /// The person behind <paramref name="identityId"/> as the review line
    /// shows them — through <see cref="Principals"/> when composed, and
    /// never a raw Windows SID or GUID (colour review board M4, the
    /// identical rule <see cref="Tempest.Desktop.Documents.Timesheets.TimesheetPrincipalLabel"/>
    /// applies to the timesheet's own heading).
    /// </summary>
    private string PersonLabel(string? identityId) =>
        Tempest.Desktop.Documents.Timesheets.TimesheetPrincipalLabel.Resolve(Principals?.Describe(identityId), identityId);

    /// <summary>The line under the quote's own identity saying where it stands in draft-and-review (runbook C3).</summary>
    private string DescribeReviewState(Quotation quote)
    {
        var review = quote.Review;
        var nextLabel = QuotationReview.LabelFor(quote.RevisionNumber + 1);

        switch (quote.Status)
        {
            case QuotationStatus.Draft:
                var saved = review.DraftSavedAt is { } at
                    ? $"Draft — saved {at.ToLocalTime():yyyy-MM-dd HH:mm}"
                    : "Draft — not saved yet";
                var after = quote.RevisionNumber > 0 ? $" A new draft after {quote.Review.Revisions.LastOrDefault()?.Label ?? QuotationReview.LabelFor(quote.RevisionNumber)}; the next approval issues {nextLabel}." : (_secondPersonRequired ? $" Approval by a second person issues {nextLabel}." : $" Approval issues {nextLabel}.");
                var returned = review.ReturnComment is { Length: > 0 } comment ? $" Returned by {PersonLabel(review.ReturnedBy)}: \"{comment}\"" : string.Empty;
                return saved + "." + after + returned;

            case QuotationStatus.InReview:
                return _secondPersonRequired
                    ? $"In review — submitted by {PersonLabel(review.SubmittedBy)} at {review.SubmittedAt?.ToLocalTime():yyyy-MM-dd HH:mm}. A second person approves it as {nextLabel}, or returns it to draft with a comment."
                    : $"In review — submitted by {PersonLabel(review.SubmittedBy)} at {review.SubmittedAt?.ToLocalTime():yyyy-MM-dd HH:mm}. Approve it as {nextLabel}, or return it to draft with a comment.";

            case QuotationStatus.Approved:
                var approved = review.Revisions.LastOrDefault();
                return approved is null
                    ? $"Approved {quote.RevisionLabel} — ready to export and send."
                    : $"Approved {approved.Label} by {PersonLabel(approved.ApprovedBy)}{SelfApprovedSuffix(approved)} at {approved.ApprovedAt.ToLocalTime():yyyy-MM-dd HH:mm} — ready to export and send.";

            default:
                var issued = review.Revisions.LastOrDefault();
                return issued is null
                    ? $"Issued as {quote.RevisionLabel}."
                    : $"Issued as {issued.Label}, approved by {PersonLabel(issued.ApprovedBy)}{SelfApprovedSuffix(issued)} at {issued.ApprovedAt.ToLocalTime():yyyy-MM-dd HH:mm}.";
        }
    }

    /// <summary>" (self-approved)" for a revision its own author approved with second-person sign-off off (`ADR-0161`); empty otherwise.</summary>
    private static string SelfApprovedSuffix(QuotationRevision revision) => revision.SelfApproved ? " (self-approved)" : string.Empty;

    /// <summary>Whether <paramref name="quote"/>'s own lines can be changed here — a draft, or an approved revision (the change then starts a new draft, runbook C3).</summary>
    private static bool LinesEditable(Quotation quote) => quote.Status is QuotationStatus.Draft or QuotationStatus.Approved;

    /// <summary>The pinned rate card's own hourly entries, in the quote's currency, in card order.</summary>
    private static IEnumerable<RateCardEntry> HourlyEntries(RateCard? card, CurrencyCode currency) =>
        card?.Entries.Where(e => e.Basis == PricingBasis.Hourly && e.Rate.Currency == currency) ?? [];

    /// <summary>The project's own pinned rate card revision, or <see langword="null"/> when none is pinned, no catalog is composed (<see cref="RateCards"/>), or the pin no longer resolves.</summary>
    private async Task<RateCard?> LoadRateCardAsync(Quotation quote)
    {
        if (RateCards is not { } catalog
            || (quote.ParentId ?? _currentProjectId()) is not { } projectId
            || await _domainContext.Repository.FindAsync(projectId, CancellationToken.None).ConfigureAwait(true) is not Project { RateCardPin: { } pin })
        {
            return null;
        }

        try
        {
            var revision = await catalog.GetRevisionAsync(pin.RecordId, pin.RevisionNumber, CancellationToken.None).ConfigureAwait(true);
            return revision.Definition;
        }
        catch (Exception ex) when (ex is ReferenceRecordNotFoundException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>One choice in the rate dropdown: a pinned rate card entry, a line's own kept rate, or (both <see langword="null"/>) Fixed.</summary>
    private sealed record RateOption(RateCardEntry? Entry, Money? ManualRate);

    private async Task<Control> BuildAcceptedResultsSectionAsync(Quotation quote)
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(new TextBlock { Text = "Created on acceptance", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody });

        foreach (var line in quote.Lines)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
            row.Children.Add(new TextBlock { Text = line.Description, VerticalAlignment = VerticalAlignment.Center, FontSize = DesignTokens.FontSizeBody });

            if (line.DeliverableId is { } deliverableId)
            {
                var open = new Button { Content = "Open deliverable", MinHeight = DesignTokens.MinControlSize };
                open.Classes.Add(ChromeStyles.Flat);
                AutomationProperties.SetName(open, $"Open deliverable — {line.Description}");
                // `WP 19.10P` (D6): `_openObject` is a fire-and-forget
                // `Action`, not a `Task` this handler can await — a failure
                // inside it (there was one: no view was ever registered for
                // the Deliverable Kind, see `DeliverableCompletionWorkspaceRegistration`)
                // used to reach only the shell's own global toast, never
                // this view's own `ActionCompleted`/status line. Guarded
                // here so a future regression in the open path is not
                // silently swallowed a second time.
                open.Click += (_, _) =>
                {
                    try
                    {
                        _openObject(deliverableId, CanonicalObjectKinds.Deliverable);
                    }
                    catch (Exception ex)
                    {
                        Report($"The deliverable could not be opened: {ex.Message}", succeeded: false);
                    }
                };
                row.Children.Add(open);
            }

            if (line.RequirementId is { } requirementId)
            {
                var open = new Button { Content = "Open requirement", MinHeight = DesignTokens.MinControlSize };
                open.Classes.Add(ChromeStyles.Flat);
                AutomationProperties.SetName(open, $"Open requirement — {line.Description}");
                open.Click += (_, _) => _openObject(requirementId, RequirementsService.RequirementDocumentKind);
                row.Children.Add(open);
            }

            panel.Children.Add(row);
        }

        await Task.CompletedTask.ConfigureAwait(true);
        return panel;
    }

    private async Task OnCreateAsync()
    {
        var projectId = _currentProjectId();
        if (projectId is not { } id)
            return;

        var command = new CreateQuotationCommand(id, reference: null);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            Report(result.Message ?? "The quotation could not be opened.", succeeded: false);
            return;
        }

        _selectedQuotationId = result.SubjectId;
        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Quotation opened.", succeeded: true);
    }

    private async Task<bool> OnAddLineAsync(
        Guid quotationId, string kind, string description, decimal? hours, Money? rate, Money? fixedPrice, string? rateCardServiceCode)
    {
        var command = new AddQuotationLineCommand(quotationId, kind, description, hours, rate, fixedPrice, rateCardServiceCode);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            Report(result.Message ?? "The line could not be added.", succeeded: false);
            return false;
        }

        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Line added.", succeeded: true);
        return true;
    }

    private async Task<bool> OnUpdateLineAsync(
        Guid quotationId, string kind, Guid lineId, string description, decimal? hours, Money? rate, Money? fixedPrice, string? rateCardServiceCode)
    {
        var command = new UpdateQuotationLineCommand(quotationId, kind, lineId, description, hours, rate, fixedPrice, rateCardServiceCode);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            Report(result.Message ?? "The line could not be updated.", succeeded: false);
            return false;
        }

        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Line updated.", succeeded: true);
        return true;
    }

    private async Task OnRemoveLineAsync(Guid quotationId, Guid lineId)
    {
        var quote = _quotations.FirstOrDefault(q => q.Id == quotationId);
        var command = new RemoveQuotationLineCommand(quotationId, quote?.Kind ?? Quotation.CanonicalKind, lineId);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            Report(result.Message ?? "The line could not be removed.", succeeded: false);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Line removed.", succeeded: true);
    }

    /// <summary>
    /// Save draft (runbook C3): saves a line still being typed in the form
    /// first, then stamps the draft saved — no export, no send.
    /// </summary>
    private async Task OnSaveDraftAsync(Guid quotationId)
    {
        if (_pendingLineCommit is { } commit && !await commit().ConfigureAwait(true))
            return;

        await OnReviewActAsync(quotationId, QuotationReviewAct.SaveDraft, null).ConfigureAwait(true);
    }

    /// <summary>Submit for review (runbook C3): saves a line still being typed first, exactly as <see cref="OnSaveDraftAsync"/> does.</summary>
    private async Task OnSubmitForReviewAsync(Guid quotationId)
    {
        if (_pendingLineCommit is { } commit && !await commit().ConfigureAwait(true))
            return;

        await OnReviewActAsync(quotationId, QuotationReviewAct.SubmitForReview, null).ConfigureAwait(true);
    }

    /// <summary>Dispatches one draft-and-review act (runbook C3) — directly, like a line edit: none of them is irreversible.</summary>
    private async Task OnReviewActAsync(Guid quotationId, QuotationReviewAct act, string? comment)
    {
        var quote = _quotations.FirstOrDefault(q => q.Id == quotationId);
        var command = new QuotationReviewCommand(quotationId, quote?.Kind ?? Quotation.CanonicalKind, act, comment);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            Report(result.Message ?? "The quotation could not be updated.", succeeded: false);
            return;
        }

        _editingLineId = null;
        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Done.", succeeded: true);
    }

    /// <summary>
    /// Sends the quotation, then — on success — renders and attaches the
    /// quote sheet directly (this class's own remarks), so the sent sheet
    /// is on record (Product Owner comment item 9).
    /// </summary>
    private async Task OnSendAsync(Guid quotationId)
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can confirm the send here — Send is unavailable.", succeeded: false);
            return;
        }

        var context = new CommandContext([new CommandContextObject(quotationId, Quotation.CanonicalKind)]);
        var invocation = await _commandRegistry.InvokeAsync(QuotationCommandIds.Send, context, ParameterPrompt, CancellationToken.None).ConfigureAwait(true);

        if (invocation.Outcome == CommandOutcome.Cancelled)
            return;

        if (invocation.Outcome == CommandOutcome.Unavailable || invocation.Result is not { } result)
        {
            Report(invocation.Reason ?? "Send is unavailable.", succeeded: false);
            return;
        }

        if (!result.Succeeded)
        {
            Report(result.Message ?? "Send failed.", succeeded: false);
            return;
        }

        var message = result.Message ?? "Sent.";
        if (await _domainContext.Repository.FindAsync(quotationId, CancellationToken.None).ConfigureAwait(true) is Quotation sent)
        {
            try
            {
                var bytes = await RenderSheetAsync(sent).ConfigureAwait(true);
                await sent.AttachContentAsync(QuotationExport.FileName(sent), "application/pdf", bytes, CancellationToken.None).ConfigureAwait(true);
                message = $"{message} Quote sheet attached.";
            }
            catch (InvalidOperationException)
            {
                // No IAttachmentContentStore configured (a Core-only test
                // host, say) — the send itself still succeeded; the sheet
                // is simply not attached, exactly as `IssueEvidenceCommandHandler`'s
                // own "no renderer, no sheet, still issued" disclosed gap.
            }
        }

        await RefreshAsync().ConfigureAwait(true);
        Report(message, succeeded: true);
    }

    private async Task OnAcceptAsync(Guid quotationId)
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can confirm the acceptance here — Accept is unavailable.", succeeded: false);
            return;
        }

        var context = new CommandContext([new CommandContextObject(quotationId, Quotation.CanonicalKind)]);
        var invocation = await _commandRegistry.InvokeAsync(QuotationCommandIds.Accept, context, ParameterPrompt, CancellationToken.None).ConfigureAwait(true);

        if (invocation.Outcome == CommandOutcome.Cancelled)
            return;

        if (invocation.Outcome == CommandOutcome.Unavailable || invocation.Result is not { } result)
        {
            Report(invocation.Reason ?? "Accept is unavailable.", succeeded: false);
            return;
        }

        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? (result.Succeeded ? "Accepted." : "Accept failed."), succeeded: result.Succeeded);
    }

    private async Task OnDeclineAsync(Guid quotationId)
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can confirm the decline here — Decline is unavailable.", succeeded: false);
            return;
        }

        var context = new CommandContext([new CommandContextObject(quotationId, Quotation.CanonicalKind)]);
        var invocation = await _commandRegistry.InvokeAsync(QuotationCommandIds.Decline, context, ParameterPrompt, CancellationToken.None).ConfigureAwait(true);

        if (invocation.Outcome == CommandOutcome.Cancelled)
            return;

        if (invocation.Outcome == CommandOutcome.Unavailable || invocation.Result is not { } result)
        {
            Report(invocation.Reason ?? "Decline is unavailable.", succeeded: false);
            return;
        }

        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? (result.Succeeded ? "Declined." : "Decline failed."), succeeded: result.Succeeded);
    }

    /// <summary>Renders the quote sheet and saves it through <see cref="IFilePicker.PickSavePathAsync"/> (`ADR-0152`, Product Owner comment item 9) — the same mechanism the Evidence issue sheet already exports through.</summary>
    private async Task OnExportAsync(Guid quotationId)
    {
        if (_quotations.FirstOrDefault(q => q.Id == quotationId) is not { } quote)
            return;

        var startFolder = await QuoteStartFolderAsync(quote).ConfigureAwait(true);
        var destination = await _filePicker
            .PickSavePathAsync(new SavePickerRequest($"Export {quote.Reference} {QuotationExport.RevisionText(quote)}", QuotationExport.FileName(quote), startFolder), CancellationToken.None)
            .ConfigureAwait(true);

        if (destination is null)
        {
            Report("Export was cancelled.", succeeded: false);
            return;
        }

        var model = await BuildSheetModelAsync(quote).ConfigureAwait(true);
        var bytes = _sheetRenderer.Render(model).ToArray();
        await File.WriteAllBytesAsync(destination, bytes, CancellationToken.None).ConfigureAwait(true);

        // `v0.24.0` U3: an issued quotation keeps the exact bytes saved, so
        // its Xero copy carries the same PDF (X6's file source reads it back);
        // a re-export of the unchanged sheet keeps nothing more.
        if (XeroIssuedPdf.IsIssued(quote))
        {
            await XeroIssuedPdf.AttachAsync(
                quote, QuotationExport.FileName(quote), bytes, at => _sheetRenderer.Render(model with { GeneratedAtUtc = at }), CancellationToken.None).ConfigureAwait(true);
        }

        // Reading and saving a copy changes nothing in the domain — never
        // `ActionOutcome.Changed`, mirroring `ObjectEditorView.OnExportAttachmentAsync`'s
        // own identical remark.
        Report($"Exported to '{destination}'.", succeeded: true);
    }

    /// <summary>The project folder (or its quote subfolder) to start the export picker in — <see langword="null"/> whenever there is none, never an exception (see <see cref="ProjectFolders"/>).</summary>
    private Task<string?> QuoteStartFolderAsync(Quotation quote) =>
        QuotationSheetModelBuilder.StartFolderAsync(ProjectFolders, quote.ParentId ?? _currentProjectId(), CancellationToken.None);

    private async Task<byte[]> RenderSheetAsync(Quotation quote) =>
        _sheetRenderer.Render(await BuildSheetModelAsync(quote).ConfigureAwait(true)).ToArray();

    private Task<QuotationSheetModel> BuildSheetModelAsync(Quotation quote) =>
        QuotationSheetModelBuilder.BuildAsync(
            quote, _domainContext, _organisations, _issuerName(), _applicationVersionText(), _time.GetUtcNow(), CancellationToken.None);

    private Task<string> ResolveClientNameAsync(string? clientOrganisationId) =>
        QuotationSheetModelBuilder.ResolveClientNameAsync(_organisations, clientOrganisationId, CancellationToken.None);

    private void OnWorkspaceChanged(WorkspaceChange change)
    {
        if (!change.Entries.Any(e =>
            e.Kind == Quotation.CanonicalKind || e.Kind == CanonicalObjectKinds.Deliverable || e.Kind == RequirementsService.RequirementDocumentKind))
        {
            return;
        }

        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await RefreshAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                ActionCompleted?.Invoke($"Refresh failed: {ex.Message}", ActionOutcome.Failed);
            }
        });
    }

    private void Report(string message, bool succeeded)
    {
        _status.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(succeeded));
    }

    /// <summary>Disables <paramref name="control"/>, with the archived tooltip, while the open project is archived (`WP 19.10H`, `TD-179`).</summary>
    private void ApplyArchivedState(Button control)
    {
        control.IsEnabled = !_isArchived;
        ToolTip.SetTip(control, _isArchived ? ArchivedTooltip : null);
    }

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
