using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using System.IO;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Quotations;
using Tempest.Core.Requirements;
using Tempest.Desktop.Quotations;
using Tempest.Desktop.Theming;
using Tempest.Workspace;
using Tempest.Workspace.Files;
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

    private IWorkspaceChanges? _workspaceChanges;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>
    /// Collects Send/Accept/Decline's own confirmation — see this class's
    /// own remarks. <see langword="null"/> (any test that constructs this
    /// view directly) leaves all three honestly unavailable rather than
    /// run without asking.
    /// </summary>
    public CommandParameterPrompt? ParameterPrompt { get; set; }

    /// <summary>The change feed this view reloads its own list from (`WP 18.1A`, `WP 18.9.1`).</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges;
        set
        {
            if (ReferenceEquals(_workspaceChanges, value))
                return;

            if (_workspaceChanges is not null)
                _workspaceChanges.Changed -= OnWorkspaceChanged;

            _workspaceChanges = value;

            if (_workspaceChanges is not null)
                _workspaceChanges.Changed += OnWorkspaceChanged;
        }
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

        this.DetachedFromVisualTree += (_, _) => WorkspaceChanges = null;

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

    /// <summary>Reloads the open project's own quotation(s) — empty, honestly, when no project is open.</summary>
    public async Task RefreshAsync()
    {
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

        var members = await _domainContext.Repository.ListChildrenAsync(id, CancellationToken.None).ConfigureAwait(true);
        _quotations = members
            .OfType<Quotation>()
            .Where(IsLive)
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

        var identity = new StackPanel { Spacing = DesignTokens.SpaceXs };
        identity.Children.Add(new TextBlock
        {
            Text = $"{quote.Reference} — {quote.Status}",
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
        _detailPanel.Children.Add(identity);

        _detailPanel.Children.Add(BuildLinesSection(quote));

        if (quote.Status == QuotationStatus.Draft)
            _detailPanel.Children.Add(BuildLineEntryForm(quote));

        var termsBox = new TextBlock { Text = $"Terms: {quote.Terms ?? "(none)"}", FontSize = DesignTokens.FontSizeBody, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        _detailPanel.Children.Add(termsBox);

        _detailPanel.Children.Add(BuildActionsRow(quote));

        if (quote.Status == QuotationStatus.Accepted)
            _detailPanel.Children.Add(await BuildAcceptedResultsSectionAsync(quote).ConfigureAwait(true));
    }

    private Control BuildLinesSection(Quotation quote)
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

            var text = new TextBlock
            {
                Text = line.Basis == QuotationLineBasis.Hourly
                    ? $"{line.Description}  •  {line.Hours:0.##} × {line.Rate}  =  {line.Amount}"
                    : $"{line.Description}  •  {line.Amount} (fixed)",
                FontSize = DesignTokens.FontSizeBody,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(text, 0);
            row.Children.Add(text);

            if (quote.Status == QuotationStatus.Draft)
            {
                var edit = new Button { Content = "Edit", MinHeight = DesignTokens.MinControlSize };
                edit.Classes.Add(ChromeStyles.Flat);
                AutomationProperties.SetName(edit, $"Edit {line.Description}");
                var editLine = line;
                edit.Click += (_, _) => BeginEditLine(quote, editLine);
                Grid.SetColumn(edit, 1);
                row.Children.Add(edit);

                var remove = new Button { Content = "Remove", MinHeight = DesignTokens.MinControlSize };
                remove.Classes.Add(ChromeStyles.Flat);
                AutomationProperties.SetName(remove, $"Remove {line.Description}");
                var removeId = line.Id;
                remove.Click += async (_, _) => await OnRemoveLineAsync(quote.Id, removeId).ConfigureAwait(true);
                Grid.SetColumn(remove, 2);
                row.Children.Add(remove);
            }

            panel.Children.Add(row);
        }

        panel.Children.Add(new TextBlock
        {
            Text = $"Total {quote.Total}",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0),
        });

        return panel;
    }

    private Control BuildLineEntryForm(Quotation quote)
    {
        var descriptionBox = new TextBox { Watermark = "Description", MinHeight = DesignTokens.ControlSizeMedium };
        AutomationProperties.SetName(descriptionBox, "Line description");

        var hoursBox = new NumericUpDown { Watermark = "Hours", MinHeight = DesignTokens.ControlSizeMedium, FormatString = "0.##" };
        AutomationProperties.SetName(hoursBox, "Line hours");

        var rateBox = new NumericUpDown { Watermark = $"Rate ({quote.Currency})", MinHeight = DesignTokens.ControlSizeMedium, FormatString = "0.##" };
        AutomationProperties.SetName(rateBox, "Line rate");

        var fixedPriceBox = new NumericUpDown { Watermark = $"Fixed price ({quote.Currency})", MinHeight = DesignTokens.ControlSizeMedium, FormatString = "0.##" };
        AutomationProperties.SetName(fixedPriceBox, "Line fixed price");

        var lineStatus = new TextBlock { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

        var saveButton = new Button { Content = _editingLineId is null ? "Add line" : "Save line", MinHeight = DesignTokens.MinControlSize };
        saveButton.Classes.Add(ChromeStyles.Primary);
        AutomationProperties.SetName(saveButton, saveButton.Content?.ToString() ?? "Add line");

        var cancelButton = new Button { Content = "Cancel edit", MinHeight = DesignTokens.MinControlSize, IsVisible = _editingLineId is not null };
        cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(cancelButton, "Cancel edit");

        if (_editingLineId is { } editingId && quote.Lines.FirstOrDefault(l => l.Id == editingId) is { } editingLine)
        {
            descriptionBox.Text = editingLine.Description;
            hoursBox.Value = editingLine.Hours;
            rateBox.Value = editingLine.Rate?.Amount;
            fixedPriceBox.Value = editingLine.FixedPrice?.Amount;
        }

        saveButton.Click += async (_, _) =>
        {
            var description = descriptionBox.Text?.Trim() ?? string.Empty;
            if (description.Length == 0)
            {
                lineStatus.Text = "A description is required.";
                return;
            }

            var hours = (decimal?)hoursBox.Value;
            var rate = rateBox.Value is { } r ? new Money(r, quote.Currency) : (Money?)null;
            var fixedPrice = fixedPriceBox.Value is { } f ? new Money(f, quote.Currency) : (Money?)null;

            var succeeded = _editingLineId is { } lineId
                ? await OnUpdateLineAsync(quote.Id, quote.Kind, lineId, description, hours, rate, fixedPrice).ConfigureAwait(true)
                : await OnAddLineAsync(quote.Id, quote.Kind, description, hours, rate, fixedPrice).ConfigureAwait(true);

            if (succeeded)
                _editingLineId = null;
        };

        cancelButton.Click += async (_, _) =>
        {
            _editingLineId = null;
            await RefreshAsync().ConfigureAwait(true);
        };

        var fieldsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        fieldsRow.Children.Add(descriptionBox);
        fieldsRow.Children.Add(hoursBox);
        fieldsRow.Children.Add(rateBox);
        fieldsRow.Children.Add(fixedPriceBox);
        fieldsRow.Children.Add(saveButton);
        fieldsRow.Children.Add(cancelButton);

        var form = new StackPanel { Spacing = DesignTokens.SpaceXs };
        form.Children.Add(fieldsRow);
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
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };

        if (quote.Status == QuotationStatus.Draft)
        {
            var send = new Button { Content = "Send", MinHeight = DesignTokens.MinControlSize };
            send.Classes.Add(ChromeStyles.Primary);
            AutomationProperties.SetName(send, $"Send {quote.Reference}");
            send.Click += async (_, _) => await OnSendAsync(quote.Id).ConfigureAwait(true);
            actions.Children.Add(send);
        }

        if (quote.Status == QuotationStatus.Sent)
        {
            var accept = new Button { Content = "Accept", MinHeight = DesignTokens.MinControlSize };
            accept.Classes.Add(ChromeStyles.Primary);
            AutomationProperties.SetName(accept, $"Accept {quote.Reference}");
            accept.Click += async (_, _) => await OnAcceptAsync(quote.Id).ConfigureAwait(true);
            actions.Children.Add(accept);

            var decline = new Button { Content = "Decline", MinHeight = DesignTokens.MinControlSize };
            decline.Classes.Add(ChromeStyles.Flat);
            AutomationProperties.SetName(decline, $"Decline {quote.Reference}");
            decline.Click += async (_, _) => await OnDeclineAsync(quote.Id).ConfigureAwait(true);
            actions.Children.Add(decline);
        }

        var export = new Button { Content = "Export", MinHeight = DesignTokens.MinControlSize };
        export.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(export, $"Export {quote.Reference}");
        export.Click += async (_, _) => await OnExportAsync(quote.Id).ConfigureAwait(true);
        actions.Children.Add(export);

        return actions;
    }

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
                open.Click += (_, _) => _openObject(deliverableId, CanonicalObjectKinds.Deliverable);
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

    private async Task<bool> OnAddLineAsync(Guid quotationId, string kind, string description, decimal? hours, Money? rate, Money? fixedPrice)
    {
        var command = new AddQuotationLineCommand(quotationId, kind, description, hours, rate, fixedPrice);
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

    private async Task<bool> OnUpdateLineAsync(Guid quotationId, string kind, Guid lineId, string description, decimal? hours, Money? rate, Money? fixedPrice)
    {
        var command = new UpdateQuotationLineCommand(quotationId, kind, lineId, description, hours, rate, fixedPrice);
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
                await sent.AttachContentAsync($"{SanitiseFileNameSegment(sent.Reference)}-quote.pdf", "application/pdf", bytes, CancellationToken.None).ConfigureAwait(true);
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

        var destination = await _filePicker
            .PickSavePathAsync(new SavePickerRequest($"Export {quote.Reference}", $"{SanitiseFileNameSegment(quote.Reference)}-quote.pdf"), CancellationToken.None)
            .ConfigureAwait(true);

        if (destination is null)
        {
            Report("Export was cancelled.", succeeded: false);
            return;
        }

        var bytes = await RenderSheetAsync(quote).ConfigureAwait(true);
        await File.WriteAllBytesAsync(destination, bytes, CancellationToken.None).ConfigureAwait(true);

        // Reading and saving a copy changes nothing in the domain — never
        // `ActionOutcome.Changed`, mirroring `ObjectEditorView.OnExportAttachmentAsync`'s
        // own identical remark.
        Report($"Exported to '{destination}'.", succeeded: true);
    }

    private async Task<byte[]> RenderSheetAsync(Quotation quote)
    {
        var (projectCode, projectName) = await ResolveProjectAsync(quote.ParentId).ConfigureAwait(true);
        var clientName = await ResolveClientNameAsync(quote.ClientOrganisationId).ConfigureAwait(true);

        var lines = quote.Lines.Select(l => new QuotationSheetLineRow(
            l.Description,
            l.Basis == QuotationLineBasis.Hourly ? l.Hours?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : null,
            l.Basis == QuotationLineBasis.Hourly ? l.Rate?.ToString() : null,
            l.Amount.ToString())).ToList();

        var model = new QuotationSheetModel(
            IssuerName: _issuerName(),
            ProjectCode: projectCode,
            ProjectName: projectName,
            Client: clientName,
            Reference: quote.Reference,
            QuoteDate: quote.QuoteDate,
            ValidityDays: quote.ValidityDays,
            Currency: quote.Currency.ToString(),
            Lines: lines,
            Total: quote.Total.ToString(),
            Terms: quote.Terms,
            Status: quote.Status.ToString(),
            GeneratedAtUtc: _time.GetUtcNow(),
            ApplicationVersionText: _applicationVersionText());

        return _sheetRenderer.Render(model).ToArray();
    }

    private async Task<(string Code, string Name)> ResolveProjectAsync(Guid? projectId)
    {
        if (projectId is not { } id || await _domainContext.Repository.FindAsync(id, CancellationToken.None).ConfigureAwait(true) is not { } project)
            return (string.Empty, string.Empty);

        return ((project as IHasBusinessIdentifier)?.Identifier ?? string.Empty, (project as IHasBusinessIdentifier)?.DisplayName ?? string.Empty);
    }

    private async Task<string> ResolveClientNameAsync(string? clientOrganisationId)
    {
        if (string.IsNullOrWhiteSpace(clientOrganisationId))
            return "(none)";

        var found = await _organisations.FindAsync(clientOrganisationId, CancellationToken.None).ConfigureAwait(true);
        return found?.Definition.Name ?? clientOrganisationId;
    }

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

    private static string SanitiseFileNameSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
        return new string(chars);
    }

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
