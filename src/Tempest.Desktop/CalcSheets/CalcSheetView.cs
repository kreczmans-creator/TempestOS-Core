using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Calc.Engine;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// `v0.26.0` (ADR-0165, §3.8a): the one form every worksheet calculator is
/// entered through. Left: the catalogue (discipline → template, with
/// version and hash). Right: the generated inputs table (value, unit picker,
/// provenance, tooltip with symbol and description, inline validation), the
/// results panel headed by the governing check's PASS/FAIL bar, "Show
/// working" revealing formula → substitution → result exactly as printed,
/// applicability notices, and the lifecycle actions. Every input change
/// recalculates at once; while a recalculation is pending the results carry
/// a visible stale marker. An issued instance is read-only under an ISSUED
/// watermark. The view raises events and renders what it is given; the
/// coordinator talks to the service.
/// </summary>
public sealed partial class CalcSheetView : UserControl
{
    public const string Heading = "Calculation worksheets";
    public const string CatalogueAutomationName = "Worksheet catalogue";
    public const string SaveCaption = "Save as calculation";
    public const string CheckCaption = "Record check";
    public const string IssueCaption = "Issue";
    public const string ReviseCaption = "Revise";
    public const string ShowWorkingCaption = "Show working";
    public const string ExportCsvCaption = "Export inputs (CSV)";
    public const string ImportCaption = "Import inputs (CSV/JSON)";
    public const string ImportCadCaption = "Import CAD parameters…";
    public const string ImportScanCaption = "Import scan deviations…";
    public const string AttachEvidenceCaption = "Attach external report…";
    public const string CompareCaption = "Compare with previous revision";
    public const string ComparisonName = "Comparison";
    public const string ArchivesNodeName = "Archived legacy calculations";

    /// <summary>Archived legacy records listed in the catalogue; the rest are counted (§3.8 caps).</summary>
    public const int MaxArchivesListed = 500;
    public const string StaleMarker = "Recalculating…";
    public const string PickGuidance = "Choose a worksheet from the catalogue on the left. Its inputs, with their units, sources and limits, appear here.";

    private static readonly string[] ProvenanceKinds = ["manual", "drawing", "spec", "assumption", "evidence", "reference", "link"];

    public const string LinkCaption = "Link input";
    public const string LinkPanelName = "Link an input to another calculation";
    public const string StaleBannerName = "Stale marker";
    public const string UpdateLinksName = "Update linked inputs";

    public const string RecordPanelName = "Reference records";
    public const string ApplyRecordCaption = "Apply record";
    public const string SaveRecordCaption = "Save inputs as record";

    /// <summary>A reference record the shown draft can take values from: its id and how it is listed (citation and revision).</summary>
    public sealed record RecordChoice(Guid Id, string Label);

    /// <summary>What the user typed to save the form's tabulated inputs as a reference record (Addendum A, §A3).</summary>
    public sealed record RecordDraft(string Kind, string Title, string SourceTitle, string Edition, string TableOrClause, string LicenceHolder, bool LicensedSource);

