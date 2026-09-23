using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.Materials;
using Tempest.Core.ReferenceData;
using Tempest.Core.UnitsAndQuantities;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views.EngineeringAssets;

/// <summary>
/// The bracket verification artefact, filled in from the Desktop
/// (`WP 21.2B`, `TD-165`): a form over <see cref="GovernedBracketCheckRequest"/>
/// (every input a quantity with a unit picker) — <b>Check</b> runs
/// <see cref="GovernedBracketCheckService"/>, the result and its
/// intermediates are shown with units and the pass/fail against the
/// governed limits, and <b>Record verification artefact</b> writes
/// through <see cref="BracketEngineeringRecordService"/> — the identical
/// Core path <c>BracketEngineeringDemonstrationTests</c> proves — so the
/// artefact lists under Verification artefacts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not the Engineering Calculations surface.</b> That rail entry, its
/// Calculations tab and the bracket workbench behind it
/// (<c>BracketCalculationWorkbench</c>) stay exactly as they are (Product
/// Owner guard): they run and name a calculation, and read the
/// verification artefact, but — by that class's own documented remarks —
/// never write into the calculation pack or the verification artefact.
/// This view is the surface that closes that gap: it calls the identical
/// <see cref="GovernedBracketCheckService"/> the workbench uses, but its
/// own <b>Record</b> action reaches
/// <see cref="BracketEngineeringRecordService.RecordCalculationAsync"/>
/// and <see cref="BracketEngineeringRecordService.RecordVerificationAsync"/>,
/// which nothing under <c>src/Tempest.Desktop</c> called before this Work
/// Package.
/// </para>
/// <para>
/// <b>Records into an existing pack and artefact; never invents one.</b>
/// Both write methods revise a record that must already be registered —
/// exactly the two the shipped demonstration corpus seeds
/// (<c>cpk-bracket-stress-check</c>, <c>ver-bracket-yield-margin</c>) —
/// so this form offers a picker over whatever the two libraries currently
/// hold rather than fabricating a target. Where neither library holds a
/// record yet, Record says so plainly rather than pretending to succeed.
/// </para>
/// </remarks>
public sealed class BracketVerificationView : UserControl
{
    private readonly IMaterialCatalog _materials;
    private readonly GovernedBracketCheckService _check;
    private readonly BracketEngineeringRecordService _records;
    private readonly ICalculationPackCatalog _packs;
    private readonly IVerificationArtefactCatalog _artefacts;
    private readonly Func<string?> _sessionPrincipalId;
    private readonly Func<Task> _onRecorded;

    private readonly ComboBox _materialBox = new() { MinHeight = DesignTokens.ControlSizeMedium };
    private readonly QuantityField<Force> _load = new("Applied load", ForceUnits.All);
    private readonly QuantityField<Area> _area = new("Section area", AreaUnits.All);
    private readonly QuantityField<Length> _length = new("Member length", LengthUnits.All);
    private readonly QuantityField<Mass> _massLimit = new("Mass limit", MassUnits.All);
    private readonly Button _checkButton = new() { Content = "Check", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _checkStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _resultPanel = new() { Spacing = DesignTokens.SpaceXs, Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceMd) };

