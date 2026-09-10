using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Timesheets;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Timesheets area (`WP 19.0A`, `ADR-0150`): the current principal's
/// own week, Monday to Sunday — a row per entry (project, task, hours,
/// billable, grade, frozen billing rate, invoiced marker), totals per day
/// and for the week, and the resulting utilisation (billable ÷ available
/// hours from the working pattern).
/// </summary>
/// <remarks>
/// <para>
/// <b>Renders from the domain, refreshed by the change feed.</b> The
/// week's own entries are read fresh through <see cref="ITimesheetService.ListForPrincipalWeekAsync"/>
/// every time <see cref="IWorkspaceChanges.Changed"/> touches a
/// <see cref="TimesheetEntry"/>, mirroring
/// <see cref="EvidenceWorkspaceView"/>'s own identical discipline
/// (`WP 18.9.1`) — there is no manual refresh call site anywhere else in
/// this class.
/// </para>
/// <para>
/// <b>No blocking calls.</b> Every read and write here is
/// <see langword="await"/>ed — the `WP 18.1A` structural guard
/// (<c>NoBlockingPersistenceCallsTests</c>) applies to this file exactly
/// as it does to every other one under <c>src/Tempest.Desktop</c>.
/// </para>
/// </remarks>
public sealed class TimesheetWeekView : UserControl
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly ITimesheetService _timesheetService;
    private readonly IWorkingPatternProvider _workingPatterns;
    private readonly ICommandDispatcher _commandDispatcher;
    private readonly ICommandRegistry _commandRegistry;
    private readonly Func<string?> _currentPrincipalId;
    private readonly TimesheetEntryPrompt _recordPrompt;
    private readonly Action<Guid, string> _openObject;

    private readonly TextBlock _weekLabel = new() { FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeHeading, FontWeight = DesignTokens.WeightHeading };
    private readonly TextBlock _status = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private readonly TextBlock _utilisation = new() { FontSize = DesignTokens.FontSizeBody, FontWeight = DesignTokens.WeightHeading };
    private readonly Button _previousWeek = new() { Content = "◀ Previous", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _nextWeek = new() { Content = "Next ▶", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _recordButton = new() { Content = "Record", MinHeight = DesignTokens.MinControlSize };
    private readonly StackPanel _days = new() { Spacing = DesignTokens.SpaceMd };

    private DateOnly _weekStart;
    private IWorkspaceChanges? _workspaceChanges;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>
    /// Collects Amend's own values and Delete's own confirmation — reuses
    /// <see cref="Views.RibbonView.ParameterPrompt"/>'s own pattern
    /// (`WP 18.2A`, §2), so both dispatch through the already-registered
    /// <c>timesheet.amend</c>/<c>timesheet.delete</c> descriptors exactly
    /// as the Ribbon and the Command Palette do. <see langword="null"/>
    /// (any test that constructs this view directly) leaves both honestly
    /// unavailable rather than run without asking.
    /// </summary>
    public CommandParameterPrompt? ParameterPrompt { get; set; }

    /// <summary>The change feed this view reloads its own week from (`WP 18.1A`, `WP 18.9.1`).</summary>
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

    /// <summary>Initialises a new instance of the <see cref="TimesheetWeekView"/> class.</summary>
    public TimesheetWeekView(
        EngineeringDomainContext domainContext, ITimesheetService timesheetService, IWorkingPatternProvider workingPatterns,
        ICommandDispatcher commandDispatcher, ICommandRegistry commandRegistry, Func<string?> currentPrincipalId,
        TimesheetEntryPrompt recordPrompt, Action<Guid, string> openObject)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(timesheetService);
        ArgumentNullException.ThrowIfNull(workingPatterns);
        ArgumentNullException.ThrowIfNull(commandDispatcher);
        ArgumentNullException.ThrowIfNull(commandRegistry);
        ArgumentNullException.ThrowIfNull(currentPrincipalId);
        ArgumentNullException.ThrowIfNull(recordPrompt);
        ArgumentNullException.ThrowIfNull(openObject);

        _domainContext = domainContext;
        _timesheetService = timesheetService;
        _workingPatterns = workingPatterns;
        _commandDispatcher = commandDispatcher;
        _commandRegistry = commandRegistry;
        _currentPrincipalId = currentPrincipalId;
        _recordPrompt = recordPrompt;
        _openObject = openObject;

        _weekStart = TimesheetWeek.WeekOf(DateOnly.FromDateTime(DateTime.Now));

        this.DetachedFromVisualTree += (_, _) => WorkspaceChanges = null;

        _recordButton.Classes.Add(ChromeStyles.Primary);
        _previousWeek.Classes.Add(ChromeStyles.Subtle);
        _nextWeek.Classes.Add(ChromeStyles.Subtle);

        _previousWeek.Click += async (_, _) => await ChangeWeekAsync(-7).ConfigureAwait(true);
        _nextWeek.Click += async (_, _) => await ChangeWeekAsync(7).ConfigureAwait(true);
        _recordButton.Click += async (_, _) => await OnRecordAsync().ConfigureAwait(true);

        var nav = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        nav.Children.Add(_previousWeek);
        nav.Children.Add(_nextWeek);
        nav.Children.Add(_recordButton);

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(_weekLabel, 0);
        Grid.SetColumn(nav, 1);
        header.Children.Add(_weekLabel);
        header.Children.Add(nav);

        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        body.Children.Add(header);
        body.Children.Add(_status);
        body.Children.Add(_utilisation);
        body.Children.Add(_days);

        AutomationProperties.SetName(this, "Timesheets");
        Content = new ScrollViewer { Content = body };
    }

    /// <summary>The Monday this view is currently showing the week of.</summary>
    public DateOnly WeekStart => _weekStart;

    /// <summary>Reloads the current principal's own week — empty, honestly, when nobody is signed in.</summary>
    public async Task RefreshAsync()
    {
        _weekLabel.Text = $"Week of {_weekStart:yyyy-MM-dd}";

        var identityId = _currentPrincipalId();
        if (identityId is null)
        {
            _status.Text = "No principal is signed in.";
            _utilisation.Text = string.Empty;
            _days.Children.Clear();
            return;
        }

        var entries = await _timesheetService.ListForPrincipalWeekAsync(identityId, _weekStart).ConfigureAwait(true);
        var available = await _workingPatterns.AvailableHoursAsync(identityId, _weekStart).ConfigureAwait(true);

        var rows = new List<EntryRow>(entries.Count);
        foreach (var entry in entries)
            rows.Add(await ToRowAsync(entry).ConfigureAwait(true));

        var weekTotal = rows.Sum(r => r.Hours);
        var billableTotal = rows.Where(r => r.Billable).Sum(r => r.Hours);
        var utilisation = available > 0m ? billableTotal / available : 0m;

        _status.Text = rows.Count == 0
            ? "No time recorded this week."
            : $"{rows.Count} entr{(rows.Count == 1 ? "y" : "ies")} — {weekTotal:0.##}h total, {billableTotal:0.##}h billable.";
        _utilisation.Text = $"Available: {available:0.##}h · Utilisation: {utilisation:P0}";

        _days.Children.Clear();
        foreach (var day in Enumerable.Range(0, 7).Select(offset => _weekStart.AddDays(offset)))
        {
            var dayRows = rows.Where(r => r.Entry.Date == day).OrderBy(r => r.Entry.TaskDescription, StringComparer.Ordinal).ToList();
            _days.Children.Add(BuildDay(day, dayRows));
        }
    }

    private Control BuildDay(DateOnly day, IReadOnlyList<EntryRow> rows)
    {
        var dayTotal = rows.Sum(r => r.Hours);

        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(new TextBlock
        {
            Text = $"{day:dddd, yyyy-MM-dd} — {dayTotal:0.##}h",
            FontWeight = DesignTokens.WeightHeading,
            FontSize = DesignTokens.FontSizeBody,
        });

        if (rows.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "(no time recorded)", Opacity = 0.6, FontSize = DesignTokens.FontSizeCaption });
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

    private Control BuildRow(EntryRow row)
    {
        var entry = row.Entry;

        var text = new TextBlock
        {
            Text = $"{row.ProjectName} — {entry.TaskDescription} — {entry.Hours:0.##}h — {(entry.Billable ? "billable" : "non-billable")} — {entry.Grade} — {entry.BillingRate}"
                + (entry.InvoicedBy is not null ? " — INVOICED" : string.Empty),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var amend = new Button { Content = "Amend", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody, Tag = entry.Id };
        var delete = new Button { Content = "Delete", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody, Tag = entry.Id };
        amend.Classes.Add(ChromeStyles.Flat);
        delete.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(amend, $"Amend {entry.TaskDescription}");
        AutomationProperties.SetName(delete, $"Delete {entry.TaskDescription}");
        amend.Click += async (_, _) => await OnAmendAsync(entry.Id).ConfigureAwait(true);
        delete.Click += async (_, _) => await OnDeleteAsync(entry.Id).ConfigureAwait(true);

        var row2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd };
        row2.Children.Add(text);
        row2.Children.Add(amend);
        row2.Children.Add(delete);

        var openButton = new Button { Content = "Open", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody };
        openButton.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(openButton, $"Open {entry.TaskDescription}");
        openButton.Click += (_, _) => _openObject(entry.Id, TimesheetEntry.CanonicalKind);
        row2.Children.Add(openButton);

        return row2;
    }

    private async Task<EntryRow> ToRowAsync(TimesheetEntry entry)
    {
        var project = await _domainContext.Repository.FindAsync(entry.ProjectId).ConfigureAwait(true);
        var projectName = project is IHasBusinessIdentifier identity ? identity.DisplayName : entry.ProjectId.ToString();

        return new EntryRow(entry, projectName);
    }

    private async Task ChangeWeekAsync(int days)
    {
        _weekStart = _weekStart.AddDays(days);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task OnRecordAsync()
    {
        var input = await _recordPrompt.PromptAsync().ConfigureAwait(true);
        if (input is null)
        {
            Report("Record was cancelled.", succeeded: false);
            return;
        }

        var command = new Tempest.Workspace.Timesheets.RecordTimesheetCommand(
            input.ProjectId, input.Date, input.Hours, input.Billable, input.Grade, input.Task);
        var result = await _commandDispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(true);

        if (!result.Succeeded)
        {
            Report(result.Message ?? "Record failed.", succeeded: false);
            return;
        }

        // The week actually recorded to might not be the one on screen —
        // follow it, exactly as opening the record itself would show it.
        _weekStart = TimesheetWeek.WeekOf(input.Date);
        await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? "Recorded.", succeeded: true);

        // `WP 17.9.4`: what you make opens right up.
        if (result.SubjectId is { } createdId)
            _openObject(createdId, TimesheetEntry.CanonicalKind);
    }

    private async Task OnAmendAsync(Guid entryId)
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can collect the amendment here — Amend is unavailable.", succeeded: false);
            return;
        }

        var context = new CommandContext([new CommandContextObject(entryId, TimesheetEntry.CanonicalKind)]);
        var invocation = await _commandRegistry.InvokeAsync("timesheet.amend", context, ParameterPrompt, CancellationToken.None).ConfigureAwait(true);

        if (invocation.Outcome == CommandOutcome.Cancelled)
            return;

        if (invocation.Outcome == CommandOutcome.Unavailable || invocation.Result is not { } result)
        {
            Report(invocation.Reason ?? "Amend is unavailable.", succeeded: false);
            return;
        }

        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? (result.Succeeded ? "Entry amended." : "Amend failed."), succeeded: result.Succeeded);
    }

    private async Task OnDeleteAsync(Guid entryId)
    {
        if (ParameterPrompt is null)
        {
            Report("Nothing can confirm the deletion here — Delete is unavailable.", succeeded: false);
            return;
        }

        var context = new CommandContext([new CommandContextObject(entryId, TimesheetEntry.CanonicalKind)]);
        var invocation = await _commandRegistry.InvokeAsync("timesheet.delete", context, ParameterPrompt, CancellationToken.None).ConfigureAwait(true);

        if (invocation.Outcome == CommandOutcome.Cancelled)
            return;

        if (invocation.Outcome == CommandOutcome.Unavailable || invocation.Result is not { } result)
        {
            Report(invocation.Reason ?? "Delete is unavailable.", succeeded: false);
            return;
        }

        if (result.Succeeded)
            await RefreshAsync().ConfigureAwait(true);
        Report(result.Message ?? (result.Succeeded ? "Entry deleted." : "Delete failed."), succeeded: result.Succeeded);
    }

    private void OnWorkspaceChanged(WorkspaceChange change)
    {
        if (!change.Entries.Any(e => e.Kind == TimesheetEntry.CanonicalKind))
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

    private sealed record EntryRow(TimesheetEntry Entry, string ProjectName)
    {
        public decimal Hours => Entry.Hours;
        public bool Billable => Entry.Billable;
    }
}
