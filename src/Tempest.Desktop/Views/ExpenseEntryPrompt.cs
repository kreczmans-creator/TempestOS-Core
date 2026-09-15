using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;
using Tempest.Workspace.Mechanical;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>Record expense's own collected values (`WP 21.3B`).</summary>
/// <param name="ProjectId">The project the expense is recorded against.</param>
/// <param name="Date">The day the expense was incurred.</param>
/// <param name="Description">What the expense was.</param>
/// <param name="Category">What the expense was for.</param>
/// <param name="NetAmount">The amount before VAT, as the receipt states it.</param>
/// <param name="VatAmount">The VAT amount, as the receipt states it.</param>
/// <param name="Billable">Whether the expense is billable to the client.</param>
public sealed record ExpenseEntryInput(Guid ProjectId, DateOnly Date, string Description, ExpenseCategory Category, Money NetAmount, Money VatAmount, bool Billable);

/// <summary>
/// The "Record expense…" dialog (`WP 21.3B`) — reachable from Business →
/// Timesheets, beside Record, and from the project's own Details tab.
/// Mirrors <see cref="TimesheetEntryPrompt"/>'s own shape: a project
/// picker over every open project, a date (defaulting to today),
/// description, category, net and VAT amounts (as the receipt states
/// them, in the project's own currency), and billable. Initially hidden,
/// shares the Dialog Framework's own established panel styling and real
/// modal behaviour.
/// </summary>
public sealed class ExpenseEntryPrompt : Border
{
    private readonly EngineeringDomainContext _domainContext;

