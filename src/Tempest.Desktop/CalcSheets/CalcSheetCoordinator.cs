using Avalonia.Controls;
using Avalonia.Input.Platform;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Core.Evidence;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// `v0.26.0` (ADR-0165): the worksheet surface's own collaborator (the
/// same `ADR-0103` shape as <c>CalculationModulesCoordinator</c>). It
/// turns the view's events into calls on <see cref="CalcInstanceService"/>
/// and renders what comes back. Live recalculation is a
/// <see cref="CalcInstanceService.Preview"/> on every input change; a
/// save creates or updates the instance under the current project.
/// </summary>
public sealed class CalcSheetCoordinator : Tempest.Core.Events.IEventHandler<CalcStalenessChanged>
{
    private readonly CalcInstanceService _service;
    private readonly CalcSheetView _view;
    private readonly IProjectContext _projects;
    private readonly Func<string?> _principalId;
    private CalcInputSet? _latest;

    public CalcSheetCoordinator(CalcInstanceService service, CalcSheetView view, IProjectContext projects, Func<string?> principalId, Tempest.Core.Events.IEventBus? events = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(principalId);

        _service = service;
        _view = view;
        _projects = projects;
        _principalId = principalId;

        _view.InputsChanged += inputs => Guarded(() => Recalculate(inputs), "recalculate");
        _view.SaveRequested += () => _ = GuardedAsync(SaveAsync, "save that calculation");
        _view.CheckRequested += () => _ = GuardedAsync(CheckAsync, "record the check");
        _view.IssueRequested += () => _ = GuardedAsync(IssueAsync, "issue that calculation");
        _view.ReviseRequested += () => _ = GuardedAsync(ReviseAsync, "revise that calculation");
        _view.ExportRequested += () => _ = GuardedAsync(ExportAsync, "export the inputs");
        _view.ImportRequested += () => _ = GuardedAsync(ImportAsync, "import the inputs");
        _view.LinkRequested += (input, upstream, output) => _ = GuardedAsync(() => LinkAsync(input, upstream, output), "link that input");
        _view.UpdateLinksRequested += () => _ = GuardedAsync(UpdateLinksAsync, "update the linked inputs");
        _view.ApplyRecordRequested += id => _ = GuardedAsync(() => ApplyRecordAsync(id), "apply that record");
        _view.SaveRecordRequested += draft => _ = GuardedAsync(() => SaveRecordAsync(draft), "save that record");
        events?.Subscribe(this);
    }

    /// <summary>Addendum A, L1: something upstream changed; refresh the stale marker if the shown calculation is affected.</summary>
    public Task HandleAsync(CalcStalenessChanged @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (_view.CurrentInstance is { } shown && @event.AffectedIds.Contains(shown.Id))
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = GuardedAsync(() => RefreshLinkStateAsync(shown.Id), "refresh the stale marker"));

