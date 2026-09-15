using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.Core.Audit;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Evidence;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Theming;
using Tempest.Workspace.Evidence;

namespace Tempest.Desktop.Editors.Sections;

/// <summary>
/// Evidence's own Subject section (`WP 18.2A`, `ADR-0148`, §4) — the Part,
/// Assembly, Requirement or Deliverable it is about, read-only, named and
/// linked, with a Change action. Moved verbatim from
/// <see cref="ObjectEditorView"/>'s own former Subject half of
/// <c>PopulateEvidenceSectionsAsync</c>/<c>OnChangeSubjectAsync</c>
/// (`WP 21.1B`). Kept in the same file as its four sibling Evidence
/// sections since all five shared one pre-split populate method and one
/// <c>AppliesTo</c> (a real <see cref="Evidence"/>) — each is still its own
/// independent <see cref="IEditorSection"/> with no shared state.
/// </summary>
internal sealed class EvidenceSubjectSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _changeSubjectButton = new() { Content = "Change Subject", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Subject";

    public bool AppliesTo(IEngineeringObject? subject) => subject is Evidence;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;

        var body = new StackPanel { Spacing = DesignTokens.SpaceXs };
        body.Children.Add(_panel);
        body.Children.Add(_changeSubjectButton);
        body.Children.Add(_statusMessage);

        _changeSubjectButton.Classes.Add(ChromeStyles.Subtle);
        _changeSubjectButton.Click += async (_, _) => await OnChangeSubjectAsync().ConfigureAwait(true);

