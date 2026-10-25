using Tempest.Calc.Sheets;
using Tempest.Core.EngineeringDomain;

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
        {
            if (FeatureRefusal(step.TemplateId) is { } switchedOff)
                return $"Pack template '{template.Title}': step '{step.Key}': {switchedOff}";

            if (_catalogue.Find(step.TemplateId) is not { } entry)
                return $"Pack template '{template.Title}': step '{step.Key}' names template '{step.TemplateId}', which is not registered.";

            foreach (var symbol in (step.Example ?? []).Select(v => v.Symbol).Concat(step.Omit ?? []))
                if (entry.Template.FindInput(symbol) is null)
                    return $"Pack template '{template.Title}': step '{step.Key}' gives an example for '{symbol}', which '{step.TemplateId}' does not have.";
        }

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

            if ((b.Row is null) != (b.Column is null))
                return $"Pack template '{template.Title}': a link to a table cell needs both a row and a column ({b.FromStep} → {b.ToStep}.{b.Input}).";

            if (b.Row is { } row && b.Column is { } column)
            {
                var table = _catalogue.Get(target.TemplateId).Template.FindInput(b.Input);

                if (table is not { Kind: CalcInputKind.Table })
                    return $"Pack template '{template.Title}': '{target.TemplateId}' has no table input '{b.Input}'.";

                if (row < 0 || table.Columns?.FirstOrDefault(c => string.Equals(c.Key, column, StringComparison.Ordinal)) is not { Kind: CalcInputKind.Quantity or CalcInputKind.Number })
                    return $"Pack template '{template.Title}': '{target.TemplateId}' table '{b.Input}' has no number or quantity column '{column}'.";

                continue;
            }

            if (_catalogue.Get(target.TemplateId).Template.FindInput(b.Input) is not { Kind: CalcInputKind.Quantity or CalcInputKind.Number })
                return $"Pack template '{template.Title}': '{target.TemplateId}' has no number or quantity input '{b.Input}'.";
        }

        return null;
    }

    /// <summary>The provenance basis of every example input a pack template supplies.</summary>
    public const string ExampleBasis = "example input — replace with your own";

    /// <summary>
    /// The inputs <paramref name="step"/> opens on when the caller gives none: its example inputs over the sheet's opening inputs (less the
    /// inputs a link supplies and the step's omissions), or <see langword="null"/> when the step has no example. Each example input
    /// cites <paramref name="chosen"/> and each opening input left in place cites <paramref name="rest"/>; both default to the
    /// assumption "<see cref="ExampleBasis"/>".
    /// </summary>
    public IReadOnlyList<CalcInputValue>? ExampleInputs(CalcPackTemplate template, CalcPackStep step, CalcProvenance? chosen = null, CalcProvenance? rest = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(step);

        if (step.Example is not { Count: > 0 } example)
            return null;

        var linked = template.Bindings.Where(b => string.Equals(b.ToStep, step.Key, StringComparison.Ordinal) && b.Row is null).Select(b => b.Input)
            .Concat(step.Omit ?? []).ToHashSet(StringComparer.Ordinal);
        var set = new CalcInputSet(DefaultInputs(_catalogue.Get(step.TemplateId).Template).Where(v => !linked.Contains(v.Symbol)));
        var named = example.Select(v => v.Symbol).ToHashSet(StringComparer.Ordinal);

        foreach (var value in example)
            set.Set(value);

        var assumed = CalcProvenance.Assumed(ExampleBasis);
        return [.. set.Values.Select(v => v.Provenance is not null ? v : v with { Provenance = named.Contains(v.Symbol) ? chosen ?? assumed : rest ?? assumed })];
    }

    private static string BlockedReason(CalcPackTemplate template, CalcPackBinding binding, CalcPackStep target, string reason)
    {
        var hint = template.Steps.First(s => string.Equals(s.Key, binding.FromStep, StringComparison.Ordinal)).Hint;
        return $"Step '{binding.FromStep}' is blocked, so '{target.Key}' cannot take '{binding.Output}' from it: {reason}{(string.IsNullOrWhiteSpace(hint) ? string.Empty : " " + hint)}";
    }

    /// <summary>
    /// Creates every calculation of <paramref name="template"/> under the project (with <paramref name="inputs"/> per step
    /// key, else the step's example inputs, else the template defaults), binds the links in step order and gathers them in one draft pack.
    /// </summary>
    public async Task<(CalcPackInstantiation? Result, string? Refusal)> InstantiatePackTemplateAsync(
        CalcPackTemplate template, Guid? projectId, string title,
        IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>>? inputs = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        if (ValidatePackTemplate(template) is { } invalid)
            return (null, invalid);

        // Run the whole chain in memory first, so a refusal leaves no stray drafts behind.
        if (DryRunRefusal(template, inputs, cancellationToken) is { } refused)
            return (null, refused);

        // The names are business identifiers, unique within the project: a clash is a refusal now, not an exception half way through (Red R-A-13).
        if (NameClash(template, projectId, title) is { } clash)
            return (null, clash);

        var created = new Dictionary<string, CalcInstance>(StringComparer.Ordinal);

        foreach (var step in template.Steps)
        {
            var given = inputs is not null && inputs.TryGetValue(step.Key, out var list) ? list : ExampleInputs(template, step);
            CalcInstance instance;

            try
            {
                instance = await CreateAsync(step.TemplateId, projectId, step.Name, given, cancellationToken).ConfigureAwait(false);
            }
            catch (CalcLinkRefusedException ex)
            {
                return (null, $"Step '{step.Key}': {ex.Message}");
            }
            catch (DuplicateBusinessIdentifierException ex)
            {
                return (null, $"Step '{step.Key}': {ex.Message}");
            }

            foreach (var b in template.Bindings.Where(b => string.Equals(b.ToStep, step.Key, StringComparison.Ordinal)))
            {
                // A blocked upstream has no outputs to give; say why rather than that an output is missing.
                if (created[b.FromStep].Blocks is { Count: > 0 } blocks)
                    return (null, BlockedReason(template, b, step, blocks[0].Message));

                var bound = b.Row is { } row && b.Column is { } column
                    ? await BindCellAsync(instance.Id, b.Input, row, column, created[b.FromStep].Id, b.Output, cancellationToken).ConfigureAwait(false)
                    : await BindAsync(instance.Id, b.Input, created[b.FromStep].Id, b.Output, cancellationToken).ConfigureAwait(false);

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

    /// <summary>Why <paramref name="template"/> cannot be instantiated under <paramref name="projectId"/>: a step, or the pack, would take a name the project already uses.</summary>
    private string? NameClash(CalcPackTemplate template, Guid? projectId, string title)
    {
        if (projectId is not { } scope)
            return null;

        string? Taken(string kind, string identifier) =>
            _domain.BusinessIdentifierIndex.FindConflict(kind, scope, identifier, Guid.Empty) is null ? null : identifier;

        foreach (var step in template.Steps)
        {
            var name = CalcSheetTextRenderer.Clean(step.Name.Length > 200 ? step.Name[..200] : step.Name);

            if (Taken(RegisterKind, name) is not null || Taken(Tempest.Core.Evidence.Evidence.CanonicalKind, EvidenceTitle(name, _catalogue.Get(step.TemplateId).Template.Title, "A")) is not null)
                return $"The project already holds a calculation called '{name}' (step '{step.Key}'); {template.Title} cannot be instantiated here twice, or beside a calculation of that name. Rename the existing one first.";
        }

        var packTitle = CalcSheetTextRenderer.Clean(title.Length > 200 ? title[..200] : title);
        return Taken(Tempest.Core.Evidence.Evidence.CanonicalKind, $"{packTitle} — calculation pack") is not null
            ? $"The project already holds a calculation pack called '{packTitle}'; choose another title."
            : null;
    }

    /// <summary>
    /// The refusal instantiating <paramref name="template"/> would meet, found by running every step in memory with its
    /// linked values (nothing is stored); <see langword="null"/> when every link can be bound.
    /// </summary>
    private string? DryRunRefusal(CalcPackTemplate template, IReadOnlyDictionary<string, IReadOnlyList<CalcInputValue>>? inputs, CancellationToken cancellationToken)
    {
        var runs = new Dictionary<string, CalcInstance>(StringComparer.Ordinal);

        foreach (var step in template.Steps)
        {
            var entry = _catalogue.Get(step.TemplateId);
            var values = (inputs is not null && inputs.TryGetValue(step.Key, out var given) ? given : ExampleInputs(template, step) ?? DefaultInputs(entry.Template)).ToList();

            foreach (var b in template.Bindings.Where(b => string.Equals(b.ToStep, step.Key, StringComparison.Ordinal)))
            {
                var upstream = runs[b.FromStep];

                if (upstream.Blocks is { Count: > 0 } blocks)
                    return BlockedReason(template, b, step, blocks[0].Message);

                if (upstream.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, b.Output, StringComparison.Ordinal)) is not { Si: { } } output)
                {
                    // Say why when the upstream sheet has a warning naming the output (only that one, never an unrelated warning) (E1 gives no P_rated without its rated speed n_rated).
                    var reason = upstream.Warnings.FirstOrDefault(w => w.Message.Contains(b.Output, StringComparison.Ordinal));
                    var why = reason is null ? string.Empty : " " + reason.Message;
                    return $"Step '{step.Key}': '{step.Name}' cannot take '{b.Output}': '{upstream.Name}' gives no numeric output '{b.Output}'.{why}";
                }

                var spec = entry.Template.FindInput(b.Input)!;

                if (b.Row is { } row && b.Column is { } column)
                {
                    var index = values.FindIndex(v => string.Equals(v.Symbol, b.Input, StringComparison.Ordinal));
                    var columnSpec = spec.Columns!.First(c => string.Equals(c.Key, column, StringComparison.Ordinal));

                    if (index < 0 || values[index].Rows is not { } rows || row >= rows.Count)
                        return $"Step '{step.Key}': '{b.Input}' of '{step.Name}' has {(index < 0 ? 0 : values[index].Rows?.Count ?? 0)} row(s); there is no row {row + 1} to link (a link never creates rows).";

                    if (ConvertFor(columnSpec.Kind, columnSpec.DefaultUnit, output) is not { } cell)
                        return $"Step '{step.Key}': '{upstream.Name}'.{b.Output} cannot feed '{CellLabel(b.Input, row, column)}': the dimensions differ.";

                    values[index] = WithCell(values[index], row, column, new CalcCell(cell.Number, cell.Unit));
                    continue;
                }

                if (ValueFor(spec, output) is not { } value)
                    return $"Step '{step.Key}': '{upstream.Name}'.{b.Output} cannot feed '{b.Input}': the dimensions differ.";

                values.RemoveAll(v => string.Equals(v.Symbol, b.Input, StringComparison.Ordinal));
                values.Add(value);
            }

            var stub = new CalcInstance(Guid.Empty, entry.Reference, entry.ContentHash, null, step.Name, "A", values, [], [], [], [],
                CalcInstanceStatus.Draft, null, null, string.Empty, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
            runs[step.Key] = Snapshot(stub, CalcRunner.Run(entry.Calculator, new CalcInputSet(values), _engine, cancellationToken: cancellationToken));
        }

        return null;
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

            // A template names its steps; steps from several projects would copy other projects' calculation names into one stored template (Red R-A-12).
            if (members.Count > 0 && instance.ProjectId != members[0].ProjectId)
                return (null, $"'{instance.Name}' is in another project than '{members[0].Name}'; a pack template is made from the calculations of one project.");

            members.Add(instance);
        }

        // Upstream first: repeatedly take a member none of whose links come from a member not yet taken.
        var ordered = new List<CalcInstance>();
        var remaining = members.ToList();

        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(m => m.Links.All(l => !remaining.Any(r => r.Id == l.Link.UpstreamId)) && m.CellLinks.All(l => !remaining.Any(r => r.Id == l.Link.UpstreamId)));

            if (next is null)
                return (null, "The calculations link to each other in a cycle.");

            ordered.Add(next);
            remaining.Remove(next);
        }

        var keys = ordered.Select((m, i) => (m.Id, Key: $"s{i + 1}")).ToDictionary(x => x.Id, x => x.Key);
        var steps = ordered.Select(m => new CalcPackStep(keys[m.Id], m.TemplateId, m.Name)).ToList();
        var bindings = ordered.SelectMany(m => m.Links
            .Where(l => keys.ContainsKey(l.Link.UpstreamId))
            .Select(l => new CalcPackBinding(keys[l.Link.UpstreamId], l.Link.OutputSymbol, keys[m.Id], l.Input.Symbol))
            .Concat(m.CellLinks
                .Where(l => keys.ContainsKey(l.Link.UpstreamId))
                .Select(l => new CalcPackBinding(keys[l.Link.UpstreamId], l.Link.OutputSymbol, keys[m.Id], l.Symbol, l.Row, l.Column)))).ToList();

        var template = new CalcPackTemplate($"saved-{Guid.NewGuid():N}", CalcSheetTextRenderer.Clean(title.Length > 200 ? title[..200] : title), "Saved from a linked chain.", steps, bindings);

        if (ValidatePackTemplate(template) is { } invalid)
            return (null, invalid);

        await _store.SavePackTemplateAsync(template, cancellationToken).ConfigureAwait(false);
        return (template, null);
    }
}
