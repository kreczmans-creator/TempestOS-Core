using Avalonia.Controls;
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

    public CalcSheetCoordinator(CalcInstanceService service, CalcSheetView view, IProjectContext projects, Func<string?> principalId)
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
