using System.Globalization;
using Avalonia.Controls;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Editors;
using Tempest.Workspace.Mechanical;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The Mechanical BOM section (`WP 10.7A`) — gated on <see cref="IHasBomLine"/>
/// and either a Kind's own declaration or the legacy <see cref="ObjectEditorView.BomKinds"/>
/// set, moved verbatim from <see cref="ObjectEditorView"/>'s own former
/// <c>PopulateBom</c>/<c>OnSaveBomAsync</c> (`WP 21.1B`). Writes through
/// <see cref="SetBomLineCommand"/>.
/// </summary>
internal sealed class BillOfMaterialsSection : IEditorSection
{
    private readonly TextBox _quantityBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _unitOfMeasureBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _findNumberBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _itemNumberBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _referenceDesignatorBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly Button _saveButton = new() { Content = "Save BOM Line", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Bill of Materials";

    public bool AppliesTo(IEngineeringObject? subject)
    {
        if (subject is not IHasBomLine)
            return false;

        var declaration = _ctx?.Declarations?.For(_ctx.ObjectKind);
        return declaration is not null
            ? declaration.HasSection(EditorSectionKeys.BillOfMaterials)
            : _ctx?.ObjectKind is { } kind && ObjectEditorView.BomKinds.Contains(kind);
    }

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;

        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(EditorSectionHelpers.LabeledRow("Quantity", _quantityBox));
        panel.Children.Add(EditorSectionHelpers.LabeledRow("Unit of Measure", _unitOfMeasureBox));
        panel.Children.Add(EditorSectionHelpers.LabeledRow("Find Number", _findNumberBox));
        panel.Children.Add(EditorSectionHelpers.LabeledRow("Item Number", _itemNumberBox));
        panel.Children.Add(EditorSectionHelpers.LabeledRow("Reference Designator", _referenceDesignatorBox));
        panel.Children.Add(_saveButton);
        panel.Children.Add(_statusMessage);

        _saveButton.Classes.Add(ChromeStyles.Primary);
        _saveButton.Click += async (_, _) => await OnSaveAsync().ConfigureAwait(true);

        _expander = EditorSectionHelpers.BuildSection(Title, panel);
        _expander.IsVisible = false;
        return _expander;
    }

    public Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        var applies = AppliesTo(subject);
        _expander.IsVisible = applies;

        if (!applies)
            return Task.CompletedTask;

        var bomLine = (IHasBomLine)subject!;
        _quantityBox.Text = bomLine.Quantity.ToString(CultureInfo.InvariantCulture);
        _unitOfMeasureBox.Text = bomLine.UnitOfMeasure ?? string.Empty;
        _findNumberBox.Text = bomLine.FindNumber ?? string.Empty;
        _itemNumberBox.Text = bomLine.ItemNumber ?? string.Empty;
        _referenceDesignatorBox.Text = bomLine.ReferenceDesignator ?? string.Empty;
        _statusMessage.Text = string.Empty;
        return Task.CompletedTask;
    }

    private async Task OnSaveAsync()
    {
        if (!decimal.TryParse(_quantityBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        {
            _statusMessage.Text = "Quantity must be a positive number.";
            return;
        }

        var result = await _ctx.CommandDispatcher.DispatchAsync(
            new SetBomLineCommand(
                _ctx.ObjectId, _ctx.ObjectKind, quantity,
                EditorSectionHelpers.NullIfEmpty(_unitOfMeasureBox.Text), EditorSectionHelpers.NullIfEmpty(_findNumberBox.Text),
                EditorSectionHelpers.NullIfEmpty(_itemNumberBox.Text), EditorSectionHelpers.NullIfEmpty(_referenceDesignatorBox.Text)),
            CancellationToken.None).ConfigureAwait(true);

        // Refresh() first — it re-runs LoadAsync, which resets this
        // section's own status message to empty as part of a clean
        // re-read; setting the real outcome message only after Refresh()
        // returns is what makes it actually survive to be seen, rather
        // than being immediately overwritten by the same success path
        // that produced it.
        var message = result.Succeeded ? "BOM line saved." : result.Message ?? "Save failed.";
        if (result.Succeeded)
            await _ctx.RefreshAsync().ConfigureAwait(true);
        _statusMessage.Text = message;
        _ctx.ReportAction(message, ActionOutcome.From(result.Succeeded));
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