        _expander = EditorSectionHelpers.BuildSection(Title, body);
        _expander.IsVisible = false;
        return _expander;
    }

    public async Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        var applies = AppliesTo(subject);
        _expander.IsVisible = applies;

        if (!applies)
            return;

        var evidence = (Evidence)subject!;
        _panel.Children.Clear();
        _panel.Children.Add(evidence.SubjectId is { } subjectId
            ? await EditorSectionHelpers.BuildObjectReferenceRowAsync(_ctx, subjectId).ConfigureAwait(true)
            : new TextBlock { Text = "(no subject tagged)", Opacity = 0.7 });
        _changeSubjectButton.IsVisible = _ctx.EvidenceSupport is not null;
        _statusMessage.Text = string.Empty;
    }

    /// <summary>
    /// Change Subject: collects a Part, Assembly, Requirement or
    /// Deliverable via the real Subject picker and dispatches
    /// <see cref="SetEvidenceSubjectCommand"/>. A refusal — the record is
    /// Issued — shows here and, via <see cref="EditorSectionContext.ReportAction"/>,
    /// in the shell's own status bar.
    /// </summary>
    private async Task OnChangeSubjectAsync()
    {
        if (_ctx.EvidenceSupport is null)
            return;

        var picked = await _ctx.EvidenceSupport.PickSubjectAsync(CancellationToken.None).ConfigureAwait(true);

        var result = await _ctx.CommandDispatcher.DispatchAsync(
            new SetEvidenceSubjectCommand(_ctx.ObjectId, _ctx.ObjectKind, picked), CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Subject changed." : result.Message ?? "The subject change was refused.";
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

/// <summary>Evidence's own Citations section (`WP 18.2A`) — see <see cref="EvidenceSubjectSection"/>'s own remarks for why the five Evidence sections share one file.</summary>
internal sealed class EvidenceCitationsSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _citeButton = new() { Content = "Cite", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Citations";

    public bool AppliesTo(IEngineeringObject? subject) => subject is Evidence;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;

        var body = new StackPanel { Spacing = DesignTokens.SpaceXs };
        body.Children.Add(_citeButton);
        body.Children.Add(_panel);
        body.Children.Add(_statusMessage);

        _citeButton.Classes.Add(ChromeStyles.Primary);
        _citeButton.Click += async (_, _) => await OnCiteAsync().ConfigureAwait(true);

        _expander = EditorSectionHelpers.BuildSection(Title, body);
        _expander.IsVisible = false;
        return _expander;
    }

    public Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        var applies = AppliesTo(subject);
        _expander.IsVisible = applies;

        if (!applies)
            return Task.CompletedTask;

        var evidence = (Evidence)subject!;
        _panel.Children.Clear();
        _citeButton.IsVisible = _ctx.EvidenceSupport is not null;
        if (evidence.Citations.Count == 0)
        {
            _panel.Children.Add(new TextBlock { Text = "No citations recorded.", Opacity = 0.7 });
        }
        else
        {
            foreach (var citation in evidence.Citations)
                _panel.Children.Add(BuildCitationRow(citation));
        }
        _statusMessage.Text = string.Empty;
        return Task.CompletedTask;
    }

    private Control BuildCitationRow(EvidenceCitation citation)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, DesignTokens.SpaceXs) };

        var text = new TextBlock
        {
            Text = $"{citation.Pin} — {citation.RecordDisplayName}" + (citation.SourceCitationSnapshot is null ? string.Empty : $" — {citation.SourceCitationSnapshot}"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        var remove = new Button { Content = "Remove", Padding = new Thickness(10, 1), FontSize = DesignTokens.FontSizeBody };
        remove.Classes.Add(ChromeStyles.Subtle);
        remove.Click += async (_, _) => await OnRemoveCitationAsync(citation.Pin).ConfigureAwait(true);
        Grid.SetColumn(remove, 1);
        row.Children.Add(remove);

        return row;
    }

    private async Task OnRemoveCitationAsync(ReferencePin pin)
    {
        var result = await _ctx.CommandDispatcher.DispatchAsync(new RemoveEvidenceCitationCommand(_ctx.ObjectId, _ctx.ObjectKind, pin), CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Citation removed." : result.Message ?? "Remove failed.";
        if (result.Succeeded)
            await _ctx.RefreshAsync().ConfigureAwait(true);
        _statusMessage.Text = message;
        _ctx.ReportAction(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// Cite: collects a released record via
    /// <see cref="EvidenceEditorSupport.PickCitationAsync"/> and dispatches
    /// <see cref="CiteEvidenceCommand"/>. A refusal shows here and, via
    /// <see cref="EditorSectionContext.ReportAction"/>, in the shell's own
    /// status bar, naming the record and its state.
    /// </summary>
    private async Task OnCiteAsync()
    {
        if (_ctx.EvidenceSupport is null)
            return;

        var picked = await _ctx.EvidenceSupport.PickCitationAsync(CancellationToken.None).ConfigureAwait(true);
        if (picked is null)
        {
            _statusMessage.Text = "Cite was cancelled.";
            return;
        }

        var result = await _ctx.CommandDispatcher.DispatchAsync(
            new CiteEvidenceCommand(_ctx.ObjectId, _ctx.ObjectKind, picked.Library, picked.RecordId), CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Cited." : result.Message ?? "The citation was refused.";
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

/// <summary>Evidence's own Declared figures section (`WP 18.2A`) — see <see cref="EvidenceSubjectSection"/>'s own remarks for why the five Evidence sections share one file.</summary>
internal sealed class EvidenceDeclaredFiguresSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _declareFigureButton = new() { Content = "Declare Figure", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Declared figures";

    public bool AppliesTo(IEngineeringObject? subject) => subject is Evidence;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;

        var body = new StackPanel { Spacing = DesignTokens.SpaceXs };
        body.Children.Add(_declareFigureButton);
        body.Children.Add(_panel);
        body.Children.Add(_statusMessage);

        _declareFigureButton.Classes.Add(ChromeStyles.Primary);
        _declareFigureButton.Click += async (_, _) => await OnDeclareFigureAsync().ConfigureAwait(true);

        _expander = EditorSectionHelpers.BuildSection(Title, body);
        _expander.IsVisible = false;
        return _expander;
    }

    public Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        var applies = AppliesTo(subject);
        _expander.IsVisible = applies;

        if (!applies)
            return Task.CompletedTask;

        var evidence = (Evidence)subject!;
        _panel.Children.Clear();
        _declareFigureButton.IsVisible = _ctx.EvidenceSupport is not null;
        if (evidence.DeclaredFigures.Count == 0)
        {
            _panel.Children.Add(new TextBlock { Text = "No figures declared.", Opacity = 0.7 });
        }
        else
        {
            foreach (var figure in evidence.DeclaredFigures)
            {
                _panel.Children.Add(new TextBlock
                {
                    Text = $"{figure.Name} ({figure.Role}) = {figure.Quantity}",
                    FontSize = DesignTokens.FontSizeBody,
                    TextWrapping = TextWrapping.Wrap,
                });
            }
        }
        _statusMessage.Text = string.Empty;
        return Task.CompletedTask;
    }

    private async Task OnDeclareFigureAsync()
    {
        if (_ctx.EvidenceSupport is null)
            return;

        var input = await _ctx.EvidenceSupport.PickDeclaredFigureAsync(CancellationToken.None).ConfigureAwait(true);
        if (input is null)
        {
            _statusMessage.Text = "Declare was cancelled.";
            return;
        }

        var result = await _ctx.CommandDispatcher.DispatchAsync(
            new DeclareEvidenceFigureCommand(_ctx.ObjectId, _ctx.ObjectKind, input.Name, input.Role, input.Quantity), CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Declared." : result.Message ?? "Declare failed.";
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

/// <summary>
/// Evidence's own Lifecycle section (`WP 18.2A`/`WP 18.2B`) — status, the
/// check and issue records read-only, and the Check/Issue/Revise actions
/// themselves. See <see cref="EvidenceSubjectSection"/>'s own remarks for
/// why the five Evidence sections share one file. Not to be confused with
/// the generic <see cref="LifecycleSection"/>, which this Kind suppresses
/// in favour of this one (that section's own <c>AppliesTo</c>).
/// </summary>
internal sealed class EvidenceLifecycleSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private readonly Button _checkButton = new() { Content = "Check", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _issueButton = new() { Content = "Issue", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly Button _reviseButton = new() { Content = "Revise", MinHeight = DesignTokens.MinControlSize, IsVisible = false };
    private readonly TextBlock _statusMessage = new() { FontSize = DesignTokens.FontSizeCaption, Opacity = 0.8 };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Lifecycle";

    public bool AppliesTo(IEngineeringObject? subject) => subject is Evidence;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;

        // `WP 18.2B`, §1/§2/§3: the Check/Issue/Revise actions themselves,
        // each visible only when the record's own current status permits
        // it (this section's own LoadAsync).
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm };
        actions.Children.Add(_checkButton);
        actions.Children.Add(_issueButton);
        actions.Children.Add(_reviseButton);
        var body = new StackPanel { Spacing = DesignTokens.SpaceXs };
        body.Children.Add(_panel);
        body.Children.Add(actions);
        body.Children.Add(_statusMessage);

        _checkButton.Classes.Add(ChromeStyles.Primary);
        _issueButton.Classes.Add(ChromeStyles.Primary);
        _reviseButton.Classes.Add(ChromeStyles.Subtle);
        _checkButton.Click += async (_, _) => await OnCheckAsync().ConfigureAwait(true);
        _issueButton.Click += async (_, _) => await OnIssueAsync().ConfigureAwait(true);
        _reviseButton.Click += async (_, _) => await OnReviseAsync().ConfigureAwait(true);

        _expander = EditorSectionHelpers.BuildSection(Title, body);
        _expander.IsVisible = false;
        return _expander;
    }

    public Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        var applies = AppliesTo(subject);
        _expander.IsVisible = applies;

        if (!applies)
            return Task.CompletedTask;

        var evidence = (Evidence)subject!;
        _panel.Children.Clear();
        _panel.Children.Add(new TextBlock { Text = $"Status: {evidence.Status}", FontWeight = FontWeight.SemiBold, FontSize = DesignTokens.FontSizeBody });
        _panel.Children.Add(new TextBlock
        {
            Text = evidence.Check is { } check
                ? $"Check: {check.Outcome} by {check.CheckerName} ({check.CheckerOrganisation}) on {check.DateUtc:u} — \"{check.Statement}\""
                : "Check: (not yet checked)",
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
        });
        _panel.Children.Add(new TextBlock
        {
            Text = evidence.Issue is { } issue
                ? $"Issue: '{issue.IssueReference}' rev '{issue.Revision}' to '{issue.Client}' on {issue.DateUtc:u}"
                : "Issue: (not yet issued)",
            TextWrapping = TextWrapping.Wrap,
            FontSize = DesignTokens.FontSizeBody,
        });

        // `WP 18.2B`: each action visible only when
        // `EvidenceStatusTransitions` actually permits it from here —
        // Check from Draft, Issue from Checked, Revise from Issued.
        _checkButton.IsVisible = _ctx.EvidenceSupport is not null && evidence.Status == EvidenceStatus.Draft;
        _issueButton.IsVisible = _ctx.EvidenceSupport is not null && evidence.Status == EvidenceStatus.Checked;
        _reviseButton.IsVisible = _ctx.EvidenceSupport is not null && evidence.Status == EvidenceStatus.Issued;
        _statusMessage.Text = string.Empty;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Check: collects the checker's name, organisation, statement and
    /// outcome and dispatches <see cref="RecordEvidenceCheckCommand"/> —
    /// the client's own review, entered by hand, unless
    /// <c>Evidence:IndependentCheck</c> is on.
    /// </summary>
    private async Task OnCheckAsync()
    {
        if (_ctx.EvidenceSupport is null)
            return;

        var input = await _ctx.EvidenceSupport.PickCheckAsync(CancellationToken.None).ConfigureAwait(true);
        if (input is null)
        {
            _statusMessage.Text = "Check was cancelled.";
            return;
        }

        var result = await _ctx.CommandDispatcher.DispatchAsync(
            new RecordEvidenceCheckCommand(_ctx.ObjectId, _ctx.ObjectKind, input.CheckerName, input.CheckerOrganisation, input.Statement, input.Outcome),
            CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Checked." : result.Message ?? "The check was refused.";
        if (result.Succeeded)
            await _ctx.RefreshAsync().ConfigureAwait(true);
        _statusMessage.Text = message;
        _ctx.ReportAction(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// Issue: collects the issue reference, revision and client and
    /// dispatches <see cref="IssueEvidenceCommand"/>, which — where a
    /// renderer is available — also renders and attaches the issue sheet.
    /// </summary>
    private async Task OnIssueAsync()
    {
        if (_ctx.EvidenceSupport is null)
            return;

        var input = await _ctx.EvidenceSupport.PickIssueAsync(CancellationToken.None).ConfigureAwait(true);
        if (input is null)
        {
            _statusMessage.Text = "Issue was cancelled.";
            return;
        }

        var result = await _ctx.CommandDispatcher.DispatchAsync(
            new IssueEvidenceCommand(_ctx.ObjectId, _ctx.ObjectKind, input.IssueReference, input.Revision, input.Client),
            CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Issued." : result.Message ?? "The issue was refused.";
        if (result.Succeeded)
            await _ctx.RefreshAsync().ConfigureAwait(true);
        _statusMessage.Text = message;
        _ctx.ReportAction(message, ActionOutcome.From(result.Succeeded));
    }

    /// <summary>
    /// Revise: reopens the selected, issued evidence as a new Draft
    /// revision (<see cref="ReviseEvidenceCommand"/>); the issued revision
    /// stays readable, unchanged, via its own revision history. No form of
    /// its own — nothing needs collecting.
    /// </summary>
    private async Task OnReviseAsync()
    {
        var result = await _ctx.CommandDispatcher.DispatchAsync(
            new ReviseEvidenceCommand(_ctx.ObjectId, _ctx.ObjectKind), CancellationToken.None).ConfigureAwait(true);

        var message = result.Succeeded ? result.Message ?? "Revised." : result.Message ?? "The revision was refused.";
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

/// <summary>Evidence's own Audit section — the object's own audit trail. See <see cref="EvidenceSubjectSection"/>'s own remarks for why the five Evidence sections share one file.</summary>
internal sealed class EvidenceAuditSection : IEditorSection
{
    private readonly StackPanel _panel = new() { Spacing = DesignTokens.SpaceXs };
    private Expander _expander = null!;
    private EditorSectionContext _ctx = null!;

    public string Title => "Audit";

    public bool AppliesTo(IEngineeringObject? subject) => subject is Evidence;

    public Control Build(EditorSectionContext ctx)
    {
        _ctx = ctx;
        _expander = EditorSectionHelpers.BuildSection(Title, _panel);
        _expander.IsVisible = false;
        return _expander;
    }

    public async Task LoadAsync(IEngineeringObject? subject, CancellationToken ct)
    {
        var applies = AppliesTo(subject);
        _expander.IsVisible = applies;

        if (!applies)
            return;

        var evidence = (Evidence)subject!;
        _panel.Children.Clear();

        if (_ctx.AuditQuery is null)
        {
            _panel.Children.Add(new TextBlock { Text = "Audit is unavailable.", Opacity = 0.7 });
            return;
        }

        var records = await _ctx.AuditQuery.QueryAsync(new AuditQueryCriteria(objectId: evidence.Id)).ConfigureAwait(true);
        if (records.Count == 0)
        {
            _panel.Children.Add(new TextBlock { Text = "No audit rows recorded yet.", Opacity = 0.7 });
            return;
        }

        foreach (var record in records.OrderByDescending(r => r.OccurredAt).Take(25))
        {
            var detail = record.Detail.Count == 0 ? string.Empty : " — " + string.Join("; ", record.Detail.Select(kv => $"{kv.Key}: {kv.Value}"));
            _panel.Children.Add(new TextBlock
            {
                Text = $"{record.OccurredAt:u}  {record.Action}  by {record.ActorId}{detail}",
                FontSize = DesignTokens.FontSizeCaption,
                Opacity = 0.85,
                TextWrapping = TextWrapping.Wrap,
            });
        }
    }

    public void React(WorkspaceChange change)
    {
        // Reserved — see IEditorSection's own remarks.
    }
}
