using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using System.IO;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Quotations;
using Tempest.Desktop.Quotations;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Files;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Quotations;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Quotes area (`WP 19.5B`, `ADR-0152`, Product Owner comment items 4
/// and 9, and comment item 6's "Business tree" sketch, sub-item Quotes:
/// "New quote · Quotes sent · Outstanding"): every <see cref="Quotation"/>
/// across every live project — or, when a project is open, that project's
/// own quotations alone — as three named lists: <b>New</b> (Draft),
/// <b>Sent</b>, and <b>Outstanding</b> (Sent more than seven days ago);
/// <b>Open</b> opens the project's own Quote tab, right up, selecting that
/// quotation; <b>Export</b> renders and saves the sheet; <b>New Quote</b>
/// picks a project and opens a Draft with it.
/// </summary>
/// <remarks>
/// <para>
/// <b>One rail entry today, `WP 19.7A` moves it under Business later</b> —
/// this Work Package's own brief scope item 5: "registered as
/// <c>ShellArea.Quotes</c> (add the enum member and rail descriptor; `WP
/// 19.7A` will move it under Business later, so keep the registration one
/// line)".
/// </para>
/// <para>
/// <b>Renders from the domain, refreshed by the change feed.</b> Mirrors
/// <see cref="InvoicingView"/>'s own identical discipline: every quotation
/// is read fresh, through one coherent
/// <see cref="EngineeringDomainContext.Repository"/> read, every time
/// <see cref="IWorkspaceChanges.Changed"/> touches
/// <see cref="Quotation.CanonicalKind"/> — there is no manual refresh call
/// site anywhere else in this class.
/// </para>
/// <para>
/// <b>New Quote dispatches directly, Open never navigates through a
/// second mechanism.</b> Creating a quotation needs no confirmation
/// (mirrors <see cref="ProjectDeliverablesView"/>'s own "Complete" — a
/// create action, never destructive); <b>Open</b> reuses the identical
/// "switch area, reveal, select" shape every other "opens right up"
/// callback in this platform already follows (`WP 17.9.4`), specialised
/// here to the project's own Quote tab rather than the generic Object
/// Editor, matching this Work Package's own brief wording verbatim
/// ("Open (opens the project's Quote tab right up)").
/// </para>
/// </remarks>
public sealed class QuotesView : UserControl
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly Func<Guid?> _currentProjectId;
    private readonly IOrganisationCatalog _organisations;
    private readonly IProjectDirectory _projectDirectory;
    private readonly ProjectPicker _projectPicker;
    private readonly IFilePicker _filePicker;
    private readonly QuotationSheetRenderer _sheetRenderer;
    private readonly Func<string> _issuerName;
    private readonly Func<string> _applicationVersionText;
    private readonly Action<Guid, Guid> _openQuote;
    private readonly TimeProvider _time;

    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly StackPanel _groups = new() { Spacing = DesignTokens.SpaceMd };
    private readonly Button _newQuoteButton = new() { Content = "New Quote", MinHeight = DesignTokens.ControlSizeMedium };

    private readonly WorkspaceChangesSubscription _workspaceChanges;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>The change feed this view reloads its own list from (`WP 18.1A`, `WP 18.9.1`).</summary>
    public IWorkspaceChanges? WorkspaceChanges
    {
        get => _workspaceChanges.Feed;
        set => _workspaceChanges.Feed = value;
    }

    /// <summary>Initialises a new instance of the <see cref="QuotesView"/> class.</summary>
    public QuotesView(
        EngineeringDomainContext domainContext, ICommandDispatcher commandDispatcher, Func<Guid?> currentProjectId,
        IOrganisationCatalog organisations, IProjectDirectory projectDirectory, ProjectPicker projectPicker,
        IFilePicker filePicker, QuotationSheetRenderer sheetRenderer, Func<string> issuerName, Func<string> applicationVersionText,
        Action<Guid, Guid> openQuote, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(currentProjectId);
        ArgumentNullException.ThrowIfNull(organisations);
        ArgumentNullException.ThrowIfNull(projectDirectory);
        ArgumentNullException.ThrowIfNull(projectPicker);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(sheetRenderer);
        ArgumentNullException.ThrowIfNull(issuerName);
        ArgumentNullException.ThrowIfNull(applicationVersionText);
        ArgumentNullException.ThrowIfNull(openQuote);

        _domainContext = domainContext;
        _commandDispatcher = commandDispatcher;
        _currentProjectId = currentProjectId;
        _organisations = organisations;
        _projectDirectory = projectDirectory;
        _projectPicker = projectPicker;
        _filePicker = filePicker;
        _sheetRenderer = sheetRenderer;
        _issuerName = issuerName;
        _applicationVersionText = applicationVersionText;
        _openQuote = openQuote;
        _time = timeProvider ?? TimeProvider.System;

        _workspaceChanges = new WorkspaceChangesSubscription(this, OnWorkspaceChanged);

        var heading = new TextBlock
        {
            Text = "Quotes",
            FontFamily = DesignTokens.TitleFont,
            FontSize = DesignTokens.FontSizeHeading,
            FontWeight = DesignTokens.WeightHeading,
        };

        AutomationProperties.SetName(_newQuoteButton, "New Quote");
        _newQuoteButton.Classes.Add(ChromeStyles.Primary);
        _newQuoteButton.Click += async (_, _) => await OnNewQuoteAsync().ConfigureAwait(true);

        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        headerRow.Children.Add(heading);
        headerRow.Children.Add(_newQuoteButton);

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(headerRow);
        body.Children.Add(_status);
        body.Children.Add(_groups);

        AutomationProperties.SetName(this, "Quotes");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>Reloads every quotation in scope — every live project's own quotations, or the open project's alone when one is open.</summary>
    /// <summary>Test-only (`WP 19.7C`, <c>WorkspaceChangesReattachTests</c>): counts every <see cref="RefreshAsync"/> call, proving a reattached view's subscription still reaches <see cref="OnWorkspaceChanged"/>.</summary>
    internal int RefreshCount { get; private set; }

    public async Task RefreshAsync()
    {
        RefreshCount++;

        var scopedProjectId = _currentProjectId();

        var projects = await ProjectsInScopeAsync(scopedProjectId).ConfigureAwait(true);

        var rows = new List<QuoteRow>();
        foreach (var project in projects)
        {
            var quotations = (await _domainContext.Repository.ListChildrenAsync(project.Id).ConfigureAwait(true))
                .OfType<Quotation>()
                .Where(IsLive);

            var projectName = DisplayNameOf(project);
            foreach (var quotation in quotations)
                rows.Add(new QuoteRow(quotation, project.Id, projectName, await ResolveClientNameAsync(quotation.ClientOrganisationId).ConfigureAwait(true)));
        }

        _status.Text = rows.Count == 0
            ? (scopedProjectId is null ? "No quotations yet." : "No quotations for this project yet.")
            : $"{rows.Count} quotation(s) across {projects.Count} project(s).";

        var asOf = _time.GetUtcNow();

        var draft = rows.Where(r => r.Quotation.Status == QuotationStatus.Draft).OrderByDescending(r => r.Quotation.QuoteDate).ToList();
        var sent = rows.Where(r => r.Quotation.Status == QuotationStatus.Sent).OrderByDescending(r => r.Quotation.SentOn).ToList();
        var outstanding = sent.Where(r => IsOutstanding(r.Quotation, asOf)).ToList();

        _groups.Children.Clear();
        _groups.Children.Add(BuildGroup("New", draft));
        _groups.Children.Add(BuildGroup("Sent", sent));
        _groups.Children.Add(BuildGroup("Outstanding (sent more than seven days ago)", outstanding));
    }

    /// <summary>A disclosed heuristic — Sent, and more than seven days since <see cref="Quotation.SentOn"/>, mirroring <c>OperationalGovernance.IsOverdueAt</c>'s own named "days since" shape.</summary>
    private static bool IsOutstanding(Quotation quotation, DateTimeOffset asOf) =>
        quotation.Status == QuotationStatus.Sent
        && quotation.SentOn is { } sentOn
        && DateOnly.FromDateTime(asOf.UtcDateTime).DayNumber - sentOn.DayNumber > 7;

    private async Task<List<IEngineeringObject>> ProjectsInScopeAsync(Guid? scopedProjectId)
    {
        if (scopedProjectId is { } id)
        {
            var project = await _domainContext.Repository.FindAsync(id).ConfigureAwait(true);
            return project is not null && IsLive(project) ? [project] : [];
        }

        return (await _domainContext.Repository.ListByKindAsync(ProjectDirectory.ProjectKind).ConfigureAwait(true))
            .Where(IsLive)
            .OrderBy(DisplayNameOf, StringComparer.Ordinal)
            .ToList();
    }

    private Control BuildGroup(string title, IReadOnlyList<QuoteRow> rows)
    {
        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(new TextBlock
        {
            Text = $"{title} ({rows.Count})",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
        });

        if (rows.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "(none)", Opacity = 0.6, FontSize = DesignTokens.FontSizeCaption });
        }
        else
        {
            foreach (var row in rows)
                panel.Children.Add(BuildRow(row));
        }

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = panel,
        };
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }

    private Control BuildRow(QuoteRow row)
    {
        var quote = row.Quotation;
        var rows = new StackPanel { Spacing = DesignTokens.SpaceXs };

        rows.Children.Add(new TextBlock
        {
            Text = $"{quote.Reference} — {row.ProjectName} — Client {row.ClientName} — {quote.Total}",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        rows.Children.Add(new TextBlock
        {
            Text = $"Date {quote.QuoteDate:yyyy-MM-dd}"
                + $" — Sent {quote.SentOn?.ToString("yyyy-MM-dd") ?? "(not yet sent)"}"
                + $" — Decided {quote.DecidedOn?.ToString("yyyy-MM-dd") ?? "(not yet decided)"}",
            FontSize = DesignTokens.FontSizeCaption,
            Opacity = 0.85,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };

        var open = new Button { Content = "Open", MinHeight = DesignTokens.MinControlSize };
        open.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(open, $"Open {quote.Reference}");
        var projectId = row.ProjectId;
        var quoteId = quote.Id;
        open.Click += (_, _) => _openQuote(projectId, quoteId);
        actions.Children.Add(open);

        var export = new Button { Content = "Export", MinHeight = DesignTokens.MinControlSize };
        export.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(export, $"Export {quote.Reference}");
        export.Click += async (_, _) => await OnExportAsync(quote.Id).ConfigureAwait(true);
        actions.Children.Add(export);

        rows.Children.Add(actions);

        var border = new Border
        {
            Padding = DesignTokens.PanelPadding,
            CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius),
            BorderThickness = new Thickness(1),
            Child = rows,
            Tag = quote.Id,
        };
        ThemeReactiveBrush.Bind(border, Border.BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(border, Border.BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);
        return border;
    }

    private async Task OnNewQuoteAsync()
    {
        var projectId = await _projectPicker.PickAsync().ConfigureAwait(true);
        if (projectId is not { } id)
        {
            Report("New Quote was cancelled.", succeeded: false);
            return;
        }

        var command = new CreateQuotationCommand(id, reference: null);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            Report(result.Message ?? "The quotation could not be opened.", succeeded: false);
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Quotation opened.", succeeded: true);

        // `WP 17.9.4`: what you make opens right up — the project's own Quote tab.
        if (result.SubjectId is { } createdId)
            _openQuote(id, createdId);
    }

    private async Task OnExportAsync(Guid quotationId)
    {
        if (await _domainContext.Repository.FindAsync(quotationId, CancellationToken.None).ConfigureAwait(true) is not Quotation quote)
            return;

        var destination = await _filePicker
            .PickSavePathAsync(new SavePickerRequest($"Export {quote.Reference}", $"{SanitiseFileNameSegment(quote.Reference)}-quote.pdf"), CancellationToken.None)
            .ConfigureAwait(true);

        if (destination is null)
        {
            Report("Export was cancelled.", succeeded: false);
            return;
        }

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

        var bytes = _sheetRenderer.Render(model).ToArray();
        await File.WriteAllBytesAsync(destination, bytes, CancellationToken.None).ConfigureAwait(true);

        Report($"Exported to '{destination}'.", succeeded: true);
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
        if (!change.Entries.Any(e => e.Kind == Quotation.CanonicalKind))
            return;

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

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };

    private sealed record QuoteRow(Quotation Quotation, Guid ProjectId, string ProjectName, string ClientName);
}
