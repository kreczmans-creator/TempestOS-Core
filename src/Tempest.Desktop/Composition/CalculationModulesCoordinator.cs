using Tempest.Core.Calculations.Modules;
using Tempest.Desktop.Views;
using Tempest.Workspace.Engineering;

namespace Tempest.Desktop.Composition;

/// <summary>
/// The Engineering Calculators' own collaborator (`WP 21.7B`, the same
/// `ADR-0103` shape as <see cref="EngineeringCalculationCoordinator"/>):
/// it hands <see cref="CalculationModulesView"/> the catalogue and the
/// released materials, fills a form from a picked record, and turns
/// Calculate into a run the view renders.
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

        _view.ModuleSelected += module => _ = GuardedAsync(() => FillFromPickedMaterialAsync(module), "generate that calculation's form");
        _view.MaterialPicked += material => _ = GuardedAsync(() => ApplyMaterialAsync(material), "read that material record");
        _view.CalculateRequested += () => _ = GuardedAsync(CalculateAsync, "run that calculation");
    }

    /// <summary>Re-reads the catalogue and the released materials — on entry, and after a release elsewhere in the session.</summary>
    public Task RefreshAsync() => GuardedAsync(RefreshCoreAsync, "load the calculators");

    private async Task RefreshCoreAsync()
    {
        _view.ShowCatalogue(CalculationModuleWorkbench.Catalogue());
        _view.ShowMaterials(await _workbench.ListReleasedMaterialsAsync().ConfigureAwait(true));
    }

    private Task FillFromPickedMaterialAsync(CalculationModuleDescriptor module) =>
        _view.SelectedMaterial is { } material ? ApplyMaterialAsync(material) : Task.CompletedTask;

    private async Task ApplyMaterialAsync(ReleasedMaterialOption material)
    {
        if (_view.SelectedModule is not { } module)
            return;

        if (module.Inputs.All(i => i.MaterialPropertyName is null && i.Kind != CalculationInputKind.Reference))
        {
            _view.ShowStatus($"{module.Title} takes nothing from a material record.");
            return;
        }

        _view.ApplyFill(await _workbench.FillFromMaterialAsync(module, material.RecordId).ConfigureAwait(true));
    }

    private async Task CalculateAsync()
    {
        if (_view.SelectedModule is not { } module)
        {
            _view.ShowStatus(CalculationModulesView.PickModuleGuidance);
            return;
        }

        var attempt = await _workbench.CalculateAsync(module, _view.ReadForm(), _view.SelectedMaterial).ConfigureAwait(true);

        if (attempt.Succeeded)
        {
            _view.ShowRun(attempt.Run!);
            _view.ShowStatus(attempt.Run!.IsRefused
                ? "The method refused this input; nothing was computed. The refusal is recorded with the calculation."
                : $"Calculated and recorded. {attempt.Run.OutcomeSummary}.");
            return;
        }

        _view.ShowProblems(attempt.Problems, attempt.Rejection);
        _view.ShowStatus(attempt.Rejection is not null
            ? "The calculation rejected the input as malformed. Nothing was recorded."
            : "Some inputs could not be read. Nothing was calculated.");
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
