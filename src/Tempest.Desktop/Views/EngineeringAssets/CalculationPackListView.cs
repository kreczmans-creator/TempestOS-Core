using System.IO;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.EngineeringAssets.CalculationPacks;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Engineering;
using Tempest.Workspace.Files;

namespace Tempest.Desktop.Views.EngineeringAssets;

/// <summary>
/// Engineering Assets → Calculation packs (`WP 21.2B`, `TD-160`): every
/// registered <see cref="CalculationPack"/>, each opened beside the list
/// into its own <b>Details</b> (fields, applicability, governance and
/// validation) and <b>Trace</b> tab — the pack's own
/// <see cref="CalculationTrace"/> (`TD-160`'s own second named gap:
/// "rendered nowhere"), read-only, exported as text through the file
/// picker.
/// </summary>
public sealed class CalculationPackListView : UserControl
{
    private readonly ICalculationPackCatalog _packs;
    private readonly ICalculationPackValidationService _validation;
    private readonly IEngineeringTraceRegister _trace;
    private readonly IFilePicker _filePicker;

    private readonly TextBox _filter = new() { Watermark = "Filter", MinHeight = DesignTokens.MinControlSize };
    private readonly StackPanel _rows = new() { Spacing = DesignTokens.SpaceXs };
    private readonly DockPanel _container = new();
    private readonly ScrollViewer _listScroll;
    private readonly ScrollViewer _detailScroll;
    private readonly Button _backButton = new() { Content = "← Back to Calculation packs", MinHeight = DesignTokens.MinControlSize };
    private readonly StackPanel _detailsBody = new() { Spacing = DesignTokens.SpaceXs };
    private readonly StackPanel _traceBody = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _exportTraceButton = new() { Content = "Export trace", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _detailStatus = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };

    private IReadOnlyList<IReferenceRecord<CalculationPack>> _all = [];
    private string? _openRecordId;
    private CalculationTrace? _openTrace;
    private bool _compact;

    /// <summary>Raised after an action completes — mirrors every other Desktop View's own <c>ActionCompleted</c> convention (`TD-58`).</summary>
    public event Action<string, ActionOutcome>? ActionCompleted;

    /// <summary>Initialises a new instance of the <see cref="CalculationPackListView"/> class.</summary>
    public CalculationPackListView(
        ICalculationPackCatalog packs, ICalculationPackValidationService validation, IEngineeringTraceRegister trace, IFilePicker filePicker)
    {
        ArgumentNullException.ThrowIfNull(packs);
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(trace);
        ArgumentNullException.ThrowIfNull(filePicker);

        _packs = packs;
        _validation = validation;
        _trace = trace;
        _filePicker = filePicker;

        AutomationProperties.SetName(_filter, "Filter calculation packs");
        _filter.TextChanged += (_, _) => Render();

        var listBody = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        listBody.Children.Add(new TextBlock { Text = "Calculation packs", FontFamily = DesignTokens.TitleFont, FontSize = DesignTokens.FontSizeTitle, FontWeight = DesignTokens.WeightHeading });
        listBody.Children.Add(_filter);
        listBody.Children.Add(_rows);
        _listScroll = new ScrollViewer { Content = listBody };

        _backButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_backButton, "Back to Calculation packs");
        _backButton.Click += (_, _) => CloseRecord();

        _exportTraceButton.Classes.Add(ChromeStyles.Subtle);
        AutomationProperties.SetName(_exportTraceButton, "Export trace");
        _exportTraceButton.Click += async (_, _) => await OnExportTraceAsync().ConfigureAwait(true);

        var detailsTab = new TabItem { Header = "Details", Content = new ScrollViewer { Content = _detailsBody } };
        var traceTab = new TabItem { Header = "Trace", Content = new ScrollViewer { Content = _traceBody } };
        AutomationProperties.SetName(detailsTab, "Details");
        AutomationProperties.SetName(traceTab, "Trace");

        var detailTabs = new TabControl();
        detailTabs.Items.Add(detailsTab);
        detailTabs.Items.Add(traceTab);

        var detailBody = new StackPanel { Margin = DesignTokens.PanelPadding, Spacing = DesignTokens.SpaceMd };
        detailBody.Children.Add(_backButton);
        detailBody.Children.Add(_detailStatus);
        detailBody.Children.Add(detailTabs);
        _detailScroll = new ScrollViewer { Content = detailBody };