    // Addendum A, §A3: apply a reference record to the draft, or save the form's tabulated values as one.
    private readonly ComboBox _recordPick = new() { MinWidth = 320, PlaceholderText = "Record" };
    private readonly Button _applyRecord = new() { Content = ApplyRecordCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly ComboBox _recordKind = new() { MinWidth = 200, PlaceholderText = "Record kind" };
    private readonly TextBox _recordTitle = new() { MinWidth = 200, Watermark = "Title (e.g. 6206 ratings)", MaxLength = 200 };
    private readonly TextBox _recordSource = new() { MinWidth = 200, Watermark = "Source title", MaxLength = 200 };
    private readonly TextBox _recordEdition = new() { MinWidth = 140, Watermark = "Edition and amendment", MaxLength = 200 };
    private readonly TextBox _recordTable = new() { MinWidth = 140, Watermark = "Table or clause", MaxLength = 200 };
    private readonly TextBox _recordHolder = new() { MinWidth = 160, Watermark = "Licence holder", MaxLength = 200 };
    private readonly CheckBox _recordLicensed = new() { Content = "Licensed source (never exported)" };
    private readonly Button _saveRecord = new() { Content = SaveRecordCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly StackPanel _recordPanel = new() { Spacing = DesignTokens.SpaceXs, IsVisible = false };
    private IReadOnlyList<RecordChoice> _records = [];

    /// <summary>A calculation an input can be linked to: its id, how it is listed, and the outputs it offers.</summary>
    public sealed record LinkTarget(Guid Id, string Label, IReadOnlyList<CalcOutputPort> Outputs);

    // Addendum A, L1: link an input to another calculation's output; a stale banner with "Update to revision n".
    private readonly ComboBox _linkInput = new() { MinWidth = 140, PlaceholderText = "Input" };
    private readonly ComboBox _linkTarget = new() { MinWidth = 260, PlaceholderText = "Calculation" };
    private readonly ComboBox _linkOutput = new() { MinWidth = 160, PlaceholderText = "Output" };
    private readonly Button _linkButton = new() { Content = LinkCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly StackPanel _linkPanel = new() { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm, IsVisible = false };
    private readonly TextBlock _staleBanner = new() { Foreground = Brushes.DarkOrange, TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody, IsVisible = false };
    private readonly Button _updateLinksButton = new() { MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private IReadOnlyList<LinkTarget> _linkTargets = [];

    /// <summary>The link provenance each linked input was shown with, so saving the form keeps the pinned revision.</summary>
    private readonly Dictionary<string, CalcProvenance> _shownLinks = new(StringComparer.Ordinal);

    private readonly TreeView _catalogue = new() { MinHeight = 200 };
    private readonly TextBlock _title = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 4, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _watermark = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeTitle, Foreground = Brushes.IndianRed, IsVisible = false };
    private readonly TextBlock _reference = Caption(string.Empty);
    private readonly TextBlock _scope = Caption(PickGuidance);
    private readonly TextBox _nameBox = Input(string.Empty, "What to call this calculation");
    private readonly StackPanel _form = new() { Spacing = DesignTokens.SpaceSm };
    private readonly TextBlock _stale = new() { Text = StaleMarker, FontSize = DesignTokens.FontSizeCaption, Foreground = Brushes.DarkOrange, IsVisible = false };
    private readonly TextBlock _governing = new() { FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 2, TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _utilisation = new() { Minimum = 0, Maximum = 1.5, Height = 10, IsVisible = false };
    private readonly Button _governingLink = new() { Content = "Go to step", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly StackPanel _results = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _checks = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _notices = new() { Spacing = DesignTokens.SpaceXs };
    private readonly ToggleButton _showWorking = new() { Content = ShowWorkingCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly StackPanel _working = new() { Spacing = DesignTokens.SpaceXs, IsVisible = false };
    private readonly TextBlock _problems = new() { FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, MaxWidth = 640, Foreground = Brushes.IndianRed };
    private readonly TextBlock _status = Caption(string.Empty);
    private readonly Button _saveButton = new() { Content = SaveCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _checkButton = new() { Content = CheckCaption, MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _issueButton = new() { Content = IssueCaption, MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _reviseButton = new() { Content = ReviseCaption, MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _exportButton = new() { Content = ExportCsvCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _importButton = new() { Content = ImportCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _importCadButton = new() { Content = ImportCadCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _importScanButton = new() { Content = ImportScanCaption, MinHeight = DesignTokens.MinControlSize, IsEnabled = false };
    private readonly Button _attachButton = new() { Content = AttachEvidenceCaption, MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _compareButton = new() { Content = CompareCaption, MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly SelectableTextBlock _comparison = new() { FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeCaption + 1, TextWrapping = TextWrapping.NoWrap, IsVisible = false };
    private IReadOnlyList<CalcInstance> _archives = [];

    // ADR-0158, D-41: the template's reference diagram beside the inputs, read against the form as it stands.
    private readonly Tempest.Desktop.Views.CalculationDiagramView _diagram = new() { Width = Tempest.Desktop.Views.CalculationDiagramView.PanelWidth, VerticalAlignment = VerticalAlignment.Top, IsVisible = false };
    private string? _markedSymbol;

    /// <summary>Below this width of the right column the diagram stacks above the inputs (a laptop window): label, value, unit, source and reference columns, plus the panel.</summary>
    public const double DiagramBesideMinimumWidth = 200 + 160 + 90 + 120 + 150 + DesignTokens.SpaceLg + Tempest.Desktop.Views.CalculationDiagramView.PanelWidth;
    /// <summary>Inputs the form has no field for (tables, vectors): carried from the seed so an imported or saved set keeps them (§3.8).</summary>
    private readonly List<CalcInputValue> _carriedInputs = [];
    private readonly Dictionary<string, FieldControls> _fields = new(StringComparer.Ordinal);
    /// <summary>One picker per reference library (or slot) the template draws on; items are the released records' labels (ADR-0167 §3).</summary>
    private readonly Dictionary<string, ComboBox> _referencePickers = new(StringComparer.Ordinal);
    /// <summary>The labels last listed per library for <see cref="_referenceLabelsFor"/>: a rebuilt form (an import, a save, a revision) shows them again instead of "Loading…".</summary>
    private readonly Dictionary<string, IReadOnlyList<string>> _referenceLabels = new(StringComparer.Ordinal);
    private string? _referenceLabelsFor;
    private readonly StackPanel _cadHost = new();
    private readonly StackPanel _scanHost = new();
    private readonly Dictionary<string, StackPanel> _stepRows = new(StringComparer.Ordinal);
    private bool _loadingForm;
    private string? _governingStep;

    private sealed record FieldControls(CalcInputSpec Spec, TextBox? Text, ComboBox? Unit, ComboBox? Choice, CheckBox? Flag, ComboBox Provenance, TextBox ProvenanceRef, TextBlock Problem, Grid Row);

    public CalcSheetView()
    {
        AutomationProperties.SetName(this, Heading);
        AutomationProperties.SetName(_catalogue, CatalogueAutomationName);
        AutomationProperties.SetName(_nameBox, "Calculation name");
        AutomationProperties.SetName(_governing, "Governing check");
        AutomationProperties.SetLiveSetting(_governing, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_stale, "Stale marker");
        AutomationProperties.SetName(_watermark, "Status watermark");
        AutomationProperties.SetName(_problems, "Input problems");
        AutomationProperties.SetLiveSetting(_problems, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_status, "Worksheet status");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_saveButton, SaveCaption);
        AutomationProperties.SetName(_checkButton, CheckCaption);
        AutomationProperties.SetName(_issueButton, IssueCaption);
        AutomationProperties.SetName(_reviseButton, ReviseCaption);
        AutomationProperties.SetName(_showWorking, ShowWorkingCaption);
        AutomationProperties.SetName(_exportButton, ExportCsvCaption);
        AutomationProperties.SetName(_importButton, ImportCaption);
        AutomationProperties.SetName(_importCadButton, ImportCadCaption);
        AutomationProperties.SetName(_importScanButton, ImportScanCaption);
        AutomationProperties.SetName(_compareButton, CompareCaption);
        AutomationProperties.SetName(_governingLink, "Go to the governing step");
        AutomationProperties.SetName(_comparison, ComparisonName);
        AutomationProperties.SetName(_working, "Working");
        AutomationProperties.SetName(_results, "Results");

        _catalogue.SelectionChanged += (_, _) => OnCatalogueSelectionChanged();
        _saveButton.Click += (_, _) => SaveRequested?.Invoke();
        _checkButton.Click += (_, _) => CheckRequested?.Invoke();
        _issueButton.Click += (_, _) => IssueRequested?.Invoke();
        _reviseButton.Click += (_, _) => ReviseRequested?.Invoke();
        _exportButton.Click += (_, _) => ExportRequested?.Invoke();
        _importButton.Click += (_, _) => ImportRequested?.Invoke();
        _importCadButton.Click += (_, _) => ImportCadRequested?.Invoke();
        _importScanButton.Click += (_, _) => ImportScanRequested?.Invoke();
        AutomationProperties.SetName(_attachButton, AttachEvidenceCaption);
        _attachButton.Click += (_, _) => AttachEvidenceRequested?.Invoke();
        _compareButton.Click += (_, _) => CompareRequested?.Invoke();
        _showWorking.IsCheckedChanged += (_, _) => _working.IsVisible = _showWorking.IsChecked == true;
        _governingLink.Click += (_, _) => RevealGoverningStep();
        _diagram.InputActivated += FocusField;
        _diagram.InputHovered += MarkRow;
        ActualThemeVariantChanged += (_, _) => MarkRow(_markedSymbol);

        AutomationProperties.SetName(_linkPanel, LinkPanelName);
        AutomationProperties.SetName(_linkInput, "Input to link");
        AutomationProperties.SetName(_linkTarget, "Calculation to link to");
        AutomationProperties.SetName(_linkOutput, "Output to link to");
        AutomationProperties.SetName(_linkButton, LinkCaption);
        AutomationProperties.SetName(_staleBanner, StaleBannerName);
        AutomationProperties.SetLiveSetting(_staleBanner, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(_updateLinksButton, UpdateLinksName);
        _linkTarget.SelectionChanged += (_, _) =>
        {
            _linkOutput.ItemsSource = _linkTarget.SelectedIndex >= 0 ? _linkTargets[_linkTarget.SelectedIndex].Outputs.Select(o => $"{o.Symbol} ({(o.Unit.Length == 0 ? "–" : o.Unit)}) — {o.Description}").ToList() : [];
            UpdateLinkButton();
        };
        _linkInput.SelectionChanged += (_, _) => UpdateLinkButton();
        _linkOutput.SelectionChanged += (_, _) => UpdateLinkButton();
        _linkButton.Click += (_, _) =>
        {
            if (_linkInput.SelectedItem is string input && _linkTarget.SelectedIndex >= 0 && _linkOutput.SelectedIndex >= 0)
            {
                var target = _linkTargets[_linkTarget.SelectedIndex];
                LinkRequested?.Invoke(input, target.Id, target.Outputs[_linkOutput.SelectedIndex].Symbol);
            }
        };
        _updateLinksButton.Click += (_, _) => UpdateLinksRequested?.Invoke();

        AutomationProperties.SetName(_recordPanel, RecordPanelName);
        AutomationProperties.SetName(_recordPick, "Record to apply");
        AutomationProperties.SetName(_applyRecord, ApplyRecordCaption);
        AutomationProperties.SetName(_recordKind, "Kind of record to save");
        AutomationProperties.SetName(_recordTitle, "Record title");
        AutomationProperties.SetName(_recordSource, "Record source title");
        AutomationProperties.SetName(_recordEdition, "Record edition and amendment");
        AutomationProperties.SetName(_recordTable, "Record table or clause");
        AutomationProperties.SetName(_recordHolder, "Record licence holder");
        AutomationProperties.SetName(_recordLicensed, "Record from a licensed source");
        AutomationProperties.SetName(_saveRecord, SaveRecordCaption);
        _recordPick.SelectionChanged += (_, _) => UpdateRecordButtons();
        _recordKind.SelectionChanged += (_, _) => UpdateRecordButtons();

        foreach (var box in new[] { _recordTitle, _recordSource, _recordEdition, _recordTable, _recordHolder })
            box.TextChanged += (_, _) => UpdateRecordButtons();

        _applyRecord.Click += (_, _) =>
        {
            if (_recordPick.SelectedIndex >= 0)
                ApplyRecordRequested?.Invoke(_records[_recordPick.SelectedIndex].Id);
        };
        _saveRecord.Click += (_, _) =>
        {
            if (DraftRecord() is { } draft)
                SaveRecordRequested?.Invoke(draft);
        };

        WireSavedAndPacks();
        WireGovernance();
        Content = BuildLayout();
    }

    public event Action<CalcCatalogueEntry>? TemplateSelected;

    /// <summary>Raised on every input change with the current form; the coordinator answers with <see cref="ShowRun"/>.</summary>
    public event Action<CalcInputSet>? InputsChanged;

    public event Action? SaveRequested;

    public event Action? CheckRequested;

    public event Action? IssueRequested;

    public event Action? ReviseRequested;

    public event Action? ExportRequested;

    /// <summary>The user asked to file an external report (a PROFIS PDF) on the calculation's Evidence.</summary>
    public event Action? AttachEvidenceRequested;

    /// <summary>The text input that cites an external report (a template's input whose symbol ends in "_ref"), or <see langword="null"/>.</summary>
    public string? ExternalReportInput => SelectedTemplate?.Template.Inputs.FirstOrDefault(i => i.Kind == CalcInputKind.Text && i.Symbol.EndsWith("_ref", StringComparison.Ordinal))?.Symbol;

    /// <summary>The user asked to import inputs (CSV or JSON) from the clipboard.</summary>
    public event Action? ImportRequested;

    /// <summary>The user asked to import CAD parameters into the shown draft (Addendum B, W1).</summary>
    public event Action? ImportCadRequested;

    /// <summary>The user asked to import scan deviations into the shown draft (Addendum B, W2).</summary>
    public event Action? ImportScanRequested;

    /// <summary>The panel that previews and applies a scan deviation import; the coordinator gives it its view model.</summary>
    public ScanImportPanel? ScanPanel { get; private set; }

    /// <summary>Installs the scan import panel under the CAD import panel. Done once, by the coordinator.</summary>
    public void InstallScanImportPanel(ScanImportPanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        if (ScanPanel is not null)
            return;

        ScanPanel = panel;
        _scanHost.Children.Add(panel);
    }

    /// <summary>Whether the scan import action is available: a saved draft is shown.</summary>
    public bool CanImportScan => _importScanButton.IsEnabled;

    /// <summary>The panel that previews and applies a CAD parameter import; the coordinator gives it its view model.</summary>
    public CadImportPanel? CadPanel { get; private set; }

    /// <summary>Installs the CAD import panel under the records panel. Done once, by the coordinator.</summary>
    public void InstallCadImportPanel(CadImportPanel panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        if (CadPanel is not null)
            return;

        CadPanel = panel;
        _cadHost.Children.Add(panel);
    }

    /// <summary>Whether the CAD import action is available: a saved draft is shown.</summary>
    public bool CanImportCad => _importCadButton.IsEnabled;

    /// <summary>Link an input (symbol) to another calculation's output (id, output symbol).</summary>
    public event Action<string, Guid, string>? LinkRequested;

    /// <summary>"Update to revision n": re-pin every link to the current upstream revision.</summary>
    public event Action? UpdateLinksRequested;

    /// <summary>Apply the chosen reference record (its id) to the draft.</summary>
    public event Action<Guid>? ApplyRecordRequested;

    /// <summary>Save the form's tabulated inputs of one record kind as a new reference record.</summary>
    public event Action<RecordDraft>? SaveRecordRequested;

    /// <summary>The reference records the shown draft can draw on (the coordinator lists them); the panel shows only when the template declares record kinds.</summary>
    public void SetRecords(IReadOnlyList<RecordChoice> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        _records = records;
        _recordPick.ItemsSource = records.Select(r => r.Label).ToList();
        _recordKind.ItemsSource = SelectedTemplate?.Template.RecordKinds?.Select(k => $"{k.Title} ({string.Join(", ", k.Symbols)})").ToList() ?? [];
        _recordPanel.IsVisible = CurrentInstance is { Status: CalcInstanceStatus.Draft } && SelectedTemplate?.Template.RecordKinds is { Count: > 0 };
        UpdateRecordButtons();
    }

    /// <summary>Whether the records panel is shown.</summary>
    public bool IsRecordPanelShown => _recordPanel.IsVisible;

    /// <summary>The records listed, as shown.</summary>
    public IReadOnlyList<string> RecordLabels => _records.Select(r => r.Label).ToList();

    /// <summary>Picks a record to apply, as the picker would.</summary>
    public void ChooseRecord(string label) => _recordPick.SelectedIndex = _records.ToList().FindIndex(r => r.Label == label);

    /// <summary>Fills the save-as-record fields, as typing would (the kind by its index in the template's record kinds).</summary>
    public void FillRecordDraft(int kindIndex, string title, string sourceTitle, string edition, string tableOrClause, string licenceHolder, bool licensed)
    {
        _recordKind.SelectedIndex = kindIndex;
        _recordTitle.Text = title;
        _recordSource.Text = sourceTitle;
        _recordEdition.Text = edition;
        _recordTable.Text = tableOrClause;
        _recordHolder.Text = licenceHolder;
        _recordLicensed.IsChecked = licensed;
        UpdateRecordButtons();
    }

    private RecordDraft? DraftRecord()
    {
        var kinds = SelectedTemplate?.Template.RecordKinds ?? [];

        if (_recordKind.SelectedIndex < 0 || _recordKind.SelectedIndex >= kinds.Count)
            return null;

        var fields = new[] { _recordTitle.Text, _recordSource.Text, _recordEdition.Text, _recordTable.Text, _recordHolder.Text };
        return fields.Any(string.IsNullOrWhiteSpace)
            ? null
            : new RecordDraft(kinds[_recordKind.SelectedIndex].Kind, fields[0]!.Trim(), fields[1]!.Trim(), fields[2]!.Trim(), fields[3]!.Trim(), fields[4]!.Trim(), _recordLicensed.IsChecked == true);
    }

    private void UpdateRecordButtons()
    {
        var draft = CurrentInstance is { Status: CalcInstanceStatus.Draft };
        _applyRecord.IsEnabled = draft && _recordPick.SelectedIndex >= 0;
        _saveRecord.IsEnabled = draft && DraftRecord() is not null;
    }

    /// <summary>The calculations the shown draft can link an input to (the coordinator lists them).</summary>
    public void SetLinkTargets(IReadOnlyList<LinkTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        _linkTargets = targets;
        _linkTarget.ItemsSource = targets.Select(t => t.Label).ToList();
        _linkOutput.ItemsSource = Array.Empty<string>();
        _linkInput.ItemsSource = SelectedTemplate?.Template.Inputs.Where(i => i.Kind is CalcInputKind.Quantity or CalcInputKind.Number).Select(i => i.Symbol).ToList() ?? [];
        _linkPanel.IsVisible = CurrentInstance is { Status: CalcInstanceStatus.Draft } && targets.Count > 0;
        UpdateLinkButton();
    }

    /// <summary>Shows whether the calculation is stale and, when updating its links would cure it, the update action.</summary>
    public void ShowStaleness(CalcStaleness staleness, string? updateCaption)
    {
        ArgumentNullException.ThrowIfNull(staleness);
        _staleBanner.Text = staleness.IsStale ? "Stale — " + string.Join(" ", staleness.Reasons) : string.Empty;
        _staleBanner.IsVisible = staleness.IsStale;
        _updateLinksButton.Content = updateCaption;
        _updateLinksButton.IsVisible = staleness.IsStale && updateCaption is not null && CurrentInstance is { Status: not CalcInstanceStatus.Superseded };
    }

    /// <summary>The stale text shown, or empty.</summary>
    public string StaleText => _staleBanner.IsVisible ? _staleBanner.Text ?? string.Empty : string.Empty;

    /// <summary>Whether the update action is offered, and its caption.</summary>
    public string? UpdateLinksCaption => _updateLinksButton.IsVisible ? _updateLinksButton.Content as string : null;

    /// <summary>Picks a link as the panel's three pickers would (input, calculation label, output symbol).</summary>
    public void ChooseLink(string input, string targetLabel, string outputSymbol)
    {
        _linkInput.SelectedItem = input;
        _linkTarget.SelectedIndex = _linkTargets.ToList().FindIndex(t => t.Label == targetLabel);
        _linkOutput.SelectedIndex = _linkTarget.SelectedIndex < 0 ? -1 : _linkTargets[_linkTarget.SelectedIndex].Outputs.ToList().FindIndex(o => o.Symbol == outputSymbol);
    }

    private void UpdateLinkButton() =>
        _linkButton.IsEnabled = _linkInput.SelectedItem is string && _linkTarget.SelectedIndex >= 0 && _linkOutput.SelectedIndex >= 0 && CurrentInstance is { Status: CalcInstanceStatus.Draft };

    /// <summary>Raised by Compare with previous revision (ADR-0167 §4).</summary>
    public event Action? CompareRequested;

    /// <summary>Raised when an archived legacy record is picked in the catalogue; carries its instance Id.</summary>
    public event Action<Guid>? ArchiveSelected;

    /// <summary>The comparison last shown, or null.</summary>
    public CalcComparison? LastComparison { get; private set; }

    /// <summary>The archived legacy records the catalogue lists.</summary>
    public IReadOnlyList<CalcInstance> Archives => _archives;

    /// <summary>Lists the migrated legacy records under their own catalogue node, newest first (Y8-06).</summary>
    public void ShowArchives(IReadOnlyList<CalcInstance> archives)
    {
        ArgumentNullException.ThrowIfNull(archives);
        _archives = [.. archives.Where(a => a.IsLegacyArchive).OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Name, StringComparer.Ordinal)];
        ShowCatalogue(Catalogue);
    }

    /// <summary>Shows two revisions side by side as text: every input, output and check that differs.</summary>
    public void ShowComparison(CalcComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(comparison);
        LastComparison = comparison;
        _comparison.Text = comparison.Text;
        _comparison.IsVisible = true;
    }

    private void ClearComparison()
    {
        LastComparison = null;
        _comparison.Text = string.Empty;
        _comparison.IsVisible = false;
    }

    /// <summary>The text of the comparison shown, or empty.</summary>
    public string ComparisonText => _comparison.IsVisible ? _comparison.Text ?? string.Empty : string.Empty;

    /// <summary>The user picked a released reference record for a library (or slot): the library key and the index into the options last set by <see cref="SetReferenceOptions"/>.</summary>
    public event Action<string, int>? ReferencePicked;

    public static string ReferencePickerName(string library) => $"{library} record";

    /// <summary>The libraries the shown template draws on, in input order; empty when none.</summary>
    public IReadOnlyList<string> ReferenceLibraries => _referencePickers.Keys.ToList();

    /// <summary>Fills a library's picker with the released records' labels; the selection is cleared.</summary>
    public void SetReferenceOptions(string library, IReadOnlyList<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        if (!_referencePickers.TryGetValue(library, out var picker))
            return;

        _referenceLabels[library] = labels;
        _referenceLabelsFor = SelectedTemplate?.Reference;
        ShowOptions(picker, labels);
    }

    private void ShowOptions(ComboBox picker, IReadOnlyList<string> labels)
    {
        var loading = _loadingForm;
        _loadingForm = true;
        picker.ItemsSource = labels;
        picker.SelectedIndex = -1;
        picker.IsEnabled = labels.Count > 0 && !IsReadOnly;
        picker.PlaceholderText = labels.Count == 0 ? "No released record" : $"Pick a released record ({labels.Count})";
        _loadingForm = loading;
    }

    /// <summary>Writes the values a reference record supplied into their fields, with the record's pin as the source.</summary>
    public void ApplyReferenceFill(IReadOnlyList<CalcInputValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (IsReadOnly)
            return;

        foreach (var value in values)
        {
            if (value.Unit is { } unit)
                SetField(value.Symbol, unit: unit);

            SetField(value.Symbol,
                text: value.Number is { } n ? n.ToString("R", CultureInfo.InvariantCulture) : null,
                choice: value.Text,
                provenance: ProvenanceLabel(CalcProvenanceKind.Reference),
                reference: value.Provenance?.Reference ?? string.Empty);
        }
    }

    public CalcCatalogueEntry? SelectedTemplate { get; private set; }

    public CalcInstance? CurrentInstance { get; private set; }

    public CalcRunResult? LastRun { get; private set; }

    public IReadOnlyList<CalcCatalogueEntry> Catalogue { get; private set; } = [];

    public string? CalculationName => string.IsNullOrWhiteSpace(_nameBox.Text) ? null : _nameBox.Text;

    /// <summary>Fills the name of a new, unsaved sheet (a draft carried onto a revised template is offered under a name of its own, since the old draft keeps its name).</summary>
    public void SuggestName(string name) => _nameBox.Text = name;

    public bool IsReadOnly { get; private set; }

    public bool IsWorkingShown => _working.IsVisible;

    public bool IsStale => _stale.IsVisible;

    /// <summary>The worksheet discipline a Disciplines-tree category maps to, or <see langword="null"/> (Manufacturing has no worksheets yet).</summary>
    public static CalcDiscipline? DisciplineFor(string category) => category switch
    {
        "Mechanical" => CalcDiscipline.Mechanical,
        "Structural" => CalcDiscipline.Structural,
        "Electrical" => CalcDiscipline.Electrical,
        "Building Services" => CalcDiscipline.BuildingServices,
        "Fluid Power" => CalcDiscipline.FluidPower,
        _ => null,
    };

    /// <summary>The Disciplines-tree and command category a worksheet discipline files under: the inverse of <see cref="DisciplineFor"/>.</summary>
    public static string CategoryFor(CalcDiscipline discipline) => discipline switch
    {
        CalcDiscipline.BuildingServices => "Building Services",
        CalcDiscipline.FluidPower => "Fluid Power",
        var d => d.ToString(),
    };

    private CalcDiscipline? _filter;

    /// <summary>Shows only one discipline's worksheets (a discipline's own Worksheets node), or every worksheet.</summary>
    public void FilterDiscipline(CalcDiscipline? discipline)
    {
        _filter = discipline;
        ShowCatalogue(Catalogue);
    }

    public void ShowCatalogue(IReadOnlyList<CalcCatalogueEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Catalogue = entries;
        _catalogue.Items.Clear();

        foreach (var group in entries.Where(e => _filter is null || e.Template.Discipline == _filter).GroupBy(e => e.Template.Discipline).OrderBy(g => g.Key))
        {
            var node = new TreeViewItem { Header = DisciplineLabel(group.Key), IsExpanded = true };
            AutomationProperties.SetName(node, DisciplineLabel(group.Key));

            foreach (var entry in group.OrderBy(e => e.Template.Title, StringComparer.Ordinal))
            {
                var leaf = new TreeViewItem { Header = $"{entry.Template.Title}  ·  v{entry.Template.Version} {entry.ShortHash[..8]}", Tag = entry };
                AutomationProperties.SetName(leaf, entry.Template.Title);
                ToolTip.SetTip(leaf, $"{entry.Reference} — hash {entry.ShortHash}\n{entry.Template.Scope}");
                node.Items.Add(leaf);
            }

            _catalogue.Items.Add(node);
        }

        if (_archives.Count > 0)
        {
            var node = new TreeViewItem { Header = $"{ArchivesNodeName} ({_archives.Count})", IsExpanded = false };
            AutomationProperties.SetName(node, ArchivesNodeName);

            foreach (var archive in _archives.Take(MaxArchivesListed))
            {
                var leaf = new TreeViewItem { Header = $"{archive.Name}  ·  {archive.CreatedAt:yyyy-MM-dd}", Tag = archive };
                AutomationProperties.SetName(leaf, archive.Name);
                ToolTip.SetTip(leaf, $"{archive.TemplateReference}\nRead-only archive of a legacy calculation record (ADR-0167).");
                node.Items.Add(leaf);
            }

            if (_archives.Count > MaxArchivesListed)
                node.Items.Add(new TreeViewItem { Header = $"… and {_archives.Count - MaxArchivesListed} more", IsEnabled = false });

            _catalogue.Items.Add(node);
        }

        if (entries.Count == 0)
            _scope.Text = "No worksheets are registered in this build.";
    }

    public void SelectTemplate(string reference)
    {
        foreach (var node in _catalogue.Items.OfType<TreeViewItem>())
        {
            foreach (var leaf in node.Items.OfType<TreeViewItem>())
            {
                if (leaf.Tag is CalcCatalogueEntry e && (e.Reference == reference || e.Template.Id == reference))
                {
                    _catalogue.SelectedItem = leaf;
                    return;
                }
            }
        }
    }

    /// <summary>Builds the form for a template, seeding it from an instance (its inputs) or the template's defaults.</summary>
    public void ShowTemplate(CalcCatalogueEntry entry, CalcInstance? instance = null) => ShowTemplate(entry, instance, null);

    /// <summary>Shows a template with the form seeded from <paramref name="seedOverride"/> (an import) instead of the instance or the template defaults.</summary>
    public void ShowTemplate(CalcCatalogueEntry entry, CalcInstance? instance, CalcInputSet? seedOverride)
    {
        ArgumentNullException.ThrowIfNull(entry);
        SelectedTemplate = entry;
        CurrentInstance = instance;
        IsReadOnly = instance is { IsImmutable: true };
        ClearComparison();
        _loadingForm = true;

        _title.Text = entry.Template.Title;
        _reference.Text = $"{entry.Reference}  ·  hash {entry.ShortHash}  ·  {entry.Template.ClauseRefs.Count} reference(s)";
        _scope.Text = entry.Template.Scope;
        _nameBox.Text = instance?.Name ?? string.Empty;
        _nameBox.IsEnabled = !IsReadOnly;
        _watermark.Text = instance is null ? string.Empty : instance.Status.ToString().ToUpperInvariant();
        _watermark.IsVisible = instance is not null;
        _watermark.Foreground = instance?.Status == CalcInstanceStatus.Issued ? Brushes.SeaGreen : instance?.Status == CalcInstanceStatus.Superseded ? Brushes.Gray : Brushes.DarkOrange;
        _form.Children.Clear();
        _fields.Clear();
        _referencePickers.Clear();

        if (_referenceLabelsFor != entry.Reference)
            _referenceLabels.Clear();

        _problems.Text = string.Empty;
        _status.Text = IsReadOnly ? $"This calculation is {instance!.Status}; it is read-only. Revise it to start a new revision." : string.Empty;

        var seed = seedOverride ?? (instance is null ? CalcTemplateHasher.NewSheetInputs(entry.Template) : new CalcInputSet(instance.Inputs));
        _shownLinks.Clear();

        foreach (var value in seed.Values.Where(v => v.Provenance is { Kind: CalcProvenanceKind.Link or CalcProvenanceKind.CadImport or CalcProvenanceKind.ScanImport }))
            _shownLinks[value.Symbol] = value.Provenance!;

        _staleBanner.IsVisible = false;
        _updateLinksButton.IsVisible = false;
        _linkPanel.IsVisible = false;
        _carriedInputs.Clear();
        _carriedInputs.AddRange(entry.Template.Inputs.Where(s => s.Kind is CalcInputKind.Table or CalcInputKind.Vector).Select(s => seed.Find(s.Symbol)).OfType<CalcInputValue>());
        string? lastGroup = null;

        foreach (var library in CalcReferenceFill.LibrariesOf(entry.Template))
        {
            var picker = new ComboBox { MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch, IsEnabled = false, PlaceholderText = "Loading released records…" };
            var key = library;
            picker.SelectionChanged += (_, _) =>
            {
                if (!_loadingForm && picker.SelectedIndex >= 0)
                    ReferencePicked?.Invoke(key, picker.SelectedIndex);
            };
            AutomationProperties.SetName(picker, ReferencePickerName(library));
            ToolTip.SetTip(picker, $"Fills {string.Join(", ", entry.Template.Inputs.Where(i => i.IsReferenceFillable && i.ReferenceLibrary == library).Select(i => i.Symbol))} from a released reference record, with the record's pin as the source.");
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*"), Margin = DesignTokens.ControlMargin };
            var label = new TextBlock { Text = CalcReferenceFill.PickerLabel(library), VerticalAlignment = VerticalAlignment.Center, FontSize = DesignTokens.FontSizeBody };
            Grid.SetColumn(picker, 1);
            row.Children.Add(label);
            row.Children.Add(picker);
            _form.Children.Add(row);
            _referencePickers[library] = picker;

            if (_referenceLabels.TryGetValue(library, out var known))
                ShowOptions(picker, known);
        }

        foreach (var spec in entry.Template.Inputs)
        {
            if (spec.Kind is CalcInputKind.Table or CalcInputKind.Vector)
                continue;

            if (spec.Group is { Length: > 0 } g && g != lastGroup)
            {
                _form.Children.Add(new TextBlock { Text = g, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody, Margin = DesignTokens.SectionMargin });
                lastGroup = g;
            }

            // A saved instance or an import states its inputs in full: an optional input it leaves out stays blank rather than
            // taking the template default, which would arrive unsourced and refuse the check (the showcase journey found it).
            var stated = seedOverride is not null || instance is not null;
            _form.Children.Add(BuildField(spec, seed.Find(spec.Symbol), blankWhenAbsent: stated && spec.IsOptional));
        }

        _loadingForm = false;
        var template = entry.Template;
        _diagram.IsVisible = true;
        _diagram.ShowReading(template.Title, () => SelectedTemplate is { } t ? CalcDiagramReader.Read(t.Template, ReadForm()) : null, symbol => template.FindInput(symbol)?.Description);
        UpdateActions();
        RaiseInputsChanged();
    }

    /// <summary>The reference diagram beside the inputs (ADR-0158, D-41).</summary>
    public Tempest.Desktop.Views.CalculationDiagramView Diagram => _diagram;

    /// <summary>The row of <paramref name="symbol"/>'s field, or <see langword="null"/>.</summary>
    public Control? FieldRow(string symbol) => _fields.TryGetValue(symbol, out var f) ? f.Row : null;

    /// <summary>Whether <paramref name="symbol"/>'s row is marked (the pointer is on one of its shapes).</summary>
    public bool IsRowMarked(string symbol) => _fields.TryGetValue(symbol, out var f) && f.Row.Background is not null;

    /// <summary>
    /// Marks the row of the input whose shape the pointer is on: the accent wash behind it and its label bold in the
    /// focus-ring colour (a wash alone is barely visible). Re-applied on a theme change.
    /// </summary>
    private void MarkRow(string? symbol)
    {
        _markedSymbol = symbol;
        var wash = ThemeReactiveBrush.Resolve(this, ApplicationPalette.AccentPanelBackgroundBrushKey) ?? Brushes.LightBlue;
        var ring = ThemeReactiveBrush.Resolve(this, ApplicationPalette.FocusRingBrushKey) ?? Brushes.RoyalBlue;

        foreach (var (key, field) in _fields)
        {
            var marked = key == symbol;
            field.Row.Background = marked ? wash : null;

            if (field.Row.Children.OfType<TextBlock>().FirstOrDefault() is not { } label)
                continue;

            if (marked)
            {
                label.Foreground = ring;
                label.FontWeight = FontWeight.Bold;
            }
            else
            {
                label.ClearValue(TextBlock.ForegroundProperty);
                label.FontWeight = FontWeight.Normal;
            }
        }
    }

    /// <summary>A shape on the diagram was clicked: its input's own control takes focus.</summary>
    private void FocusField(string symbol)
    {
        if (FieldControl(symbol) is { } control)
        {
            control.Focus();
            control.BringIntoView();
        }
    }

    /// <summary>Shows a run: governing bar, results, checks, notices, working. Clears the stale marker.</summary>
    public void ShowRun(CalcRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        LastRun = result;
        _stale.IsVisible = false;
        _results.Children.Clear();
        _checks.Children.Clear();
        _notices.Children.Clear();
        _working.Children.Clear();
        _stepRows.Clear();
        _governingStep = null;

        foreach (var notice in result.Notices.Where(n => n.RuleId.StartsWith("input.", StringComparison.Ordinal)))
        {
            var symbol = notice.RuleId["input.".Length..];
            var key = symbol.Contains('[', StringComparison.Ordinal) ? symbol[..symbol.IndexOf('[', StringComparison.Ordinal)] : symbol;

            if (_fields.TryGetValue(key, out var field))
            {
                field.Problem.Text = notice.Message;
                field.Problem.Foreground = notice.Severity == CalcNoticeSeverity.Block ? Brushes.IndianRed : Brushes.DarkOrange;
                field.Problem.IsVisible = true;
            }
        }

        if (result.Governing is { } governing)
        {
            _governing.Text = $"{(governing.Passed ? "PASS" : "FAIL")} — {governing.Name}: utilisation {FormatUtilisation(governing.Utilisation)}";
            _governing.Foreground = governing.Passed ? Brushes.SeaGreen : Brushes.IndianRed;
            _utilisation.Value = double.IsFinite(governing.Utilisation) ? Math.Min(governing.Utilisation, 1.5) : 1.5;
            _utilisation.Foreground = governing.Passed ? Brushes.SeaGreen : Brushes.IndianRed;
            _utilisation.IsVisible = true;
            _governingStep = governing.StepSymbol;
            _governingLink.IsVisible = governing.StepSymbol is not null;
        }
        else if (result.IsBlocked)
        {
            _governing.Text = "Blocked — " + result.Blocks[0].Message;
            _governing.Foreground = Brushes.IndianRed;
            _utilisation.IsVisible = false;
            _governingLink.IsVisible = false;
        }
        else
        {
            _governing.Text = result.Outputs.Count == 0 ? string.Empty : "No pass/fail check in this calculation.";
            _governing.Foreground = Brushes.Gray;
            _utilisation.IsVisible = false;
            _governingLink.IsVisible = false;
        }

        foreach (var o in result.Outputs)
            _results.Children.Add(Readout(o.Symbol, o.ValueText, o.Description));

        foreach (var c in result.Checks.OrderByDescending(c => c.Utilisation))
            _checks.Children.Add(CheckRow(c));

        foreach (var n in result.Blocks)
            _notices.Children.Add(Typeset(new TextBlock { Foreground = Brushes.IndianRed, TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody }, $"Block — {n.Message}"));

        foreach (var n in result.Warnings)
            _notices.Children.Add(Typeset(new TextBlock { Foreground = Brushes.DarkOrange, TextWrapping = TextWrapping.Wrap, FontSize = DesignTokens.FontSizeBody }, $"Warning — {n.Message}"));

        foreach (var s in result.Steps.Where(s => s.Kind != CalcStepKind.Input))
        {
            var row = new StackPanel { Spacing = 0, Margin = new Thickness(0, DesignTokens.SpaceXs) };
            row.Children.Add(Mono($"[{s.Ordinal}] {s.Symbol} = {s.Formula}{(s.Clause is { Length: > 0 } ? $"    « {s.Clause}" : string.Empty)}"));

            if (!string.Equals(s.Substitution, s.Formula, StringComparison.Ordinal))
                row.Children.Add(Mono($"      = {s.Substitution}"));

            row.Children.Add(Mono($"      = {s.ResultText}"));

            if (s.Description is { Length: > 0 })
                row.Children.Add(Typeset(Caption(string.Empty), "        " + s.Description));

            AutomationProperties.SetName(row, $"Step {s.Symbol}");
            _working.Children.Add(row);
            _stepRows[s.Symbol] = row;
        }

        _saveButton.IsEnabled = !IsReadOnly && SelectedTemplate is not null && !result.IsBlocked;
        _exportButton.IsEnabled = SelectedTemplate is not null;
        _importButton.IsEnabled = SelectedTemplate is not null && !IsReadOnly;
        _attachButton.IsVisible = ExternalReportInput is not null && CurrentInstance is { Status: CalcInstanceStatus.Draft };
        UpdateActions();
    }

    public void ShowInstance(CalcInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        CurrentInstance = instance;
        IsReadOnly = instance.IsImmutable;
        _nameBox.Text = instance.Name;
        _nameBox.IsEnabled = !IsReadOnly;
        _watermark.Text = instance.Status.ToString().ToUpperInvariant();
        _watermark.IsVisible = true;
        _watermark.Foreground = instance.Status == CalcInstanceStatus.Issued ? Brushes.SeaGreen : instance.Status == CalcInstanceStatus.Superseded ? Brushes.Gray : Brushes.DarkOrange;

        foreach (var f in _fields.Values)
            SetEnabled(f, !IsReadOnly);

        UpdateActions();
    }

    public void ShowStatus(string message) => _status.Text = message;

    /// <summary>The status line as shown.</summary>
    public string StatusText => _status.Text ?? string.Empty;

    public void ShowProblems(IEnumerable<string> problems) => _problems.Text = string.Join(Environment.NewLine, problems);

    /// <summary>Reads the form into an input set, every value as typed with its unit and provenance. Unparseable numbers become NaN so validation names them.</summary>
    public CalcInputSet ReadForm()
    {
        var set = new CalcInputSet();

        foreach (var (symbol, f) in _fields)
        {
            var provenance = ReadProvenance(f);

            // A linked input keeps its pin (upstream, output and revision) while its reference text is unchanged.
            if (provenance.Kind is CalcProvenanceKind.Link or CalcProvenanceKind.CadImport or CalcProvenanceKind.ScanImport && _shownLinks.TryGetValue(symbol, out var shown) && shown.Kind == provenance.Kind && string.Equals(shown.Reference, provenance.Reference, StringComparison.Ordinal))
                provenance = shown;
            else if (provenance.Kind is CalcProvenanceKind.CadImport or CalcProvenanceKind.ScanImport)
                provenance = CalcProvenance.Manual; // typed by hand, or the text was changed: not an import

            switch (f.Spec.Kind)
            {
                case CalcInputKind.Quantity:
                case CalcInputKind.Number:
                {
                    var text = f.Text!.Text?.Trim() ?? string.Empty;

                    if (text.Length == 0)
                    {
                        if (!f.Spec.IsOptional && f.Spec.Default is null)
                            set.Set(new CalcInputValue(symbol, Number: double.NaN, Unit: f.Unit?.SelectedItem as string, Provenance: provenance));

                        break;
                    }

                    var number = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : double.NaN;
                    set.Set(new CalcInputValue(symbol, Number: number, Unit: f.Unit?.SelectedItem as string, Provenance: provenance));
                    break;
                }

                case CalcInputKind.Choice:
                    set.Set(new CalcInputValue(symbol, Text: f.Choice!.SelectedItem as string, Provenance: provenance));
                    break;

                case CalcInputKind.Text:
                    set.Set(new CalcInputValue(symbol, Text: f.Text!.Text ?? string.Empty, Provenance: provenance));
                    break;

                case CalcInputKind.Boolean:
                    set.Set(new CalcInputValue(symbol, Flag: f.Flag!.IsChecked == true, Provenance: provenance));
                    break;
            }
        }

        foreach (var carried in _carriedInputs)
            set.Set(carried);

        return set;
    }

    /// <summary>Re-seeds the form from an imported input set (the current instance, if any, stays the one being edited).</summary>
    public void ApplyImportedInputs(CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (SelectedTemplate is not { } entry || IsReadOnly)
            return;

        ShowTemplate(entry, CurrentInstance, inputs);
    }

    /// <summary>Sets one field as a test or an import would: the value text, the unit, the choice or the flag.</summary>
    public void SetField(string symbol, string? text = null, string? unit = null, string? choice = null, bool? flag = null, string? provenance = null, string? reference = null)
    {
        if (!_fields.TryGetValue(symbol, out var f))
            return;

        _loadingForm = true;

        if (text is not null && f.Text is not null)
            f.Text.Text = text;

        if (choice is not null && f.Choice is not null)
            f.Choice.SelectedItem = choice;

        if (flag is not null && f.Flag is not null)
            f.Flag.IsChecked = flag;

        if (provenance is not null)
            f.Provenance.SelectedItem = provenance;

        if (reference is not null)
            f.ProvenanceRef.Text = reference;

        _loadingForm = false;

        // A unit change converts the value and raises the change itself.
        if (unit is not null && f.Unit is not null && !string.Equals(f.Unit.SelectedItem as string, unit, StringComparison.Ordinal))
        {
            f.Unit.SelectedItem = unit;
            return;
        }

        RaiseInputsChanged();
    }

    public Control? FieldControl(string symbol) => _fields.TryGetValue(symbol, out var f) ? (Control?)f.Text ?? f.Choice ?? (Control?)f.Flag : null;

    public ComboBox? UnitPicker(string symbol) => _fields.TryGetValue(symbol, out var f) ? f.Unit : null;

    public string? FieldProblem(string symbol) => _fields.TryGetValue(symbol, out var f) && f.Problem.IsVisible ? f.Problem.Text : null;

    public string GoverningText => _governing.Text ?? string.Empty;

    public IReadOnlyList<string> WorkingLines => _working.Children.OfType<StackPanel>().SelectMany(p => p.Children.OfType<TextBlock>()).Select(SymbolText.Raw).ToList();

    public void ToggleWorking(bool shown) => _showWorking.IsChecked = shown;

    // ------------------------------------------------------------------

    private void OnCatalogueSelectionChanged()
    {
        if (_catalogue.SelectedItem is TreeViewItem { Tag: CalcCatalogueEntry entry })
        {
            ShowTemplate(entry);
            TemplateSelected?.Invoke(entry);
        }
        else if (_catalogue.SelectedItem is TreeViewItem { Tag: CalcInstance archive })
        {
            ArchiveSelected?.Invoke(archive.Id);
        }
    }

    private void RaiseInputsChanged()
    {
        if (_loadingForm || SelectedTemplate is null)
            return;

        foreach (var f in _fields.Values)
            f.Problem.IsVisible = false;

        _stale.IsVisible = true;
        _diagram.Refresh();
        InputsChanged?.Invoke(ReadForm());
    }

    /// <summary>Automation name of the read-only text an archived legacy record shows.</summary>
    public const string ArchiveSheetName = "Archived legacy calculation";

    /// <summary>
    /// Shows a migrated legacy calculation record (ADR-0167) as its archive
    /// sheet: read-only, no form, no lifecycle actions. Its inputs can be
    /// re-entered in the matching worksheet template for a sheet with working.
    /// </summary>
    public void ShowArchive(CalcInstance instance, string sheetText)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(sheetText);

        _loadingForm = true;
        SelectedTemplate = null;
        CurrentInstance = instance;
        IsReadOnly = true;
        ClearComparison();
        _diagram.ShowReading(null, null);
        _diagram.IsVisible = false;
        LastRun = null;
        _title.Text = instance.Name;
        _reference.Text = instance.TemplateReference;
        _scope.Text = LegacyArchiveSheet.MethodText;
        _watermark.Text = "ARCHIVE";
        _watermark.IsVisible = true;
        _watermark.Foreground = Brushes.Gray;
        _form.Children.Clear();
        _fields.Clear();
        _referencePickers.Clear();
        _carriedInputs.Clear();
        _problems.Text = string.Empty;
        var text = new SelectableTextBlock { Text = sheetText, FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeCaption + 1, TextWrapping = TextWrapping.NoWrap };
        AutomationProperties.SetName(text, ArchiveSheetName);
        _form.Children.Add(new ScrollViewer { Content = text, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
        _status.Text = "A migrated legacy calculation record: read-only. Re-enter its inputs in the matching worksheet template for a sheet with working.";
        _exportButton.IsEnabled = false;
        _importButton.IsEnabled = false;
        _loadingForm = false;
        UpdateActions();
    }

    private void UpdateActions()
    {
        var instance = CurrentInstance;
        _saveButton.Content = instance is null ? SaveCaption : "Save changes";
        _saveButton.IsEnabled = !IsReadOnly && SelectedTemplate is not null && LastRun is { IsBlocked: false };
        _checkButton.IsVisible = instance is { Status: CalcInstanceStatus.Draft, IsLegacyArchive: false };
        _compareButton.IsVisible = instance is { PredecessorId: not null, IsLegacyArchive: false };
        _issueButton.IsVisible = instance is { Status: CalcInstanceStatus.Checked };
        _reviseButton.IsVisible = instance is { Status: CalcInstanceStatus.Issued };
        _importCadButton.IsEnabled = instance is { Status: CalcInstanceStatus.Draft };
        _importScanButton.IsEnabled = instance is { Status: CalcInstanceStatus.Draft };
        UpdateGovernance();
    }

    private void RevealGoverningStep()
    {
        if (_governingStep is null || !_stepRows.TryGetValue(_governingStep, out var row))
            return;

        _showWorking.IsChecked = true;
        row.Background = new SolidColorBrush(Color.FromArgb(40, 64, 162, 206));
        row.BringIntoView();
    }

    private Control BuildField(CalcInputSpec spec, CalcInputValue? seed, bool blankWhenAbsent = false)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("200,160,90,120,*") };
        var label = Typeset(new TextBlock { FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }, $"{spec.Symbol} — {spec.Description}");
        var tip = $"{spec.Symbol}: {spec.Description}\nValid range: {spec.RangeText}{(spec.IsOptional ? "\nOptional" : string.Empty)}";
        ToolTip.SetTip(label, tip);
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        TextBox? text = null;
        ComboBox? unit = null;
        ComboBox? choice = null;
        CheckBox? flag = null;

        switch (spec.Kind)
        {
            case CalcInputKind.Quantity:
            case CalcInputKind.Number:
            case CalcInputKind.Text:
            {
                var initial = seed?.Number is { } n ? n.ToString("R", CultureInfo.InvariantCulture) : seed?.Text ?? (!blankWhenAbsent && spec.Default is { } d ? d.ToString("R", CultureInfo.InvariantCulture) : string.Empty);
                text = Input(initial, spec.Kind == CalcInputKind.Text ? "text" : "number");
                text.TextChanged += (_, _) => RaiseInputsChanged();
                AutomationProperties.SetName(text, spec.Symbol);
                ToolTip.SetTip(text, tip);
                Grid.SetColumn(text, 1);
                row.Children.Add(text);

                if (spec.Kind == CalcInputKind.Quantity)
                {
                    unit = new ComboBox { MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch };
                    var units = UnitChoices(spec, seed?.Unit).ToList();

                    // A seed unit the engine cannot use (unknown text such as "kn", or a unit of another dimension) is kept visible as it came, so the run refuses it with the engine's own message; replacing it with the
                    // default unit would read its number in that unit and run (desktop.calc-sheet-view-02).
                    if (seed?.Unit is { Length: > 0 } seedUnit && !units.Contains(seedUnit))
                        units.Add(seedUnit);

                    unit.ItemsSource = units;
                    unit.SelectedItem = seed?.Unit is { } u && units.Contains(u) ? u : spec.DefaultUnit;
                    unit.Tag = unit.SelectedItem;
                    unit.SelectionChanged += (_, _) => OnUnitChanged(spec.Symbol);
                    AutomationProperties.SetName(unit, $"{spec.Symbol} unit");
                    Grid.SetColumn(unit, 2);
                    row.Children.Add(unit);
                }

                break;
            }

            case CalcInputKind.Choice:
            {
                choice = new ComboBox { MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = spec.Choices ?? [] };
                choice.SelectedItem = seed?.Text ?? spec.DefaultChoice;
                choice.SelectionChanged += (_, _) => RaiseInputsChanged();
                AutomationProperties.SetName(choice, spec.Symbol);
                ToolTip.SetTip(choice, tip);
                Grid.SetColumn(choice, 1);
                Grid.SetColumnSpan(choice, 2);
                row.Children.Add(choice);
                break;
            }

            case CalcInputKind.Boolean:
            {
                flag = new CheckBox { IsChecked = seed?.Flag ?? (spec.Default is { } d && d != 0), MinHeight = DesignTokens.MinControlSize };
                flag.IsCheckedChanged += (_, _) => RaiseInputsChanged();
                AutomationProperties.SetName(flag, spec.Symbol);
                ToolTip.SetTip(flag, tip);
                Grid.SetColumn(flag, 1);
                row.Children.Add(flag);
                break;
            }
        }

        // "cad import" and "scan import" are offered only for a value that was imported: they cannot be picked by hand. The service also checks them against the import record.
        var provenance = new ComboBox { MinHeight = DesignTokens.MinControlSize, ItemsSource = seed?.Provenance?.Kind switch { CalcProvenanceKind.CadImport => ProvenanceKinds.Append("cad import").ToArray(), CalcProvenanceKind.ScanImport => ProvenanceKinds.Append("scan import").ToArray(), _ => ProvenanceKinds }, HorizontalAlignment = HorizontalAlignment.Stretch };
        provenance.SelectedItem = ProvenanceLabel(seed?.Provenance?.Kind ?? CalcProvenanceKind.Manual);
        provenance.SelectionChanged += (_, _) => RaiseInputsChanged();
        AutomationProperties.SetName(provenance, $"{spec.Symbol} source");
        ToolTip.SetTip(provenance, "Where this value comes from. A source other than 'manual' is needed to issue.");
        Grid.SetColumn(provenance, 3);
        row.Children.Add(provenance);

        var reference = Input(seed?.Provenance?.Reference ?? string.Empty, "reference / revision");
        reference.TextChanged += (_, _) => RaiseInputsChanged();
        AutomationProperties.SetName(reference, $"{spec.Symbol} source reference");
        Grid.SetColumn(reference, 4);
        row.Children.Add(reference);

        var problem = new TextBlock { FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, IsVisible = false, Foreground = Brushes.DarkOrange };
        AutomationProperties.SetName(problem, $"{spec.Symbol} problem");
        var block = new StackPanel { Spacing = 0 };
        block.Children.Add(row);
        block.Children.Add(problem);

        foreach (var control in new Control?[] { text, unit, choice, flag }.OfType<Control>())
        {
            control.GotFocus += (_, _) => _diagram.Highlight(spec.Symbol);
            control.LostFocus += (_, _) => _diagram.Highlight(null);
        }

        var controls = new FieldControls(spec, text, unit, choice, flag, provenance, reference, problem, row);
        _fields[spec.Symbol] = controls;
        SetEnabled(controls, !IsReadOnly);
        return block;
    }

    /// <summary>A unit change converts the value so the physical quantity is unchanged (§3.8a: values convert on change; the engine stores SI).</summary>
    private void OnUnitChanged(string symbol)
    {
        if (_loadingForm || !_fields.TryGetValue(symbol, out var f) || f.Text is null || f.Unit is null)
            return;

        var previous = f.Unit.Tag as string;
        var next = f.Unit.SelectedItem as string;
        f.Unit.Tag = next;

        if (previous is null || next is null || previous == next)
        {
            RaiseInputsChanged();
            return;
        }

        if (double.TryParse(f.Text.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(previous, out var from, out _)
            && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(next, out var to, out _)
            && from.Dimension == to.Dimension)
        {
            _loadingForm = true;
            f.Text.Text = Tempest.Calc.Engine.Values.CalcValue.FormatNumber(to.FromSi(from.ToSi(value)), 8);
            _loadingForm = false;
        }

        RaiseInputsChanged();
    }

    /// <summary>
    /// The units offered for a quantity field: the template's unit, then the common units of the same dimension. A seeded unit that is valid for the
    /// dimension but not in the common list (N*mm, kN*mm, MW, daN) is offered too, so the number it came with is shown in its own unit and is never
    /// silently read in the default unit (desktop.calc-sheet-view-01).
    /// </summary>
    internal static IReadOnlyList<string> UnitChoices(CalcInputSpec spec, string? seedUnit = null)
    {
        var dimension = spec.Dimension;
        var candidates = new List<string> { spec.DefaultUnit };

        foreach (var symbol in CommonUnits)
        {
            if (symbol != spec.DefaultUnit && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(symbol, out var u, out _) && u.Dimension == dimension)
                candidates.Add(symbol);
        }

        if (seedUnit is { Length: > 0 } && !candidates.Contains(seedUnit) && Tempest.Calc.Engine.Units.UnitCatalog.TryParse(seedUnit, out var seeded, out _) && seeded.Dimension == dimension)
            candidates.Add(seedUnit);

        return candidates;
    }

    private static readonly string[] CommonUnits =
    [
        "mm", "cm", "m", "in", "ft", "µm", "mm^2", "cm^2", "m^2", "in^2", "mm^3", "m^3", "L", "mm^4", "cm^4", "m^4", "in^4", "mm^3", "cm^3",
        "N", "kN", "MN", "lbf", "kip", "Pa", "kPa", "MPa", "GPa", "bar", "psi", "ksi", "N/mm^2", "N*m", "kN*m", "lbf*ft", "kg", "g", "t", "lb",
        "s", "min", "h", "K", "degC", "degF", "W", "kW", "hp", "J", "kJ", "kWh", "A", "V", "kV", "Ω", "mΩ", "Hz", "rpm", "rad/s", "m/s", "mm/s", "m/s^2",
        "kg/m^3", "Pa*s", "mPa*s", "cP", "m^2/s", "cSt", "L/s", "L/min", "m^3/h", "m^3/s", "cfm", "gpm", "kg/s", "kg/h", "W/(m*K)", "W/(m^2*K)", "J/(kg*K)",
        "kg*m^2", "N/m", "kN/m", "N/mm", "deg", "rad", "%", "1/K", "mm/m", "Ω/km", "mV/A/m", "kJ/kg", "g/kg",
    ];

    private static CalcProvenance ReadProvenance(FieldControls f)
    {
        var kind = (f.Provenance.SelectedItem as string) switch
        {
            "drawing" => CalcProvenanceKind.Drawing,
            "spec" => CalcProvenanceKind.Specification,
            "assumption" => CalcProvenanceKind.Assumption,
            "evidence" => CalcProvenanceKind.Evidence,
            "reference" => CalcProvenanceKind.Reference,
            "link" => CalcProvenanceKind.Link,
            "cad import" => CalcProvenanceKind.CadImport,
            "scan import" => CalcProvenanceKind.ScanImport,
            _ => CalcProvenanceKind.Manual,
        };

        var reference = f.ProvenanceRef.Text?.Trim();
        return new CalcProvenance(kind, string.IsNullOrEmpty(reference) ? null : reference);
    }

    private static string ProvenanceLabel(CalcProvenanceKind kind) => kind switch
    {
        CalcProvenanceKind.Drawing => "drawing",
        CalcProvenanceKind.Specification => "spec",
        CalcProvenanceKind.Assumption => "assumption",
        CalcProvenanceKind.Evidence => "evidence",
        CalcProvenanceKind.Reference => "reference",
        CalcProvenanceKind.Link => "link",
        CalcProvenanceKind.CadImport => "cad import",
        CalcProvenanceKind.ScanImport => "scan import",
        _ => "manual",
    };

    private static void SetEnabled(FieldControls f, bool enabled)
    {
        if (f.Text is not null)
            f.Text.IsReadOnly = !enabled;

        if (f.Unit is not null)
            f.Unit.IsEnabled = enabled;

        if (f.Choice is not null)
            f.Choice.IsEnabled = enabled;

        if (f.Flag is not null)
            f.Flag.IsEnabled = enabled;

        f.Provenance.IsEnabled = enabled;
        f.ProvenanceRef.IsReadOnly = !enabled;
    }

    private static string FormatUtilisation(double u) => double.IsInfinity(u) ? "—" : Tempest.Calc.Engine.Values.CalcValue.FormatNumber(u, 3);

    private static string DisciplineLabel(CalcDiscipline d) => d switch
    {
        CalcDiscipline.BuildingServices => "Building Services / HVAC",
        CalcDiscipline.FluidPower => "Fluid Power",
        _ => d.ToString(),
    };

    private Control BuildLayout()
    {
        var left = new StackPanel { Spacing = DesignTokens.SpaceSm, Margin = DesignTokens.PanelPadding };
        left.Children.Add(new TextBlock { Text = Heading, FontFamily = DesignTokens.TitleFont, FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });
        left.Children.Add(_catalogue);
        left.Children.Add(BuildSavedPanel());
        left.Children.Add(BuildPacksPanel());

        var right = new StackPanel { Spacing = DesignTokens.SpaceSm, Margin = DesignTokens.PanelPadding };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceLg };
        head.Children.Add(_title);
        head.Children.Add(_watermark);
        right.Children.Add(head);
        right.Children.Add(_reference);
        right.Children.Add(_scope);
        right.Children.Add(LabelledRow("Calculation name", _nameBox));
        _linkPanel.Children.Add(new TextBlock { Text = "Link", VerticalAlignment = VerticalAlignment.Center, FontSize = DesignTokens.FontSizeBody });
        _linkPanel.Children.Add(_linkInput);
        _linkPanel.Children.Add(new TextBlock { Text = "to", VerticalAlignment = VerticalAlignment.Center, FontSize = DesignTokens.FontSizeBody });
        _linkPanel.Children.Add(_linkTarget);
        _linkPanel.Children.Add(_linkOutput);
        _linkPanel.Children.Add(_linkButton);
        var staleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        staleRow.Children.Add(_staleBanner);
        staleRow.Children.Add(_updateLinksButton);
        right.Children.Add(staleRow);
        // The diagram sits beside the inputs, or above them on a laptop window; the inputs stay first in the tab order.
        var inputsBeside = new Tempest.Desktop.Views.BesideOrAbovePanel(DiagramBesideMinimumWidth, DesignTokens.SpaceLg);
        inputsBeside.Children.Add(_form);
        inputsBeside.Children.Add(_diagram);
        right.Children.Add(Section("Inputs", inputsBeside));
        right.Children.Add(_linkPanel);
        var applyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        applyRow.Children.Add(new TextBlock { Text = "Take values from", VerticalAlignment = VerticalAlignment.Center, FontSize = DesignTokens.FontSizeBody });
        applyRow.Children.Add(_recordPick);
        applyRow.Children.Add(_applyRecord);
        var saveRow = new WrapPanel { Orientation = Orientation.Horizontal };

        foreach (var control in new Control[] { _recordKind, _recordTitle, _recordSource, _recordEdition, _recordTable, _recordHolder, _recordLicensed, _saveRecord })
        {
            control.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceXs);
            saveRow.Children.Add(control);
        }

        _recordPanel.Children.Add(applyRow);
        _recordPanel.Children.Add(saveRow);
        right.Children.Add(_recordPanel);
        right.Children.Add(_cadHost);
        right.Children.Add(_scanHost);
        right.Children.Add(_problems);

        var resultsHead = new StackPanel { Spacing = DesignTokens.SpaceXs };
        var governingRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceLg };
        governingRow.Children.Add(_governing);
        governingRow.Children.Add(_governingLink);
        governingRow.Children.Add(_stale);
        resultsHead.Children.Add(governingRow);
        resultsHead.Children.Add(_utilisation);
        resultsHead.Children.Add(_results);
        resultsHead.Children.Add(_checks);
        resultsHead.Children.Add(_notices);
        resultsHead.Children.Add(_showWorking);
        resultsHead.Children.Add(_working);
        right.Children.Add(Section("Results", resultsHead));

        right.Children.Add(BuildGovernancePanel());

        // A wrapping row: Compare (ADR-0167) and the two import panels' buttons (Addendum B) no longer fit one line in a narrow right column.
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(_saveButton);
        actions.Children.Add(_checkButton);
        actions.Children.Add(_issueButton);
        actions.Children.Add(_reviseButton);
        actions.Children.Add(_exportButton);
        actions.Children.Add(_importButton);
        actions.Children.Add(_importCadButton);
        actions.Children.Add(_importScanButton);
        actions.Children.Add(_attachButton);
        actions.Children.Add(_compareButton);

        foreach (var button in actions.Children.OfType<Control>())
            button.Margin = new Thickness(0, 0, DesignTokens.SpaceSm, DesignTokens.SpaceXs);

        right.Children.Add(actions);
        right.Children.Add(_status);
        right.Children.Add(new ScrollViewer { Content = _comparison, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("380,*") };
        var leftScroll = new ScrollViewer { Content = left };
        var rightScroll = new ScrollViewer { Content = right };
        AutomationProperties.SetName(leftScroll, "Worksheets left column");
        AutomationProperties.SetName(rightScroll, "Worksheets right column");
        Grid.SetColumn(leftScroll, 0);
        Grid.SetColumn(rightScroll, 1);
        grid.Children.Add(leftScroll);
        grid.Children.Add(rightScroll);
        KeyboardNavigation.SetTabNavigation(right, KeyboardNavigationMode.Continue);
        return grid;
    }

    // Stretched, so a section is as wide as the column and not as wide as its widest line (the showcase journey found Results shrunk to its values).
    private static Expander Section(string title, Control content) => new() { Header = title, IsExpanded = true, Margin = DesignTokens.SectionMargin, Content = content, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };

    private static TextBlock Caption(string text) => new() { Text = text, FontSize = DesignTokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap, MaxWidth = 760, Opacity = 0.8 };

    private static TextBlock Mono(string text) => Typeset(new TextBlock { FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap, MaxWidth = 760 }, text);

    /// <summary>Shows <paramref name="text"/> with its symbols set as an engineer writes them (D_1 as D with a subscript 1).</summary>
    private static TextBlock Typeset(TextBlock block, string text)
    {
        SymbolText.Set(block, text);
        return block;
    }

    private static Control LabelledRow(string label, Control control)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*") };
        var text = new TextBlock { Text = label, Opacity = 0.8, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(control, 1);
        row.Children.Add(text);
        row.Children.Add(control);
        return row;
    }

    private static Control Readout(string label, string value, string? note)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("120,220,*") };
        var name = Typeset(new TextBlock { Opacity = 0.8, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Top }, label);
        var read = Typeset(new TextBlock { FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap }, value);
        var desc = Caption(note ?? string.Empty);
        AutomationProperties.SetName(read, $"Result {label}");
        Grid.SetColumn(name, 0);
        Grid.SetColumn(read, 1);
        Grid.SetColumn(desc, 2);
        row.Children.Add(name);
        row.Children.Add(read);
        row.Children.Add(desc);
        return row;
    }

    /// <summary>A check: PASS/FAIL, then the check's text across the width, then its clause.</summary>
    private static Control CheckRow(CalcCheck check)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*,Auto"), Margin = new Thickness(0, 0, 0, DesignTokens.SpaceXs) };
        var verdict = new TextBlock { Text = check.Passed ? "PASS" : "FAIL", FontWeight = DesignTokens.WeightHeading, Foreground = check.Passed ? Brushes.SeaGreen : Brushes.IndianRed, FontSize = DesignTokens.FontSizeBody };
        var text = Typeset(new TextBlock { FontFamily = DesignTokens.MonoFont, FontSize = DesignTokens.FontSizeBody, TextWrapping = TextWrapping.Wrap }, $"{check.Name}: {check.DemandText} vs {check.CapacityText} (u = {FormatUtilisation(check.Utilisation)})");
        var clause = new TextBlock { Text = check.Clause ?? string.Empty, FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, Margin = new Thickness(DesignTokens.SpaceSm, 0, 0, 0) };
        AutomationProperties.SetName(text, $"Result {verdict.Text}");
        Grid.SetColumn(text, 1);
        Grid.SetColumn(clause, 2);
        row.Children.Add(verdict);
        row.Children.Add(text);
        row.Children.Add(clause);
        return row;
    }

    private static TextBox Input(string initial, string? watermark = null) => new() { Text = initial, Watermark = watermark, FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize, HorizontalAlignment = HorizontalAlignment.Stretch };
}