        return Task.CompletedTask;
    }

    /// <summary>Re-reads the shown calculation's staleness and the calculations its inputs can link to.</summary>
    public async Task RefreshLinkStateAsync(Guid? instanceId = null)
    {
        var id = instanceId ?? _view.CurrentInstance?.Id;

        if (id is null || await _service.Store.FindInstanceAsync(id.Value).ConfigureAwait(true) is not { } instance)
        {
            _view.SetLinkTargets([]);
            _view.SetRecords([]);
            return;
        }

        var staleness = await _service.StalenessAsync(instance).ConfigureAwait(true);
        string? caption = null;

        if (staleness.IsStale && instance.Links.Count > 0)
            caption = instance.Status == CalcInstanceStatus.Draft ? "Update linked inputs" : $"Update to revision {CalcInstanceService.NextRevisionLabel(instance.Revision)}";

        _view.ShowStaleness(staleness, caption);

        var targets = new List<CalcSheetView.LinkTarget>();

        foreach (var candidate in await _service.Store.ListInstancesAsync(instance.ProjectId).ConfigureAwait(true))
        {
            if (candidate.Id == instance.Id || candidate.Status == CalcInstanceStatus.Superseded || candidate.Outputs.Count == 0 || targets.Count >= 200)
                continue;

            var ports = candidate.Outputs.Where(o => o.Si is not null).Select(o => new CalcOutputPort(o.Symbol, o.Unit ?? string.Empty, o.Description)).ToList();
            targets.Add(new CalcSheetView.LinkTarget(candidate.Id, $"{candidate.Name} · rev {candidate.Revision} · {candidate.Status}", ports));
        }

        _view.SetLinkTargets(targets);

        var records = _service.Catalogue.Find(instance.TemplateReference) is null ? [] : await _service.RecordsForAsync(instance.TemplateReference).ConfigureAwait(true);
        _view.SetRecords([.. records.Take(200).Select(r => new CalcSheetView.RecordChoice(r.Id, $"{r.Citation} · rev {r.Revision}"))]);
    }

    private async Task ApplyRecordAsync(Guid recordId)
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var result = await _service.ApplyRecordAsync(instance.Id, recordId).ConfigureAwait(true);

        if (result.Succeeded && result.Instance is { } applied && _service.Catalogue.Find(applied.TemplateReference) is { } entry)
            _view.ShowTemplate(entry, applied);

        await ReportAsync(result, "Record applied: those inputs now cite it, its edition and its revision.").ConfigureAwait(true);
    }

    /// <summary>Saves the form, stores its values of one record kind as a new reference record, then applies it so the inputs cite it.</summary>
    private async Task SaveRecordAsync(CalcSheetView.RecordDraft draft)
    {
        if (_view.CurrentInstance is not { } instance || _view.SelectedTemplate?.Template.RecordKinds?.FirstOrDefault(k => k.Kind == draft.Kind) is not { } kind)
            return;

        var form = _view.ReadForm();
        var saved = await _service.UpdateInputsAsync(instance.Id, form.Values).ConfigureAwait(true);

        if (!saved.Succeeded)
        {
            await ReportAsync(saved, string.Empty).ConfigureAwait(true);
            return;
        }

        var values = kind.Symbols.Select(form.Find).OfType<CalcInputValue>().Where(v => v.Number is not null || v.Text is not null || v.Rows is not null)
            .Select(v => new CalcDataValue(v.Symbol, v.Number, v.Unit, v.Text, v.Rows)).ToList();
        var (record, refusal) = await _service.SaveRecordAsync(new CalcDataRecord(Guid.Empty, draft.Kind, draft.Title, 0, draft.SourceTitle, draft.Edition, draft.TableOrClause, draft.LicenceHolder, string.Empty, draft.LicensedSource, CalcDataOrigin.UserEntered, values, default)).ConfigureAwait(true);

        if (record is null)
        {
            _view.ShowStatus("Record not saved: " + refusal);
            return;
        }

        var result = await _service.ApplyRecordAsync(instance.Id, record.Id).ConfigureAwait(true);

        if (result.Succeeded && result.Instance is { } applied && _service.Catalogue.Find(applied.TemplateReference) is { } entry)
            _view.ShowTemplate(entry, applied);

        await ReportAsync(result, $"Saved '{record.Title}' as a reference record (rev {record.Revision}{(record.LicensedSource ? ", licensed: its values never leave in an export" : string.Empty)}) and applied it.").ConfigureAwait(true);
    }

    private async Task LinkAsync(string input, Guid upstreamId, string output)
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var result = await _service.BindAsync(instance.Id, input, upstreamId, output).ConfigureAwait(true);

        if (result.Succeeded && result.Instance is { } bound && _service.Catalogue.Find(bound.TemplateReference) is { } entry)
            _view.ShowTemplate(entry, bound);

        await ReportAsync(result, $"Linked {input} to {output}; it is pinned to that calculation's current revision.").ConfigureAwait(true);
    }

    private async Task UpdateLinksAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var result = await _service.UpdateLinksAsync(instance.Id).ConfigureAwait(true);

        if (result.Succeeded && result.Instance is { } updated && _service.Catalogue.Find(updated.TemplateReference) is { } entry)
            _view.ShowTemplate(entry, updated);

        await ReportAsync(result, result.Instance is { } i && i.Id != instance.Id
            ? $"Linked inputs updated as revision {i.Revision}; revision {instance.Revision} is superseded. Check and issue it again."
            : "Linked inputs updated to the current revisions and recalculated.").ConfigureAwait(true);
    }

    private async Task ReportAsync(CalcActionResult result, string success)
    {
        Report(result, success);
        await RefreshLinkStateAsync(result.Instance?.Id).ConfigureAwait(true);
    }

    public Task RefreshAsync()
    {
        _view.ShowCatalogue(_service.Catalogue.Current());
        return Task.CompletedTask;
    }

    /// <summary>Opens an existing instance in the form (read-only when issued).</summary>
    public async Task OpenAsync(Guid instanceId)
    {
        var instance = await _service.Store.FindInstanceAsync(instanceId).ConfigureAwait(true);

        if (instance is null)
        {
            _view.ShowStatus("That calculation no longer exists.");
            return;
        }

        var entry = _service.Catalogue.Find(instance.TemplateReference);

        if (entry is null)
        {
            _view.ShowStatus($"Template '{instance.TemplateReference}' is not registered in this build.");
            return;
        }

        _view.SelectTemplate(entry.Reference);
        _view.ShowTemplate(entry, instance);
        await RefreshLinkStateAsync(instance.Id).ConfigureAwait(true);
    }

    private void Recalculate(CalcInputSet inputs)
    {
        if (_view.SelectedTemplate is not { } entry)
            return;

        _latest = inputs;
        _view.ShowRun(_service.Preview(entry.Reference, inputs));
    }

    private async Task SaveAsync()
    {
        if (_view.SelectedTemplate is not { } entry || _latest is null)
            return;

        if (_view.CurrentInstance is { } existing)
        {
            var updated = await _service.UpdateInputsAsync(existing.Id, _latest.Values).ConfigureAwait(true);
            await ReportAsync(updated, "Saved.").ConfigureAwait(true);
            return;
        }

        var name = _view.CalculationName ?? $"{entry.Template.Title} {DateTime.UtcNow:yyyyMMdd-HHmm}";
        var created = await _service.CreateAsync(entry.Reference, _projects.Current?.Id, name, _latest.Values).ConfigureAwait(true);
        _view.ShowInstance(created);
        _view.ShowStatus(_projects.HasProject
            ? $"Saved as '{created.Name}' in {_projects.Current!.DisplayName}; it is in the Calculation register and has its own Evidence."
            : $"Saved as '{created.Name}' with its own Evidence (open a project first to list it in a register).");
        await RefreshLinkStateAsync(created.Id).ConfigureAwait(true);
    }

    private async Task CheckAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var who = _principalId() ?? "engineer";
        var result = await _service.CheckAsync(instance.Id, who, "Tempest Engineering", "Checked in the worksheet form.", CheckOutcome.Accepted).ConfigureAwait(true);
        await ReportAsync(result, "Checked. Issue when ready.").ConfigureAwait(true);
    }

    private async Task IssueAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var reference = $"{CalcInstanceService.Safe(instance.Name)}-{instance.Revision}";
        var client = _projects.Current?.DisplayName ?? "—";
        var result = await _service.IssueAsync(instance.Id, reference, client).ConfigureAwait(true);
        await ReportAsync(result, $"Issued as '{reference}'. The sheet is attached to its Evidence and listed under Reports.").ConfigureAwait(true);
    }

    private async Task ReviseAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var result = await _service.ReviseAsync(instance.Id).ConfigureAwait(true);

        if (result.Succeeded && result.Instance is { } next && _service.Catalogue.Find(next.TemplateReference) is { } entry)
        {
            _view.ShowTemplate(entry, next);
            _view.ShowStatus($"Revision {next.Revision} started as a draft; revision {instance.Revision} is superseded.");
            await RefreshLinkStateAsync(next.Id).ConfigureAwait(true);
            return;
        }

        Report(result, "Revised.");
    }

    private async Task ExportAsync()
    {
        if (_view.SelectedTemplate is not { } entry || _latest is null)
            return;

        // §A5: values taken from a licensed reference record are withheld from every export.
        var csv = CalcCsv.ExportInputs(entry.Template, await _service.WithholdLicensedAsync(_latest).ConfigureAwait(true));
        var clipboard = TopLevel.GetTopLevel(_view)?.Clipboard;

        if (clipboard is null)
        {
            _view.ShowStatus("No clipboard is available here.");
            return;
        }

        await clipboard.SetTextAsync(csv).ConfigureAwait(true);
        _view.ShowStatus($"Copied {entry.Template.Inputs.Count} input row(s) as CSV to the clipboard.");
    }

    /// <summary>Inputs come in from the clipboard as the CSV the form exports or as a worked example's <c>inputs.json</c>; the file formats' own caps and notices apply (§3.8).</summary>
    private async Task ImportAsync()
    {
        if (_view.SelectedTemplate is null)
            return;

        var clipboard = TopLevel.GetTopLevel(_view)?.Clipboard;
        var text = clipboard is null ? null : await clipboard.TryGetTextAsync().ConfigureAwait(true);

        if (string.IsNullOrWhiteSpace(text))
        {
            _view.ShowStatus("Copy the inputs (CSV or JSON) to the clipboard first.");
            return;
        }

        ImportInputsText(text);
    }

    /// <summary>Parses <paramref name="text"/> as JSON (a list) or CSV, reports every notice, and re-seeds the form when nothing blocks.</summary>
    public void ImportInputsText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (_view.SelectedTemplate is not { } entry)
            return;

        var (inputs, notices) = ParseInputs(entry.Template, text);
        var blocks = notices.Where(n => n.Severity == CalcNoticeSeverity.Block).ToList();

        if (blocks.Count > 0)
        {
            _view.ShowStatus("Nothing imported: " + string.Join(" ", blocks.Select(b => b.Message)));
            return;
        }

        var known = inputs.Values.Count(v => entry.Template.FindInput(v.Symbol) is not null);
        _view.ApplyImportedInputs(inputs);
        _view.ShowStatus($"Imported {known} input(s){(notices.Count > 0 ? "; " + string.Join(" ", notices.Select(n => n.Message)) : ".")}");
    }

    /// <summary>JSON when the text starts a list or an object, else CSV. Shared with the tests.</summary>
    public static (CalcInputSet Inputs, IReadOnlyList<CalcNotice> Notices) ParseInputs(CalcTemplate template, string text)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return trimmed.StartsWith('[') || trimmed.StartsWith('{') ? CalcInputJson.Read(trimmed) : CalcCsv.ImportInputs(template, text);
    }

    private void Report(CalcActionResult result, string success)
    {
        if (result.Instance is { } instance)
            _view.ShowInstance(instance);

        _view.ShowStatus(result.Succeeded ? success : result.Reason ?? "Refused.");
    }

    private void Guarded(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _view.ShowStatus($"Could not {what}: {failure.Message}");
        }
    }

    private async Task GuardedAsync(Func<Task> action, string what)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _view.ShowStatus($"Could not {what}: {failure.Message}");
        }
    }
}
