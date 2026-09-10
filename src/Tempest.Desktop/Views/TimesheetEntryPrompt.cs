using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;
using Tempest.Workspace.Mechanical;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>Record's own collected values (`WP 19.0A`, `ADR-0150`).</summary>
/// <param name="ProjectId">The project the time is recorded against.</param>
/// <param name="Date">The day the work was done.</param>
/// <param name="Hours">How many hours.</param>
/// <param name="Billable">Whether the time is billable.</param>
/// <param name="Grade">The grade this time is recorded at — one of the project's own pinned rate card's grades.</param>
/// <param name="Task">What the work was.</param>
public sealed record TimesheetEntryInput(Guid ProjectId, DateOnly Date, decimal Hours, bool Billable, string Grade, string Task);

/// <summary>
/// The weekly timesheet view's own Record dialog (`WP 19.0A`, `ADR-0150`):
/// a project picker over open projects that carry a Released rate-card
/// pin, a date (defaulting to today), hours, billable, a grade drawn from
/// the chosen project's own pinned card, and a task. Initially hidden,
/// shares the Dialog Framework's own established panel styling and real
/// modal behaviour (mirrors <see cref="CheckEntry"/>).
/// </summary>
public sealed class TimesheetEntryPrompt : Border
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly IRateCardCatalog _rateCards;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly ComboBox _project = new() { MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly DatePicker _date = new() { MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly NumericUpDown _hours = new() { Minimum = 0.25m, Maximum = 24m, Increment = 0.25m, Value = 1m, MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly CheckBox _billable = new() { Content = "Billable", IsChecked = true, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly ComboBox _grade = new() { MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _task = new() { Watermark = "Task", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBlock _validation = new() { FontSize = DesignTokens.FontSizeCaption, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };
    private readonly Button _recordButton = new() { Content = "Record", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private IReadOnlyList<(Guid Id, string Label, ReferencePin Pin)> _projects = [];
    private TaskCompletionSource<TimesheetEntryInput?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="TimesheetEntryPrompt"/> class, initially hidden.</summary>
    public TimesheetEntryPrompt(EngineeringDomainContext domainContext, IRateCardCatalog rateCards)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(rateCards);
        _domainContext = domainContext;
        _rateCards = rateCards;

        IsVisible = false;
        IsHitTestVisible = true;
        MinWidth = 380;
        MaxWidth = 460;
        CornerRadius = new CornerRadius(DesignTokens.DialogCornerRadius);
        Padding = DesignTokens.DialogPadding;
        BorderThickness = new Thickness(1);
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        ThemeReactiveBrush.Bind(this, BackgroundProperty, ApplicationPalette.PanelBackgroundBrushKey);
        ThemeReactiveBrush.Bind(this, BorderBrushProperty, ApplicationPalette.PanelBorderBrushKey);

        _title.Text = "Record time";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_recordButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_project);
        body.Children.Add(_date);
        body.Children.Add(_hours);
        body.Children.Add(_billable);
        body.Children.Add(_grade);
        body.Children.Add(_task);
        body.Children.Add(_validation);
        body.Children.Add(buttons);
        Child = body;

        _recordButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);

        _project.SelectionChanged += async (_, _) => await ReloadGradesAsync().ConfigureAwait(true);
        _recordButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>Shows this dialog and returns the entry entered, or <see langword="null"/> if the user cancelled.</summary>
    public async Task<TimesheetEntryInput?> PromptAsync(CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _date.SelectedDate = DateTimeOffset.Now;
        _hours.Value = 1m;
        _billable.IsChecked = true;
        _task.Text = string.Empty;
        _validation.IsVisible = false;

        await ReloadProjectsAsync(cancellationToken).ConfigureAwait(true);
        IsVisible = true;

        _pending = new TaskCompletionSource<TimesheetEntryInput?>();
        return await _pending.Task.ConfigureAwait(true);
    }

    private async Task ReloadProjectsAsync(CancellationToken cancellationToken)
    {
        var everyProject = await _domainContext.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(true);

        _projects =
        [
            .. everyProject
                .OfType<Project>()
                .Where(p => p is not IDeletable { IsDeleted: true } && p.RateCardPin is not null)
                .Select(p => (p.Id, p.DisplayName, p.RateCardPin!))
                .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase),
        ];

        _project.ItemsSource = _projects.Select(p => new ComboBoxItem { Content = p.Label, Tag = p.Id }).ToList();
        _project.SelectedIndex = _projects.Count > 0 ? 0 : -1;

        await ReloadGradesAsync().ConfigureAwait(true);
    }

    private async Task ReloadGradesAsync()
    {
        _grade.ItemsSource = null;

        if (_project.SelectedItem is not ComboBoxItem { Tag: Guid projectId })
            return;

        var pin = _projects.First(p => p.Id == projectId).Pin;

        try
        {
            var record = await _rateCards.GetRevisionAsync(pin.RecordId, pin.RevisionNumber).ConfigureAwait(true);
            var grades = record.Definition.Entries
                .Select(e => e.Grade)
                .Where(g => !string.IsNullOrWhiteSpace(g))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _grade.ItemsSource = grades;
            _grade.SelectedIndex = grades.Count > 0 ? 0 : -1;
        }
        catch (Exception)
        {
            // The pinned revision could not be read (a corrupted pin, or a
            // catalogue reset between sessions) — the grade list stays
            // empty and Record refuses below with a plain validation
            // message, exactly as an unresolvable pin should read: not a
            // crash, a refusal.
            _grade.ItemsSource = null;
        }
    }

    private void TryComplete()
    {
        if (_project.SelectedItem is not ComboBoxItem { Tag: Guid projectId })
        {
            ShowValidationError("A project with a Released rate-card pin is required.");
            return;
        }

        if (_date.SelectedDate is not { } date)
        {
            ShowValidationError("A date is required.");
            return;
        }

        if (_hours.Value is not { } hours || hours <= 0m)
        {
            ShowValidationError("Hours must be more than zero.");
            return;
        }

        if (_grade.SelectedItem is not string grade)
        {
            ShowValidationError("A grade priced on the project's own rate card is required.");
            return;
        }

        var task = _task.Text?.Trim();
        if (string.IsNullOrWhiteSpace(task))
        {
            ShowValidationError("A task is required.");
            return;
        }

        Complete(new TimesheetEntryInput(projectId, DateOnly.FromDateTime(date.Date), hours, _billable.IsChecked ?? false, grade, task));
    }

    private void ShowValidationError(string message)
    {
        _validation.Text = message;
        _validation.IsVisible = true;
    }

    private void Complete(TimesheetEntryInput? input)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(input);
    }
}
