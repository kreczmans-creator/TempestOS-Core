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
/// <param name="Dropped">The symbols of the draft that were not carried: those the current template no longer declares, two old symbols of one input holding different values, and an input whose meaning changed since the draft's version (the message says which and why).</param>
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

        // A draft made by a newer build than this one is not "revised to" this build's version; it is shown as stored (OpenOlderVersion).
        if (_catalogue.Resolve(instance.TemplateReference).State == CalcVersionState.Newer || _catalogue.Find(instance.TemplateId) is not { } current)
            return null;

        var carried = new CalcInputSet();
        var dropped = new List<string>();
        var reasons = new List<string>();
        var template = current.Template;

        // C-2: an input that was renamed carries over under its new symbol instead of being dropped. The old record is read input by input: two old
        // symbols that resolve to one input (the current symbol beside an alias, or two aliases) carry it once when they hold the same value and
        // carry neither when they differ, both named (ADR-0177 review: nothing is chosen silently).
        foreach (var group in instance.Inputs.GroupBy(v => template.ResolveInput(v.Symbol)?.Symbol ?? v.Symbol, StringComparer.Ordinal))
        {
            var values = group.ToList();

            if (template.ResolveInput(group.Key) is not { } spec)
            {
                dropped.AddRange(values.Select(v => v.Symbol));
                continue;
            }

            if (values.Any(v => spec.MeaningChangedSince(v.Symbol, instance.TemplateVersion)))
            {
                dropped.AddRange(values.Select(v => v.Symbol));
                reasons.Add($"{spec.MeaningChangeText(instance.TemplateId, instance.TemplateReference)}, so its stored value was not carried: enter it afresh.");
                continue;
            }

            if (values.Count > 1 && values.Any(v => !CalcInputAliases.SameValue(spec, values[0], v)))
            {
                dropped.AddRange(values.Select(v => v.Symbol));
                reasons.Add($"{spec.Symbol} was stored as {string.Join(" and as ", values.Select(v => $"'{v.Symbol}'"))} with different values, so neither was carried: enter it afresh.");
                continue;
            }

            var chosen = (values.FirstOrDefault(v => string.Equals(v.Symbol, spec.Symbol, StringComparison.Ordinal)) ?? values[0]) with { Symbol = spec.Symbol };

            // A bare number is carried with the unit it was recorded in, so an input whose default unit changed since the draft's version keeps its
            // value on the new sheet (ADR-0177 review, CalcInputUnits); one the draft's record cannot date is left out, named.
            if (!CalcInputUnits.TryPin(spec, chosen, instance.TemplateVersion, out var pinned, out var unitRefusal))
            {
                dropped.AddRange(values.Select(v => v.Symbol));
                reasons.Add($"{unitRefusal} It was not carried.");
                continue;
            }

            carried.Set(pinned);
        }

        var gone = dropped.Where(d => template.ResolveInput(d) is null).ToList();
        var droppedText = gone.Count == 0 ? string.Empty : $" {gone.Count} input{(gone.Count == 1 ? "" : "s")} the new version no longer has ({string.Join(", ", gone.Take(8))}{(gone.Count > 8 ? ", …" : string.Empty)}) {(gone.Count == 1 ? "was" : "were")} left out.";
        var reasonText = reasons.Count == 0 ? string.Empty : " " + string.Join(" ", reasons.Take(8)) + (reasons.Count > 8 ? $" And {reasons.Count - 8} more inputs were left out for the same reasons." : string.Empty);
        var message = $"'{instance.Name}' was made on {instance.TemplateReference}, which has been revised to {current.Reference}, so that draft can no longer be edited, checked or issued. Its inputs are loaded into a new sheet on {current.Reference}: check them, then Save to keep it as a new calculation. The old draft is left unchanged.{droppedText}{reasonText}";
        return new CalcDraftUpgrade(instance, current, carried, dropped, message);
    }
}