        DockPanel.SetDock(_listScroll, Dock.Left);
        _container.Children.Add(_listScroll);
        _container.Children.Add(_detailScroll);
        Content = _container;
        UpdateLayoutMode();
    }

    /// <summary>Below <see cref="DesignTokens.CompactShellWidth"/> the open record replaces the list (with Back) rather than sitting beside it — the same threshold every other master/detail surface in this area folds at.</summary>
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

    /// <summary>Reloads every registered calculation pack.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _all = await _packs.ListAsync(cancellationToken).ConfigureAwait(true);
        Render();

        if (_openRecordId is { } id && _all.All(r => r.Id != id))
            CloseRecord();
    }

    /// <summary>Opens one calculation pack's own Details/Trace pane — the same navigation a row's own Open button uses.</summary>
    public Task OpenAsync(string recordId) => OpenRecordAsync(recordId);

    private void Render()
    {
        var rows = _all.Select(r => new AssetListRow(
            r.Id,
            $"{r.Definition.Reference} — {r.Definition.Title}  ·  {r.Definition.Method.Kind}  ·  rev {r.RevisionNumber}  ·  {r.ValidationState}"));

        EngineeringAssetListBuilder.Render(_rows, rows, _filter.Text, id => _ = OpenRecordAsync(id));
    }

    private async Task OpenRecordAsync(string recordId)
    {
        var record = await _packs.FindAsync(recordId).ConfigureAwait(true);
        if (record is null)
        {
            _detailStatus.Text = $"'{recordId}' is no longer registered.";
            ActionCompleted?.Invoke(_detailStatus.Text, ActionOutcome.Failed);
            return;
        }

        _openRecordId = recordId;
        UpdateLayoutMode();

        _detailStatus.Text = string.Empty;
        BuildDetails(record);

        _openTrace = await _trace.TraceCalculationAsync(recordId).ConfigureAwait(true);
        BuildTrace(record, _openTrace);
    }

    private void CloseRecord()
    {
        _openRecordId = null;
        UpdateLayoutMode();
    }

    private void BuildDetails(IReferenceRecord<CalculationPack> record)
    {
        var pack = record.Definition;
        _detailsBody.Children.Clear();

        Add(_detailsBody, $"Reference: {pack.Reference}", heading: true);
        Add(_detailsBody, $"Title: {pack.Title}");
        Add(_detailsBody, $"Purpose: {pack.Purpose}");
        Add(_detailsBody, $"Method: {pack.Method.Kind} — {pack.Method.Description}");

        if (pack.Method.GoverningEquations.Count > 0)
            Add(_detailsBody, $"Equations: {string.Join("; ", pack.Method.GoverningEquations)}");

        Add(_detailsBody, "Inputs:", heading: true);
        foreach (var input in pack.Inputs)
            Add(_detailsBody, $"  {input.Reference} — {input.Description}: {input.Value}"
                + (input.SourcePin is { } pin ? $" [{pin.Library}/{pin.RecordId} r{pin.RevisionNumber}]" : string.Empty));

        Add(_detailsBody, "Outputs:", heading: true);
        foreach (var output in pack.Outputs)
            Add(_detailsBody, $"  {output.Reference} — {output.Description}: {output.Value}"
                + (output.HasAcceptanceCriterion ? $" ({output.AcceptanceCriterion})" : string.Empty));

        if (pack.Assumptions.Count > 0)
        {
            Add(_detailsBody, "Assumptions:", heading: true);
            foreach (var assumption in pack.Assumptions)
                Add(_detailsBody, $"  {assumption.Statement}");
        }

        if (pack.Limitations.Count > 0)
        {
            Add(_detailsBody, "Limitations:", heading: true);
            foreach (var limitation in pack.Limitations)
                Add(_detailsBody, $"  {limitation}");
        }

        if (pack.TemplateUsage is { } usage)
            Add(_detailsBody, $"Recorded on template: {usage.TemplatePin.RecordId} r{usage.TemplatePin.RevisionNumber} — {usage.UsedForDescription}");

        if (!string.IsNullOrWhiteSpace(pack.Notes))
            Add(_detailsBody, $"Notes: {pack.Notes}");

        Add(_detailsBody, "Applicability:", heading: true);
        Add(_detailsBody, $"  {EngineeringAssetFormatting.DescribeApplicability(pack.Applicability)}");

        Add(_detailsBody, "Governance:", heading: true);
        foreach (var line in EngineeringAssetFormatting.DescribeGovernance(pack.Governance))
            Add(_detailsBody, $"  {line}");

        Add(_detailsBody, "Validation:", heading: true);
        _ = RenderValidationAsync(record.Id);
    }

    private async Task RenderValidationAsync(string recordId)
    {
        var result = await _validation.ValidateAsync(recordId).ConfigureAwait(true);

        // A record opened since may already have moved on; only render
        // into the panel if this is still the open one.
        if (_openRecordId != recordId)
            return;

        foreach (var line in EngineeringAssetFormatting.DescribeValidation(result))
            Add(_detailsBody, $"  {line}");
    }

    private void BuildTrace(IReferenceRecord<CalculationPack> record, CalculationTrace? trace)
    {
        _traceBody.Children.Clear();

        Add(_traceBody, $"Definition: {record.Definition.Reference}  ·  revision {record.RevisionNumber}"
            + (record.Definition.Method.IsPlatformCalculation ? $"  ·  calculation definition {record.Definition.Method.CalculationDefinitionId}" : string.Empty), heading: true);

        if (trace is null)
        {
            Add(_traceBody, "No trace is available for this pack.");
            _exportTraceButton.IsVisible = false;
            return;
        }

        _exportTraceButton.IsVisible = true;
        _traceBody.Children.Add(_exportTraceButton);

        Add(_traceBody, "Inputs traced:", heading: true);
        foreach (var input in trace.Inputs)
            Add(_traceBody, $"  {input.Reference} — {input.Description}: {input.Value}"
                + (input.TracedTo is { } to ? $" → {to.Citation}" : " (untraceable — no governed reference pinned)"));

        if (trace.TemplateUsed is { } template)
            Add(_traceBody, $"Template used: {template.Citation}");

        Add(_traceBody, "Result (outputs):", heading: true);
        foreach (var output in trace.Outputs)
            Add(_traceBody, $"  {output}");

        Add(_traceBody, trace.IsFullyResolved
            ? "Every reference this calculation names was found."
            : $"{trace.DanglingReferences.Count} reference(s) could not be resolved.", heading: true);

        if (trace.StaleReferences.Count > 0)
            Add(_traceBody, $"{trace.StaleReferences.Count} reference(s) have moved on since this calculation pinned them.");

        if (trace.UntraceableInputs.Count > 0)
            Add(_traceBody, $"{trace.UntraceableInputs.Count} input(s) rest on nothing but the pack's own assertion.");

        Add(_traceBody, trace.RestsEntirelyOnVerifiedData
            ? "Every reference was verified against its own source."
            : "At least one reference has not been verified against its own source.");
    }

    private async Task OnExportTraceAsync()
    {
        if (_openTrace is not { } trace || _openRecordId is not { } recordId)
            return;

        var destination = await _filePicker
            .PickSavePathAsync(new SavePickerRequest($"Export trace {recordId}", $"{recordId}-trace.txt"))
            .ConfigureAwait(true);

        if (destination is null)
        {
            _detailStatus.Text = "Export was cancelled.";
            return;
        }

        await File.WriteAllTextAsync(destination, RenderTraceText(trace)).ConfigureAwait(true);
        _detailStatus.Text = $"Exported to '{destination}'.";
        ActionCompleted?.Invoke(_detailStatus.Text, ActionOutcome.Changed);
    }

    private static string RenderTraceText(CalculationTrace trace)
    {
        var lines = new List<string>
        {
            $"Calculation trace — {trace.PackReference} — {trace.Title}",
            string.Empty,
            "Inputs:",
        };

        foreach (var input in trace.Inputs)
            lines.Add($"  {input.Reference} — {input.Description}: {input.Value}"
                + (input.TracedTo is { } to ? $" -> {to.Citation}" : " (untraceable)"));

        if (trace.TemplateUsed is { } template)
        {
            lines.Add(string.Empty);
            lines.Add($"Template used: {template.Citation}");
        }

        lines.Add(string.Empty);
        lines.Add("Outputs:");
        foreach (var output in trace.Outputs)
            lines.Add($"  {output}");

        lines.Add(string.Empty);
        lines.Add(trace.IsFullyResolved ? "Every reference resolved." : $"{trace.DanglingReferences.Count} unresolved reference(s).");
        lines.Add(trace.RestsEntirelyOnVerifiedData ? "Every reference verified against its own source." : "Not every reference has been verified.");

        return string.Join(Environment.NewLine, lines);
    }

    private static void Add(StackPanel target, string text, bool heading = false) =>
        target.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            FontSize = heading ? DesignTokens.FontSizeBody + 1 : DesignTokens.FontSizeBody,
            FontWeight = heading ? DesignTokens.WeightHeading : Avalonia.Media.FontWeight.Normal,
            Margin = new Thickness(0, heading ? DesignTokens.SpaceSm : 0, 0, 0),
        });
}
