using System.Runtime.CompilerServices;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>
/// Raised when a calculation's outputs change or it is superseded: every calculation downstream of it, through any
/// number of links, is now stale (Addendum A, L1). The Desktop refreshes its stale markers from it.
/// </summary>
public sealed record CalcStalenessChanged(Guid CauseId, string Reason, IReadOnlyList<Guid> AffectedIds) : Tempest.Core.Events.IEvent;

/// <summary>
/// Addendum A, L1 — result linking. An input binds to an output of another calculation instead of a typed value; the
/// binding is pinned to that calculation's revision (an instance is one revision), never to "latest". When the upstream
/// gets new outputs or is superseded, everything downstream is stale: it cannot be checked or issued until it is
/// updated, which re-pins every link to the current revision and recalculates.
/// </summary>
public sealed partial class CalcInstanceService
{
    /// <summary>The revision label a calculation's next revision takes (A → B).</summary>
    public static string NextRevisionLabel(string revision) => NextRevision(revision);

    /// <summary>The longest chain of links allowed, counted in links.</summary>
    public const int MaxChainDepth = 20;

    private static readonly ConditionalWeakTable<ICalcCalculator, IReadOnlyList<CalcOutputPort>> DerivedPorts = [];

    /// <summary>
    /// The outputs a template offers for linking: those it declares, or else those a run on its defaults produces (with
    /// the unit each is shown in).
    /// </summary>
    public IReadOnlyList<CalcOutputPort> OutputPorts(string templateReference)
    {
        var entry = _catalogue.Get(templateReference);

        if (entry.Template.OutputPorts is { Count: > 0 } declared)
            return declared;

        return DerivedPorts.GetValue(entry.Calculator, calculator =>
        {
            var result = CalcRunner.Run(calculator, CalcTemplateHasher.DefaultInputs(calculator.Template), _engine);
            return [.. result.Outputs.Select(o => new CalcOutputPort(o.Symbol, o.Value is ScalarValue s ? s.DisplayUnit.Symbol : string.Empty, o.Description))];
        });
    }

    /// <summary>
    /// Binds input <paramref name="inputSymbol"/> of a draft to output <paramref name="outputSymbol"/> of calculation
    /// <paramref name="upstreamId"/>, pinned to its revision, and recalculates. Refused when the dimensions differ, when
    /// the link would close a cycle, or when the chain would be longer than <see cref="MaxChainDepth"/>.
    /// </summary>
    public async Task<CalcActionResult> BindAsync(Guid instanceId, string inputSymbol, Guid upstreamId, string outputSymbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputSymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputSymbol);
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (instance.Status != CalcInstanceStatus.Draft)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only a draft takes new links.", instance);

