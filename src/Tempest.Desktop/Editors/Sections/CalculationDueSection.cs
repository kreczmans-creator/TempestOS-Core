using Avalonia.Controls;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Calculations;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The Due section (`WP 20.10B`, T2) — a real <c>"Calculation"</c> only,
/// never a <c>"CalculationSet"</c> (a container, never itself a task) or a
/// synthetic <c>"CalculationTemplate"</c> (no Domain identity to set a
/// field on). Moved verbatim from <see cref="ObjectEditorView"/>'s own
/// former <c>PopulateCalculationDue</c>/<c>OnSaveCalculationDueAsync</c>
/// (`WP 21.1B`).
/// </summary>
internal sealed class CalculationDueSection : IEditorSection
{
    private readonly TextBox _dueBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize, Watermark = "yyyy-mm-dd" };
    private readonly Button _saveButton = new() { Content = "Save Due Date", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Due";

    public bool AppliesTo(IEngineeringObject? subject) => _ctx?.ObjectKind == "Calculation";

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;

        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(EditorSectionHelpers.LabeledRow("Due", _dueBox));
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
        _expander.IsVisible = AppliesTo(subject);

        if (subject is not Calculation calculation)
            return Task.CompletedTask;

        _dueBox.Text = calculation.DueOn?.ToString("O") ?? string.Empty;
        _statusMessage.Text = string.Empty;
        return Task.CompletedTask;
    }

    private async Task OnSaveAsync()
    {
        DateOnly? dueOn;

        if (string.IsNullOrWhiteSpace(_dueBox.Text))
        {
            dueOn = null;
        }
        else if (DateOnly.TryParse(_dueBox.Text, out var parsed))
        {
            dueOn = parsed;
        }
        else
        {
            _statusMessage.Text = "'Due' must be a date (yyyy-mm-dd), or blank to clear it.";
            return;
        }

        var result = await _ctx.CommandDispatcher.DispatchAsync(
            new SetCalculationDueDateCommand(_ctx.ObjectId, _ctx.ObjectKind, dueOn), CancellationToken.None).ConfigureAwait(true);

        // Refresh() before the final message — see BillOfMaterialsSection's own identical remarks.
        var message = result.Succeeded ? "Due date saved." : result.Message ?? "Save failed.";
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
