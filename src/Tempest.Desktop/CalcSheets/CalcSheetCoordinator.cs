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
public sealed class CalcSheetCoordinator
{
    private readonly CalcInstanceService _service;
    private readonly CalcSheetView _view;
    private readonly IProjectContext _projects;
    private readonly Func<string?> _principalId;
    private CalcInputSet? _latest;

    private readonly CalcReferenceFill? _references;
    private readonly Dictionary<string, IReadOnlyList<CalcReferenceOption>> _referenceOptions = new(StringComparer.Ordinal);

    private readonly CalcRegisterView? _register;

    public CalcSheetCoordinator(CalcInstanceService service, CalcSheetView view, IProjectContext projects, Func<string?> principalId, CalcReferenceFill? references = null, CalcRegisterView? register = null)
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
        _references = references;
        _view.TemplateSelected += entry => _ = GuardedAsync(() => LoadReferenceOptionsAsync(entry), "list the released reference records");
        _view.ReferencePicked += (library, index) => _ = GuardedAsync(() => PickReferenceAsync(library, index), "fill from the reference record");
        _view.CompareRequested += () => _ = GuardedAsync(CompareAsync, "compare with the previous revision");
        _view.ArchiveSelected += id => _ = GuardedAsync(() => OpenAsync(id), "open that archived calculation");
        _register = register;
    }

    /// <summary>Lists the open project's worksheet calculations in the register (every one when no project is open).</summary>
    public async Task RefreshRegisterAsync()
    {
        if (_register is null)
            return;

        var project = _projects.Current;
        var instances = await _service.Store.ListInstancesAsync().ConfigureAwait(true);
        _register.ShowEntries([.. instances.Where(i => project is null || i.ProjectId == project.Id)], project?.DisplayName, reference => _service.Catalogue.Find(reference)?.Template.Title);
    }

    /// <summary>Lists the templates, then the migrated legacy records beneath them (Y8-06: an archive has no project or register entry, so the catalogue is where it is found).</summary>
    public async Task RefreshAsync()
    {
        _view.ShowCatalogue(_service.Catalogue.Current());
        var instances = await _service.Store.ListInstancesAsync().ConfigureAwait(true);
        _view.ShowArchives([.. instances.Where(i => i.IsLegacyArchive)]);
    }

    /// <summary>ADR-0167 §4: the current revision against the one it superseded.</summary>
    private async Task CompareAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var (comparison, reason) = await _service.CompareWithPredecessorAsync(instance.Id).ConfigureAwait(true);

        if (comparison is null)
        {
            _view.ShowStatus(reason ?? "Nothing to compare.");
            return;
        }

        _view.ShowComparison(comparison);
        _view.ShowStatus(comparison.HasChanges
            ? $"Compared rev {comparison.After.Revision} with rev {comparison.Before.Revision}: {comparison.Rows.Count + comparison.Omitted} value(s) differ."
            : $"Compared rev {comparison.After.Revision} with rev {comparison.Before.Revision}: nothing differs.");
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

        if (instance.IsLegacyArchive)
        {
            var archive = await _service.RunAsync(instance).ConfigureAwait(true);
            _view.ShowArchive(instance, archive.SheetText);
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
            Report(updated, "Saved.");
            return;
        }

        var name = _view.CalculationName ?? $"{entry.Template.Title} {DateTime.UtcNow:yyyyMMdd-HHmm}";
        var created = await _service.CreateAsync(entry.Reference, _projects.Current?.Id, name, _latest.Values).ConfigureAwait(true);
        _view.ShowInstance(created);
        _view.ShowStatus(_projects.HasProject
            ? $"Saved as '{created.Name}' in {_projects.Current!.DisplayName}; it is in the Calculation register and has its own Evidence."
            : $"Saved as '{created.Name}' with its own Evidence (open a project first to list it in a register).");
    }

    private async Task CheckAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var who = _principalId() ?? "engineer";
        var result = await _service.CheckAsync(instance.Id, who, "Tempest Engineering", "Checked in the worksheet form.", CheckOutcome.Accepted).ConfigureAwait(true);
        Report(result, "Checked. Issue when ready.");
    }

    private async Task IssueAsync()
    {
        if (_view.CurrentInstance is not { } instance)
            return;

        var reference = $"{CalcInstanceService.Safe(instance.Name)}-{instance.Revision}";
        var client = _projects.Current?.DisplayName ?? "—";
        var result = await _service.IssueAsync(instance.Id, reference, client).ConfigureAwait(true);
        Report(result, $"Issued as '{reference}'. The sheet is attached to its Evidence and listed under Reports.");
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
            return;
        }

        Report(result, "Revised.");
    }

    private async Task ExportAsync()
    {
        if (_view.SelectedTemplate is not { } entry || _latest is null)
            return;

        var csv = CalcCsv.ExportInputs(entry.Template, _latest);
        var clipboard = TopLevel.GetTopLevel(_view)?.Clipboard;

        if (clipboard is null)
        {
            _view.ShowStatus("No clipboard is available here.");
            return;
        }

        await clipboard.SetTextAsync(csv).ConfigureAwait(true);
        _view.ShowStatus($"Copied {entry.Template.Inputs.Count} input row(s) as CSV to the clipboard.");
    }

    /// <summary>Lists the released records of every library the template draws on (empty lists when no catalogue is wired).</summary>
    private async Task LoadReferenceOptionsAsync(CalcCatalogueEntry entry)
    {
        _referenceOptions.Clear();

        foreach (var library in CalcReferenceFill.LibrariesOf(entry.Template))
        {
            var options = _references is null ? [] : await _references.ListReleasedAsync(library).ConfigureAwait(true);
            _referenceOptions[library] = options;
            _view.SetReferenceOptions(library, options.Select(o => o.Label).ToList());
        }
    }

    private async Task PickReferenceAsync(string library, int index)
    {
        if (_references is null || _view.SelectedTemplate is not { } entry || !_referenceOptions.TryGetValue(library, out var options) || index < 0 || index >= options.Count)
            return;

        var option = options[index];
        var fill = await _references.FillAsync(entry.Template, library, option.RecordId).ConfigureAwait(true);

        if (fill.Values.Count > 0)
            _view.ApplyReferenceFill(fill.Values);

        var filled = string.Join(", ", fill.Values.Select(v => v.Symbol));
        _view.ShowStatus(fill.Problems.Count == 0
            ? $"Filled {filled} from {option.Label} ({option.Pin})."
            : $"{(fill.Values.Count > 0 ? $"Filled {filled} from {option.Label}; " : string.Empty)}{string.Join(" ", fill.Problems.Select(p => p.Message))}");
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
