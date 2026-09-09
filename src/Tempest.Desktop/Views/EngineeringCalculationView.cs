using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Workspace.Engineering;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Engineering Calculations workspace: what has been calculated, what
/// governed reference data is held, and one active calculation with its
/// inputs, result, traceability and verification.
/// </summary>
/// <remarks>
/// <para>
/// <b>List and detail, the shape the rest of the shell already uses.</b>
/// Left: the calculations that exist and the reference library they stand
/// on. Right: the active calculation. Selecting a calculation opens it
/// read-only; <see cref="NewCalculationCaption"/> starts an editable one.
/// </para>
/// <para>
/// <b>This view decides nothing.</b> Every answer it shows — which
/// calculations exist, which materials are held, whether one may be used,
/// what a check produced, what it stood on, whether verification evidence
/// exists — is composed by <see cref="BracketCalculationWorkbench"/> in
/// <c>Tempest.Workspace</c>. The view collects text, raises intent, and renders
/// what comes back. It parses no quantity, applies no rule, and knows no
/// formula.
/// </para>
/// <para>
/// <b>Nothing here approves anything.</b> Releasing a material is the
/// governed act, performed by the person at the keyboard: they type what
/// source they actually consulted and why they are releasing it, and the
/// platform attributes the review to whoever is signed in, on its own
/// clock. The view cannot name a reviewer and cannot supply a date.
/// </para>
/// </remarks>
public sealed class EngineeringCalculationView : UserControl
{
    /// <summary>The heading, and this surface's own automation name.</summary>
    public const string Heading = "Engineering Calculations";

    /// <summary>The caption on the button that runs the check.</summary>
    public const string CalculateCaption = "Calculate";

    /// <summary>Automation name of the left column (calculations and reference library) — `WP 17.0A` layout assertions.</summary>
    public const string LeftColumnAutomationName = "Engineering calculation left column";

    /// <summary>Automation name of the right column (inputs, results, traceability, verification) — `WP 17.0A` layout assertions.</summary>
    public const string RightColumnAutomationName = "Engineering calculation right column";

    /// <summary>The caption on the button that populates the material library from the shipped seed corpus.</summary>
    public const string PopulateCaption = "Populate Material Library";

    /// <summary>The caption on the button that performs the governed review and release.</summary>
    public const string ReleaseCaption = "Verify and Release Material";

    /// <summary>The caption on the button that starts a new calculation.</summary>
    public const string NewCalculationCaption = "New Calculation";

    /// <summary>The caption on the button that adds a material record of the engineer's own, as Draft (`WP 17.9.2`).</summary>
    public const string AddMaterialCaption = "Add Material Record";

    /// <summary>What the Inputs panel says beside the material picker when nothing is released yet (`WP 17.9.2`).</summary>
    public const string NoReleasedMaterialGuidance =
        "No released material yet. In the Reference Library on the left, press \"" + PopulateCaption + "\" or add your own record, "
        + "select it, then press \"" + ReleaseCaption + "\". Only a released material can drive a calculation.";

    /// <summary>The caption on the button that opens the selected calculation.</summary>
    public const string OpenCaption = "Open Calculation";

    /// <summary>The caption on the button that changes what the selected calculation is called.</summary>
    public const string RenameCaption = "Rename";

    /// <summary>The caption on the button that takes the selected calculation out of the active list without deleting it.</summary>
    public const string RetireCaption = "Retire from Active List";

    /// <summary>The caption on the box that shows retired calculations alongside the active ones.</summary>
    public const string ShowRetiredCaption = "Show retired calculations";

    /// <summary>What the surface says when a record has no governed object behind it to rename or retire.</summary>
    public const string UnnamedGuidance =
        "This calculation was recorded before it could be named, so there is no name to change and nothing to retire. "
        + "Its record and its evidence are unaffected — it can still be opened.";

    /// <summary>What the surface shows when no material is held at all.</summary>
    public const string EmptyLibraryGuidance =
        "No material records are held yet. Press \"" + PopulateCaption + "\" below to add the shipped reference records as Draft, "
        + "then select one and release it through review before it can be used for engineering work.";

    /// <summary>What the surface shows when nothing has been calculated yet.</summary>
    public const string EmptyCalculationsGuidance =
        "Nothing has been calculated yet. Press \"" + NewCalculationCaption + "\", choose a calculation, "
        + "pick a released reference material, enter the inputs and press \"" + CalculateCaption + "\".";

