using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Theming;

namespace Tempest.Desktop.Views.EngineeringAssets;

/// <summary>
/// Engineering Assets → Templates (`WP 21.2B`, `TD-160`): every registered
/// <see cref="EngineeringTemplate"/>, each opened beside the list with its
/// own structure, applicability and validation.
/// </summary>
public sealed class EngineeringTemplateListView : UserControl
{
    private readonly ITemplateCatalog _templates;
    private readonly ITemplateValidationService _validation;

    private readonly TextBox _filter = new() { Watermark = "Filter", MinHeight = DesignTokens.MinControlSize };
    private readonly StackPanel _rows = new() { Spacing = DesignTokens.SpaceXs };
    private readonly DockPanel _container = new();
    private readonly ScrollViewer _listScroll;
    private readonly ScrollViewer _detailScroll;
    private readonly Button _backButton = new() { Content = "← Back to Templates", MinHeight = DesignTokens.MinControlSize };
    private readonly StackPanel _detailsBody = new() { Spacing = DesignTokens.SpaceXs };

    private IReadOnlyList<IReferenceRecord<EngineeringTemplate>> _all = [];
    private string? _openRecordId;
    private bool _compact;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Initialises a new instance of the <see cref="EngineeringTemplateListView"/> class.</summary>
    public EngineeringTemplateListView(ITemplateCatalog templates, ITemplateValidationService validation)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(validation);

        _templates = templates;
        _validation = validation;

        AutomationProperties.SetName(_filter, "Filter templates");
        _filter.TextChanged += (_, _) => Render();

        var listBody = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        listBody.Children.Add(new TextBlock { Text = "Templates", FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading });
        listBody.Children.Add(_filter);
        listBody.Children.Add(_rows);
        _listScroll = new ScrollViewer { Content = listBody };

        _backButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_backButton, "Back to Templates");
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

    /// <summary>Reloads every registered template.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _all = await _templates.ListAsync(cancellationToken).ConfigureAwait(true);
        Render();

        if (_openRecordId is { } id && _all.All(r => r.Id != id))
            CloseRecord();
    }

    /// <summary>Opens one template's own detail pane — the same navigation a row's own Open button uses.</summary>
    public Task OpenAsync(string recordId) => OpenRecordAsync(recordId);

    private void Render()
    {
        var rows = _all.Select(r => new AssetListRow(
            r.Id,
            $"{r.Id} — {r.Definition.Reference} — {r.Definition.Name}  ·  {r.Definition.Kind}  ·  rev {r.RevisionNumber}  ·  {r.ValidationState}"));

        EngineeringAssetListBuilder.Render(_rows, rows, _filter.Text, id => _ = OpenRecordAsync(id));
    }

    private async Task OpenRecordAsync(string recordId)
    {
        var record = await _templates.FindAsync(recordId).ConfigureAwait(true);
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

    private void BuildDetails(IReferenceRecord<EngineeringTemplate> record)
    {
        var template = record.Definition;
        _detailsBody.Children.Clear();

        Add($"Reference: {template.Reference}", heading: true);
        Add($"Name: {template.Name}");
        Add($"Purpose: {template.Purpose}");
        Add($"Kind: {template.Kind}");

        Add("Sections:", heading: true);
        foreach (var section in template.Sections)
            Add($"  {section.Title}{(section.IsMandatory ? " (mandatory)" : string.Empty)} — {section.Fields.Count} field(s), {section.Subsections.Count} subsection(s)");

        if (!string.IsNullOrWhiteSpace(template.Instructions))
            Add($"Instructions: {template.Instructions}");

        if (!string.IsNullOrWhiteSpace(template.SupersedesReference))
            Add($"Supersedes: {template.SupersedesReference}");

        if (!string.IsNullOrWhiteSpace(template.Notes))
            Add($"Notes: {template.Notes}");

        Add("Applicability:", heading: true);
        Add($"  {EngineeringAssetFormatting.DescribeApplicability(template.Applicability)}");

        Add("Governance:", heading: true);
        foreach (var line in EngineeringAssetFormatting.DescribeGovernance(template.Governance))
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