    private readonly TextBlock _title = new() { FontSize = DesignTokens.FontSizeTitle, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading };
    private readonly ComboBox _project = new() { MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly DatePicker _date = new() { MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBox _description = new() { Watermark = "Description", MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly ComboBox _category = new() { MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly NumericUpDown _netAmount = new() { Minimum = 0m, Increment = 1m, MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly NumericUpDown _vatAmount = new() { Minimum = 0m, Increment = 1m, MinHeight = DesignTokens.ControlSizeMedium, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly CheckBox _billable = new() { Content = "Billable", IsChecked = true, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
    private readonly TextBlock _validation = new() { FontSize = DesignTokens.FontSizeCaption, Margin = new Thickness(0, DesignTokens.SpaceXs, 0, 0), IsVisible = false };

    private readonly Button _recordButton = new() { Content = "Record", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _cancelButton = new() { Content = "Cancel", MinHeight = DesignTokens.ControlSizeMedium };

    private IReadOnlyList<(Guid Id, string Label)> _projects = [];
    private TaskCompletionSource<ExpenseEntryInput?>? _pending;

    /// <summary>Initialises a new instance of the <see cref="ExpenseEntryPrompt"/> class, initially hidden.</summary>
    public ExpenseEntryPrompt(EngineeringDomainContext domainContext)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        _domainContext = domainContext;

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

        _title.Text = "Record expense";

        foreach (var category in Enum.GetValues<ExpenseCategory>())
            _category.Items.Add(new ComboBoxItem { Content = category.ToString(), Tag = category });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceMd, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, DesignTokens.SpaceLg, 0, 0) };
        buttons.Children.Add(_cancelButton);
        buttons.Children.Add(_recordButton);

        var body = new StackPanel();
        body.Children.Add(_title);
        body.Children.Add(_project);
        body.Children.Add(_date);
        body.Children.Add(_description);
        body.Children.Add(_category);
        body.Children.Add(_netAmount);
        body.Children.Add(_vatAmount);
        body.Children.Add(_billable);
        body.Children.Add(_validation);
        body.Children.Add(buttons);
        Child = body;

        _recordButton.Classes.Add(ChromeStyles.Primary);
        _cancelButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_project, "Project");
        AutomationProperties.SetName(_date, "Date");
        AutomationProperties.SetName(_description, "Description");
        AutomationProperties.SetName(_category, "Category");
        AutomationProperties.SetName(_netAmount, "Net amount");
        AutomationProperties.SetName(_vatAmount, "VAT amount");
        AutomationProperties.SetName(_billable, "Billable");
        AutomationProperties.SetName(_recordButton, "Record");
        AutomationProperties.SetName(_cancelButton, "Cancel");
        ToolTip.SetTip(_recordButton, "Record");
        ToolTip.SetTip(_cancelButton, "Cancel");

        _recordButton.Click += (_, _) => TryComplete();
        _cancelButton.Click += (_, _) => Complete(null);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Complete(null);
        };

        DialogModality.Install(this);
    }

    /// <summary>
    /// Shows this dialog and returns the expense entered, or <see langword="null"/>
    /// if the user cancelled.
    /// </summary>
    /// <param name="preferredProjectId">
    /// Pre-selects this project in the Project drop-down when it is one of
    /// the open projects listed — the project's own Details tab's "Record
    /// expense…" call site (`WP 21.3B`); the caller can still change it.
    /// <see langword="null"/> (Business → Timesheets' own call site) leaves
    /// the first project in the list selected, exactly as
    /// <see cref="TimesheetEntryPrompt"/>'s own identical default.
    /// </param>
    public async Task<ExpenseEntryInput?> PromptAsync(Guid? preferredProjectId = null, CancellationToken cancellationToken = default)
    {
        _pending?.TrySetResult(null);

        _date.SelectedDate = DateTimeOffset.Now;
        _description.Text = string.Empty;
        _category.SelectedIndex = 4; // Other — a safe default, never assumed by the model itself (`VatRate.OutOfScope`'s own identical "no assumption" shape).
        _netAmount.Value = 0m;
        _vatAmount.Value = 0m;
        _billable.IsChecked = true;
        _validation.IsVisible = false;

        await ReloadProjectsAsync(cancellationToken).ConfigureAwait(true);

        if (preferredProjectId is { } preferred)
        {
            var match = _project.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Equals(i.Tag, preferred));
            if (match is not null)
                _project.SelectedItem = match;
        }

        IsVisible = true;

        _pending = new TaskCompletionSource<ExpenseEntryInput?>();
        return await _pending.Task.ConfigureAwait(true);
    }

    private async Task ReloadProjectsAsync(CancellationToken cancellationToken)
    {
        // `WP 21.5B`: list results are index rows — live rows filtered on the row, then materialised.
        var everyProjectEntries = (await _domainContext.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(true))
            .Where(entry => !entry.IsDeleted)
            .ToList();
        var everyProject = await _domainContext.Repository.MaterialiseAsync<Project>(everyProjectEntries, cancellationToken).ConfigureAwait(true);

        // `WP 21.3B`: every open project is listed, exactly as `TimesheetEntryPrompt`'s
        // own Project field lists (D12) — an expense needs no pinned rate
        // card, so there is no "one real reason it cannot record" gate
        // here at all.
        _projects =
        [
            .. everyProject
                .OfType<Project>()
                .Where(p => p is not IDeletable { IsDeleted: true } && p.ClosedOn is null)
                .Select(p => (p.Id, p.DisplayName))
                .OrderBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase),
        ];

        _project.ItemsSource = _projects.Select(p => new ComboBoxItem { Content = p.Label, Tag = p.Id }).ToList();
        _project.SelectedIndex = _projects.Count > 0 ? 0 : -1;
    }

    private void TryComplete()
    {
        if (_project.SelectedItem is not ComboBoxItem { Tag: Guid projectId })
        {
            ShowValidationError("An open project is required.");
            return;
        }

        if (_date.SelectedDate is not { } date)
        {
            ShowValidationError("A date is required.");
            return;
        }

        var description = _description.Text?.Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            ShowValidationError("A description is required.");
            return;
        }

        if (_category.SelectedItem is not ComboBoxItem { Tag: ExpenseCategory category })
        {
            ShowValidationError("A category is required.");
            return;
        }

        // `WP 21.3B`: GBP — this platform's own fixture currency throughout
        // (`CurrencyCode.Gbp`'s own remarks) — is what every amount here is
        // recorded in; a project pricing in another currency is this
        // dialog's own disclosed simplification, not the model's (a
        // consultant amends the recorded expense's own amounts directly
        // if a receipt was in a different currency).
        var net = new Money(_netAmount.Value ?? 0m, CurrencyCode.Gbp);
        var vat = new Money(_vatAmount.Value ?? 0m, CurrencyCode.Gbp);

        Complete(new ExpenseEntryInput(projectId, DateOnly.FromDateTime(date.Date), description, category, net, vat, _billable.IsChecked ?? false));
    }

    private void ShowValidationError(string message)
    {
        _validation.Text = message;
        _validation.IsVisible = true;
    }

    private void Complete(ExpenseEntryInput? input)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        pending?.TrySetResult(input);
    }
}
