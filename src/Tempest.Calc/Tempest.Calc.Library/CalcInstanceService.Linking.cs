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

        if (FeatureRefusal(instance.TemplateReference) is { } switchedOff)
            return CalcActionResult.Refused(switchedOff, instance);

        if (upstreamId == instanceId)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' cannot take an input from its own output.", instance);

        var entry = _catalogue.Find(instance.TemplateReference);

        // C-2: a symbol the input had before it was renamed binds the input it has become.
        if (entry is not null)
            inputSymbol = CalcInputAliases.Canonical(entry.Template, inputSymbol);

        if (entry?.Template.FindInput(inputSymbol) is not { Kind: CalcInputKind.Quantity or CalcInputKind.Number } spec)
            return CalcActionResult.Refused($"'{inputSymbol}' is not a number or quantity input of '{instance.Name}'; only those can be linked.", instance);

        var upstream = await _store.FindInstanceAsync(upstreamId, cancellationToken).ConfigureAwait(false);

        if (upstream is null)
            return CalcActionResult.Refused($"No calculation '{upstreamId}' exists to link to.", instance);

        if (upstream.Status == CalcInstanceStatus.Superseded)
            return CalcActionResult.Refused($"'{upstream.Name}' rev {upstream.Revision} is superseded; link to its current revision.", instance);

        if (upstream.ProjectId != instance.ProjectId)
            return CalcActionResult.Refused($"'{upstream.Name}' is in another project; links stay within one project, where cycles and staleness can be traced.", instance);

        if (FindOutput(upstream, outputSymbol) is not { Si: { } } output)
            return CalcActionResult.Refused($"'{upstream.Name}' has no numeric output '{outputSymbol}'.", instance);

        if ((PortProblem(upstream, outputSymbol) ?? MeaningProblem(upstream, outputSymbol)) is { } notPort)
            return CalcActionResult.Refused(notPort, instance);

        if (ValueFor(spec, output) is not { } value)
            return CalcActionResult.Refused($"'{upstream.Name}'.{outputSymbol} is in {Shown(output.Unit)} ({DimensionText(output.Unit)}); '{inputSymbol}' needs {Shown(spec.DefaultUnit)} ({DimensionText(spec.Kind == CalcInputKind.Number ? null : spec.DefaultUnit)}). The dimensions differ, so the link is refused.", instance);

        var instances = await AllInstancesAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false);
        instances[instance.Id] = instance;

        if (CycleOrDepthProblem(instance, upstream, instances) is { } problem)
            return CalcActionResult.Refused(problem.Replace("{input}", inputSymbol, StringComparison.Ordinal), instance);

        var bound = value with { Provenance = CalcProvenance.Link(upstream.Id, outputSymbol, upstream.Revision) };
        var inputs = instance.Inputs.Where(i => !string.Equals(i.Symbol, inputSymbol, StringComparison.Ordinal)).Append(bound).ToList();
        return await UpdateInputsAsync(instanceId, inputs, instance.UpdatedAt, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Binds the cell (<paramref name="row"/>, <paramref name="column"/>) of table input <paramref name="tableSymbol"/> of a
    /// draft to output <paramref name="outputSymbol"/> of calculation <paramref name="upstreamId"/> (ADR-0168 amendment 2).
    /// <paramref name="row"/> is zero-based and must exist (a link never creates rows) and the column must be a number or
    /// quantity column; the cell takes the output converted to the column's default unit. The same refusals as
    /// <see cref="BindAsync"/> apply.
    /// </summary>
    public async Task<CalcActionResult> BindCellAsync(Guid instanceId, string tableSymbol, int row, string column, Guid upstreamId, string outputSymbol, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableSymbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(column);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputSymbol);
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (instance.Status != CalcInstanceStatus.Draft)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only a draft takes new links.", instance);

        if (FeatureRefusal(instance.TemplateReference) is { } switchedOff)
            return CalcActionResult.Refused(switchedOff, instance);

        if (upstreamId == instanceId)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' cannot take an input from its own output.", instance);

        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is not null)
            tableSymbol = CalcInputAliases.Canonical(entry.Template, tableSymbol);

        var label = CellLabel(tableSymbol, row, column);

        if (entry?.Template.FindInput(tableSymbol) is not { Kind: CalcInputKind.Table, Columns: { } columns })
            return CalcActionResult.Refused($"'{tableSymbol}' is not a table input of '{instance.Name}'.", instance);

        if (columns.FirstOrDefault(c => string.Equals(c.Key, column, StringComparison.Ordinal)) is not { Kind: CalcInputKind.Quantity or CalcInputKind.Number } spec)
            return CalcActionResult.Refused($"'{tableSymbol}' has no number or quantity column '{column}'; only those can be linked.", instance);

        var table = instance.Inputs.FirstOrDefault(i => string.Equals(i.Symbol, tableSymbol, StringComparison.Ordinal));

        if (table is not null && CalcInputValidator.IsRecordPin(table.Provenance))
            return CalcActionResult.Refused(RecordPinnedTable(label, tableSymbol), instance);

        if (table?.Rows is not { } rows || row < 0 || row >= rows.Count)
            return CalcActionResult.Refused($"'{tableSymbol}' of '{instance.Name}' has {table?.Rows?.Count ?? 0} row(s); there is no row {row + 1} to link (a link never creates rows).", instance);

        var upstream = await _store.FindInstanceAsync(upstreamId, cancellationToken).ConfigureAwait(false);

        if (upstream is null)
            return CalcActionResult.Refused($"No calculation '{upstreamId}' exists to link to.", instance);

        if (upstream.Status == CalcInstanceStatus.Superseded)
            return CalcActionResult.Refused($"'{upstream.Name}' rev {upstream.Revision} is superseded; link to its current revision.", instance);

        if (upstream.ProjectId != instance.ProjectId)
            return CalcActionResult.Refused($"'{upstream.Name}' is in another project; links stay within one project, where cycles and staleness can be traced.", instance);

        if (FindOutput(upstream, outputSymbol) is not { Si: { } } output)
            return CalcActionResult.Refused($"'{upstream.Name}' has no numeric output '{outputSymbol}'.", instance);

        if ((PortProblem(upstream, outputSymbol) ?? MeaningProblem(upstream, outputSymbol)) is { } notPort)
            return CalcActionResult.Refused(notPort, instance);

        if (ConvertFor(spec.Kind, spec.DefaultUnit, output) is not { } converted)
            return CalcActionResult.Refused($"'{upstream.Name}'.{outputSymbol} is in {Shown(output.Unit)} ({DimensionText(output.Unit)}); '{label}' needs {Shown(spec.DefaultUnit)} ({DimensionText(spec.Kind == CalcInputKind.Number ? null : spec.DefaultUnit)}). The dimensions differ, so the link is refused.", instance);

        var instances = await AllInstancesAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false);
        instances[instance.Id] = instance;

        if (CycleOrDepthProblem(instance, upstream, instances) is { } problem)
            return CalcActionResult.Refused(problem.Replace("{input}", label, StringComparison.Ordinal), instance);

        var cell = new CalcCell(converted.Number, converted.Unit, Provenance: CalcProvenance.Link(upstream.Id, outputSymbol, upstream.Revision));
        var inputs = instance.Inputs.Select(i => ReferenceEquals(i, table) ? WithCell(i, row, column, cell) : i).ToList();
        return await UpdateInputsAsync(instanceId, inputs, instance.UpdatedAt, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// "Update to revision n": re-pins every link (inputs and table cells) to the current revision of its upstream calculation
    /// and recalculates. A draft is updated in place; a checked or issued calculation becomes a new revision (the old one is
    /// superseded), so the change is checked and issued again.
    /// </summary>
    public async Task<CalcActionResult> UpdateLinksAsync(Guid instanceId, CancellationToken cancellationToken = default)
    {
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (!instance.HasLinks)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' has no linked inputs.", instance);

        if (instance.Status == CalcInstanceStatus.Superseded)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' rev {instance.Revision} is superseded; update its current revision.", instance);

        var entry = _catalogue.Find(instance.TemplateReference);

        if (entry is null)
            return CalcActionResult.Refused(OlderVersionRefusal(instance), instance);

        var instances = await AllInstancesAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false);
        var successors = SuccessorIndex(instances);
        var inputs = instance.Inputs.ToList();
        var problems = new Dictionary<Guid, string?>();
        string? Problem(CalcInstance upstream)
        {
            if (!problems.TryGetValue(upstream.Id, out var found))
                problems[upstream.Id] = found = CycleOrDepthProblem(instance, upstream, instances);

            return found;
        }

        foreach (var (input, link) in instance.Links)
        {
            if (!instances.TryGetValue(link.UpstreamId, out var pinned))
                return CalcActionResult.Refused($"'{input.Symbol}' is linked to calculation {link.UpstreamId}, which no longer exists.", instance);

            var current = Latest(pinned, successors);

            if (FindOutput(current, link.OutputSymbol) is not { Si: { } } output
                || entry.Template.FindInput(input.Symbol) is not { } spec || ValueFor(spec, output) is not { } value)
                return CalcActionResult.Refused($"'{current.Name}' rev {current.Revision} no longer gives '{link.OutputSymbol}' in a unit '{input.Symbol}' takes; re-link it by hand.", instance);

            if ((RepinMeaningProblem(pinned, current, input.Symbol, link.OutputSymbol) ?? MeaningProblem(current, link.OutputSymbol)) is { } changedMeaning)
                return CalcActionResult.Refused(changedMeaning, instance);

            if (Problem(current) is { } problem)
                return CalcActionResult.Refused(problem.Replace("{input}", input.Symbol, StringComparison.Ordinal), instance);

            inputs[inputs.IndexOf(input)] = value with { Provenance = CalcProvenance.Link(current.Id, link.OutputSymbol, current.Revision) };
        }

        foreach (var (symbol, row, column, link) in instance.CellLinks)
        {
            var label = CellLabel(symbol, row, column);

            if (!instances.TryGetValue(link.UpstreamId, out var pinned))
                return CalcActionResult.Refused($"'{label}' is linked to calculation {link.UpstreamId}, which no longer exists.", instance);

            var current = Latest(pinned, successors);
            var spec = entry.Template.FindInput(symbol)?.Columns?.FirstOrDefault(c => string.Equals(c.Key, column, StringComparison.Ordinal));

            if (FindOutput(current, link.OutputSymbol) is not { Si: { } } output
                || spec is not { Kind: CalcInputKind.Quantity or CalcInputKind.Number } || ConvertFor(spec.Kind, spec.DefaultUnit, output) is not { } converted)
                return CalcActionResult.Refused($"'{current.Name}' rev {current.Revision} no longer gives '{link.OutputSymbol}' in a unit '{label}' takes; re-link it by hand.", instance);

            if ((RepinMeaningProblem(pinned, current, label, link.OutputSymbol) ?? MeaningProblem(current, link.OutputSymbol)) is { } changedMeaning)
                return CalcActionResult.Refused(changedMeaning, instance);

            if (Problem(current) is { } problem)
                return CalcActionResult.Refused(problem.Replace("{input}", label, StringComparison.Ordinal), instance);

            var table = inputs.First(i => string.Equals(i.Symbol, symbol, StringComparison.Ordinal));
            inputs[inputs.IndexOf(table)] = WithCell(table, row, column, new CalcCell(converted.Number, converted.Unit, Provenance: CalcProvenance.Link(current.Id, link.OutputSymbol, current.Revision)));
        }

        return instance.Status == CalcInstanceStatus.Draft
            ? await UpdateInputsCoreAsync(instanceId, inputs, instance.UpdatedAt, true, cancellationToken).ConfigureAwait(false)
            : await ReviseRepinnedAsync(instanceId, inputs, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// An instance is stale when its template changed, when a value taken from a reference record has been revised (or no
    /// longer matches the record it cites), when a linked upstream calculation was superseded or now gives a different value than
    /// the one pinned, or when anything it is linked to is itself stale for any of those reasons — through any number of links.
    /// </summary>
    public async Task<CalcStaleness> StalenessAsync(CalcInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return (await StalenessForAsync(instance.ProjectId, [instance], cancellationToken).ConfigureAwait(false))[instance.Id];
    }

    /// <summary>
    /// The staleness of every calculation in a project, worked out from one listing of the project and one lookup per record
    /// (the list view asks once, not once per row — Red R-A-07).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, CalcStaleness>> StalenessForProjectAsync(Guid? projectId, CancellationToken cancellationToken = default)
    {
        var instances = await _store.ListInstancesAsync(projectId, cancellationToken).ConfigureAwait(false);
        return await StalenessForAsync(projectId, instances, cancellationToken, instances).ConfigureAwait(false);
    }

    /// <summary>The staleness of <paramref name="roots"/> (read from the store just now), sharing one read of the project and one lookup per record.</summary>
    private async Task<Dictionary<Guid, CalcStaleness>> StalenessForAsync(Guid? projectId, IEnumerable<CalcInstance> roots, CancellationToken cancellationToken, IReadOnlyList<CalcInstance>? listed = null)
    {
        var list = roots.ToList();
        var instances = listed is null ? await AllInstancesAsync(projectId, cancellationToken).ConfigureAwait(false) : listed.ToDictionary(i => i.Id);

        foreach (var root in list)
            instances[root.Id] = root;

        var context = new StaleContext(instances);
        var scope = new Dictionary<Guid, CalcInstance>();

        foreach (var root in list)
        {
            scope[root.Id] = root;

            foreach (var up in Upstream(root.Id, instances))
            {
                if (instances.TryGetValue(up, out var upstream))
                    scope[up] = upstream;
            }
        }

        await PrefetchRecordsAsync(context, scope.Values, cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<Guid, CalcStaleness>();

        foreach (var root in list)
        {
            var reasons = new List<string>();
            Stale(root, context, reasons, [], 0, []);
            result[root.Id] = reasons.Count == 0 ? CalcStaleness.Fresh : new CalcStaleness(true, reasons);
        }

        return result;
    }

    /// <summary>Every calculation that takes a value from <paramref name="instanceId"/>, directly or through others.</summary>
    public async Task<IReadOnlyList<Guid>> DownstreamAsync(Guid instanceId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        var instances = await AllInstancesAsync(projectId, cancellationToken).ConfigureAwait(false);
        return Downstream(instanceId, instances);
    }

    // ------------------------------------------------------------------

    /// <summary>What one staleness question works from: the project's instances, who superseded whom, and the records its inputs pin.</summary>
    private sealed class StaleContext
    {
        public StaleContext(Dictionary<Guid, CalcInstance> instances)
        {
            Instances = instances;
            Successors = SuccessorIndex(instances);
        }

        public Dictionary<Guid, CalcInstance> Instances { get; }

        public Dictionary<Guid, CalcInstance> Successors { get; }

        public Dictionary<Guid, CalcDataRecord?> Heads { get; } = [];

        public Dictionary<(Guid Id, int Revision), CalcDataRecord?> Pinned { get; } = [];
    }

    /// <summary>Each calculation's newest successor (the revision made from it), so the current revision is found by following one entry per hop.</summary>
    private static Dictionary<Guid, CalcInstance> SuccessorIndex(Dictionary<Guid, CalcInstance> instances)
    {
        var index = new Dictionary<Guid, CalcInstance>();

        foreach (var i in instances.Values)
        {
            if (i.PredecessorId is { } p && (!index.TryGetValue(p, out var known) || i.CreatedAt > known.CreatedAt))
                index[p] = i;
        }

        return index;
    }

    private async Task PrefetchRecordsAsync(StaleContext context, IEnumerable<CalcInstance> scope, CancellationToken cancellationToken)
    {
        foreach (var instance in scope)
        {
            foreach (var input in instance.Inputs)
            {
                if (CalcDataRecord.PinOf(input.Provenance) is not { } pin)
                    continue;

                if (!context.Heads.ContainsKey(pin.Id))
                    context.Heads[pin.Id] = await _store.FindRecordAsync(pin.Id, null, cancellationToken).ConfigureAwait(false);

                if (!context.Pinned.ContainsKey(pin))
                    context.Pinned[pin] = context.Heads[pin.Id] is { } head && head.Revision == pin.Revision
                        ? head
                        : await _store.FindRecordAsync(pin.Id, pin.Revision, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void Stale(CalcInstance instance, StaleContext context, List<string> reasons, HashSet<Guid> visiting, int depth, Dictionary<Guid, bool> memo)
    {
        var instances = context.Instances;

        // Template and record staleness are judged at every depth: an upstream that is stale for them makes what takes a value from it stale (Red R-A-03).
        var currentTemplate = _catalogue.Find(instance.TemplateId);

        if (currentTemplate is null)
            reasons.Add($"Template '{instance.TemplateReference}' is no longer registered.");
        else if (!string.Equals(currentTemplate.Reference, instance.TemplateReference, StringComparison.Ordinal))
            reasons.Add($"Template {instance.TemplateReference} has a newer version {currentTemplate.Reference}.");
        else if (!string.Equals(currentTemplate.ContentHash, instance.TemplateHash, StringComparison.Ordinal))
            reasons.Add($"Template {instance.TemplateReference} content changed (hash {CalcTemplateHasher.Short(instance.TemplateHash)} → {currentTemplate.ShortHash}).");

        if (depth > MaxChainDepth || !visiting.Add(instance.Id))
            return;

        // The first form of linking (v0.26.0): a "reference: calc:<id>" provenance, stale when the upstream outputs hash moved.
        var linked = instance.Links.Select(l => l.Link.UpstreamId).Concat(instance.CellLinks.Select(l => l.Link.UpstreamId)).ToHashSet();

        foreach (var upstreamId in instance.UpstreamIds.Where(u => !linked.Contains(u)))
        {
            if (!instances.TryGetValue(upstreamId, out var upstream))
            {
                reasons.Add($"Upstream calculation {upstreamId} no longer exists.");
                continue;
            }

            var recorded = instance.UpstreamHashes is { } h && h.TryGetValue(upstreamId.ToString("N"), out var recordedHash) ? recordedHash : null;

            if (Latest(upstream, context.Successors) is { } newest && newest.Id != upstream.Id)
                reasons.Add($"Upstream calculation '{upstream.Name}' rev {upstream.Revision} is superseded by rev {newest.Revision}.");
            else if (recorded is null || !string.Equals(recorded, upstream.OutputsHash, StringComparison.Ordinal))
                reasons.Add($"Upstream calculation '{upstream.Name}' has changed its outputs since this one took values from it.");
        }

        // Input links and table-cell links are judged alike: pinned revision, output still given, same value, upstream itself fresh.
        var bound = instance.Links.Select(l => (Label: l.Input.Symbol, l.Link, Si: (double?)InputSi(l.Input.Number, l.Input.Unit)))
            .Concat(instance.CellLinks.Select(l => (Label: CellLabel(l.Symbol, l.Row, l.Column), l.Link, Si: CellSi(instance, l.Symbol, l.Row, l.Column))))
            .ToList();

        foreach (var (label, link, takenSi) in bound)
        {
            if (!instances.TryGetValue(link.UpstreamId, out var upstream))
            {
                reasons.Add($"'{label}' is linked to calculation {link.UpstreamId}, which no longer exists.");
                continue;
            }

            var latest = Latest(upstream, context.Successors);

            if (latest.Id != upstream.Id)
                reasons.Add($"'{label}' is pinned to '{upstream.Name}' rev {upstream.Revision}, which is superseded by rev {latest.Revision}; update to revision {latest.Revision}.");
            else if (FindOutput(upstream, link.OutputSymbol) is not { Si: { } si })
                reasons.Add($"'{upstream.Name}' rev {upstream.Revision} no longer gives '{link.OutputSymbol}'.");
            else if (MeaningProblem(upstream, link.OutputSymbol) is { } changedMeaning)
                reasons.Add(changedMeaning);
            else if (!SameValue(takenSi, si))
                reasons.Add($"'{upstream.Name}' rev {upstream.Revision} now gives a different '{link.OutputSymbol}' than the value '{label}' took.");

            // Many links (200 cells of one table) often name one upstream: walk its own chain once per question.
            var before = reasons.Count;

            if (!memo.TryGetValue(upstream.Id, out var innerStale))
            {
                var inner = new List<string>();
                Stale(upstream, context, inner, visiting, depth + 1, memo);
                innerStale = inner.Count > 0;
                memo[upstream.Id] = innerStale;
            }

            if (innerStale && reasons.Count == before)
                reasons.Add($"'{upstream.Name}', which '{label}' is linked to, is itself stale.");
        }

        RecordReasons(instance, context, reasons);
        visiting.Remove(instance.Id);
    }

    /// <summary>Stale reasons from records: a pinned revision has been revised since, does not exist, or no longer matches the value the input holds.</summary>
    private void RecordReasons(CalcInstance instance, StaleContext context, List<string> reasons)
    {
        var template = _catalogue.Find(instance.TemplateReference)?.Template;

        foreach (var input in instance.Inputs)
        {
            if (CalcDataRecord.PinOf(input.Provenance) is not { } pin || !context.Heads.TryGetValue(pin.Id, out var latest))
                continue;

            if (latest is null)
            {
                reasons.Add($"'{input.Symbol}' takes its value from reference record {pin.Id:N}, which no longer exists.");
                continue;
            }

            if (!context.Pinned.TryGetValue(pin, out var pinned) || pinned is null)
            {
                reasons.Add($"'{input.Symbol}' cites revision {pin.Revision} of '{latest.Title}', which does not exist (the latest is revision {latest.Revision}). Apply the record again.");
                continue;
            }

            if (latest.Revision > pin.Revision)
                reasons.Add($"'{input.Symbol}' takes its value from '{latest.Title}' revision {pin.Revision}; revision {latest.Revision} has been entered since. Apply the record again.");
            else if (!MatchesRecord(input, pinned, template?.FindInput(input.Symbol)))
                reasons.Add($"'{input.Symbol}' no longer matches '{pinned.Title}' revision {pin.Revision}, which it cites: the value was changed after the record was applied. Apply the record again, or enter the value by hand.");
        }
    }

    /// <summary>Whether an input still holds the value of the record it cites (a number or quantity in SI, a text, or a table cell by cell).</summary>
    private static bool MatchesRecord(CalcInputValue input, CalcDataRecord record, CalcInputSpec? spec)
    {
        // A material's values are under the property they give, not under the input that takes them (Addendum B, W3).
        if (MaterialLibrary.IsMaterial(record.Kind))
            return MatchesMaterial(input, record, spec);

        return record.Values.FirstOrDefault(v => string.Equals(v.Symbol, input.Symbol, StringComparison.Ordinal)) is { } value && MatchesValue(input, value, spec);
    }

    private static bool MatchesValue(CalcInputValue input, CalcDataValue value, CalcInputSpec? spec)
    {
        if (input.Rows is not null || value.Rows is not null)
        {
            if (input.Rows is not { } rows || value.Rows is not { } recorded || rows.Count != recorded.Count)
                return false;

            for (var r = 0; r < rows.Count; r++)
            {
                var a = rows[r]?.Cells ?? new Dictionary<string, CalcCell>();
                var b = recorded[r]?.Cells ?? new Dictionary<string, CalcCell>();

                foreach (var key in a.Keys.Union(b.Keys, StringComparer.Ordinal))
                {
                    var column = spec?.Columns?.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));
                    a.TryGetValue(key, out var x);
                    b.TryGetValue(key, out var y);

                    if (!SameCell(x, y, column?.DefaultUnit))
                        return false;
                }
            }

            return true;
        }

        return SameNumber(input.Number, input.Unit, value.Number, value.Unit, spec?.DefaultUnit) && string.Equals(input.Text ?? string.Empty, value.Text ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool SameCell(CalcCell? a, CalcCell? b, string? defaultUnit)
    {
        var emptyA = a is null || (a.Number is null && a.Text is null && a.Flag is null);
        var emptyB = b is null || (b.Number is null && b.Text is null && b.Flag is null);

        if (emptyA || emptyB)
            return emptyA && emptyB;

        return SameNumber(a!.Number, a.Unit, b!.Number, b.Unit, defaultUnit)
            && string.Equals(a.Text ?? string.Empty, b.Text ?? string.Empty, StringComparison.Ordinal)
            && a.Flag == b.Flag;
    }

    private static bool SameNumber(double? a, string? unitA, double? b, string? unitB, string? defaultUnit)
    {
        if (a is null || b is null)
            return a is null && b is null;

        var x = InputSi(a, string.IsNullOrEmpty(unitA) ? defaultUnit : unitA);
        var y = InputSi(b, string.IsNullOrEmpty(unitB) ? defaultUnit : unitB);
        return Math.Abs(x - y) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(x), Math.Abs(y)));
    }

    private static bool SameValue(double? taken, double upstreamSi) =>
        taken is { } si && Math.Abs(si - upstreamSi) <= 1e-9 * Math.Max(1.0, Math.Abs(upstreamSi));

    private static double InputSi(double? number, string? unit) =>
        unit is { Length: > 0 } && UnitCatalog.TryParse(unit, out var u, out _) ? u.ToSi(number ?? double.NaN) : number ?? double.NaN;

    /// <summary>The SI value a linked cell holds; a cell without a unit is read in its column's default unit.</summary>
    private double? CellSi(CalcInstance instance, string symbol, int row, string column)
    {
        var cell = instance.Inputs.FirstOrDefault(i => string.Equals(i.Symbol, symbol, StringComparison.Ordinal))?.Rows?[row]?.Get(column);

        if (cell?.Number is not { } number)
            return null;

        var unit = cell.Unit ?? _catalogue.Find(instance.TemplateReference)?.Template.FindInput(symbol)?.Columns?.FirstOrDefault(c => string.Equals(c.Key, column, StringComparison.Ordinal))?.DefaultUnit;
        return InputSi(number, unit);
    }

    private static string RecordPinnedTable(string label, string tableSymbol) =>
        $"'{label}' cannot be linked: '{tableSymbol}' is pinned to a reference record, whose values only the record can change. Enter the table by hand (which drops the pin) to link its cells.";

    private static string CellLabel(string symbol, int row, string column) => $"{symbol}[{row + 1}].{column}";

    /// <summary>A copy of a table input with one cell replaced.</summary>
    private static CalcInputValue WithCell(CalcInputValue table, int row, string column, CalcCell cell)
    {
        var rows = table.Rows!.Select((r, i) => i != row ? r : new CalcRow(new Dictionary<string, CalcCell>(r.Cells, StringComparer.Ordinal) { [column] = cell })).ToList();
        return table with { Rows = rows };
    }

    /// <summary>The input value an output gives, in the input's own default unit; <see langword="null"/> when the dimensions differ.</summary>
    private static CalcInputValue? ValueFor(CalcInputSpec spec, CalcOutputSnapshot output) =>
        ConvertFor(spec.Kind, spec.DefaultUnit, output) is { } c
            ? new CalcInputValue(spec.Symbol, Number: c.Number, Unit: c.Unit)
            : null;

    /// <summary>
    /// The number an output gives for something of <paramref name="kind"/> (an input or a table column) whose default unit is
    /// <paramref name="defaultUnit"/>; <see langword="null"/> when the dimensions differ. A plain number carries no unit.
    /// </summary>
    private static (double Number, string? Unit)? ConvertFor(CalcInputKind kind, string defaultUnit, CalcOutputSnapshot output)
    {
        var outputUnit = output.Unit is { Length: > 0 } text && UnitCatalog.TryParse(text, out var u, out _) ? u : Unit.One;

        if (kind == CalcInputKind.Number)
            return outputUnit.Dimension == Dimension.None ? (output.Si!.Value, null) : null;

        if (!UnitCatalog.TryParse(defaultUnit, out var target, out _) || target.Dimension != outputUnit.Dimension)
            return null;

        // A temperature difference (K) and an absolute temperature (°C, °F) share a dimension but not a meaning: refused
        // rather than shifted by 273.15 (Opus review, finding 14).
        if (target.IsAffine != outputUnit.IsAffine)
            return null;

        return (target.FromSi(output.Si!.Value), defaultUnit);
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
        above.UnionWith(Lineage(upstream, instances));

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

    /// <summary>
    /// The first problem with the links (inputs and table cells) in <paramref name="inputs"/> that differ from the instance's
    /// current ones, or <see langword="null"/>.
    /// </summary>
    private async Task<string?> LinkProblemAsync(CalcInstance instance, CalcTemplate template, IReadOnlyList<CalcInputValue> inputs, CancellationToken cancellationToken)
    {
        var current = instance.Links.ToDictionary(l => l.Input.Symbol, l => l.Link, StringComparer.Ordinal);
        var currentCells = instance.CellLinks.ToDictionary(l => CellLabel(l.Symbol, l.Row, l.Column), l => l.Link, StringComparer.Ordinal);
        var currentLegacy = instance.Inputs.Select(i => CalcInstance.LegacyUpstreamOf(i.Provenance)).OfType<Guid>().ToHashSet();

        // Every new or changed link, input or cell: what it is called, what it points at, and the kind and unit it must take.
        // A legacy "reference: calc:<id>" names an upstream without an output; it meets the same rules except those of an output (Red R-A-15).
        var changed = new List<(string Label, CalcLink Link, CalcInputKind? Kind, string DefaultUnit, string? Bad, bool Legacy)>();

        foreach (var input in inputs)
        {
            if (CalcLink.From(input.Provenance) is { } link && !(current.TryGetValue(input.Symbol, out var was) && was == link))
                changed.Add((input.Symbol, link, template.FindInput(input.Symbol)?.Kind, template.FindInput(input.Symbol)?.DefaultUnit ?? string.Empty, null, false));
            else if (CalcInstance.LegacyUpstreamOf(input.Provenance) is { } legacyId && !currentLegacy.Contains(legacyId))
                changed.Add((input.Symbol, new CalcLink(legacyId, string.Empty, string.Empty), null, string.Empty, null, true));

            for (var r = 0; r < (input.Rows?.Count ?? 0); r++)
            {
                foreach (var (column, cell) in input.Rows![r]?.Cells ?? new Dictionary<string, CalcCell>())
                {
                    var label = CellLabel(input.Symbol, r, column);

                    if (cell is null || CalcLink.From(cell.Provenance) is not { } cellLink || (currentCells.TryGetValue(label, out var wasCell) && wasCell == cellLink))
                        continue;

                    var spec = template.FindInput(input.Symbol)?.Columns?.FirstOrDefault(c => string.Equals(c.Key, column, StringComparison.Ordinal));
                    var bad = CalcInputValidator.IsRecordPin(input.Provenance) ? RecordPinnedTable(label, input.Symbol)
                        : spec is { Kind: CalcInputKind.Quantity or CalcInputKind.Number } ? null : $"'{label}' is linked, but only a number or quantity column of a table can take a link.";
                    changed.Add((label, cellLink, spec?.Kind, spec?.DefaultUnit ?? string.Empty, bad, false));
                }
            }
        }

        Dictionary<Guid, CalcInstance>? instances = null;
        var cycleProblems = new Dictionary<Guid, string?>();

        foreach (var (label, link, kind, defaultUnit, bad, legacy) in changed)
        {
            if (bad is not null)
                return bad;

            instances ??= await AllInstancesAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false);

            var upstream = instances.GetValueOrDefault(link.UpstreamId) ?? await _store.FindInstanceAsync(link.UpstreamId, cancellationToken).ConfigureAwait(false);

            if (upstream is null)
                return $"'{label}' is linked to calculation {link.UpstreamId}, which does not exist.";

            if (upstream.Id == instance.Id)
                return $"'{label}' cannot take a value from its own calculation.";

            if (upstream.Status == CalcInstanceStatus.Superseded)
                return $"'{label}' is linked to '{upstream.Name}' rev {upstream.Revision}, which is superseded; link to its current revision.";

            if (upstream.ProjectId != instance.ProjectId)
                return $"'{label}' is linked to '{upstream.Name}' in another project; links stay within one project.";

            if (!legacy)
            {
                // The label is a claim, not a fact: the sheet prints the upstream's own revision, and an import cannot name another (Red R-A-08).
                if (!string.Equals(link.Revision, upstream.Revision, StringComparison.Ordinal))
                    return $"'{label}' is linked to '{upstream.Name}' rev {link.Revision}, but that calculation is rev {upstream.Revision}; link to the revision that exists.";

                if (kind is not { } k
                    || FindOutput(upstream, link.OutputSymbol) is not { Si: { } } output
                    || ConvertFor(k, defaultUnit, output) is null)
                    return $"'{label}' is linked to '{upstream.Name}'.{link.OutputSymbol}, which it cannot take (missing, or a different dimension).";

                if ((PortProblem(upstream, link.OutputSymbol) ?? MeaningProblem(upstream, link.OutputSymbol)) is { } notPort)
                    return notPort;
            }

            if (!cycleProblems.TryGetValue(upstream.Id, out var cycle))
                cycleProblems[upstream.Id] = cycle = CycleOrDepthProblem(instance, upstream, instances);

            if (cycle is not null)
                return cycle.Replace("{input}", label, StringComparison.Ordinal);
        }

        return null;
    }

    private static string Shown(string? unit) => string.IsNullOrEmpty(unit) ? "a plain number" : unit;

    private static string DimensionText(string? unit) =>
        unit is { Length: > 0 } && UnitCatalog.TryParse(unit, out var u, out _) ? u.Dimension.ToString() : "dimensionless";

    /// <summary>The current revision of a calculation: the end of its chain of successors (one index lookup per hop).</summary>
    private static CalcInstance Latest(CalcInstance instance, Dictionary<Guid, CalcInstance> successors)
    {
        var current = instance;

        for (var hop = 0; hop < 1_000 && successors.TryGetValue(current.Id, out var next); hop++)
            current = next;

        return current;
    }

    /// <summary>
    /// Why an output cannot be linked to: when a template declares output ports, only those are offered for linking; an output it does
    /// not declare (a factor it echoes from a record, say) is not (Red R-A-09). A template that declares none offers every output.
    /// </summary>
    private string? PortProblem(CalcInstance upstream, string outputSymbol)
    {
        // ADR-0173: nothing takes a value from a calculation of a switched-off calculator, declared port or not.
        if (FeatureRefusal(upstream.TemplateReference) is { } switchedOff)
            return $"'{upstream.Name}' cannot be linked to: {switchedOff}";

        if (_catalogue.Find(upstream.TemplateReference)?.Template is not { OutputPorts: { Count: > 0 } ports } template
            || ports.Any(p => p.Matches(outputSymbol)))
            return null;

        return $"'{upstream.Name}'.{outputSymbol} is not an output {template.Title} offers for linking; link to one of: {string.Join(", ", ports.Select(p => p.PublicName))}.";
    }

    /// <summary>
    /// Why <paramref name="nameOrSymbol"/> cannot be taken from <paramref name="upstream"/> although it has such an output: the template's current
    /// version declares that this symbol or public name came to mean something else at a version later than the one the upstream was made on
    /// (<see cref="CalcOutputPort.MeaningChangedAt"/>), so the stored number is not what the name now says. The link is refused, never rebound;
    /// a link under an alias (the old symbol of what the port is now) keeps its meaning. <see langword="null"/> when there is no such change.
    /// </summary>
    private string? MeaningProblem(CalcInstance upstream, string nameOrSymbol)
    {
        if (_catalogue.Find(upstream.TemplateId) is not { } current || current.Template.FindPort(nameOrSymbol) is not { } port || !port.MeaningChangedSince(nameOrSymbol, upstream.TemplateVersion))
            return null;

        return $"'{upstream.Name}' rev {upstream.Revision} was made on {upstream.TemplateReference}, where {port.MeaningChangeText(upstream.TemplateId, upstream.TemplateReference)}, so '{nameOrSymbol}' is not linked from it: revise '{upstream.Name}' onto {current.Reference} and link to that revision.";
    }

    /// <summary>
    /// Why a link that took <paramref name="symbol"/> from <paramref name="pinned"/> is not moved to <paramref name="current"/>, the latest revision of
    /// that calculation: the pinned revision was made before the template declared that the symbol came to mean something else
    /// (<see cref="CalcOutputPort.MeaningChangedAt"/>), so the value the link took is not what the same name gives on the revision it would be moved
    /// to. Refused, never rebound; the link stays pinned where it was. <see langword="null"/> when the meaning is unchanged or the link is under an alias.
    /// </summary>
    private string? RepinMeaningProblem(CalcInstance pinned, CalcInstance current, string label, string symbol)
    {
        if (_catalogue.Find(pinned.TemplateId) is not { } template || template.Template.FindPort(symbol) is not { } port || !port.MeaningChangedSince(symbol, pinned.TemplateVersion))
            return null;

        return current.Id == pinned.Id
            ? MeaningProblem(pinned, symbol)
            : $"'{label}' took '{symbol}' from '{pinned.Name}' rev {pinned.Revision}, made on {pinned.TemplateReference}, where {port.MeaningChangeText(pinned.TemplateId, pinned.TemplateReference)}, so the link is not moved to rev {current.Revision}: link '{label}' by hand to the output of rev {current.Revision} that gives what it takes.";
    }

    /// <summary>
    /// The stored output of <paramref name="upstream"/> that <paramref name="nameOrSymbol"/> names: by the output's own symbol (which is
    /// what every link saved before C-2 holds), else through the template's port that the public name or an alias belongs to (C-2, row 43).
    /// A link saved under an old symbol keeps resolving after the port is renamed.
    /// </summary>
    private CalcOutputSnapshot? FindOutput(CalcInstance upstream, string nameOrSymbol)
    {
        if (upstream.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, nameOrSymbol, StringComparison.Ordinal)) is { } exact)
            return exact;

        var template = (_catalogue.Find(upstream.TemplateReference) ?? _catalogue.Find(upstream.TemplateId))?.Template;

        return template?.FindPort(nameOrSymbol) is { } port ? upstream.Outputs.FirstOrDefault(o => port.Matches(o.Symbol)) : null;
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
        // Who takes from whom, worked out once: each instance's upstream list is itself a walk over its inputs and cells.
        var below = instances.Values.SelectMany(i => i.UpstreamIds.Select(u => (Up: u, Down: i.Id))).ToLookup(x => x.Up, x => x.Down);
        var seen = new HashSet<Guid>();
        var order = new List<Guid>();
        var queue = new Queue<Guid>([id]);

        while (queue.Count > 0 && seen.Count < 10_000)
        {
            foreach (var down in below[queue.Dequeue()])
            {
                if (seen.Add(down))
                {
                    order.Add(down);
                    queue.Enqueue(down);
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
    /// record by its citation ("record: UK NA factors — BS EN 1990 NA, +A1:2005, NA.A1.2(B) (licensed source)"); a value from a material
    /// also names the property and the value's own source, edition and page or table. A linked value in <paramref name="withheldValues"/>
    /// (its upstream holds a licensed record's value) prints the link and says it is withheld in place of the number (§A5).
    /// </summary>
    private async Task<CalcSheet> DescribeLinksAsync(CalcSheet sheet, CalcInputSet inputs, IReadOnlySet<string> withheldValues, CancellationToken cancellationToken)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        // Many cells name one upstream: look each calculation up once per sheet, and print its own revision, not the label the link carries (Red R-A-07, R-A-08).
        var upstreams = new Dictionary<Guid, CalcInstance?>();

        async Task<CalcInstance?> UpstreamAsync(Guid id)
        {
            if (!upstreams.TryGetValue(id, out var found))
                upstreams[id] = found = await _store.FindInstanceAsync(id, cancellationToken).ConfigureAwait(false);

            return found;
        }

        foreach (var value in inputs.Values)
        {
            if (CalcLink.From(value.Provenance) is { } link)
            {
                var upstream = await UpstreamAsync(link.UpstreamId).ConfigureAwait(false);
                names[value.Symbol] = CalcSheetTextRenderer.Clean($"link: {upstream?.Name ?? link.UpstreamId.ToString("N")[..8]} rev {upstream?.Revision ?? link.Revision} · {link.OutputSymbol}");
            }
            else if (CalcDataRecord.PinOf(value.Provenance) is { } pin && await _store.FindRecordAsync(pin.Id, pin.Revision, cancellationToken).ConfigureAwait(false) is { } record)
            {
                var own = MaterialLibrary.IsMaterial(record.Kind) ? MaterialLibrary.ValueSourceText(record, value.Provenance) : null;
                var text = CalcSheetTextRenderer.Clean($"record: {record.Citation} rev {record.Revision}{(own is null ? string.Empty : " · " + own)}");
                var cap = own is null ? 200 : 400;
                names[value.Symbol] = text.Length > cap ? text[..cap] + "…" : text;
            }
        }

        // A table's linked cells are named on the row's own line ("Phi: link: Room A rev A · Phi_HL").
        foreach (var table in inputs.Values.Where(v => v.Rows is not null))
        {
            for (var r = 0; r < table.Rows!.Count; r++)
            {
                var parts = new List<string>();

                foreach (var (column, cell) in (table.Rows[r]?.Cells ?? new Dictionary<string, CalcCell>()).OrderBy(c => c.Key, StringComparer.Ordinal))
                {
                    if (cell is null || CalcLink.From(cell.Provenance) is not { } cellLink)
                        continue;

                    var upstream = await UpstreamAsync(cellLink.UpstreamId).ConfigureAwait(false);
                    parts.Add(CalcSheetTextRenderer.Clean($"{column}: link: {upstream?.Name ?? cellLink.UpstreamId.ToString("N")[..8]} rev {upstream?.Revision ?? cellLink.Revision} · {cellLink.OutputSymbol}"));
                }

                if (parts.Count > 0)
                    names[$"{table.Symbol}[{r + 1}]"] = string.Join("; ", parts);
            }
        }

        if (names.Count == 0 && withheldValues.Count == 0)
            return sheet;

        return sheet with
        {
            Inputs =
            [
                .. sheet.Inputs.Select(r => withheldValues.Contains(r.Symbol)
                    ? r with { Value = LicensedDerivedText, Unit = string.Empty, Source = names.GetValueOrDefault(r.Symbol) ?? r.Source }
                    : names.TryGetValue(r.Symbol, out var source) ? r with { Source = source } : r),
            ],
        };
    }
}

/// <summary>A new calculation's stored record would pass the store's size cap (C-7h); the message names the calculation by its title.</summary>
public sealed class CalcInstanceTooLargeException : InvalidOperationException
{
    public CalcInstanceTooLargeException(string message) : base(message) { }

    public CalcInstanceTooLargeException() { }

    public CalcInstanceTooLargeException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>A calculation or pack was to be made in a project that has been deleted (security review SEC-C-05); the message is one sentence.</summary>
public sealed class CalcProjectDeletedException : InvalidOperationException
{
    public CalcProjectDeletedException(string message) : base(message) { }

    public CalcProjectDeletedException() { }

    public CalcProjectDeletedException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>A new calculation's inputs carry a link that breaks the linking rules (ADR-0168 amendment).</summary>
public sealed class CalcLinkRefusedException : InvalidOperationException
{
    public CalcLinkRefusedException(string message) : base(message) { }

    public CalcLinkRefusedException() { }

    public CalcLinkRefusedException(string message, Exception innerException) : base(message, innerException) { }
}
