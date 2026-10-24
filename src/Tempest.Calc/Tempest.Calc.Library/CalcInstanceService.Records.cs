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
    public async Task<(CalcDataRecord? Record, string? Refusal)> SaveRecordAsync(CalcDataRecord draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var latest = draft.Id == Guid.Empty ? null : await _store.FindRecordAsync(draft.Id, null, cancellationToken).ConfigureAwait(false);
        var record = draft with
        {
            Id = draft.Id == Guid.Empty ? Guid.NewGuid() : draft.Id,
            Revision = (latest?.Revision ?? 0) + 1,
            EnteredBy = string.IsNullOrWhiteSpace(draft.EnteredBy) ? _domain.ResolveCurrentPrincipalId() : draft.EnteredBy,
            CreatedAt = _time.GetUtcNow(),
            Title = CalcSheetTextRenderer.Clean(draft.Title),
            SourceTitle = CalcSheetTextRenderer.Clean(draft.SourceTitle),
            Edition = CalcSheetTextRenderer.Clean(draft.Edition),
            TableOrClause = CalcSheetTextRenderer.Clean(draft.TableOrClause),
            LicenceHolder = CalcSheetTextRenderer.Clean(draft.LicenceHolder),
        };

        if (latest is not null && !string.Equals(latest.Kind, record.Kind, StringComparison.Ordinal))
            return (null, $"Record '{latest.Title}' is a {latest.Kind} record; a revision keeps its kind.");

        // §A5: once licensed, always licensed — a revision cannot release values from the export lock (Opus review, finding 5).
        if (latest is { LicensedSource: true } && !record.LicensedSource)
            return (null, $"Record '{latest.Title}' holds values from a licensed source; its revisions stay licensed and are never exported.");

        if (record.Problem() is { } problem)
            return (null, problem);

        try
        {
            await _store.SaveRecordAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (CalcInstanceImmutableException)
        {
            // Another save took this revision number first (Opus review, finding 15).
            return (null, $"Revision {record.Revision} of '{record.Title}' was saved by someone else at the same moment; save again to make the next revision.");
        }

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

        foreach (var input in sourceInputs)
        {
            if (CalcDataRecord.PinOf(input.Provenance) is { } pin
                && await _store.FindRecordAsync(pin.Id, pin.Revision, cancellationToken).ConfigureAwait(false) is { LicensedSource: true })
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

        foreach (var kind in kinds)
            list.AddRange(await _store.ListRecordsAsync(kind.Kind, cancellationToken).ConfigureAwait(false));

        return list;
    }

    /// <summary>
    /// Fills a draft's inputs from a reference record (its latest revision, or <paramref name="revision"/>): every value
    /// whose symbol the template's record kind lists, each with the record's pin and table or clause as its source.
    /// </summary>
    public async Task<CalcActionResult> ApplyRecordAsync(Guid instanceId, Guid recordId, int? revision = null, CancellationToken cancellationToken = default)
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
            : await UpdateInputsAsync(instanceId, inputs, cancellationToken).ConfigureAwait(false);
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
    /// and the record's pin stay, so the receiver knows which record the user must supply). §A5.
    /// </summary>
    public async Task<CalcInputSet> WithholdLicensedAsync(CalcInputSet inputs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var result = new CalcInputSet();

        foreach (var value in inputs.Values)
        {
            if (CalcDataRecord.PinOf(value.Provenance) is { } pin
                && await _store.FindRecordAsync(pin.Id, pin.Revision, cancellationToken).ConfigureAwait(false) is { LicensedSource: true } record)
            {
                result.Set(new CalcInputValue(value.Symbol, Provenance: new CalcProvenance(CalcProvenanceKind.Reference, $"{record.Pin} · licensed value withheld", value.Provenance!.Revision)));
                continue;
            }

            result.Set(value);
        }

        return result;
    }

    /// <summary>A record as CSV (symbol, value, unit, then its rows), refused for a licensed record (§A5).</summary>
    public async Task<(string? Csv, string? Refusal)> ExportRecordAsync(Guid recordId, int? revision = null, CancellationToken cancellationToken = default)
    {
        var record = await _store.FindRecordAsync(recordId, revision, cancellationToken).ConfigureAwait(false);

        if (record is null)
            return (null, $"No reference record '{recordId}' exists.");

        if (record.LicensedSource)
            return (null, $"Record '{record.Title}' holds values from a licensed source ({record.SourceTitle}, {record.Edition}); its values are never exported.");

        var sb = new StringBuilder();
        sb.Append("# ").Append(record.Citation).Append('\n').Append("symbol,value,unit,text\n");

        foreach (var v in record.Values)
        {
            sb.Append(Csv(v.Symbol)).Append(',').Append(v.Number?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty).Append(',').Append(Csv(v.Unit ?? string.Empty)).Append(',').Append(Csv(v.Text ?? string.Empty)).Append('\n');

            foreach (var row in v.Rows ?? [])
                sb.Append(Csv(v.Symbol + "[]")).Append(',').Append(',').Append(',').Append(Csv(string.Join("; ", row.Cells.Select(c => $"{c.Key}={c.Value.Number?.ToString("R", CultureInfo.InvariantCulture) ?? c.Value.Text}{(c.Value.Unit is { Length: > 0 } u ? " " + u : string.Empty)}")))).Append('\n');
        }

        return (sb.ToString(), null);
    }

    private static string Csv(string text)
    {
        var clean = CalcCsv.IsFormulaLike(text) ? "'" + text : text;
        return clean.Contains(',', StringComparison.Ordinal) || clean.Contains('"', StringComparison.Ordinal) ? $"\"{clean.Replace("\"", "\"\"", StringComparison.Ordinal)}\"" : clean;
    }

    /// <summary>Stale reasons from records: an input pins a revision of a record that has been revised since.</summary>
    private async Task RecordStalenessAsync(CalcInstance instance, List<string> reasons, CancellationToken cancellationToken)
    {
        foreach (var input in instance.Inputs)
        {
            if (CalcDataRecord.PinOf(input.Provenance) is not { } pin)
                continue;

            var latest = await _store.FindRecordAsync(pin.Id, null, cancellationToken).ConfigureAwait(false);

            if (latest is null)
                reasons.Add($"'{input.Symbol}' takes its value from reference record {pin.Id:N}, which no longer exists.");
            else if (latest.Revision > pin.Revision)
                reasons.Add($"'{input.Symbol}' takes its value from '{latest.Title}' revision {pin.Revision}; revision {latest.Revision} has been entered since. Apply the record again.");
        }
    }
}
