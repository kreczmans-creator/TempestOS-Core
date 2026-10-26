using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Calc.Library;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// Addendum C, C-3: the records a calculator needs and does not have. Under the title the form says which required records are missing, in words,
/// each with a "Fill" link that opens the records screen on that record type; the records that are recommended or needed only in some cases are
/// listed below, smaller. The coordinator reads the store and hands the list in (<see cref="SetRecordRequirements"/>); the view only draws it.
/// </summary>
public sealed partial class CalcSheetView
{
    public const string RequirementsPanelName = "Required records";
    public const string RequirementsSummaryName = "Required records summary";
    public const string OpenRecordsCaption = "Open records screen";

    /// <summary>The automation name of the link that fills one record type: <c>Fill record: &lt;title&gt;</c>.</summary>
    public static string FillRecordName(string kindTitle) => $"Fill record: {kindTitle}";

    private readonly StackPanel _requirementsPanel = new() { Spacing = DesignTokens.SpaceXs, IsVisible = false, Margin = DesignTokens.SectionMargin };
    private readonly TextBlock _requirementsSummary = new() { FontSize = DesignTokens.FontSizeBody, FontWeight = DesignTokens.WeightHeading, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _requirementsRows = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _openRecords = new() { Content = OpenRecordsCaption, MinHeight = DesignTokens.MinControlSize };
    private IReadOnlyList<RecordRequirement> _requirements = [];

    /// <summary>The user asked for the records screen, on this record kind (or, with <see langword="null"/>, on whichever the form suggests).</summary>
    public event Action<string?>? OpenRecordsRequested;

    /// <summary>Raised when a template's form has been built (a new sheet, an opened calculation): the coordinator then reads which records it is missing.</summary>
    public event Action<CalcCatalogueEntry>? TemplateShown;

    /// <summary>The record kinds the shown template draws on, with whether each is entered, as last given by the coordinator.</summary>
    public IReadOnlyList<RecordRequirement> Requirements => _requirements;

    /// <summary>The required record kinds that no record satisfies.</summary>
    public IReadOnlyList<string> MissingRequiredKinds => [.. _requirements.Where(r => r.Need == RecordNeed.Required && r.IsMissing).Select(r => r.Kind)];

    /// <summary>Whether the missing-records panel is shown.</summary>
    public bool IsRequirementsShown => _requirementsPanel.IsVisible;

    /// <summary>The panel's summary sentence.</summary>
    public string RequirementsSummary => _requirementsSummary.Text ?? string.Empty;

    private void InitialiseRequirements()
    {
        AutomationProperties.SetName(_requirementsPanel, RequirementsPanelName);
        AutomationProperties.SetName(_requirementsSummary, RequirementsSummaryName);
        AutomationProperties.SetLiveSetting(_requirementsSummary, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_openRecords, OpenRecordsCaption);
        _requirementsPanel.Children.Add(_requirementsSummary);
        _requirementsPanel.Children.Add(_requirementsRows);
        _openRecords.Click += (_, _) => OpenRecordsRequested?.Invoke(null);
    }

    /// <summary>The record kind the "Open records screen" link should open on: the one picked for saving, else the first missing required, else the first the template draws on.</summary>
    public string? SuggestedRecordKind()
    {
        var kinds = SelectedTemplate?.Template.RecordKinds ?? [];

        if (_recordKind.SelectedIndex >= 0 && _recordKind.SelectedIndex < kinds.Count)
            return kinds[_recordKind.SelectedIndex].Kind;

        return MissingRequiredKinds.FirstOrDefault() ?? _requirements.FirstOrDefault(r => r.IsMissing)?.Kind ?? kinds.FirstOrDefault()?.Kind;
    }

    /// <summary>Shows which of the template's record kinds are entered. A list for another template than the one shown (a late answer) is ignored.</summary>
    public void SetRecordRequirements(string templateReference, IReadOnlyList<RecordRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        if (SelectedTemplate is null || !string.Equals(SelectedTemplate.Reference, templateReference, StringComparison.Ordinal))
            return;

        _requirements = requirements;
        _requirementsRows.Children.Clear();
        _requirementsPanel.IsVisible = requirements.Count > 0;

        if (requirements.Count == 0)
            return;

        var required = requirements.Where(r => r.Need == RecordNeed.Required).ToList();
        var missingRequired = required.Where(r => r.IsMissing).ToList();
        var others = requirements.Where(r => r.Need != RecordNeed.Required && r.IsMissing).OrderBy(r => r.Need).ToList();

        if (missingRequired.Count > 0)
        {
            _requirementsSummary.Text = missingRequired.Count == 1
                ? "1 required record is missing: this calculation cannot be completed without it."
                : $"{missingRequired.Count} required records are missing: this calculation cannot be completed without them.";
            _requirementsSummary.Foreground = Brushes.DarkOrange;
        }
        else if (required.Count > 0)
        {
            _requirementsSummary.Text = $"All {required.Count} required record(s) are entered.";
            _requirementsSummary.Foreground = Brushes.SeaGreen;
        }
        else
        {
            _requirementsSummary.Text = others.Count > 0 ? "No record is required; the defaults apply until you enter the ones below." : "Every record this calculation draws on is entered.";
            _requirementsSummary.Foreground = null;
        }

        foreach (var requirement in missingRequired)
            _requirementsRows.Children.Add(Row(requirement, bold: true));

        foreach (var requirement in others)
            _requirementsRows.Children.Add(Row(requirement, bold: false));

        if (_requirementsRows.Children.Count > 0 || required.Count > 0)
        {
            var open = new Button { Content = "Open the records screen", MinHeight = DesignTokens.MinControlSize, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, 0) };
            AutomationProperties.SetName(open, "Open the records screen from the required records");
            open.Click += (_, _) => OpenRecordsRequested?.Invoke(SuggestedRecordKind());
            _requirementsRows.Children.Add(open);
        }
    }

    private Control Row(RecordRequirement requirement, bool bold)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 1) };
        var label = requirement.Need switch
        {
            RecordNeed.Required => "Required",
            RecordNeed.Conditional => "Needed in some cases",
            RecordNeed.Recommended => "Recommended",
            _ => "Optional",
        };
        var text = new TextBlock
        {
            Text = $"{requirement.Title} ({string.Join(", ", requirement.Symbols)}) · {label} · not entered. {requirement.Reason}",
            FontSize = bold ? DesignTokens.FontSizeBody : DesignTokens.FontSizeCaption,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = bold ? 1 : 0.85,
        };
        var fill = new Button { Content = "Fill", MinHeight = DesignTokens.MinControlSize, Margin = new Thickness(DesignTokens.SpaceMd, 0, 0, 0) };
        AutomationProperties.SetName(fill, FillRecordName(requirement.Title));
        var kind = requirement.Kind;
        fill.Click += (_, _) => OpenRecordsRequested?.Invoke(kind);
        Grid.SetColumn(fill, 1);
        row.Children.Add(text);
        row.Children.Add(fill);
        return row;
    }
}