    private readonly ListBox _calculationList = new() { MinHeight = 120, MaxHeight = 220, FontSize = DesignTokens.FontSizeBody };
    private readonly ListBox _referenceList = new() { MinHeight = 120, MaxHeight = 220, FontSize = DesignTokens.FontSizeBody };
    private readonly ComboBox _definitionPicker = new()
    {
        FontSize = DesignTokens.FontSizeBody,
        MinHeight = DesignTokens.MinControlSize,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private readonly TextBox _loadBox = Input("12");
    private readonly TextBox _areaBox = Input("60");
    private readonly TextBox _lengthBox = Input("150");
    private readonly TextBox _massLimitBox = Input("50");

    private readonly TextBox _nameBox = Input(string.Empty, "What to call this calculation");
    private readonly TextBox _renameBox = Input(string.Empty, "A new name for the selected calculation");

    private readonly TextBox _sourceConsultedBox = Input(string.Empty, "What you checked this record against");
    private readonly TextBox _releaseRationaleBox = Input(string.Empty, "Why it is being released");

    // `WP 17.9.2`: the material is chosen beside the inputs it drives, and a
    // record of the engineer's own can be added. The first Windows review
    // saw "Select a governed material" with nothing on the Inputs panel to
    // select, and no way to add one.
    private readonly ComboBox _materialPicker = new()
    {
        FontSize = DesignTokens.FontSizeBody,
        MinHeight = DesignTokens.MinControlSize,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        PlaceholderText = "Choose a released material",
    };

    private readonly TextBlock _materialGuidance = Caption(string.Empty);
    private bool _syncingMaterial;

    private readonly TextBox _newMaterialNameBox = Input(string.Empty, "e.g. 6082-T6 aluminium alloy");
    private readonly TextBox _newMaterialDesignationBox = Input(string.Empty, "e.g. 6082-T6");
    private readonly ComboBox _newMaterialFamilyPicker = new()
    {
        FontSize = DesignTokens.FontSizeBody,
        MinHeight = DesignTokens.MinControlSize,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        ItemsSource = Enum.GetValues<Tempest.Core.Materials.MaterialFamily>(),
        SelectedItem = Tempest.Core.Materials.MaterialFamily.Steel,
    };

    private readonly TextBox _newMaterialYieldBox = Input(string.Empty, "MPa");
    private readonly TextBox _newMaterialDensityBox = Input(string.Empty, "g/cm3");
    private readonly TextBox _newMaterialSourceOrganisationBox = Input(string.Empty, "Who published the figures");
    private readonly TextBox _newMaterialSourceDocumentBox = Input(string.Empty, "Datasheet, standard or handbook");
    private readonly Button _addMaterialButton = new() { Content = AddMaterialCaption, MinHeight = DesignTokens.MinControlSize };

    private readonly Button _calculateButton = new() { Content = CalculateCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _populateButton = new() { Content = PopulateCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _releaseButton = new() { Content = ReleaseCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _newButton = new() { Content = NewCalculationCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _openButton = new() { Content = OpenCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _renameButton = new() { Content = RenameCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _retireButton = new() { Content = RetireCaption, MinHeight = DesignTokens.MinControlSize };

    private readonly CheckBox _showRetiredBox = new()
    {
        Content = ShowRetiredCaption,
        MinHeight = DesignTokens.MinControlSize,
        FontSize = DesignTokens.FontSizeBody,
    };

    private readonly TextBlock _populateExplanation = Caption(
        "Adds the shipped reference material records to this library as Draft. Nothing is released and nothing "
        + "is approved by populating, and running it again adds only what is missing — it never overwrites a "
        + "record somebody has corrected.");

    private readonly TextBlock _calculationsEmpty = Caption(EmptyCalculationsGuidance);
    private readonly TextBlock _selectedCalculationNote = Caption(string.Empty);

    /// <summary>The calculation the rename box currently holds the name of, so a refresh can tell a genuine change of selection from a rebuilt list item.</summary>
    private Guid? _renameBoxBoundTo;

    /// <summary>
    /// True while the calculation list is being replaced. Replacing the
    /// items clears the selection before the surviving one is restored, so
    /// without this the transient "nothing selected" would read as the
    /// engineer having moved away and would discard a half-typed name.
    /// </summary>
    private bool _replacingCalculations;
    private readonly TextBlock _definitionNote = Caption(string.Empty);
    private readonly TextBlock _activeMode = Caption(string.Empty);
    private readonly TextBlock _materialState = Caption(string.Empty);
    private readonly TextBlock _statusMessage = Caption(string.Empty);

    private readonly StackPanel _validationPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _resultPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _traceabilityPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _verificationPanel = new() { Spacing = DesignTokens.SpaceXs };

    /// <summary>Initialises a new instance of the <see cref="EngineeringCalculationView"/> class.</summary>
    private readonly Func<string?, string> _describePrincipal;

    /// <param name="describePrincipal">
    /// Turns a stored identity id into a name for display (`WP 17.9.1`);
    /// <see langword="null"/> shows ids as recorded.
    /// </param>
    public EngineeringCalculationView(Func<string?, string>? describePrincipal = null)
    {
        _describePrincipal = describePrincipal ?? (id => id ?? string.Empty);
        AutomationProperties.SetName(this, Heading);

        _calculateButton.Classes.Add(ChromeStyles.Primary);
        _newButton.Classes.Add(ChromeStyles.Primary);
        _populateButton.Classes.Add(ChromeStyles.Flat);
        _releaseButton.Classes.Add(ChromeStyles.Flat);
        _openButton.Classes.Add(ChromeStyles.Flat);
        _renameButton.Classes.Add(ChromeStyles.Flat);
        _retireButton.Classes.Add(ChromeStyles.Flat);

        Describe(_populateButton, PopulateCaption, "Add the shipped reference material records to this library as Draft.");
        Describe(_releaseButton, ReleaseCaption, "Verify this material against the source you name, then release it for engineering work.");
        Describe(_calculateButton, CalculateCaption, "Run the calculation against the inputs above.");
        Describe(_newButton, NewCalculationCaption, "Start a new calculation from the product's registered catalogue.");
        Describe(_openButton, OpenCaption, "Open the selected calculation, read-only, exactly as it was recorded.");
        Describe(_renameButton, RenameCaption, "Change what the selected calculation is called. Its record, revisions and references are unchanged.");
        Describe(_retireButton, RetireCaption, "Take the selected calculation out of the active list. Nothing is deleted and it can still be opened, but the lifecycle state it reaches is terminal and cannot be undone — you are asked to confirm.");
        Describe(_showRetiredBox, ShowRetiredCaption, "List retired calculations alongside the active ones. This only changes what is shown; nothing is written.");
        Describe(_addMaterialButton, AddMaterialCaption, "Add a material record of your own to the library as Draft. It must then be verified and released before a calculation can use it.");
        Describe(_materialPicker, "Material for this calculation", "The released material this calculation stands on.");
        AutomationProperties.SetName(_newMaterialNameBox, "New material name");
        AutomationProperties.SetName(_newMaterialDesignationBox, "New material designation");
        AutomationProperties.SetName(_newMaterialFamilyPicker, "New material family");
        AutomationProperties.SetName(_newMaterialYieldBox, "New material yield strength in megapascals");
        AutomationProperties.SetName(_newMaterialDensityBox, "New material density in grams per cubic centimetre");
        AutomationProperties.SetName(_newMaterialSourceOrganisationBox, "New material source organisation");
        AutomationProperties.SetName(_newMaterialSourceDocumentBox, "New material source document");
        _addMaterialButton.Classes.Add(ChromeStyles.Flat);
        _addMaterialButton.Click += (_, _) => AddMaterialRequested?.Invoke();
        _materialPicker.SelectionChanged += (_, _) =>
        {
            if (_syncingMaterial || _materialPicker.SelectedItem is not BracketMaterialOption chosen)
                return;

            _syncingMaterial = true;
            try
            {
                _referenceList.SelectedItem = Materials.FirstOrDefault(m => m.RecordId == chosen.RecordId) ?? chosen;
            }
            finally
            {
                _syncingMaterial = false;
            }
        };

        AutomationProperties.SetName(_calculationList, "Existing calculations");
        AutomationProperties.SetName(_referenceList, "Reference library");
        AutomationProperties.SetName(_definitionPicker, "Calculation to run");
        AutomationProperties.SetName(_loadBox, "Axial load in kilonewtons");
        AutomationProperties.SetName(_areaBox, "Section area in square millimetres");
        AutomationProperties.SetName(_lengthBox, "Member length in millimetres");
        AutomationProperties.SetName(_massLimitBox, "Mass limit in grams");
        AutomationProperties.SetName(_nameBox, "Calculation name");
        AutomationProperties.SetName(_renameBox, "New name for the selected calculation");
        AutomationProperties.SetName(_showRetiredBox, ShowRetiredCaption);
        AutomationProperties.SetName(_sourceConsultedBox, "Source consulted");
        AutomationProperties.SetName(_releaseRationaleBox, "Release rationale");
        AutomationProperties.SetName(_selectedCalculationNote, "Selected calculation");
        AutomationProperties.SetLiveSetting(_selectedCalculationNote, AutomationLiveSetting.Polite);
        AutomationProperties.SetLiveSetting(_statusMessage, AutomationLiveSetting.Polite);

        _calculateButton.Click += (_, _) => CalculateRequested?.Invoke();
        _populateButton.Click += (_, _) => PopulateRequested?.Invoke();
        _releaseButton.Click += (_, _) => ReleaseRequested?.Invoke();
        _newButton.Click += (_, _) => NewCalculationRequested?.Invoke();
        _openButton.Click += (_, _) => RaiseOpen();
        _renameButton.Click += (_, _) => RaiseRename();
        _retireButton.Click += (_, _) => RaiseRetire();
        _showRetiredBox.IsCheckedChanged += (_, _) => ShowRetiredChanged?.Invoke(ShowRetired);

        _referenceList.SelectionChanged += (_, _) =>
        {
            ShowSelectedMaterialState();
            SyncMaterialPickerToList();
        };
        _calculationList.SelectionChanged += (_, _) =>
        {
            if (!_replacingCalculations)
                ShowSelectedCalculationActions();
        };
        _calculationList.DoubleTapped += (_, _) => RaiseOpen();
        _definitionPicker.SelectionChanged += (_, _) => ShowSelectedDefinition();

        Content = BuildLayout();
    }

    /// <summary>Raised when the engineer asks for the calculation to run.</summary>
    public event Action? CalculateRequested;

    /// <summary>Raised when the engineer asks for the material library to be populated from the shipped seed corpus.</summary>
    public event Action? PopulateRequested;

    /// <summary>Raised when the engineer asks to add the material record described by <see cref="NewMaterial"/> (`WP 17.9.2`).</summary>
    public event Action? AddMaterialRequested;

    /// <summary>The material record the add-material form currently describes, as typed (`WP 17.9.2`).</summary>
    public NewMaterialRecord NewMaterial => new(
        _newMaterialNameBox.Text ?? string.Empty,
        _newMaterialDesignationBox.Text ?? string.Empty,
        _newMaterialFamilyPicker.SelectedItem is Tempest.Core.Materials.MaterialFamily family ? family : Tempest.Core.Materials.MaterialFamily.Unspecified,
        _newMaterialYieldBox.Text ?? string.Empty,
        _newMaterialDensityBox.Text ?? string.Empty,
        _newMaterialSourceOrganisationBox.Text ?? string.Empty,
        _newMaterialSourceDocumentBox.Text ?? string.Empty);

    /// <summary>Fills the add-material form; the test and the coordinator's own retry path use it.</summary>
    public void SetNewMaterial(NewMaterialRecord material)
    {
        ArgumentNullException.ThrowIfNull(material);
        _newMaterialNameBox.Text = material.Name;
        _newMaterialDesignationBox.Text = material.Designation;
        _newMaterialFamilyPicker.SelectedItem = material.Family;
        _newMaterialYieldBox.Text = material.YieldStrengthMegapascals;
        _newMaterialDensityBox.Text = material.DensityGramsPerCubicCentimetre;
        _newMaterialSourceOrganisationBox.Text = material.SourceOrganisation;
        _newMaterialSourceDocumentBox.Text = material.SourceDocument;
    }

    /// <summary>Clears the add-material form after a record has been added.</summary>
    public void ClearNewMaterial() =>
        SetNewMaterial(new NewMaterialRecord(string.Empty, string.Empty, Tempest.Core.Materials.MaterialFamily.Steel, string.Empty, string.Empty, string.Empty, string.Empty));

    /// <summary>The released materials the Inputs panel offers (`WP 17.9.2`).</summary>
    public IReadOnlyList<BracketMaterialOption> ReleasedMaterials => Materials.Where(m => m.IsUsableForEngineering).ToList();

    /// <summary>What the Inputs panel says beside the material picker.</summary>
    public string MaterialGuidance => _materialGuidance.Text ?? string.Empty;

    /// <summary>Raised when the engineer asks for the selected material to be verified and released.</summary>
    public event Action? ReleaseRequested;

    /// <summary>Raised when the engineer asks to start a new calculation.</summary>
    public event Action? NewCalculationRequested;

    /// <summary>Raised when the engineer asks to open a persisted calculation.</summary>
    public event Action<Guid>? OpenCalculationRequested;

    /// <summary>Raised when the engineer asks to change what the selected calculation is called, with its governed object's Id and the new name.</summary>
    public event Action<Guid, string>? RenameRequested;

    /// <summary>Raised when the engineer asks to take the selected calculation out of the active list, with its governed object's Id.</summary>
    public event Action<Guid>? RetireRequested;

    /// <summary>Raised when the engineer asks to see, or stop seeing, retired calculations.</summary>
    public event Action<bool>? ShowRetiredChanged;

    /// <summary>Raised when the engineer selects a different calculation — the point at which anything waiting on a confirmation stops applying.</summary>
    public event Action? SelectionMoved;

    /// <summary>The persisted calculations currently listed, newest first.</summary>
    public IReadOnlyList<CalculationListEntry> Calculations { get; private set; } = [];

    /// <summary>The calculation currently selected in the list, or <see langword="null"/>.</summary>
    public CalculationListEntry? SelectedCalculation => _calculationList.SelectedItem as CalculationListEntry;

    /// <summary>The product's registered calculation catalogue, as offered.</summary>
    public IReadOnlyList<CalculationCatalogueEntry> Catalogue { get; private set; } = [];

    /// <summary>The calculation chosen for a new run, or <see langword="null"/>.</summary>
    public CalculationCatalogueEntry? SelectedDefinition => _definitionPicker.SelectedItem as CalculationCatalogueEntry;

    /// <summary>The materials currently offered, in the order they are offered.</summary>
    public IReadOnlyList<BracketMaterialOption> Materials { get; private set; } = [];

    /// <summary>The material currently selected, or <see langword="null"/> where none is.</summary>
    public BracketMaterialOption? SelectedMaterial => _referenceList.SelectedItem as BracketMaterialOption;

    /// <summary>Whether the surface is telling the engineer the library is empty.</summary>
    public bool IsShowingEmptyLibrary => Materials.Count == 0;

    /// <summary>Whether the surface is telling the engineer nothing has been calculated.</summary>
    public bool IsShowingEmptyCalculations => Calculations.Count == 0;

    /// <summary>
    /// Whether the active calculation is a persisted record being viewed
    /// rather than a new one being entered.
    /// </summary>
    /// <remarks>
    /// Viewing never edits: in this state the inputs and the Calculate
    /// action are disabled, so opening a record cannot recalculate it or
    /// write to it. <see cref="NewCalculationCaption"/> leaves the state.
    /// </remarks>
    public bool IsReadOnly { get; private set; }

    /// <summary>The outcome currently displayed, or <see langword="null"/> where none is.</summary>
    public BracketCalculationOutcome? DisplayedOutcome { get; private set; }

    /// <summary>The verification evidence currently displayed, or <see langword="null"/>.</summary>
    public VerificationEvidence? DisplayedVerification { get; private set; }

    /// <summary>Whether retired calculations are being shown alongside the active ones.</summary>
    public bool ShowRetired => _showRetiredBox.IsChecked == true;

    /// <summary>What the engineer wants the calculation they are entering to be called. Blank takes the workbench's own default.</summary>
    public string CalculationName => _nameBox.Text ?? string.Empty;

    /// <summary>The new name typed for the selected calculation.</summary>
    public string NewName => _renameBox.Text ?? string.Empty;

    /// <summary>What the engineer has typed, ready to hand to the workbench.</summary>
    public BracketCalculationInputs CurrentInputs => new(
        SelectedMaterial?.RecordId ?? string.Empty,
        _loadBox.Text ?? string.Empty,
        _areaBox.Text ?? string.Empty,
        _lengthBox.Text ?? string.Empty,
        _massLimitBox.Text ?? string.Empty);

    /// <summary>What the engineer typed as the source they consulted.</summary>
    public string SourceConsulted => _sourceConsultedBox.Text ?? string.Empty;

    /// <summary>What the engineer typed as the reason for release.</summary>
    public string ReleaseRationale => _releaseRationaleBox.Text ?? string.Empty;

    /// <summary>The last status line shown.</summary>
    public string StatusMessage => _statusMessage.Text ?? string.Empty;

    /// <summary>Shows the persisted calculations, keeping the current selection where it survives.</summary>
    /// <param name="calculations">Every calculation the engine has recorded.</param>
    public void ShowCalculations(IReadOnlyList<CalculationListEntry> calculations)
    {
        ArgumentNullException.ThrowIfNull(calculations);

        var keep = SelectedCalculation?.RecordId;

        _replacingCalculations = true;
        try
        {
            Calculations = calculations;
            _calculationList.ItemsSource = calculations;
            _calculationList.SelectedItem = calculations.FirstOrDefault(c => c.RecordId == keep);
        }
        finally
        {
            _replacingCalculations = false;
        }

        _calculationsEmpty.IsVisible = calculations.Count == 0;
        ShowSelectedCalculationActions();
    }

    /// <summary>Shows the product's registered calculation catalogue.</summary>
    /// <param name="catalogue">Every calculation the product registers.</param>
    public void ShowCatalogue(IReadOnlyList<CalculationCatalogueEntry> catalogue)
    {
        ArgumentNullException.ThrowIfNull(catalogue);

        Catalogue = catalogue;
        _definitionPicker.ItemsSource = catalogue;
        _definitionPicker.SelectedItem ??= catalogue.FirstOrDefault(c => c.IsDrivableHere) ?? catalogue.FirstOrDefault();
        ShowSelectedDefinition();
    }

    /// <summary>Shows the governed materials on offer, keeping the current selection where it survives.</summary>
    /// <param name="materials">Every material the library holds.</param>
    /// <param name="selectRecordId">The record to select, where one should be.</param>
    public void ShowMaterials(IReadOnlyList<BracketMaterialOption> materials, string? selectRecordId = null)
    {
        ArgumentNullException.ThrowIfNull(materials);

        Materials = materials;
        var keep = selectRecordId ?? SelectedMaterial?.RecordId;

        _referenceList.ItemsSource = materials;

        // The Inputs panel offers only what a calculation may stand on.
        _syncingMaterial = true;
        try
        {
            _materialPicker.ItemsSource = materials.Where(m => m.IsUsableForEngineering).ToList();
        }
        finally
        {
            _syncingMaterial = false;
        }

        _materialGuidance.Text = materials.Any(m => m.IsUsableForEngineering) ? string.Empty : NoReleasedMaterialGuidance;
        _materialGuidance.IsVisible = _materialGuidance.Text.Length > 0;

        _referenceList.SelectedItem = materials.FirstOrDefault(m => m.RecordId == keep)
            ?? materials.FirstOrDefault(m => m.IsUsableForEngineering)
            ?? materials.FirstOrDefault();

        ShowSelectedMaterialState();
        SyncMaterialPickerToList();
    }

    /// <summary>Keeps the Inputs picker on the same record as the Reference Library list, or on nothing when that record is not released.</summary>
    private void SyncMaterialPickerToList()
    {
        if (_syncingMaterial)
            return;

        _syncingMaterial = true;
        try
        {
            var selected = SelectedMaterial;
            _materialPicker.SelectedItem = selected is { IsUsableForEngineering: true }
                ? (_materialPicker.ItemsSource as IEnumerable<BracketMaterialOption>)?.FirstOrDefault(m => m.RecordId == selected.RecordId)
                : null;
        }
        finally
        {
            _syncingMaterial = false;
        }
    }

    /// <summary>Shows an outcome — a result, a refusal, or a rejected input.</summary>
    /// <param name="outcome">What the workbench answered.</param>
    /// <param name="readOnly">Whether this is a persisted record being viewed rather than a new run.</param>
    /// <param name="recordedName">
    /// What the recorded calculation is called, where the caller knows.
    /// Supplied because the list this view holds is filtered — a retired
    /// calculation is not in it — so falling back to the list alone would
    /// show a blank name for a calculation that has one.
    /// </param>
    public void ShowOutcome(BracketCalculationOutcome outcome, bool readOnly = false, string? recordedName = null)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        DisplayedOutcome = outcome;
        SetReadOnly(readOnly);

        // A recorded calculation shows its own name in the Name box, not
        // whatever was last typed there. The box is disabled in this state,
        // so leaving a stale name beside another calculation's figures
        // would misattribute the record on screen.
        if (readOnly)
        {
            _nameBox.Text = recordedName
                ?? Calculations.FirstOrDefault(c => c.RecordId == outcome.CalculationRecordId && c.IsNamed)?.Title
                ?? string.Empty;
        }

        _validationPanel.Children.Clear();
        _resultPanel.Children.Clear();
        _traceabilityPanel.Children.Clear();

        foreach (var problem in outcome.Problems)
            _validationPanel.Children.Add(ObjectEditorView.BuildSeverityRow(FeedbackSeverity.Error, problem));

        if (outcome.RefusalReason is not null)
        {
            // A refusal is a governance answer, not an error the user
            // caused — Warning, and the platform's own sentence verbatim.
            _validationPanel.Children.Add(ObjectEditorView.BuildSeverityRow(FeedbackSeverity.Warning, outcome.RefusalReason));
        }

        _validationPanel.IsVisible = _validationPanel.Children.Count > 0;

        if (!outcome.Performed)
        {
            _resultPanel.IsVisible = false;
            _traceabilityPanel.IsVisible = false;
            return;
        }

        _resultPanel.IsVisible = true;
        _traceabilityPanel.IsVisible = true;

        _resultPanel.Children.Add(ObjectEditorView.BuildSeverityRow(
            outcome.MeetsCriteria ? FeedbackSeverity.Success : FeedbackSeverity.Error, outcome.OutcomeLabel));
        _resultPanel.Children.Add(Readout("Applied stress", outcome.AppliedStress));
        _resultPanel.Children.Add(Readout("Allowable stress", outcome.AllowableStress));
        _resultPanel.Children.Add(Readout("Stress margin", outcome.StressMargin));
        _resultPanel.Children.Add(Readout("Stress criterion", outcome.StressCriterionMet ? "Met" : "Not met"));
        _resultPanel.Children.Add(Readout("Density", outcome.Density));
        _resultPanel.Children.Add(Readout("Estimated mass", outcome.EstimatedMass));
        _resultPanel.Children.Add(Readout("Mass limit", outcome.MassLimit));
        _resultPanel.Children.Add(Readout("Mass criterion", outcome.MassCriterionMet ? "Met" : "Not met"));

        _traceabilityPanel.Children.Add(Readout("Calculation", outcome.CalculationId));
        _traceabilityPanel.Children.Add(Readout("Calculation record", outcome.CalculationRecordId.ToString()));
        _traceabilityPanel.Children.Add(Readout("Calculation revision", outcome.CalculationRevision.ToString()));
        _traceabilityPanel.Children.Add(Readout("Executed", $"{outcome.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC by {_describePrincipal(outcome.ExecutedByPrincipalId)}"));
        _traceabilityPanel.Children.Add(Readout("Reference", $"{outcome.MaterialLibrary}/{outcome.MaterialRecordId}"));
        _traceabilityPanel.Children.Add(Readout("Pinned revision", outcome.PinnedRevision.ToString()));
        _traceabilityPanel.Children.Add(Readout("Reference now", $"revision {outcome.CurrentRevision?.ToString() ?? "—"}, {outcome.MaterialStateNow}"));
        _traceabilityPanel.Children.Add(Readout("Provenance", outcome.Provenance));

        if (outcome.ReviewerPrincipalId is not null)
            _traceabilityPanel.Children.Add(Readout("Verified by", $"{outcome.ReviewerPrincipalId} on {outcome.VerificationDate:yyyy-MM-dd}"));

        if (outcome.MaterialHasMovedOn)
        {
            // The whole point of pinning: the result below is the one that
            // was computed, not one recomputed against today's data.
            _traceabilityPanel.Children.Add(ObjectEditorView.BuildSeverityRow(
                FeedbackSeverity.Warning,
                $"The reference has been revised since this result was computed. The result stands on revision {outcome.PinnedRevision}, not the current revision {outcome.CurrentRevision}."));
        }

        foreach (var assumption in outcome.Assumptions)
            _traceabilityPanel.Children.Add(Readout("Assumption", assumption));
    }

    /// <summary>Shows the verification evidence, or the honest account of why there is none.</summary>
    /// <param name="evidence">What the workbench found.</param>
    public void ShowVerification(VerificationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        DisplayedVerification = evidence;
        _verificationPanel.Children.Clear();

        if (!evidence.Exists)
        {
            _verificationPanel.Children.Add(ObjectEditorView.BuildSeverityRow(FeedbackSeverity.Warning, evidence.WhyAbsent ?? "No verification artefact is held."));
            return;
        }

        _verificationPanel.Children.Add(Readout("Artefact", evidence.Reference ?? evidence.ArtefactRecordId));
        _verificationPanel.Children.Add(Readout("Standing", evidence.Standing ?? "Not performed"));

        if (evidence.Summary is not null)
            _verificationPanel.Children.Add(Readout("Summary", evidence.Summary));

        if (evidence.PerformedByPrincipalId is not null)
            _verificationPanel.Children.Add(Readout("Performed by", $"{_describePrincipal(evidence.PerformedByPrincipalId)} on {evidence.PerformedOn:yyyy-MM-dd}"));
    }

    /// <summary>Shows what just happened, as a status line.</summary>
    /// <param name="message">The message.</param>
    public void ShowStatus(string message) => _statusMessage.Text = message ?? string.Empty;

    /// <summary>Puts previously-entered figures back into the boxes.</summary>
    /// <param name="inputs">The figures to restore.</param>
    public void RestoreInputs(BracketCalculationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        _loadBox.Text = inputs.LoadKilonewtons;
        _areaBox.Text = inputs.SectionAreaSquareMillimetres;
        _lengthBox.Text = inputs.MemberLengthMillimetres;
        _massLimitBox.Text = inputs.MassLimitGrams;
    }

    /// <summary>Leaves read-only view and offers an editable calculation.</summary>
    public void BeginNewCalculation()
    {
        SetReadOnly(false);
        _calculationList.SelectedItem = null;
        _nameBox.Text = string.Empty;
        ShowStatus($"Name it, enter the inputs and press \"{CalculateCaption}\".");
    }

    private void RaiseOpen()
    {
        if (SelectedCalculation is { } selected)
            OpenCalculationRequested?.Invoke(selected.RecordId);
    }

    private void RaiseRename()
    {
        if (SelectedCalculation?.ObjectId is { } objectId)
            RenameRequested?.Invoke(objectId, NewName);
    }

    private void RaiseRetire()
    {
        if (SelectedCalculation?.ObjectId is { } objectId)
            RetireRequested?.Invoke(objectId);
    }

    /// <summary>
    /// Offers exactly the actions the selected calculation can actually
    /// take, and says why where it can take none.
    /// </summary>
    /// <remarks>
    /// Rename and retire address the governed <c>Calculation</c> object, so
    /// they are enabled only for a record that has one. A record executed
    /// before naming existed, or by a module rather than by a person, has
    /// none — and is honestly reported as having none rather than offered a
    /// button that would fail. This is the same discipline the catalogue
    /// applies to a calculation this workspace cannot drive.
    /// </remarks>
    private void ShowSelectedCalculationActions()
    {
        var selected = SelectedCalculation;

        _openButton.IsEnabled = selected is not null;
        _renameButton.IsEnabled = selected?.IsNamed == true;
        _retireButton.IsEnabled = selected is { IsNamed: true, IsRetired: false };
        _renameBox.IsEnabled = selected?.IsNamed == true;

        // Refilled only when the engineer has moved to a *different*
        // calculation, compared by record identity rather than by list
        // item. Every refresh rebuilds the entries, so the selected item is
        // a new instance each time even when the selection has not moved —
        // refilling on that would discard a name somebody is halfway
        // through typing whenever anything else refreshed the list.
        if (_renameBoxBoundTo != selected?.RecordId)
        {
            _renameBoxBoundTo = selected?.RecordId;
            _renameBox.Text = selected?.IsNamed == true ? selected.Title : string.Empty;
            SelectionMoved?.Invoke();
        }

        _selectedCalculationNote.Text = selected switch
        {
            null => Calculations.Count == 0
                ? string.Empty
                : "Select a calculation to open, rename or retire it.",
            { IsNamed: false } => UnnamedGuidance,
            { IsRetired: true } => $"Retired — its governed status is {selected.Status}. It is not in the active list, nothing was deleted, and it can still be opened.",
            { ProjectLabel: { Length: > 0 } project } => $"In {project}. Renaming changes only what it is called.",
            _ => "Not in a project — it was created outside one. Renaming changes only what it is called.",
        };
    }

    private void SetReadOnly(bool readOnly)
    {
        IsReadOnly = readOnly;

        // Disabled rather than hidden: an engineer looking at a recorded
        // calculation should still see the figures it was run with, and see
        // plainly that they are not editing them.
        foreach (var box in new[] { _nameBox, _loadBox, _areaBox, _lengthBox, _massLimitBox })
            box.IsEnabled = !readOnly;

        _materialPicker.IsEnabled = !readOnly;
        _calculateButton.IsEnabled = !readOnly;
        _activeMode.Text = readOnly
            ? "Viewing a recorded calculation. It is read-only: opening a record never recalculates it and never writes to it. "
              + $"Press \"{NewCalculationCaption}\" to start a new one."
            : "New calculation.";
    }

    private void ShowSelectedDefinition()
    {
        var selected = SelectedDefinition;

        if (selected is null)
        {
            _definitionNote.Text = string.Empty;
            return;
        }

        _definitionNote.Text = selected.IsDrivableHere
            ? selected.Description
            : $"{selected.Description}  —  {selected.WhyNotDrivable}";

        // A calculation this workspace cannot drive must not offer a
        // Calculate button that would throw. `TD-159` produced exactly that
        // once already.
        _calculateButton.IsEnabled = selected.IsDrivableHere && !IsReadOnly;
    }

    private void ShowSelectedMaterialState()
    {
        var selected = SelectedMaterial;

        if (selected is null)
        {
            _materialState.Text = Materials.Count == 0 ? EmptyLibraryGuidance : "Select a material.";
            _releaseButton.IsVisible = false;
            return;
        }

        _materialState.Text = $"{selected.StateExplanation}  {selected.Provenance}";

        // The governed review is offered where it is the thing standing
        // between the engineer and a calculation — never as a permanent
        // button inviting a release nobody needs.
        _releaseButton.IsVisible = !selected.IsUsableForEngineering;
    }

    private Control BuildLayout()
    {
        var page = new Grid { ColumnDefinitions = new ColumnDefinitions("420,*"), Margin = DesignTokens.PagePadding };

        // ---- Left: what exists -------------------------------------
        var left = new StackPanel { Spacing = DesignTokens.SpaceMd };

        var calculations = new StackPanel { Spacing = DesignTokens.SpaceSm };
        calculations.Children.Add(_newButton);
        calculations.Children.Add(LabelledRow("Calculation", _definitionPicker));
        calculations.Children.Add(_definitionNote);
        calculations.Children.Add(_calculationsEmpty);
        calculations.Children.Add(_calculationList);
        calculations.Children.Add(_showRetiredBox);
        calculations.Children.Add(_selectedCalculationNote);
        calculations.Children.Add(_openButton);
        calculations.Children.Add(LabelledRow("Rename to", _renameBox));
        calculations.Children.Add(_renameButton);
        calculations.Children.Add(_retireButton);
        left.Children.Add(Section("Calculations", calculations));

        var reference = new StackPanel { Spacing = DesignTokens.SpaceSm };
        reference.Children.Add(_referenceList);
        reference.Children.Add(_materialState);
        reference.Children.Add(_populateButton);
        reference.Children.Add(_populateExplanation);
        reference.Children.Add(LabelledRow("Source consulted", _sourceConsultedBox));
        reference.Children.Add(LabelledRow("Release rationale", _releaseRationaleBox));
        reference.Children.Add(_releaseButton);
        left.Children.Add(Section("Reference Library", reference));

        var newMaterial = new StackPanel { Spacing = DesignTokens.SpaceSm };
        newMaterial.Children.Add(Caption(
            "A record of your own, added as Draft. It is verified and released through the Reference Library above, "
            + "exactly like a shipped record, so the source you name here is what the release is checked against."));
        newMaterial.Children.Add(LabelledRow("Name", _newMaterialNameBox));
        newMaterial.Children.Add(LabelledRow("Designation", _newMaterialDesignationBox));
        newMaterial.Children.Add(LabelledRow("Family", _newMaterialFamilyPicker));
        newMaterial.Children.Add(LabelledRow("Yield strength (MPa)", _newMaterialYieldBox));
        newMaterial.Children.Add(LabelledRow("Density (g/cm3)", _newMaterialDensityBox));
        newMaterial.Children.Add(LabelledRow("Source organisation", _newMaterialSourceOrganisationBox));
        newMaterial.Children.Add(LabelledRow("Source document", _newMaterialSourceDocumentBox));
        newMaterial.Children.Add(_addMaterialButton);
        left.Children.Add(Section("Add a Material", newMaterial));

        // ---- Right: the active calculation --------------------------
        var right = new StackPanel { Spacing = DesignTokens.SpaceMd };
        right.Children.Add(PageHeading.Label("Engineering"));
        right.Children.Add(PageHeading.Title(Heading));
        right.Children.Add(PageHeading.Lead(
            "A first-order direct-stress and mass check on one bracket section, run against a released reference material. "
            + "The result records the reference revision it stood on, so it stays true after that reference moves on."));
        right.Children.Add(_activeMode);

        var inputs = new StackPanel { Spacing = DesignTokens.SpaceSm };
        inputs.Children.Add(LabelledRow("Calculation name", _nameBox));
        inputs.Children.Add(LabelledRow("Material", _materialPicker));
        inputs.Children.Add(_materialGuidance);
        inputs.Children.Add(LabelledRow("Load (kN)", _loadBox));
        inputs.Children.Add(LabelledRow("Section area (mm2)", _areaBox));
        inputs.Children.Add(LabelledRow("Member length (mm)", _lengthBox));
        inputs.Children.Add(LabelledRow("Mass limit (g)", _massLimitBox));
        inputs.Children.Add(_calculateButton);
        inputs.Children.Add(_validationPanel);
        inputs.Children.Add(_statusMessage);
        right.Children.Add(Section("Inputs", inputs));

        right.Children.Add(Section("Results", _resultPanel));
        right.Children.Add(Section("Traceability", _traceabilityPanel));
        right.Children.Add(Section("Verification", _verificationPanel));

        // The Grid's children are the two ScrollViewers, so the column
        // must be set on them. Setting it on the StackPanels inside them
        // (as this did before `WP 17.0A`) left both viewers in column 0,
        // and every section of the right column rendered on top of the
        // left — the overlap the first Windows launch of `v0.16.0` showed.
        var leftColumn = new ScrollViewer { Content = left, Margin = new Thickness(0, 0, DesignTokens.SpaceLg, 0) };
        var rightColumn = new ScrollViewer { Content = right };
        Grid.SetColumn(leftColumn, 0);
        Grid.SetColumn(rightColumn, 1);
        AutomationProperties.SetName(leftColumn, LeftColumnAutomationName);
        AutomationProperties.SetName(rightColumn, RightColumnAutomationName);
        page.Children.Add(leftColumn);
        page.Children.Add(rightColumn);

        _validationPanel.IsVisible = false;
        _resultPanel.IsVisible = false;
        _traceabilityPanel.IsVisible = false;
        ShowSelectedCalculationActions();
        SetReadOnly(false);

        return page;
    }

    private static void Describe(Control control, string name, string tip)
    {
        AutomationProperties.SetName(control, name);
        ToolTip.SetTip(control, tip);
    }

    private static Expander Section(string title, Control content) => new()
    {
        Header = title,
        IsExpanded = true,
        Margin = DesignTokens.SectionMargin,
        Content = content,
    };

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = DesignTokens.FontSizeCaption,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 520,
        Opacity = 0.8,
    };

    private static Control LabelledRow(string label, Control control)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*") };
        var text = new TextBlock
        {
            Text = label,
            Opacity = 0.8,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Grid.SetColumn(text, 0);
        Grid.SetColumn(control, 1);
        row.Children.Add(text);
        row.Children.Add(control);
        return row;
    }

    private static Control Readout(string label, string value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("160,*") };
        var name = new TextBlock
        {
            Text = label,
            Opacity = 0.8,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Top,
        };

        // Identifiers, units and timestamps are set in the mono face, the
        // role DesignTokens assigns it.
        var read = new TextBlock
        {
            Text = value,
            FontFamily = DesignTokens.MonoFont,
            FontSize = DesignTokens.FontSizeBody,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 520,
        };

        Grid.SetColumn(name, 0);
        Grid.SetColumn(read, 1);
        row.Children.Add(name);
        row.Children.Add(read);
        return row;
    }

    private static TextBox Input(string initial, string? watermark = null) => new()
    {
        Text = initial,
        Watermark = watermark,
        FontSize = DesignTokens.FontSizeBody,
        MinHeight = DesignTokens.MinControlSize,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
}
