using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.EngineeringAssets.Verification;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views.EngineeringAssets;

/// <summary>
/// Engineering Assets → Verification artefacts (`WP 21.2B`, `TD-160`,
/// `TD-165`): every registered <see cref="VerificationArtefact"/>, each
/// opened beside the list with its own result, evidence, applicability
/// and validation. The bracket verification artefact
/// (<c>ver-bracket-yield-margin</c>) lists here once the Bracket
/// verification tab has recorded it.
/// </summary>
public sealed class VerificationArtefactListView : UserControl
{
    private readonly IVerificationArtefactCatalog _artefacts;
    private readonly IVerificationArtefactValidationService _validation;

    private readonly TextBox _filter = new() { Watermark = "Filter", MinHeight = DesignTokens.MinControlSize };
    private readonly StackPanel _rows = new() { Spacing = DesignTokens.SpaceXs };
    private readonly DockPanel _container = new();
    private readonly ScrollViewer _listScroll;
    private readonly ScrollViewer _detailScroll;
    private readonly Button _backButton = new() { Content = "← Back to Verification artefacts", MinHeight = DesignTokens.MinControlSize };
    private readonly StackPanel _detailsBody = new() { Spacing = DesignTokens.SpaceXs };

    private IReadOnlyList<IReferenceRecord<VerificationArtefact>> _all = [];
    private string? _openRecordId;
    private bool _compact;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Initialises a new instance of the <see cref="VerificationArtefactListView"/> class.</summary>
    public VerificationArtefactListView(IVerificationArtefactCatalog artefacts, IVerificationArtefactValidationService validation)
    {
        ArgumentNullException.ThrowIfNull(artefacts);
        ArgumentNullException.ThrowIfNull(validation);

        _artefacts = artefacts;
        _validation = validation;

        AutomationProperties.SetName(_filter, "Filter verification artefacts");
        _filter.TextChanged += (_, _) => Render();

        var listBody = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        listBody.Children.Add(new TextBlock { Text = "Verification artefacts", FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading });
        listBody.Children.Add(_filter);
        listBody.Children.Add(_rows);
        _listScroll = new ScrollViewer { Content = listBody };

        _backButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_backButton, "Back to Verification artefacts");
        _backButton.Click += (_, _) => CloseRecord();

        var detailBody = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        detailBody.Children.Add(_backButton);
        detailBody.Children.Add(_detailsBody);
        _detailScroll = new ScrollViewer { Content = detailBody };