        if (upstreamId == instanceId)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' cannot take an input from its own output.", instance);

        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry?.Template.FindInput(inputSymbol) is not { Kind: CalcInputKind.Quantity or CalcInputKind.Number } spec)
            return CalcActionResult.Refused($"'{inputSymbol}' is not a number or quantity input of '{instance.Name}'; only those can be linked.", instance);

        var upstream = await _store.FindInstanceAsync(upstreamId, cancellationToken).ConfigureAwait(false);

        if (upstream is null)
            return CalcActionResult.Refused($"No calculation '{upstreamId}' exists to link to.", instance);

        if (upstream.Status == CalcInstanceStatus.Superseded)
            return CalcActionResult.Refused($"'{upstream.Name}' rev {upstream.Revision} is superseded; link to its current revision.", instance);

        if (upstream.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, outputSymbol, StringComparison.Ordinal)) is not { Si: { } } output)
            return CalcActionResult.Refused($"'{upstream.Name}' has no numeric output '{outputSymbol}'.", instance);

        if (ValueFor(spec, output) is not { } value)
            return CalcActionResult.Refused($"'{upstream.Name}'.{outputSymbol} is in {Shown(output.Unit)} ({DimensionText(output.Unit)}); '{inputSymbol}' needs {Shown(spec.DefaultUnit)} ({DimensionText(spec.Kind == CalcInputKind.Number ? null : spec.DefaultUnit)}). The dimensions differ, so the link is refused.", instance);

        var instances = await AllInstancesAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false);
        instances[instance.Id] = instance;

        if (CycleOrDepthProblem(instance, upstream, instances) is { } problem)
            return CalcActionResult.Refused(problem.Replace("{input}", inputSymbol, StringComparison.Ordinal), instance);

        var bound = value with { Provenance = CalcProvenance.Link(upstream.Id, outputSymbol, upstream.Revision) };
        var inputs = instance.Inputs.Where(i => !string.Equals(i.Symbol, inputSymbol, StringComparison.Ordinal)).Append(bound).ToList();
        return await UpdateInputsAsync(instanceId, inputs, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// "Update to revision n": re-pins every link to the current revision of its upstream calculation and recalculates.
    /// A draft is updated in place; a checked or issued calculation becomes a new revision (the old one is superseded),
    /// so the change is checked and issued again.
    /// </summary>
    public async Task<CalcActionResult> UpdateLinksAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (instance.Links.Count == 0)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' has no linked inputs.", instance);

        if (instance.Status == CalcInstanceStatus.Superseded)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' rev {instance.Revision} is superseded; update its current revision.", instance);

        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is null)
            return CalcActionResult.Refused($"Template '{instance.TemplateReference}' is no longer registered.", instance);

        var instances = await AllInstancesAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false);
        var inputs = instance.Inputs.ToList();

        foreach (var (input, link) in instance.Links)
        {
            if (!instances.TryGetValue(link.UpstreamId, out var pinned))
                return CalcActionResult.Refused($"'{input.Symbol}' is linked to calculation {link.UpstreamId}, which no longer exists.", instance);

            var current = Latest(pinned, instances);

            if (current.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, link.OutputSymbol, StringComparison.Ordinal)) is not { Si: { } } output
                || entry.Template.FindInput(input.Symbol) is not { } spec || ValueFor(spec, output) is not { } value)
                return CalcActionResult.Refused($"'{current.Name}' rev {current.Revision} no longer gives '{link.OutputSymbol}' in a unit '{input.Symbol}' takes; re-link it by hand.", instance);

            if (CycleOrDepthProblem(instance, current, instances) is { } problem)
                return CalcActionResult.Refused(problem.Replace("{input}", input.Symbol, StringComparison.Ordinal), instance);

            inputs[inputs.IndexOf(input)] = value with { Provenance = CalcProvenance.Link(current.Id, link.OutputSymbol, current.Revision) };
        }

        return instance.Status == CalcInstanceStatus.Draft
            ? await UpdateInputsAsync(instanceId, inputs, cancellationToken).ConfigureAwait(false)
            : await ReviseCoreAsync(instanceId, inputs, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// An instance is stale when its template changed, when a linked upstream calculation was superseded or now gives a
    /// different value than the one pinned, or when anything it is linked to is itself stale — through any number of links.
    /// </summary>
    public async Task<CalcStaleness> StalenessAsync(CalcInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var instances = await AllInstancesAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false);
        instances[instance.Id] = instance;
        var reasons = new List<string>();
        Stale(instance, instances, reasons, [], 0);
        await RecordStalenessAsync(instance, reasons, cancellationToken).ConfigureAwait(false);
        return reasons.Count == 0 ? CalcStaleness.Fresh : new CalcStaleness(true, reasons);
    }

    /// <summary>Every calculation that takes a value from <paramref name="instanceId"/>, directly or through others.</summary>
    public async Task<IReadOnlyList<Guid>> DownstreamAsync(Guid instanceId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        var instances = await AllInstancesAsync(projectId, cancellationToken).ConfigureAwait(false);
        return Downstream(instanceId, instances);
    }

    // ------------------------------------------------------------------

    private void Stale(CalcInstance instance, Dictionary<Guid, CalcInstance> instances, List<string> reasons, HashSet<Guid> visiting, int depth)
    {
        if (depth == 0)
        {
            var current = _catalogue.Find(instance.TemplateId);

            if (current is null)
                reasons.Add($"Template '{instance.TemplateReference}' is no longer registered.");
            else if (!string.Equals(current.Reference, instance.TemplateReference, StringComparison.Ordinal))
                reasons.Add($"Template {instance.TemplateReference} has a newer version {current.Reference}.");
            else if (!string.Equals(current.ContentHash, instance.TemplateHash, StringComparison.Ordinal))
                reasons.Add($"Template {instance.TemplateReference} content changed (hash {CalcTemplateHasher.Short(instance.TemplateHash)} → {current.ShortHash}).");
        }

        if (depth > MaxChainDepth || !visiting.Add(instance.Id))
            return;

        // The first form of linking (v0.26.0): a "reference: calc:<id>" provenance, stale when the upstream outputs hash moved.
        foreach (var upstreamId in instance.UpstreamIds.Except(instance.Links.Select(l => l.Link.UpstreamId)))
        {
            if (!instances.TryGetValue(upstreamId, out var upstream))
            {
                reasons.Add($"Upstream calculation {upstreamId} no longer exists.");
                continue;
            }

            var recorded = instance.UpstreamHashes is { } h && h.TryGetValue(upstreamId.ToString("N"), out var recordedHash) ? recordedHash : null;

            if (recorded is null || !string.Equals(recorded, upstream.OutputsHash, StringComparison.Ordinal))
                reasons.Add($"Upstream calculation '{upstream.Name}' has changed its outputs since this one took values from it.");
        }

        foreach (var (input, link) in instance.Links)
        {
            if (!instances.TryGetValue(link.UpstreamId, out var upstream))
            {
                reasons.Add($"'{input.Symbol}' is linked to calculation {link.UpstreamId}, which no longer exists.");
                continue;
            }

            var latest = Latest(upstream, instances);

            if (latest.Id != upstream.Id)
                reasons.Add($"'{input.Symbol}' is pinned to '{upstream.Name}' rev {upstream.Revision}, which is superseded by rev {latest.Revision}; update to revision {latest.Revision}.");
            else if (upstream.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, link.OutputSymbol, StringComparison.Ordinal)) is not { Si: { } si })
                reasons.Add($"'{upstream.Name}' rev {upstream.Revision} no longer gives '{link.OutputSymbol}'.");
            else if (!SameValue(input, si))
                reasons.Add($"'{upstream.Name}' rev {upstream.Revision} now gives a different '{link.OutputSymbol}' than the value '{input.Symbol}' took.");

            var before = reasons.Count;
            var inner = new List<string>();
            Stale(upstream, instances, inner, visiting, depth + 1);

            if (inner.Count > 0 && reasons.Count == before)
                reasons.Add($"'{upstream.Name}', which '{input.Symbol}' is linked to, is itself stale.");
        }

        visiting.Remove(instance.Id);
    }

    private static bool SameValue(CalcInputValue input, double upstreamSi)
    {
        var si = input.Unit is { Length: > 0 } unit && UnitCatalog.TryParse(unit, out var u, out _) ? u.ToSi(input.Number ?? double.NaN) : input.Number ?? double.NaN;
        return Math.Abs(si - upstreamSi) <= 1e-9 * Math.Max(1.0, Math.Abs(upstreamSi));
    }

    /// <summary>The input value an output gives, in the input's own default unit; <see langword="null"/> when the dimensions differ.</summary>
    private static CalcInputValue? ValueFor(CalcInputSpec spec, CalcOutputSnapshot output)
    {
        var outputUnit = output.Unit is { Length: > 0 } text && UnitCatalog.TryParse(text, out var u, out _) ? u : Unit.One;

        if (spec.Kind == CalcInputKind.Number)
            return outputUnit.Dimension == Dimension.None ? CalcInputValue.Of(spec.Symbol, output.Si!.Value) : null;

        if (!UnitCatalog.TryParse(spec.DefaultUnit, out var target, out _) || target.Dimension != outputUnit.Dimension)
            return null;

        // A temperature difference (K) and an absolute temperature (°C, °F) share a dimension but not a meaning: refused
        // rather than shifted by 273.15 (Opus review, finding 14).
        if (target.IsAffine != outputUnit.IsAffine)
            return null;

        return CalcInputValue.Quantity(spec.Symbol, target.FromSi(output.Si!.Value), spec.DefaultUnit);
    }

    /// <summary>
    /// Why linking <paramref name="instance"/> to <paramref name="upstream"/> is refused (a cycle through any revision of
    /// either, or a chain deeper than the cap), or <see langword="null"/>. "{input}" stands for the input symbol.
    /// </summary>
    private static string? CycleOrDepthProblem(CalcInstance instance, CalcInstance upstream, Dictionary<Guid, CalcInstance> instances)
    {
        instances[instance.Id] = instance;
        var lineage = Lineage(instance, instances);
        var above = Upstream(upstream.Id, instances);
        above.Add(upstream.Id);

        if (above.Overlaps(lineage))
            return $"Linking '{{input}}' to '{upstream.Name}' would close a cycle: '{upstream.Name}' already takes a value, directly or through others, from '{instance.Name}' (or another revision of it).";

        var depth = DepthAbove(upstream.Id, instances) + 1 + DepthBelow(instance.Id, instances);
        return depth > MaxChainDepth ? $"The chain would be {depth} links long; at most {MaxChainDepth} are allowed." : null;
    }

    /// <summary>Every revision of a calculation: its predecessors and successors.</summary>
    private static HashSet<Guid> Lineage(CalcInstance instance, Dictionary<Guid, CalcInstance> instances)
    {
        var set = new HashSet<Guid> { instance.Id };
        var changed = true;

        while (changed)
        {
            changed = false;

            foreach (var other in instances.Values)
            {
                if (set.Contains(other.Id))
                    continue;

                if ((other.PredecessorId is { } p && set.Contains(p)) || set.Any(id => instances.TryGetValue(id, out var x) && x.PredecessorId == other.Id))
                {
                    set.Add(other.Id);
                    changed = true;
                }
            }
        }

        return set;
    }

    /// <summary>The first problem with the links in <paramref name="inputs"/> that differ from the instance's current ones, or <see langword="null"/>.</summary>
    private async Task<string?> LinkProblemAsync(CalcInstance instance, CalcTemplate template, IReadOnlyList<CalcInputValue> inputs, CancellationToken cancellationToken)
    {
        var current = instance.Links.ToDictionary(l => l.Input.Symbol, l => l.Link, StringComparer.Ordinal);
        Dictionary<Guid, CalcInstance>? instances = null;

        foreach (var input in inputs)
        {
            if (CalcLink.From(input.Provenance) is not { } link || (current.TryGetValue(input.Symbol, out var was) && was == link))
                continue;

            instances ??= await AllInstancesAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false);

            var upstream = instances.GetValueOrDefault(link.UpstreamId) ?? await _store.FindInstanceAsync(link.UpstreamId, cancellationToken).ConfigureAwait(false);

            if (upstream is null)
                return $"'{input.Symbol}' is linked to calculation {link.UpstreamId}, which does not exist.";

            if (upstream.Id == instance.Id)
                return $"'{input.Symbol}' cannot take a value from its own calculation.";

            if (template.FindInput(input.Symbol) is not { } spec
                || upstream.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, link.OutputSymbol, StringComparison.Ordinal)) is not { Si: { } } output
                || ValueFor(spec, output) is null)
                return $"'{input.Symbol}' is linked to '{upstream.Name}'.{link.OutputSymbol}, which it cannot take (missing, or a different dimension).";

            if (CycleOrDepthProblem(instance, upstream, instances) is { } problem)
                return problem.Replace("{input}", input.Symbol, StringComparison.Ordinal);
        }

        return null;
    }

    private static string Shown(string? unit) => string.IsNullOrEmpty(unit) ? "a plain number" : unit;

    private static string DimensionText(string? unit) =>
        unit is { Length: > 0 } && UnitCatalog.TryParse(unit, out var u, out _) ? u.Dimension.ToString() : "dimensionless";

    /// <summary>The current revision of a calculation: the end of its chain of successors.</summary>
    private static CalcInstance Latest(CalcInstance instance, Dictionary<Guid, CalcInstance> instances)
    {
        var current = instance;

        for (var hop = 0; hop < 1_000; hop++)
        {
            var next = instances.Values.Where(i => i.PredecessorId == current.Id).OrderByDescending(i => i.CreatedAt).FirstOrDefault();

            if (next is null)
                break;

            current = next;
        }

        return current;
    }

    private static HashSet<Guid> Upstream(Guid id, Dictionary<Guid, CalcInstance> instances)
    {
        var seen = new HashSet<Guid>();
        var queue = new Queue<Guid>([id]);

        while (queue.Count > 0 && seen.Count < 10_000)
        {
            if (!instances.TryGetValue(queue.Dequeue(), out var i))
                continue;

            foreach (var up in i.UpstreamIds)
                if (seen.Add(up))
                    queue.Enqueue(up);
        }

        return seen;
    }

    private static List<Guid> Downstream(Guid id, Dictionary<Guid, CalcInstance> instances)
    {
        var seen = new HashSet<Guid>();
        var order = new List<Guid>();
        var queue = new Queue<Guid>([id]);

        while (queue.Count > 0 && seen.Count < 10_000)
        {
            var current = queue.Dequeue();

            foreach (var down in instances.Values.Where(i => i.UpstreamIds.Contains(current)))
            {
                if (seen.Add(down.Id))
                {
                    order.Add(down.Id);
                    queue.Enqueue(down.Id);
                }
            }
        }

        return order;
    }

    /// <summary>The longest chain of links above <paramref name="id"/>, memoised; a cycle (possible only from forged imported links) counts once.</summary>
    private static int DepthAbove(Guid id, Dictionary<Guid, CalcInstance> instances) =>
        Depth(id, x => instances.TryGetValue(x, out var i) ? i.UpstreamIds : [], [], []);

    private static int DepthBelow(Guid id, Dictionary<Guid, CalcInstance> instances)
    {
        var below = instances.Values.SelectMany(i => i.UpstreamIds.Select(u => (Up: u, Down: i.Id))).ToLookup(x => x.Up, x => x.Down);
        return Depth(id, x => [.. below[x]], [], []);
    }

    private static int Depth(Guid id, Func<Guid, IReadOnlyList<Guid>> next, Dictionary<Guid, int> memo, HashSet<Guid> path)
    {
        if (memo.TryGetValue(id, out var known))
            return known;

        if (!path.Add(id) || path.Count > MaxChainDepth + 2)
            return 0;

        var deepest = 0;

        foreach (var n in next(id))
            deepest = Math.Max(deepest, 1 + Depth(n, next, memo, path));

        path.Remove(id);
        memo[id] = deepest;
        return deepest;
    }

    private async Task<Dictionary<Guid, CalcInstance>> AllInstancesAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        var list = await _store.ListInstancesAsync(projectId, cancellationToken).ConfigureAwait(false);
        var map = new Dictionary<Guid, CalcInstance>();

        foreach (var i in list)
            map[i.Id] = i;

        return map;
    }

    private async Task PublishStalenessAsync(CalcInstance cause, string reason, CancellationToken cancellationToken)
    {
        if (_events is null)
            return;

        var affected = await DownstreamAsync(cause.Id, cause.ProjectId, cancellationToken).ConfigureAwait(false);

        if (affected.Count > 0)
            await _events.PublishAsync(new CalcStalenessChanged(cause.Id, reason, affected), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Names each linked input's source on the sheet ("link: CALC-E01 Motor rev A · T_rms") and each value from a reference
    /// record by its citation ("record: UK NA factors — BS EN 1990 NA, +A1:2005, NA.A1.2(B) (licensed source)").
    /// </summary>
    private async Task<CalcSheet> DescribeLinksAsync(CalcSheet sheet, CalcInputSet inputs, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var value in inputs.Values)
        {
            if (CalcLink.From(value.Provenance) is { } link)
            {
                var upstream = await _store.FindInstanceAsync(link.UpstreamId, cancellationToken).ConfigureAwait(false);
                names[value.Symbol] = CalcSheetTextRenderer.Clean($"link: {upstream?.Name ?? link.UpstreamId.ToString("N")[..8]} rev {link.Revision} · {link.OutputSymbol}");
            }
            else if (CalcDataRecord.PinOf(value.Provenance) is { } pin && await _store.FindRecordAsync(pin.Id, pin.Revision, cancellationToken).ConfigureAwait(false) is { } record)
            {
                var text = CalcSheetTextRenderer.Clean($"record: {record.Citation} rev {record.Revision}");
                names[value.Symbol] = text.Length > 200 ? text[..200] + "…" : text;
            }
        }

        if (names.Count == 0)
            return sheet;

        return sheet with { Inputs = [.. sheet.Inputs.Select(r => names.TryGetValue(r.Symbol, out var source) ? r with { Source = source } : r)] };
    }
}