    private readonly ComboBox _packBox = new() { MinHeight = DesignTokens.ControlSizeMedium };
    private readonly ComboBox _artefactBox = new() { MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBox _basis = new() { Watermark = "How the independent figures were obtained", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 60 };
    private readonly TextBox _independentMargin = new() { Watermark = "e.g. 0.30", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly QuantityField<Mass> _independentMass = new("Independent mass", MassUnits.All);
    private readonly TextBox _verifier = new() { Watermark = "Verifier", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBox _performedOn = new() { Watermark = "yyyy-mm-dd", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly Button _recordButton = new() { Content = "Record verification artefact", MinHeight = DesignTokens.ControlSizeMedium };
    private readonly TextBlock _recordStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.85, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly StackPanel _recordSection = new() { Spacing = DesignTokens.SpaceSm, Margin = new Thickness(0, DesignTokens.SpaceMd, 0, 0) };

    private GovernedBracketCheck? _lastCheck;
    private List<IReferenceRecord<MaterialDefinition>> _materialRecords = [];
    private List<IReferenceRecord<CalculationPack>> _packRecords = [];
    private List<IReferenceRecord<VerificationArtefact>> _artefactRecords = [];

    /// <summary>Raised after Check or Record completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Initialises a new instance of the <see cref="BracketVerificationView"/> class.</summary>
    /// <param name="materials">The Materials library the check resolves against.</param>
    /// <param name="check">The governed bracket section check.</param>
    /// <param name="records">Writes an executed, governed check into its calculation pack and verification artefact.</param>
    /// <param name="packs">The calculation pack library — offers the Record target.</param>
    /// <param name="artefacts">The verification artefact library — offers the Record target.</param>
    /// <param name="sessionPrincipalId">The signed-in principal's own stable identity id, read fresh — defaults the Verifier field.</param>
    /// <param name="onRecorded">Called after a successful Record, so the owning area can refresh its own Calculation packs / Verification artefacts / Engineering evidence lists.</param>
    public BracketVerificationView(
        IMaterialCatalog materials,
        GovernedBracketCheckService check,
        BracketEngineeringRecordService records,
        ICalculationPackCatalog packs,
        IVerificationArtefactCatalog artefacts,
        Func<string?> sessionPrincipalId,
        Func<Task> onRecorded)
    {
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(artefacts);
        ArgumentNullException.ThrowIfNull(sessionPrincipalId);
        ArgumentNullException.ThrowIfNull(onRecorded);

        _materials = materials;
        _check = check;
        _records = records;
        _packs = packs;
        _artefacts = artefacts;
        _sessionPrincipalId = sessionPrincipalId;
        _onRecorded = onRecorded;

        AutomationProperties.SetName(this, "Bracket verification");
        AutomationProperties.SetName(_materialBox, "Material");
        AutomationProperties.SetName(_checkButton, "Check");
        AutomationProperties.SetName(_packBox, "Calculation pack");
        AutomationProperties.SetName(_artefactBox, "Verification artefact");
        AutomationProperties.SetName(_basis, "Independent basis");
        AutomationProperties.SetName(_independentMargin, "Independent margin");
        AutomationProperties.SetName(_verifier, "Verifier");
        AutomationProperties.SetName(_performedOn, "Performed on");
        AutomationProperties.SetName(_recordButton, "Record verification artefact");

        _checkButton.Classes.Add(ChromeStyles.Primary);
        _checkButton.Click += async (_, _) => await OnCheckAsync().ConfigureAwait(true);

        _recordButton.Classes.Add(ChromeStyles.Primary);
        _recordButton.Click += async (_, _) => await OnRecordAsync().ConfigureAwait(true);

        Content = new ScrollViewer { Content = Build() };
    }

    private Control Build()
    {
        var body = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceSm };

        body.Children.Add(new TextBlock { Text = "Bracket verification", FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading });
        body.Children.Add(new TextBlock
        {
            Text = "Runs the governed bracket section check (TDE-CPK-001's own method) and records it into an "
                + "existing calculation pack and verification artefact. Meeting a criterion is not design approval.",
            FontSize = DesignTokens.FontSizeCaption, Opacity = 0.75, TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        var materialRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, DesignTokens.SpaceSm, 0, DesignTokens.SpaceSm) };
        var materialCaption = new TextBlock { Text = "Material", VerticalAlignment = VerticalAlignment.Center, Width = 190, FontSize = DesignTokens.FontSizeBody };
        Grid.SetColumn(materialCaption, 0);
        Grid.SetColumn(_materialBox, 1);
        materialRow.Children.Add(materialCaption);
        materialRow.Children.Add(_materialBox);
        body.Children.Add(materialRow);

        body.Children.Add(_load.BuildRow());
        body.Children.Add(_area.BuildRow());
        body.Children.Add(_length.BuildRow());
        body.Children.Add(_massLimit.BuildRow());
        body.Children.Add(_checkButton);
        body.Children.Add(_checkStatus);
        body.Children.Add(_resultPanel);

        _recordSection.Children.Add(new TextBlock { Text = "Record", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeHeading });

        var packRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var packCaption = new TextBlock { Text = "Calculation pack", VerticalAlignment = VerticalAlignment.Center, Width = 190, FontSize = DesignTokens.FontSizeBody };
        Grid.SetColumn(packCaption, 0);
        Grid.SetColumn(_packBox, 1);
        packRow.Children.Add(packCaption);
        packRow.Children.Add(_packBox);
        _recordSection.Children.Add(packRow);

        var artefactRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var artefactCaption = new TextBlock { Text = "Verification artefact", VerticalAlignment = VerticalAlignment.Center, Width = 190, FontSize = DesignTokens.FontSizeBody };
        Grid.SetColumn(artefactCaption, 0);
        Grid.SetColumn(_artefactBox, 1);
        artefactRow.Children.Add(artefactCaption);
        artefactRow.Children.Add(_artefactBox);
        _recordSection.Children.Add(artefactRow);

        _recordSection.Children.Add(new TextBlock { Text = "Independent check", FontWeight = DesignTokens.WeightHeading, FontSize = DesignTokens.FontSizeBody + 1 });
        _recordSection.Children.Add(new TextBlock
        {
            Text = "Figures obtained separately — on paper, in a spreadsheet, from a textbook — never by re-running this calculation.",
            FontSize = DesignTokens.FontSizeCaption, Opacity = 0.75, TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        _recordSection.Children.Add(_basis);
        _recordSection.Children.Add(_independentMargin);
        _recordSection.Children.Add(_independentMass.BuildRow());

        var verifierRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 0, 0, DesignTokens.SpaceSm) };
        var verifierCaption = new TextBlock { Text = "Verifier", VerticalAlignment = VerticalAlignment.Center, Width = 190, FontSize = DesignTokens.FontSizeBody };
        Grid.SetColumn(verifierCaption, 0);
        Grid.SetColumn(_verifier, 1);
        verifierRow.Children.Add(verifierCaption);
        verifierRow.Children.Add(_verifier);
        _recordSection.Children.Add(verifierRow);

        var dateRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 0, 0, DesignTokens.SpaceSm) };
        var dateCaption = new TextBlock { Text = "Performed on", VerticalAlignment = VerticalAlignment.Center, Width = 190, FontSize = DesignTokens.FontSizeBody };
        Grid.SetColumn(dateCaption, 0);
        Grid.SetColumn(_performedOn, 1);
        dateRow.Children.Add(dateCaption);
        dateRow.Children.Add(_performedOn);
        _recordSection.Children.Add(dateRow);

        _recordSection.Children.Add(_recordButton);
        _recordSection.Children.Add(_recordStatus);
        _recordSection.IsVisible = false;

        body.Children.Add(_recordSection);

        return body;
    }

    /// <summary>Reloads the material, calculation pack and verification artefact pickers.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _materialRecords = [.. (await _materials.ListAsync(cancellationToken).ConfigureAwait(true))
            .OrderBy(r => r.Definition.Designation ?? r.Definition.Name, StringComparer.Ordinal)];
        _materialBox.ItemsSource = _materialRecords
            .Select(r => $"{r.Id} — {r.Definition.Designation ?? r.Definition.Name} ({r.ValidationState})")
            .ToList();

