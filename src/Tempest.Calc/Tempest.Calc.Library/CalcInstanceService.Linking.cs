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

        if (upstream.ProjectId != instance.ProjectId)
            return CalcActionResult.Refused($"'{upstream.Name}' is in another project; links stay within one project, where cycles and staleness can be traced.", instance);

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

        if (upstreamId == instanceId)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' cannot take an input from its own output.", instance);

        var entry = _catalogue.Find(instance.TemplateReference);
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

        if (upstream.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, outputSymbol, StringComparison.Ordinal)) is not { Si: { } } output)
            return CalcActionResult.Refused($"'{upstream.Name}' has no numeric output '{outputSymbol}'.", instance);

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
            return CalcActionResult.Refused($"Template '{instance.TemplateReference}' is no longer registered.", instance);

        var instances = await AllInstancesAsync(instance.ProjectId, cancellationToken).ConfigureAwait(false);
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

            var current = Latest(pinned, instances);

            if (current.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, link.OutputSymbol, StringComparison.Ordinal)) is not { Si: { } } output
                || entry.Template.FindInput(input.Symbol) is not { } spec || ValueFor(spec, output) is not { } value)
                return CalcActionResult.Refused($"'{current.Name}' rev {current.Revision} no longer gives '{link.OutputSymbol}' in a unit '{input.Symbol}' takes; re-link it by hand.", instance);

            if (Problem(current) is { } problem)
                return CalcActionResult.Refused(problem.Replace("{input}", input.Symbol, StringComparison.Ordinal), instance);

            inputs[inputs.IndexOf(input)] = value with { Provenance = CalcProvenance.Link(current.Id, link.OutputSymbol, current.Revision) };
        }

        foreach (var (symbol, row, column, link) in instance.CellLinks)
        {
            var label = CellLabel(symbol, row, column);

            if (!instances.TryGetValue(link.UpstreamId, out var pinned))
                return CalcActionResult.Refused($"'{label}' is linked to calculation {link.UpstreamId}, which no longer exists.", instance);

            var current = Latest(pinned, instances);
            var spec = entry.Template.FindInput(symbol)?.Columns?.FirstOrDefault(c => string.Equals(c.Key, column, StringComparison.Ordinal));

            if (current.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, link.OutputSymbol, StringComparison.Ordinal)) is not { Si: { } } output
                || spec is not { Kind: CalcInputKind.Quantity or CalcInputKind.Number } || ConvertFor(spec.Kind, spec.DefaultUnit, output) is not { } converted)
                return CalcActionResult.Refused($"'{current.Name}' rev {current.Revision} no longer gives '{link.OutputSymbol}' in a unit '{label}' takes; re-link it by hand.", instance);

            if (Problem(current) is { } problem)
                return CalcActionResult.Refused(problem.Replace("{input}", label, StringComparison.Ordinal), instance);

            var table = inputs.First(i => string.Equals(i.Symbol, symbol, StringComparison.Ordinal));
            inputs[inputs.IndexOf(table)] = WithCell(table, row, column, new CalcCell(converted.Number, converted.Unit, Provenance: CalcProvenance.Link(current.Id, link.OutputSymbol, current.Revision)));
        }

        return instance.Status == CalcInstanceStatus.Draft
            ? await UpdateInputsAsync(instanceId, inputs, instance.UpdatedAt, cancellationToken).ConfigureAwait(false)
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
        Stale(instance, instances, reasons, [], 0, []);
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

    private void Stale(CalcInstance instance, Dictionary<Guid, CalcInstance> instances, List<string> reasons, HashSet<Guid> visiting, int depth, Dictionary<Guid, bool> memo)
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
        var linked = instance.Links.Select(l => l.Link.UpstreamId).Concat(instance.CellLinks.Select(l => l.Link.UpstreamId)).ToHashSet();

        foreach (var upstreamId in instance.UpstreamIds.Where(u => !linked.Contains(u)))
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

            var latest = Latest(upstream, instances);

            if (latest.Id != upstream.Id)
                reasons.Add($"'{label}' is pinned to '{upstream.Name}' rev {upstream.Revision}, which is superseded by rev {latest.Revision}; update to revision {latest.Revision}.");
            else if (upstream.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, link.OutputSymbol, StringComparison.Ordinal)) is not { Si: { } si })
                reasons.Add($"'{upstream.Name}' rev {upstream.Revision} no longer gives '{link.OutputSymbol}'.");
            else if (!SameValue(takenSi, si))
                reasons.Add($"'{upstream.Name}' rev {upstream.Revision} now gives a different '{link.OutputSymbol}' than the value '{label}' took.");

            // Many links (200 cells of one table) often name one upstream: walk its own chain once per question.
            var before = reasons.Count;

            if (!memo.TryGetValue(upstream.Id, out var innerStale))
            {
                var inner = new List<string>();
                Stale(upstream, instances, inner, visiting, depth + 1, memo);
                innerStale = inner.Count > 0;
                memo[upstream.Id] = innerStale;
            }

            if (innerStale && reasons.Count == before)
                reasons.Add($"'{upstream.Name}', which '{label}' is linked to, is itself stale.");
        }

        visiting.Remove(instance.Id);
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

        // Every new or changed link, input or cell: what it is called, what it points at, and the kind and unit it must take.
        var changed = new List<(string Label, CalcLink Link, CalcInputKind? Kind, string DefaultUnit, string? Bad)>();

        foreach (var input in inputs)
        {
            if (CalcLink.From(input.Provenance) is { } link && !(current.TryGetValue(input.Symbol, out var was) && was == link))
                changed.Add((input.Symbol, link, template.FindInput(input.Symbol)?.Kind, template.FindInput(input.Symbol)?.DefaultUnit ?? string.Empty, null));

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
                    changed.Add((label, cellLink, spec?.Kind, spec?.DefaultUnit ?? string.Empty, bad));
                }
            }
        }

        Dictionary<Guid, CalcInstance>? instances = null;
        var cycleProblems = new Dictionary<Guid, string?>();

        foreach (var (label, link, kind, defaultUnit, bad) in changed)
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

            if (kind is not { } k
                || upstream.Outputs.FirstOrDefault(o => string.Equals(o.Symbol, link.OutputSymbol, StringComparison.Ordinal)) is not { Si: { } } output
                || ConvertFor(k, defaultUnit, output) is null)
                return $"'{label}' is linked to '{upstream.Name}'.{link.OutputSymbol}, which it cannot take (missing, or a different dimension).";

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

                    var upstream = await _store.FindInstanceAsync(cellLink.UpstreamId, cancellationToken).ConfigureAwait(false);
                    parts.Add(CalcSheetTextRenderer.Clean($"{column}: link: {upstream?.Name ?? cellLink.UpstreamId.ToString("N")[..8]} rev {cellLink.Revision} · {cellLink.OutputSymbol}"));
                }

                if (parts.Count > 0)
                    names[$"{table.Symbol}[{r + 1}]"] = string.Join("; ", parts);
            }
        }

        if (names.Count == 0)
            return sheet;

        return sheet with { Inputs = [.. sheet.Inputs.Select(r => names.TryGetValue(r.Symbol, out var source) ? r with { Source = source } : r)] };
    }
}

/// <summary>A new calculation's inputs carry a link that breaks the linking rules (ADR-0168 amendment).</summary>
public sealed class CalcLinkRefusedException : InvalidOperationException
{
    public CalcLinkRefusedException(string message) : base(message) { }

    public CalcLinkRefusedException() { }

    public CalcLinkRefusedException(string message, Exception innerException) : base(message, innerException) { }
}
