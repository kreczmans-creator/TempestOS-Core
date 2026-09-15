using Tempest.Core.Calculations;
using Tempest.Core.Calculations.Modules;
using Tempest.Desktop.Views;
using Tempest.Workspace.Engineering;

namespace Tempest.Desktop.Composition;

/// <summary>
/// The Engineering Calculators' own collaborator (`WP 21.7B`, `WP 21.7C`;
/// the same `ADR-0103` shape as <see cref="EngineeringCalculationCoordinator"/>):
/// it hands <see cref="CalculationModulesView"/> the catalogue and the
/// released records of each library, fills a form from a picked record,
/// turns Calculate into a named run, and offers the record's own Re-run
/// and Compare commands.
/// </summary>
internal sealed class CalculationModulesCoordinator
{
    private readonly CalculationModuleWorkbench _workbench;
    private readonly CalculationModulesView _view;

    /// <summary>Initialises a new instance of the <see cref="CalculationModulesCoordinator"/> class.</summary>
    public CalculationModulesCoordinator(CalculationModuleWorkbench workbench, CalculationModulesView view)
    {
        ArgumentNullException.ThrowIfNull(workbench);
        ArgumentNullException.ThrowIfNull(view);

        _workbench = workbench;
        _view = view;

        _view.ModuleSelected += module => _ = GuardedAsync(() => RefillAsync(module), "generate that calculation's form");
        _view.ReferencePicked += (inputName, record) => _ = GuardedAsync(() => ApplyRecordAsync(inputName, record), "read that record");
        _view.CalculateRequested += () => _ = GuardedAsync(CalculateAsync, "run that calculation");
        _view.RerunRequested += () => _ = GuardedAsync(RerunAsync, "re-run that calculation");
        _view.CompareRequested += () => _ = GuardedAsync(CompareAsync, "compare that calculation with its previous run");
    }

    /// <summary>Re-reads the catalogue and the released records of every library — on entry, and after a release elsewhere in the session.</summary>
    public Task RefreshAsync() => GuardedAsync(RefreshCoreAsync, "load the calculators");

    private async Task RefreshCoreAsync()
    {
        _view.ShowCatalogue(CalculationModuleWorkbench.Catalogue());

        foreach (var library in Enum.GetValues<ReferenceLibrary>())
            _view.ShowReleased(library, await _workbench.ListReleasedAsync(library).ConfigureAwait(true));
    }

    private async Task RefillAsync(CalculationModuleDescriptor module)
    {
        foreach (var reference in module.References)
        {
            if (_view.PickedRecord(reference.Name) is { } record)
                await ApplyRecordAsync(reference.Name, record).ConfigureAwait(true);
        }
    }

    private async Task ApplyRecordAsync(string inputName, ReleasedRecordOption record)
    {
        if (_view.SelectedModule is not { } module)
            return;

        _view.ApplyFill(await _workbench.FillAsync(module, inputName, record.RecordId).ConfigureAwait(true));
    }

    private async Task CalculateAsync()
    {
        if (_view.SelectedModule is not { } module)
        {
            _view.ShowStatus(CalculationModulesView.PickModuleGuidance);
            return;
        }

        // A second Calculate on the same calculation records another run
        // against it, so Compare has two to set side by side. A different
        // name typed asks for a new calculation, as Start a new calculation
        // on the surface does.
        var name = _view.CalculationName?.Trim();
        var onto = _view.CurrentRun is { } current && (name is null || string.Equals(name, current.DisplayName, StringComparison.Ordinal)) ? current : null;
        var attempt = await _workbench.CalculateAsync(module, _view.ReadForm(), name, onto).ConfigureAwait(true);
        Show(attempt, onto is null ? "Calculated and recorded" : "Calculated again and recorded");
    }

    private async Task RerunAsync()
    {
        if (_view.CurrentRun is not { } current)
        {
            _view.ShowStatus("Calculate first; Re-run repeats the recorded calculation with its retained input.");
            return;
        }

        Show(await _workbench.RerunAsync(current).ConfigureAwait(true), "Re-ran and recorded");
    }

    private async Task CompareAsync()
    {
        if (_view.CurrentRun is not { } current)
        {
            _view.ShowStatus("Calculate first; Compare needs a calculation with at least two recorded runs.");
            return;
        }

        try
        {
            var comparison = await _workbench.CompareAsync(current).ConfigureAwait(true);
            _view.ShowComparison(comparison);
            _view.ShowStatus(comparison.HasChanges
                ? $"Compared with the previous run: {comparison.Rows.Count} field(s) differ."
                : "Compared with the previous run: nothing differs.");
        }
        catch (CalculationException refused)
        {
            _view.ShowStatus(refused.Message);
        }
    }

    private void Show(CalculationSurfaceAttempt attempt, string verb)
    {
        if (attempt.Succeeded)
        {
            _view.ShowRun(attempt.Run!);
            _view.ShowStatus(attempt.Run!.Run.IsRefused
                ? $"The method refused this input; nothing was computed. The refusal is recorded as '{attempt.Run.DisplayName}'."
                : $"{verb} as '{attempt.Run.DisplayName}'. {attempt.Run.Run.OutcomeSummary}.");
            return;
        }

        var outcome = attempt.Outcome;
        _view.ShowProblems(outcome.Problems, outcome.Refusal is CalculationModuleRefusal.InputInvalid or CalculationModuleRefusal.RecordNotFound or CalculationModuleRefusal.RecordNotReleased or CalculationModuleRefusal.RecordIncomplete ? outcome.Reason : null);
        _view.ShowStatus(outcome.Refusal switch
        {
            CalculationModuleRefusal.InputInvalid => "The calculation rejected the input as malformed. Nothing was recorded.",
            CalculationModuleRefusal.InputIncomplete => "Some inputs could not be read. Nothing was calculated.",
            CalculationModuleRefusal.RecordNotReleased => "A picked record is not released. Nothing was calculated.",
            CalculationModuleRefusal.RecordNotFound => "A picked record no longer exists. Nothing was calculated.",
            CalculationModuleRefusal.RecordIncomplete => "A picked record cannot supply every input read from it. Nothing was calculated.",
            _ => outcome.Reason ?? "Nothing was calculated.",
        });
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
