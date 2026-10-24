using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>Addendum A, L1 — pack templates: a saved chain becomes a linked set of calculations in one pack.</summary>
public sealed partial class CalcInstanceService
{
    /// <summary>The pack templates this application composes in.</summary>
    public IReadOnlyList<CalcPackTemplate> PackTemplates => _packTemplates;

    /// <summary>The composed pack templates, then those saved from linked chains.</summary>
    public async Task<IReadOnlyList<CalcPackTemplate>> ListPackTemplatesAsync(CancellationToken cancellationToken = default) =>
        [.. _packTemplates, .. await _store.ListPackTemplatesAsync(cancellationToken).ConfigureAwait(false)];

    /// <summary>Why a pack template cannot be used here, or <see langword="null"/> when it can.</summary>
    public string? ValidatePackTemplate(CalcPackTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (template.Steps.Count == 0 || template.Steps.Count > CalcPackTemplate.MaxSteps)
            return $"Pack template '{template.Title}' needs between 1 and {CalcPackTemplate.MaxSteps} steps.";

        var order = template.Steps.Select((s, i) => (s.Key, i)).ToDictionary(x => x.Key, x => x.i, StringComparer.Ordinal);

        if (order.Count != template.Steps.Count)
            return $"Pack template '{template.Title}' repeats a step key.";

        foreach (var step in template.Steps)
            if (_catalogue.Find(step.TemplateId) is null)
                return $"Pack template '{template.Title}': step '{step.Key}' names template '{step.TemplateId}', which is not registered.";

        foreach (var b in template.Bindings)
        {
            if (!order.TryGetValue(b.FromStep, out var from) || !order.TryGetValue(b.ToStep, out var to))
                return $"Pack template '{template.Title}': a link names a step that is not in it ({b.FromStep} → {b.ToStep}).";

            // Steps are listed upstream first, so a template cannot describe a cycle.
            if (from >= to)
                return $"Pack template '{template.Title}': '{b.FromStep}' must come before '{b.ToStep}' to feed it.";

            var source = template.Steps[from];
            var target = template.Steps[to];

            if (!OutputPorts(source.TemplateId).Any(p => string.Equals(p.Symbol, b.Output, StringComparison.Ordinal)))
                return $"Pack template '{template.Title}': '{source.TemplateId}' offers no output '{b.Output}'.";

            if (_catalogue.Get(target.TemplateId).Template.FindInput(b.Input) is not { Kind: CalcInputKind.Quantity or CalcInputKind.Number })
                return $"Pack template '{template.Title}': '{target.TemplateId}' has no number or quantity input '{b.Input}'.";
        }

        return null;
    }

    /// <summary>
    /// Creates every calculation of <paramref name="template"/> under the project (with <paramref name="inputs"/> per step
    /// key, else the template defaults), binds the links in step order and gathers them in one draft pack.
    /// </summary>
    public async Task<(CalcPackInstantiation? Result, string? Refusal)> InstantiatePackTemplateAsync(
        CalcPackTemplate template, Guid? projectId, string title,
        IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>>? inputs = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (ValidatePackTemplate(template) is { } invalid)
            return (null, invalid);

        var created = new Dictionary<string, CalcInstance>(StringComparer.Ordinal);

        foreach (var step in template.Steps)
        {
            var given = inputs is not null && inputs.TryGetValue(step.Key, out var list) ? list : null;
            CalcInstance instance;

            try
            {
                instance = await CreateAsync(step.TemplateId, projectId, step.Name, given, cancellationToken).ConfigureAwait(false);
            }
            catch (CalcLinkRefusedException ex)
            {
                return (null, $"Step '{step.Key}': {ex.Message}");
            }

            foreach (var b in template.Bindings.Where(b => string.Equals(b.ToStep, step.Key, StringComparison.Ordinal)))
            {
                var bound = await BindAsync(instance.Id, b.Input, created[b.FromStep].Id, b.Output, cancellationToken).ConfigureAwait(false);

                if (!bound.Succeeded)
                    return (null, $"Step '{step.Key}': {bound.Reason}");

                instance = bound.Instance!;
            }

            created[step.Key] = instance;
        }

        var pack = await CreatePackAsync(projectId, title, [.. template.Steps.Select(s => created[s.Key].Id)], cancellationToken).ConfigureAwait(false);
        var members = new Dictionary<string, CalcInstance>(StringComparer.Ordinal);

        foreach (var (key, instance) in created)
            members[key] = await _store.FindInstanceAsync(instance.Id, cancellationToken).ConfigureAwait(false) ?? instance;

        return (new CalcPackInstantiation(pack, members), null);
    }

    /// <summary>
    /// Saves (and stores) a linked chain the user built by hand as a pack template: its calculations in link order and the links
    /// between them. Links to calculations outside the chain are left out (they would have no step to come from).
    /// </summary>
    public async Task<(CalcPackTemplate? Template, string? Refusal)> PackTemplateFromChainAsync(string title, IReadOnlyList<Guid> instanceIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(instanceIds);

        if (instanceIds.Count == 0 || instanceIds.Count > CalcPackTemplate.MaxSteps)
            return (null, $"A pack template holds between 1 and {CalcPackTemplate.MaxSteps} calculations.");

        var members = new List<CalcInstance>();

        foreach (var id in instanceIds.Distinct())
        {
            var instance = await _store.FindInstanceAsync(id, cancellationToken).ConfigureAwait(false);

            if (instance is null)
                return (null, $"No calculation '{id}' exists.");

            members.Add(instance);
        }

        // Upstream first: repeatedly take a member none of whose links come from a member not yet taken.
        var ordered = new List<CalcInstance>();
        var remaining = members.ToList();

        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(m => m.Links.All(l => !remaining.Any(r => r.Id == l.Link.UpstreamId)));

            if (next is null)
                return (null, "The calculations link to each other in a cycle.");

            ordered.Add(next);
            remaining.Remove(next);
        }

        var keys = ordered.Select((m, i) => (m.Id, Key: $"s{i + 1}")).ToDictionary(x => x.Id, x => x.Key);
        var steps = ordered.Select(m => new CalcPackStep(keys[m.Id], m.TemplateId, m.Name)).ToList();
        var bindings = ordered.SelectMany(m => m.Links
            .Where(l => keys.ContainsKey(l.Link.UpstreamId))
            .Select(l => new CalcPackBinding(keys[l.Link.UpstreamId], l.Link.OutputSymbol, keys[m.Id], l.Input.Symbol))).ToList();

        var template = new CalcPackTemplate($"saved-{Guid.NewGuid():N}", CalcSheetTextRenderer.Clean(title.Length > 200 ? title[..200] : title), "Saved from a linked chain.", steps, bindings);

        if (ValidatePackTemplate(template) is { } invalid)
            return (null, invalid);

        await _store.SavePackTemplateAsync(template, cancellationToken).ConfigureAwait(false);
        return (template, null);
    }
}
