using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Verification;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Verification;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// The Verification Record Result section (`WP 10.7A`) — gated on
/// <see cref="IVerificationActivity"/>. Criteria/Evidence/linked-Id lists
/// are left at <see cref="RecordVerificationResultCommand"/>'s own empty
/// defaults — an honest minimum-viable interaction (Outcome + Method).
/// Moved verbatim from <see cref="ObjectEditorView"/>'s own former
/// <c>PopulateVerificationResult</c>/<c>OnRecordVerificationResultAsync</c>
/// (`WP 21.1B`).
/// </summary>
internal sealed class VerificationResultSection : IEditorSection
{
    private readonly Button _passButton = new() { Content = "Pass", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _failButton = new() { Content = "Fail", MinHeight = DesignTokens.MinControlSize };
    private readonly Button _conditionalButton = new() { Content = "Conditional", MinHeight = DesignTokens.MinControlSize };
    private readonly TextBox _methodBox = new() { FontSize = DesignTokens.FontSizeBody, MinHeight = DesignTokens.MinControlSize };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Record Result";

    public bool AppliesTo(IEngineeringObject? subject) => subject is IVerificationActivity;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;

        var panel = new StackPanel { Spacing = DesignTokens.SpaceXs };
        panel.Children.Add(EditorSectionHelpers.LabeledRow("Method", _methodBox));
        var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceXs };
        buttonRow.Children.Add(_passButton);
        buttonRow.Children.Add(_failButton);
        buttonRow.Children.Add(_conditionalButton);
        panel.Children.Add(buttonRow);
        panel.Children.Add(_statusMessage);

        _passButton.Classes.Add(ChromeStyles.Primary);
        _failButton.Classes.Add(ChromeStyles.Danger);
        _conditionalButton.Classes.Add(ChromeStyles.Subtle);
        _passButton.Click += async (_, _) => await OnRecordAsync(VerificationOutcome.Pass).ConfigureAwait(true);
        _failButton.Click += async (_, _) => await OnRecordAsync(VerificationOutcome.Fail).ConfigureAwait(true);
        _conditionalButton.Click += async (_, _) => await OnRecordAsync(VerificationOutcome.Conditional).ConfigureAwait(true);

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

        _methodBox.Text = ((IVerificationActivity)subject!).Method;
        _statusMessage.Text = string.Empty;
        return Task.CompletedTask;
    }

    private async Task OnRecordAsync(VerificationOutcome outcome)
    {
        var method = string.IsNullOrWhiteSpace(_methodBox.Text) ? "Inspection" : _methodBox.Text;

        var result = await _ctx.CommandDispatcher.DispatchAsync(
            new RecordVerificationResultCommand(_ctx.ObjectId, _ctx.ObjectKind, outcome, method),
            CancellationToken.None).ConfigureAwait(true);

        // Refresh() before the final message — see BillOfMaterialsSection's own identical remarks.
        var message = result.Succeeded ? $"Result recorded: {outcome}." : result.Message ?? "Record result failed.";
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