        DockPanel.SetDock(_listScroll, Dock.Left);
        _container.Children.Add(_listScroll);
        _container.Children.Add(_detailScroll);
        Content = _container;
        UpdateLayoutMode();
    }

    /// <summary>Below <see cref="DesignTokens.CompactShellWidth"/> the open record replaces the list (with Back) rather than sitting beside it.</summary>
    public void SetCompact(bool compact)
    {
        if (_compact == compact)
            return;

        _compact = compact;
        UpdateLayoutMode();
    }

    private void UpdateLayoutMode()
    {
        var hasOpenRecord = _openRecordId is not null;
        var sideBySide = hasOpenRecord && !_compact;
        var detailOnly = hasOpenRecord && _compact;

        _listScroll.IsVisible = !detailOnly;
        _detailScroll.IsVisible = hasOpenRecord;
        _backButton.IsVisible = detailOnly;
        _listScroll.Width = sideBySide ? 480 : double.NaN;
    }

    /// <summary>Reloads every registered verification artefact.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _all = await _artefacts.ListAsync(cancellationToken).ConfigureAwait(true);
        Render();

        if (_openRecordId is { } id && _all.All(r => r.Id != id))
            CloseRecord();
        else if (_openRecordId is { } openId)
            await OpenRecordAsync(openId).ConfigureAwait(true);
    }

    /// <summary>Opens one artefact's own detail pane — the same navigation a row's own Open button uses.</summary>
    public Task OpenAsync(string recordId) => OpenRecordAsync(recordId);

    private void Render()
    {
        var rows = _all.Select(r => new AssetListRow(
            r.Id,
            $"{r.Definition.Reference} — {r.Definition.Subject}  ·  {r.Definition.Standing}  ·  rev {r.RevisionNumber}  ·  {r.ValidationState}"));

        EngineeringAssetListBuilder.Render(_rows, rows, _filter.Text, id => _ = OpenRecordAsync(id));
    }

    private async Task OpenRecordAsync(string recordId)
    {
        var record = await _artefacts.FindAsync(recordId).ConfigureAwait(true);
        if (record is null)
        {
            ActionCompleted?.Invoke($"'{recordId}' is no longer registered.", ActionOutcome.Failed);
            return;
        }

        _openRecordId = recordId;
        UpdateLayoutMode();
        BuildDetails(record);
    }

    private void CloseRecord()
    {
        _openRecordId = null;
        UpdateLayoutMode();
    }

    private void BuildDetails(IReferenceRecord<VerificationArtefact> record)
    {
        var artefact = record.Definition;
        _detailsBody.Children.Clear();

        Add($"Reference: {artefact.Reference}", heading: true);
        Add($"Requirement: {artefact.Requirement.RequirementId}"
            + (artefact.Requirement.RequirementIdentifier is { } id ? $" ({id})" : string.Empty));
        Add($"Subject: {artefact.Subject}");
        Add($"Method: {artefact.Method}" + (artefact.MethodDescription is { } desc ? $" — {desc}" : string.Empty));

        if (artefact.AcceptanceCriteria.Count > 0)
            Add($"Acceptance criteria: {string.Join("; ", artefact.AcceptanceCriteria)}");

        Add($"Standing: {artefact.Standing}", heading: true);

        if (artefact.Result is { } result)
        {
            Add($"  {result.Summary}");
            Add($"  Performed by: {result.PerformedByPrincipalId ?? "not named"}"
                + (result.PerformedOn is { } on ? $" on {on:yyyy-MM-dd}" : string.Empty));

            if (result.CalculationPackReference is { } packRef)
                Add($"  Calculation pack: {packRef}");
        }
        else if (!string.IsNullOrWhiteSpace(artefact.NotApplicableReason))
        {
            Add($"  Not applicable: {artefact.NotApplicableReason}");
        }
        else
        {
            Add("  Nothing has been performed yet.");
        }

        if (artefact.Evidence.Count > 0)
        {
            Add("Evidence:", heading: true);
            foreach (var evidence in artefact.Evidence)
                Add($"  {evidence.Kind} — {evidence.Description} ({(evidence.IsLocatable ? "locatable" : "not locatable")})");
        }

        if (artefact.SourcePins.Count > 0)
            Add($"Source pins: {string.Join("; ", artefact.SourcePins.Select(p => $"{p.Library}/{p.RecordId} r{p.RevisionNumber}"))}");

        if (!string.IsNullOrWhiteSpace(artefact.Notes))
            Add($"Notes: {artefact.Notes}");

        Add("Applicability:", heading: true);
        Add($"  {EngineeringAssetFormatting.DescribeApplicability(artefact.Applicability)}");

        Add("Governance:", heading: true);
        foreach (var line in EngineeringAssetFormatting.DescribeGovernance(artefact.Governance))
            Add($"  {line}");

        Add("Validation:", heading: true);
        _ = RenderValidationAsync(record.Id);
    }

    private async Task RenderValidationAsync(string recordId)
    {
        var result = await _validation.ValidateAsync(recordId).ConfigureAwait(true);

        if (_openRecordId != recordId)
            return;

        foreach (var line in EngineeringAssetFormatting.DescribeValidation(result))
            Add($"  {line}");
    }

    private void Add(string text, bool heading = false) =>
        _detailsBody.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = heading ? DesignTokens.FontSizeBody + 1 : DesignTokens.FontSizeBody,
            FontWeight = heading ? DesignTokens.WeightHeading : Avalonia.Media.FontWeight.Normal,
            Margin = new Thickness(0, heading ? DesignTokens.SpaceSm : 0, 0, 0),
        });
}
