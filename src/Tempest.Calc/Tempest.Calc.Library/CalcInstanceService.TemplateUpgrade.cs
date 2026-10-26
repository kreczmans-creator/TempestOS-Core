using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>
/// What opening a draft made on an older version of a template comes to (Grey GR-B-01 / GR-A-01): the version it was made on is
/// not installed, so the draft cannot be edited, checked or issued; its inputs are offered as the starting point of a new,
/// unsaved sheet on the current version, and the draft itself is left exactly as it was.
/// </summary>
/// <param name="Instance">The draft, unchanged.</param>
/// <param name="Current">The current version of the same template, on which a new sheet can run.</param>
/// <param name="Inputs">The draft's inputs that the current template still declares, as the seed of the new sheet.</param>
/// <param name="Dropped">The symbols of the draft that the current template no longer declares.</param>
/// <param name="Message">The one message to show: which versions, that the draft is untouched, and what to do next.</param>
public sealed record CalcDraftUpgrade(CalcInstance Instance, CalcCatalogueEntry Current, CalcInputSet Inputs, IReadOnlyList<string> Dropped, string Message);

public sealed partial class CalcInstanceService
{
    /// <summary>
    /// A draft made on a version of its template that is no longer installed cannot run (the calculation would use another version's
    /// formulas): the reference "id@old" is not in the catalogue while "id@new" is. This returns the plan that opens it as a new sheet
    /// on the current version, carrying the inputs that still exist, or <see langword="null"/> when the draft's own version is
    /// installed (nothing to plan) or the template is gone altogether (nothing to open it on). Nothing is written.
    /// </summary>
    public CalcDraftUpgrade? PlanDraftUpgrade(CalcInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        if (instance.Status != CalcInstanceStatus.Draft || _catalogue.Find(instance.TemplateReference) is not null)
            return null;

        if (_catalogue.Find(instance.TemplateId) is not { } current)
            return null;

        var carried = new CalcInputSet();
        var dropped = new List<string>();

        foreach (var value in instance.Inputs)
        {
            // C-2: an input that was renamed carries over under its new symbol instead of being dropped.
            if (current.Template.ResolveInput(value.Symbol) is not { } spec)
                dropped.Add(value.Symbol);
            else if (carried.Find(spec.Symbol) is null || string.Equals(value.Symbol, spec.Symbol, StringComparison.Ordinal))
                carried.Set(value with { Symbol = spec.Symbol });
        }

        var droppedText = dropped.Count == 0 ? string.Empty : $" {dropped.Count} input{(dropped.Count == 1 ? "" : "s")} the new version no longer has ({string.Join(", ", dropped.Take(8))}{(dropped.Count > 8 ? ", …" : string.Empty)}) {(dropped.Count == 1 ? "was" : "were")} left out.";
        var message = $"'{instance.Name}' was made on {instance.TemplateReference}, which has been revised to {current.Reference}, so that draft can no longer be edited, checked or issued. Its inputs are loaded into a new sheet on {current.Reference}: check them, then Save to keep it as a new calculation. The old draft is left unchanged.{droppedText}";
        return new CalcDraftUpgrade(instance, current, carried, dropped, message);
    }
}
