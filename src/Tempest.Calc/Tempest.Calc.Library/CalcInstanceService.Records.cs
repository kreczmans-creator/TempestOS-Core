using System.Globalization;
using System.Text;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>
/// Addendum A, §A3 — reference records. Tabulated values come from records that carry their source, edition, licence
/// holder and whether the source is licensed; applying one to a draft fills its inputs with the record's pin as their
/// source. Values from a licensed record never leave through an export.
/// </summary>
public sealed partial class CalcInstanceService
{
    /// <summary>
    /// Saves a reference record: a new record at revision 1, or — when <paramref name="draft"/> names an existing record
    /// — that record's next revision. Earlier revisions stay as they were (a calculation pins the one it used).
    /// </summary>
    public Task<(CalcDataRecord? Record, string? Refusal)> SaveRecordAsync(CalcDataRecord draft, CancellationToken cancellationToken = default) =>
        SaveRecordCoreAsync(draft, false, cancellationToken);

    /// <summary>
    /// The save behind <see cref="SaveRecordAsync"/>. <paramref name="keepIndicativeOrigin"/> lets a material file read back by the
    /// library keep the "indicative" flag it was exported with; any other save of a material may carry that flag only when the
    /// material's earlier revision already had it.
    /// </summary>
    internal async Task<(CalcDataRecord? Record, string? Refusal)> SaveRecordCoreAsync(CalcDataRecord draft, bool keepIndicativeOrigin, CancellationToken cancellationToken)
    {
        var (record, refusal) = await PrepareRecordAsync(draft, keepIndicativeOrigin, cancellationToken).ConfigureAwait(false);

        if (record is null)
            return (null, refusal);

        try
        {
            await _store.SaveRecordAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (CalcInstanceImmutableException)
        {
            // Another save took this revision number first (Opus review, finding 15).
            return (null, $"Revision {record.Revision} of '{record.Title}' was saved by someone else at the same moment; save again to make the next revision.");
        }
        catch (CalcDocumentTooLargeException ex)
        {
            return (null, ex.Message);
        }

        return (record, null);
    }

    /// <summary>
    /// Everything a save does short of writing: the record as it would be stored (new id or next revision, signed-in person, time, cleaned text,
    /// material stamping, the licence lock carried over), or why it cannot be saved. The record import prepares a whole set with this and writes it
    /// in one transaction.
    /// </summary>
    internal async Task<(CalcDataRecord? Record, string? Refusal)> PrepareRecordAsync(CalcDataRecord draft, bool keepIndicativeOrigin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var latest = draft.Id == Guid.Empty ? null : await _store.FindRecordAsync(draft.Id, null, cancellationToken).ConfigureAwait(false);
        var record = draft with
        {
            Id = draft.Id == Guid.Empty ? Guid.NewGuid() : draft.Id,
            Revision = (latest?.Revision ?? 0) + 1,
            EnteredBy = CurrentPrincipal(),
            CreatedAt = _time.GetUtcNow(),
            Title = CalcSheetTextRenderer.Clean(draft.Title),
            SourceTitle = CalcSheetTextRenderer.Clean(draft.SourceTitle),
            Edition = CalcSheetTextRenderer.Clean(draft.Edition),
            TableOrClause = CalcSheetTextRenderer.Clean(draft.TableOrClause),
            LicenceHolder = CalcSheetTextRenderer.Clean(draft.LicenceHolder),
        };

        if (latest is not null && !string.Equals(latest.Kind, record.Kind, StringComparison.Ordinal))
            return (null, $"Record '{latest.Title}' is a {latest.Kind} record; a revision keeps its kind.");

        // The shape comes first: a missing entry or symbol is refused here, before anything below reads one (Opus review B-0, M4).
        if (record.Problem() is { } shape)
            return (null, shape);

        // Addendum B, W3: a material's values carry their own source, are stamped with who entered them and when, and one licensed
        // value makes the whole material licensed (the lock below then keeps it so).
        if (MaterialLibrary.IsMaterial(record.Kind))
        {
            // "Indicative" is the shipped seeds' flag; a user's own save keeps it only on a material that already had it (B-0 review, LOW 4).
            if (record.Origin == CalcDataOrigin.Indicative && !keepIndicativeOrigin && latest?.Origin != CalcDataOrigin.Indicative)
                record = record with { Origin = CalcDataOrigin.UserEntered };

            record = MaterialLibrary.Prepare(record, latest, record.EnteredBy, record.CreatedAt);

            if (MaterialProperties.Problem(record) is { } materialProblem)
                return (null, materialProblem);
        }

        // §A5, every kind of record: a value taken from a licensed source makes the whole record licensed, so a draft that left the flag
        // off cannot put such a value past the export lock (B-0 review, M5).
        if (!record.LicensedSource && record.Values.Any(v => v.Source?.Licence == CalcLicenceClass.LicensedSource))
            record = record with { LicensedSource = true };

        // §A5: once licensed, always licensed — a revision cannot release values from the export lock (Opus review, finding 5).
        if (latest is { LicensedSource: true } && !record.LicensedSource)
            return (null, $"Record '{latest.Title}' holds values from a licensed source; its revisions stay licensed and are never exported.");

        if (record.Problem() is { } problem)
            return (null, problem);

        return (record, null);
    }

    /// <summary>
    /// Saves a record made from a calculation's inputs. When any of those inputs takes its value from a licensed record,
    /// the new record is licensed too, so licensed values cannot be laundered into an exportable record (§A5).
    /// </summary>
    public async Task<(CalcDataRecord? Record, string? Refusal)> SaveRecordFromInputsAsync(CalcDataRecord draft, IEnumerable<CalcInputValue> sourceInputs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(sourceInputs);

        var traced = new Dictionary<Guid, bool>();

        foreach (var input in sourceInputs)
        {
            if (await IsLicensedValueAsync(input, traced, cancellationToken).ConfigureAwait(false))
            {
                draft = draft with { LicensedSource = true };
                break;
            }
        }

        return await SaveRecordAsync(draft, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The latest revision of every record a template can draw on (by its record kinds).</summary>
    public async Task<IReadOnlyList<CalcDataRecord>> RecordsForAsync(string templateReference, CancellationToken cancellationToken = default)
    {
        var kinds = _catalogue.Get(templateReference).Template.RecordKinds ?? [];
        var list = new List<CalcDataRecord>();

        // The generic starting materials are in the store before a template that takes materials asks for them.
        if (kinds.Any(k => MaterialLibrary.IsMaterial(k.Kind)))
            await Materials.EnsureSeededAsync(cancellationToken).ConfigureAwait(false);

        foreach (var kind in kinds)
            list.AddRange(await _store.ListRecordsAsync(kind.Kind, cancellationToken).ConfigureAwait(false));

        return list;
    }

    /// <summary>
    /// Fills a draft's inputs from a reference record (its latest revision, or <paramref name="revision"/>): every value
    /// whose symbol the template's record kind lists, each with the record's pin and table or clause as its source.
    /// </summary>
    public Task<CalcActionResult> ApplyRecordAsync(Guid instanceId, Guid recordId, int? revision = null, CancellationToken cancellationToken = default) =>
        ApplyRecordCoreAsync(instanceId, recordId, revision, null, false, cancellationToken);

    /// <summary>Applies a record; a material record is applied by property (and, if given, at a temperature) rather than by input symbol.</summary>
    internal async Task<CalcActionResult> ApplyRecordCoreAsync(Guid instanceId, Guid recordId, int? revision, MaterialTemperature? at, bool singleValueAtAnyTemperature, CancellationToken cancellationToken)
    {
        var instance = await _store.FindInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);

        if (instance is null)
            return CalcActionResult.Refused($"No calculation '{instanceId}' exists.");

        if (instance.Status != CalcInstanceStatus.Draft)
            return CalcActionResult.Refused($"Calculation '{instance.Name}' is {instance.Status}; only a draft takes values from a record.", instance);

        var record = await _store.FindRecordAsync(recordId, revision, cancellationToken).ConfigureAwait(false);

        if (record is null)
            return CalcActionResult.Refused($"No reference record '{recordId}' exists.", instance);

        var template = _catalogue.Get(instance.TemplateReference).Template;

        if ((template.RecordKinds ?? []).FirstOrDefault(k => string.Equals(k.Kind, record.Kind, StringComparison.Ordinal)) is not { } kind)
            return CalcActionResult.Refused($"'{template.Title}' does not draw on {record.Kind} records.", instance);

        if (MaterialLibrary.IsMaterial(record.Kind))
            return await ApplyMaterialAsync(instance, record, template, kind, at, singleValueAtAnyTemperature, cancellationToken).ConfigureAwait(false);

        if (at is not null)
            return CalcActionResult.Refused($"Record '{record.Title}' is not a material; it has no temperature to read it at.", instance);

        var provenance = RecordProvenance(record);
        var inputs = instance.Inputs.ToList();
        var applied = 0;

        foreach (var value in record.Values.Where(v => kind.Symbols.Contains(v.Symbol, StringComparer.Ordinal)))
        {
            if (template.FindInput(value.Symbol) is null)
                continue;

            inputs.RemoveAll(i => string.Equals(i.Symbol, value.Symbol, StringComparison.Ordinal));
            inputs.Add(new CalcInputValue(value.Symbol, Number: value.Number, Unit: value.Unit, Text: value.Text, Rows: value.Rows, Provenance: provenance));
            applied++;
        }

        return applied == 0
            ? CalcActionResult.Refused($"Record '{record.Title}' holds none of the inputs '{template.Title}' takes from {kind.Title}.", instance)
            : await UpdateInputsAsync(instanceId, inputs, instance.UpdatedAt, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The provenance an input takes from a record: its pin and table or clause, with the edition as revision.</summary>
    public static CalcProvenance RecordProvenance(CalcDataRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var reference = $"{record.Pin} · {record.TableOrClause}";
        return new CalcProvenance(CalcProvenanceKind.Reference,
            reference.Length > CalcProvenance.MaxReferenceLength ? reference[..CalcProvenance.MaxReferenceLength] : reference,
            record.Edition.Length > CalcProvenance.MaxRevisionLength ? record.Edition[..CalcProvenance.MaxRevisionLength] : record.Edition);
    }

    /// <summary>
    /// The inputs as they may leave the application: every value taken from a licensed record is withheld (the symbol
    /// and the record's pin stay, so the receiver knows which record the user must supply). A value that is a link to another
    /// calculation (an input, or a cell of a table) is withheld in the same way when that calculation, at the revision pinned,
    /// holds an input pinned to a licensed record, directly or through any depth of links (B-0 review, M6): the link stays so the
    /// receiver knows where it came from. §A5.
    /// </summary>
    public async Task<CalcInputSet> WithholdLicensedAsync(CalcInputSet inputs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var result = new CalcInputSet();
        var traced = new Dictionary<Guid, bool>();

        foreach (var value in inputs.Values)
        {
            if (await LicensedRecordPinAsync(value.Provenance, cancellationToken).ConfigureAwait(false) is { } pin)
            {
                result.Set(new CalcInputValue(value.Symbol, Provenance: new CalcProvenance(CalcProvenanceKind.Reference, $"{CalcRecordPin.Prefix}{pin.Id:N}@{pin.Revision} · {CalcCsv.WithheldMarker}", value.Provenance!.Revision)));
                continue;
            }

            if (await LicensedUpstreamAsync(value.Provenance, traced, cancellationToken).ConfigureAwait(false) is { } upstream)
            {
                result.Set(new CalcInputValue(value.Symbol, Provenance: WithheldLinkProvenance(upstream, value.Provenance!)));
                continue;
            }

            if (value.Rows is not null)
            {
                result.Set(value with { Rows = await WithholdCellsAsync(value.Rows, traced, static (_, provenance) => new CalcCell(Provenance: provenance), cancellationToken).ConfigureAwait(false) });
                continue;
            }

            result.Set(value);
        }

        return result;
    }

    /// <summary>What a withheld linked value says about itself on the sheet, in place of the number.</summary>
    internal const string LicensedDerivedText = "value derived from a licensed record — withheld";

    /// <summary>The rows of a table with each linked cell that derives from a licensed record made by <paramref name="withhold"/>; the other cells are as they were.</summary>
    private async Task<IReadOnlyList<CalcRow>> WithholdCellsAsync(IReadOnlyList<CalcRow> rows, Dictionary<Guid, bool> traced, Func<CalcCell, CalcProvenance, CalcCell> withhold, CancellationToken cancellationToken)
    {
        var changed = false;
        var list = new List<CalcRow>(rows.Count);

        foreach (var row in rows)
        {
            Dictionary<string, CalcCell>? cells = null;

            foreach (var (key, cell) in row?.Cells ?? new Dictionary<string, CalcCell>())
            {
                if (cell is null || await LicensedUpstreamAsync(cell.Provenance, traced, cancellationToken).ConfigureAwait(false) is not { } upstream)
                    continue;

                cells ??= new Dictionary<string, CalcCell>(row!.Cells, StringComparer.Ordinal);
                cells[key] = withhold(cell, WithheldLinkProvenance(upstream, cell.Provenance!));
            }

            changed |= cells is not null;
            list.Add(cells is null ? row! : new CalcRow(cells));
        }

        return changed ? list : rows;
    }

    /// <summary>The provenance a withheld linked value carries: where it is linked to, and that it was withheld (the importer reads the marker).</summary>
    private static CalcProvenance WithheldLinkProvenance(Guid upstreamId, CalcProvenance was)
    {
        var output = CalcLink.From(was)?.OutputSymbol;
        return new CalcProvenance(CalcProvenanceKind.Reference, $"{CalcLink.Prefix}{upstreamId:N}{(string.IsNullOrEmpty(output) ? string.Empty : "#" + output)} · {CalcCsv.WithheldMarker}", was.Revision);
    }

    /// <summary>
    /// The calculation a linked value comes from when that calculation (the revision pinned, an instance being one revision) holds an
    /// input pinned to a licensed record, directly or through any depth of links; <see langword="null"/> for a value that is not a link or
    /// whose upstream holds none. Conservative by design: it does not work out which inputs reach the output linked to (§A5).
    /// </summary>
    internal async Task<Guid?> LicensedUpstreamAsync(CalcProvenance? provenance, Dictionary<Guid, bool> traced, CancellationToken cancellationToken)
    {
        var upstreamId = CalcLink.From(provenance)?.UpstreamId ?? CalcInstance.LegacyUpstreamOf(provenance);

        if (upstreamId is not { } id)
            return null;

        if (!traced.TryGetValue(id, out var licensed))
            traced[id] = licensed = await HoldsLicensedInputAsync(id, cancellationToken).ConfigureAwait(false);

        return licensed ? id : null;
    }

    /// <summary>Whether a value, as an input of a calculation, is under the export lock: pinned to a licensed record or linked to a calculation that holds one.</summary>
    private async Task<bool> IsLicensedValueAsync(CalcInputValue input, Dictionary<Guid, bool> traced, CancellationToken cancellationToken)
    {
        if (await LicensedRecordPinAsync(input.Provenance, cancellationToken).ConfigureAwait(false) is not null
            || await LicensedUpstreamAsync(input.Provenance, traced, cancellationToken).ConfigureAwait(false) is not null)
            return true;

        foreach (var row in input.Rows ?? [])
        {
            foreach (var cell in row?.Cells.Values ?? [])
            {
                if (cell is not null && await LicensedUpstreamAsync(cell.Provenance, traced, cancellationToken).ConfigureAwait(false) is not null)
                    return true;
            }
        }

        return false;
    }

    /// <summary>Whether the calculation <paramref name="startId"/> or anything it takes a value from, through any number of links, has an input pinned to a licensed record.</summary>
    private async Task<bool> HoldsLicensedInputAsync(Guid startId, CancellationToken cancellationToken)
    {
        var seen = new HashSet<Guid>();
        var queue = new Queue<(Guid Id, int Depth)>();
        queue.Enqueue((startId, 0));

        while (queue.Count > 0)
        {
            var (id, depth) = queue.Dequeue();

            if (!seen.Add(id))
                continue;

            // Links cannot be made deeper than the cap; a chain that is (a forged import) is not guessed at.
            if (depth > MaxChainDepth + 2)
                return true;

            if (await _store.FindInstanceAsync(id, cancellationToken).ConfigureAwait(false) is not { } instance)
                continue;

            foreach (var input in instance.Inputs)
            {
                if (await LicensedRecordPinAsync(input.Provenance, cancellationToken).ConfigureAwait(false) is not null)
                    return true;
            }

            foreach (var up in instance.UpstreamIds)
                queue.Enqueue((up, depth + 1));
        }

        return false;
    }

    /// <summary>
    /// The pin of a licensed record that <paramref name="provenance"/> cites, or <see langword="null"/>. A pin to a revision that does
    /// not exist is judged by the record's latest revision, so a made-up revision number cannot slip a licensed record's value past the lock (Red R-A-06).
    /// </summary>
    internal async Task<(Guid Id, int Revision)?> LicensedRecordPinAsync(CalcProvenance? provenance, CancellationToken cancellationToken)
    {
        if (CalcDataRecord.PinOf(provenance) is not { } pin)
            return null;

        var record = await _store.FindRecordAsync(pin.Id, pin.Revision, cancellationToken).ConfigureAwait(false)
            ?? await _store.FindRecordAsync(pin.Id, null, cancellationToken).ConfigureAwait(false);

        return record is not null && await IsLicensedRecordAsync(record, cancellationToken).ConfigureAwait(false) ? pin : null;
    }

    /// <summary>
    /// Whether <paramref name="record"/> is licensed: it is when it is, or when any other revision of the same record is (earlier or later). A record
    /// flagged licensed from revision 2 on is licensed in revision 1 too, so no revision releases the values (Addendum B review, SEC-B-02).
    /// </summary>
    internal async Task<bool> IsLicensedRecordAsync(CalcDataRecord record, CancellationToken cancellationToken) =>
        record.IsLicensed() || await _store.IsLicensedInAnyRevisionAsync(record.Id, cancellationToken).ConfigureAwait(false);

    /// <summary>A record as CSV (symbol, value, unit, then its rows), refused for a licensed record (§A5).</summary>
    public async Task<(string? Csv, string? Refusal)> ExportRecordAsync(Guid recordId, int? revision = null, CancellationToken cancellationToken = default)
    {
        var record = await _store.FindRecordAsync(recordId, revision, cancellationToken).ConfigureAwait(false);

        if (record is null)
            return (null, $"No reference record '{recordId}' exists.");

        if (await IsLicensedRecordAsync(record, cancellationToken).ConfigureAwait(false))
            return (null, $"Record '{record.Title}' holds values from a licensed source ({record.SourceTitle}, {record.Edition}); its values are never exported.");

        var sb = new StringBuilder();
        // The citation is free text the user typed: it goes out as one quoted cell, never as fields a spreadsheet might evaluate (Red R-A-16).
        sb.Append(Csv("# " + record.Citation)).Append('\n').Append("symbol,value,unit,text\n");

        foreach (var v in record.Values)
        {
            sb.Append(Csv(v.Symbol)).Append(',').Append(v.Number?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty).Append(',').Append(Csv(v.Unit ?? string.Empty)).Append(',').Append(Csv(v.Text ?? string.Empty)).Append('\n');

            foreach (var row in v.Rows ?? [])
                sb.Append(Csv(v.Symbol + "[]")).Append(',').Append(',').Append(',').Append(Csv(string.Join("; ", row.Cells.Select(c => $"{c.Key}={c.Value.Number?.ToString("R", CultureInfo.InvariantCulture) ?? c.Value.Text}{(c.Value.Unit is { Length: > 0 } u ? " " + u : string.Empty)}")))).Append('\n');
        }

        return (sb.ToString(), null);
    }

    internal static string Csv(string text)
    {
        var clean = CalcCsv.IsFormulaLike(text) ? "'" + text : text;
        // A value with a line break is quoted too: unquoted it would end the row, and what follows would start a row of its own (C-3 review, finding 2).
        return clean.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{clean.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : clean;
    }
}
