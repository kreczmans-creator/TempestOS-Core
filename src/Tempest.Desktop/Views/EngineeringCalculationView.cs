using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Engineering;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views;

/// <summary>
/// The Engineering Calculation surface: choose a governed reference
/// material, enter the section inputs, run the real bracket section check,
/// and read the result beside the reference revision it stood on.
/// </summary>
/// <remarks>
/// <para>
/// <b>This view decides nothing.</b> Every answer it shows —  which
/// materials are held, whether one may be used, what the check produced,
/// what it stood on — is composed by
/// <see cref="BracketCalculationWorkbench"/> in <c>Tempest.App</c>. The
/// view collects text, hands it over, and renders what comes back. It
/// parses no quantity, applies no rule, and knows no formula: the same
/// discipline <see cref="ProjectRequirementsView"/> follows.
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

    /// <summary>The caption on the button that populates the material library from the shipped seed corpus.</summary>
    public const string PopulateCaption = "Populate Material Library";

    /// <summary>The caption on the button that performs the governed review and release.</summary>
    public const string ReleaseCaption = "Verify and Release Material";

    /// <summary>What the surface shows when no material is held at all.</summary>
    public const string EmptyLibraryGuidance =
        "No material records are held yet. Press \"" + PopulateCaption + "\" below to add the shipped reference records as Draft, "
        + "then select one and release it through review before it can be used for engineering work.";

    private readonly ComboBox _materialPicker = new()
    {
        FontSize = DesignTokens.FontSizeBody,
        MinHeight = DesignTokens.MinControlSize,
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };

    private readonly TextBox _loadBox = Input("12");
    private readonly TextBox _areaBox = Input("60");
    private readonly TextBox _lengthBox = Input("150");
    private readonly TextBox _massLimitBox = Input("50");

    private readonly TextBox _sourceConsultedBox = Input(string.Empty, "What you checked this record against");
    private readonly TextBox _releaseRationaleBox = Input(string.Empty, "Why it is being released");

    private readonly Button _calculateButton = new() { Content = CalculateCaption, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _populateButton = new() { Content = PopulateCaption, MinHeight = DesignTokens.MinControlSize };

    private readonly TextBlock _populateExplanation = new()
    {
        Text = "Adds the shipped reference material records to this library as Draft. "
            + "Nothing is released and nothing is approved by populating, and running it again "
            + "adds only what is missing — it never overwrites a record somebody has corrected.",
        FontSize = DesignTokens.FontSizeCaption,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 520,
        Opacity = 0.8,
    };
    private readonly Button _releaseButton = new() { Content = ReleaseCaption, MinHeight = DesignTokens.MinControlSize };

    private readonly StackPanel _validationPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _resultPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _traceabilityPanel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly TextBlock _materialState = new()
    {
        FontSize = DesignTokens.FontSizeCaption,
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = 520,
        Opacity = 0.85,
    };

    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8, TextWrapping = TextWrapping.Wrap, MaxWidth = 520 };

    /// <summary>Initialises a new instance of the <see cref="EngineeringCalculationView"/> class.</summary>
    public EngineeringCalculationView()
    {
        AutomationProperties.SetName(this, Heading);

        _calculateButton.Classes.Add(ChromeStyles.Primary);
        _populateButton.Classes.Add(ChromeStyles.Flat);
        _releaseButton.Classes.Add(ChromeStyles.Flat);

        AutomationProperties.SetName(_populateButton, PopulateCaption);
        ToolTip.SetTip(_populateButton, "Add the shipped reference material records to this library as Draft.");
        AutomationProperties.SetName(_releaseButton, ReleaseCaption);
        AutomationProperties.SetName(_calculateButton, CalculateCaption);
        AutomationProperties.SetName(_materialPicker, "Governed reference material");
        AutomationProperties.SetName(_loadBox, "Axial load in kilonewtons");
        AutomationProperties.SetName(_areaBox, "Section area in square millimetres");
        AutomationProperties.SetName(_lengthBox, "Member length in millimetres");
        AutomationProperties.SetName(_massLimitBox, "Mass limit in grams");
        AutomationProperties.SetName(_sourceConsultedBox, "Source consulted");
        AutomationProperties.SetName(_releaseRationaleBox, "Release rationale");
        AutomationProperties.SetLiveSetting(_statusMessage, AutomationLiveSetting.Polite);

        _calculateButton.Click += (_, _) => CalculateRequested?.Invoke();
        _populateButton.Click += (_, _) => PopulateRequested?.Invoke();
        _releaseButton.Click += (_, _) => ReleaseRequested?.Invoke();
        _materialPicker.SelectionChanged += (_, _) => ShowSelectedMaterialState();

        Content = BuildLayout();
    }

    /// <summary>Raised when the engineer asks for the calculation to run.</summary>
    public event Action? CalculateRequested;

    /// <summary>Raised when the engineer asks for the material library to be populated from the shipped seed corpus.</summary>
    public event Action? PopulateRequested;

    /// <summary>Raised when the engineer asks for the selected material to be verified and released.</summary>
    public event Action? ReleaseRequested;

    /// <summary>The materials currently offered, in the order they are offered.</summary>
    public IReadOnlyList<BracketMaterialOption> Materials { get; private set; } = [];

    /// <summary>The material currently selected, or <see langword="null"/> where none is.</summary>
    public BracketMaterialOption? SelectedMaterial => _materialPicker.SelectedItem as BracketMaterialOption;

    /// <summary>Whether the surface is telling the engineer the library is empty.</summary>
    public bool IsShowingEmptyLibrary => Materials.Count == 0;

    /// <summary>The outcome currently displayed, or <see langword="null"/> where none is.</summary>
    public BracketCalculationOutcome? DisplayedOutcome { get; private set; }

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

    /// <summary>Shows the governed materials on offer, keeping the current selection where it survives.</summary>
    /// <param name="materials">Every material the library holds.</param>
    /// <param name="selectRecordId">The record to select, where one should be.</param>
    public void ShowMaterials(IReadOnlyList<BracketMaterialOption> materials, string? selectRecordId = null)
    {
        ArgumentNullException.ThrowIfNull(materials);

        Materials = materials;
        var keep = selectRecordId ?? SelectedMaterial?.RecordId;

        _materialPicker.ItemsSource = materials;
        _materialPicker.SelectedItem = materials.FirstOrDefault(m => m.RecordId == keep)
            ?? materials.FirstOrDefault(m => m.IsUsableForEngineering)
            ?? materials.FirstOrDefault();

        // `IsVisible` is NOT set here, deliberately. This action used to be
        // hidden whenever the library held anything at all, which made the
        // one action the whole workflow starts with disappear the moment it
        // had ever been used — and made it invisible in any composition
        // that already held a material. The first manual review on Windows
        // could not find it. It is now always present and always says what
        // it does; running it twice is harmless, because seeding is
        // additive and idempotent.
        ShowSelectedMaterialState();
    }

    /// <summary>Shows an outcome — a result, a refusal, or a rejected input.</summary>
    /// <param name="outcome">What the workbench answered.</param>
    public void ShowOutcome(BracketCalculationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        DisplayedOutcome = outcome;
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
        _traceabilityPanel.Children.Add(Readout("Executed", $"{outcome.ExecutedAt:yyyy-MM-dd HH:mm:ss} UTC by {outcome.ExecutedByPrincipalId}"));
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

    /// <summary>Shows what just happened, as a status line.</summary>
    /// <param name="message">The message.</param>
    public void ShowStatus(string message) => _statusMessage.Text = message ?? string.Empty;

    /// <summary>Puts previously-entered figures back into the boxes, after recovering a stored calculation.</summary>
    /// <param name="inputs">The figures to restore.</param>
    public void RestoreInputs(BracketCalculationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        _loadBox.Text = inputs.LoadKilonewtons;
        _areaBox.Text = inputs.SectionAreaSquareMillimetres;
        _lengthBox.Text = inputs.MemberLengthMillimetres;
        _massLimitBox.Text = inputs.MassLimitGrams;
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

        // The governed review is offered only where it is the thing
        // standing between the engineer and a calculation — never as a
        // permanent button inviting a release nobody needs.
        _releaseButton.IsVisible = !selected.IsUsableForEngineering;
    }

    private Control BuildLayout()
    {
        var body = new StackPanel { Spacing = DesignTokens.SpaceMd, Margin = DesignTokens.PagePadding };

        body.Children.Add(PageHeading.Label("Engineering"));
        body.Children.Add(PageHeading.Title(Heading));
        body.Children.Add(PageHeading.Lead(
            "A first-order direct-stress and mass check on one bracket section, run against a released reference material. "
            + "The result records the reference revision it stood on, so it stays true after that reference moves on."));

        var reference = new StackPanel { Spacing = DesignTokens.SpaceSm };
        reference.Children.Add(LabelledRow("Material", _materialPicker));
        reference.Children.Add(_materialState);
        reference.Children.Add(_populateButton);
        reference.Children.Add(_populateExplanation);
        reference.Children.Add(LabelledRow("Source consulted", _sourceConsultedBox));
        reference.Children.Add(LabelledRow("Release rationale", _releaseRationaleBox));
        reference.Children.Add(_releaseButton);
        body.Children.Add(Section("Governed reference", reference));

        var inputs = new StackPanel { Spacing = DesignTokens.SpaceSm };
        inputs.Children.Add(LabelledRow("Load (kN)", _loadBox));
        inputs.Children.Add(LabelledRow("Section area (mm2)", _areaBox));
        inputs.Children.Add(LabelledRow("Member length (mm)", _lengthBox));
        inputs.Children.Add(LabelledRow("Mass limit (g)", _massLimitBox));
        inputs.Children.Add(_calculateButton);
        inputs.Children.Add(_validationPanel);
        inputs.Children.Add(_statusMessage);
        body.Children.Add(Section("Inputs", inputs));

        body.Children.Add(Section("Result", _resultPanel));
        body.Children.Add(Section("Traceability", _traceabilityPanel));

        _validationPanel.IsVisible = false;
        _resultPanel.IsVisible = false;
        _traceabilityPanel.IsVisible = false;

        return new ScrollViewer { Content = body };
    }

    private static Expander Section(string title, Control content) => new()
    {
        Header = title,
        IsExpanded = true,
        Margin = DesignTokens.SectionMargin,
        Content = content,
    };

    private static Control LabelledRow(string label, Control control)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("180,*") };
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
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("180,*") };
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