        _packRecords = [.. (await _packs.ListAsync(cancellationToken).ConfigureAwait(true))
            .OrderBy(r => r.Id, StringComparer.Ordinal)];
        _packBox.ItemsSource = _packRecords.Select(r => $"{r.Id} — {r.Definition.Reference} — {r.Definition.Title}").ToList();

        _artefactRecords = [.. (await _artefacts.ListAsync(cancellationToken).ConfigureAwait(true))
            .OrderBy(r => r.Id, StringComparer.Ordinal)];
        _artefactBox.ItemsSource = _artefactRecords.Select(r => $"{r.Id} — {r.Definition.Reference} — {r.Definition.Subject}").ToList();

        if (string.IsNullOrWhiteSpace(_verifier.Text))
            _verifier.Text = _sessionPrincipalId() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(_performedOn.Text))
            _performedOn.Text = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private async Task OnCheckAsync()
    {
        _resultPanel.Children.Clear();
        _recordSection.IsVisible = false;
        _lastCheck = null;

        if (_materialBox.SelectedIndex < 0 || _materialBox.SelectedIndex >= _materialRecords.Count)
        {
            Report(_checkStatus, "Select a governed material first.", succeeded: false);
            return;
        }

        var problems = new List<string>();
        if (!_load.TryGetValue(out var load, out var loadProblem)) problems.Add(loadProblem!);
        if (!_area.TryGetValue(out var area, out var areaProblem)) problems.Add(areaProblem!);
        if (!_length.TryGetValue(out var length, out var lengthProblem)) problems.Add(lengthProblem!);
        if (!_massLimit.TryGetValue(out var massLimit, out var massLimitProblem)) problems.Add(massLimitProblem!);

        if (problems.Count > 0)
        {
            Report(_checkStatus, string.Join(" ", problems), succeeded: false);
            return;
        }

        var materialRecordId = _materialRecords[_materialBox.SelectedIndex].Id;
        var request = new GovernedBracketCheckRequest(materialRecordId, load, area, length, massLimit);

        var check = await _check.CheckAsync(request).ConfigureAwait(true);
        _lastCheck = check;

        if (!check.WasPerformed)
        {
            Report(_checkStatus, $"Refused: {check.Reason}", succeeded: false);
            return;
        }

        var result = check.Result!;

        AddResultLine($"Applied stress: {Format(result.AppliedStress.ConvertTo(PressureUnits.Megapascal).Value, "MPa")} "
            + $"against allowable {Format(result.AllowableStress.ConvertTo(PressureUnits.Megapascal).Value, "MPa")} "
            + $"— {(result.StressCriterionMet ? "meets" : "does not meet")} the stress criterion.");
        AddResultLine($"Margin of safety: {result.StressMargin.ToString("0.####", CultureInfo.InvariantCulture)}");
        AddResultLine($"Density used: {Format(result.Density.ConvertTo(MassDensityUnits.KilogramPerCubicMetre).Value, "kg/m3")}");
        AddResultLine($"Estimated mass: {Format(result.EstimatedMass.ConvertTo(MassUnits.Kilogram).Value, "kg")} "
            + $"against limit {Format(result.MassLimit.ConvertTo(MassUnits.Kilogram).Value, "kg")} "
            + $"— {(result.MassCriterionMet ? "meets" : "does not meet")} the mass criterion.");
        AddResultLine($"Outcome: {(result.Outcome == BracketCheckOutcome.MeetsCriteria ? "Meets criteria" : "Does not meet criteria")} "
            + "(an engineering finding, not an approval).");
        AddResultLine($"Material basis: {result.MaterialPin.Library}/{result.MaterialPin.RecordId} revision {result.MaterialPin.RevisionNumber}.");

        Report(_checkStatus, "Check complete. Fill in the independent check below, then Record.", succeeded: true);

        _recordSection.IsVisible = true;

        if (_packRecords.Count == 0)
            Report(_recordStatus, "No calculation pack is registered yet — register one before recording.", succeeded: false);
        else if (_artefactRecords.Count == 0)
            Report(_recordStatus, "No verification artefact is registered yet — register one before recording.", succeeded: false);
    }

    private void AddResultLine(string text) =>
        _resultPanel.Children.Add(new TextBlock { Text = text, FontSize = DesignTokens.FontSizeBody, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

    private async Task OnRecordAsync()
    {
        if (_lastCheck is not { WasPerformed: true } check)
        {
            Report(_recordStatus, "Run Check first — there is nothing performed to record.", succeeded: false);
            return;
        }

        if (_packBox.SelectedIndex < 0 || _packBox.SelectedIndex >= _packRecords.Count)
        {
            Report(_recordStatus, "Select the calculation pack to record this check into.", succeeded: false);
            return;
        }

        if (_artefactBox.SelectedIndex < 0 || _artefactBox.SelectedIndex >= _artefactRecords.Count)
        {
            Report(_recordStatus, "Select the verification artefact to record this check into.", succeeded: false);
            return;
        }

        if (string.IsNullOrWhiteSpace(_basis.Text))
        {
            Report(_recordStatus, "State the basis of the independent check.", succeeded: false);
            return;
        }

        if (!double.TryParse(_independentMargin.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var independentMargin))
        {
            Report(_recordStatus, "Independent margin must be a number.", succeeded: false);
            return;
        }

        if (!_independentMass.TryGetValue(out var independentMass, out var massProblem))
        {
            Report(_recordStatus, massProblem!, succeeded: false);
            return;
        }

        if (string.IsNullOrWhiteSpace(_verifier.Text))
        {
            Report(_recordStatus, "Name the verifier.", succeeded: false);
            return;
        }

        if (!DateOnly.TryParseExact(_performedOn.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var performedOn))
        {
            Report(_recordStatus, "Performed on must be a date, as yyyy-mm-dd.", succeeded: false);
            return;
        }

        var packRecordId = _packRecords[_packBox.SelectedIndex].Id;
        var packReference = _packRecords[_packBox.SelectedIndex].Definition.Reference;
        var artefactRecordId = _artefactRecords[_artefactBox.SelectedIndex].Id;

        try
        {
            await _records.RecordCalculationAsync(packRecordId, check).ConfigureAwait(true);

            var artefact = await _records.RecordVerificationAsync(
                artefactRecordId,
                check,
                new IndependentCheck(_basis.Text!.Trim(), independentMargin, independentMass),
                _verifier.Text!.Trim(),
                performedOn,
                calculationPackReference: packReference).ConfigureAwait(true);

            Report(_recordStatus,
                $"Recorded. Verification artefact '{artefact.Definition.Reference}' now stands at "
                + $"{artefact.Definition.Result!.Standing}.",
                succeeded: true);

            await RefreshAsync().ConfigureAwait(true);
            await _onRecorded().ConfigureAwait(true);
        }
        catch (ReferenceRecordNotFoundException ex)
        {
            Report(_recordStatus, ex.Message, succeeded: false);
        }
        catch (ArgumentException ex)
        {
            Report(_recordStatus, ex.Message, succeeded: false);
        }
    }

    private static string Format(double value, string unit) =>
        $"{value.ToString("0.######", CultureInfo.InvariantCulture)} {unit}";

    private void Report(TextBlock target, string message, bool succeeded)
    {
        target.Text = message;
        ActionCompleted?.Invoke(message, ActionOutcome.From(succeeded));
    }
}
