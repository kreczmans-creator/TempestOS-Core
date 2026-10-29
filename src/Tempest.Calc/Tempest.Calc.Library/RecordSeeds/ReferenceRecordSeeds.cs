namespace Tempest.Calc.Library.RecordSeeds;

/// <summary>What one bundled set did at a seeding pass.</summary>
public enum RecordSeedResult
{
    /// <summary>Its records were saved now.</summary>
    Saved,

    /// <summary>Its records were already in the store.</summary>
    AlreadyPresent,

    /// <summary>No visible calculator declares its kind (an optional calculator is switched off); nothing was saved.</summary>
    KindHidden,

    /// <summary>The import refused it; the problem says why. A test keeps every shipped set clear of this.</summary>
    Refused,
}

/// <summary>The outcome of seeding one set.</summary>
public sealed record RecordSeedOutcome(RecordSeedSet Set, RecordSeedResult Result, int Records, string? Problem);

/// <summary>
/// The bundled reference records (owner decision 2026-10-29, comment 5): the web-sourced and derived record sets of
/// <see cref="RecordSeedCatalogue"/>, imported through the ordinary record import so each passes the same rules as a person's own file. A set is
/// saved once: its records carry the set's marker in their notes, and a later pass skips a set whose marker is in the store. Nothing is ever
/// deleted or revised here; a person's own record of the same kind stays as it is.
/// </summary>
public sealed class ReferenceRecordSeeds
{
    /// <summary>Who the bundled records name as having entered them.</summary>
    public const string SeedPrincipal = "TempestOS (bundled reference records)";

    private readonly CalcInstanceService _service;
    private bool _seeded;

    internal ReferenceRecordSeeds(CalcInstanceService service) => _service = service ?? throw new ArgumentNullException(nameof(service));

    /// <summary>The CSV text of a bundled set.</summary>
    public static string ReadCsv(RecordSeedSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        using var stream = typeof(ReferenceRecordSeeds).Assembly.GetManifestResourceStream(set.ResourceName)
            ?? throw new InvalidOperationException($"The bundled record set '{set.Id}' is missing from the build ({set.ResourceName}).");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The import request for a set, as the seeder sends it.</summary>
    public static RecordImportRequest Request(RecordSeedSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return new RecordImportRequest(System.Text.Encoding.UTF8.GetBytes(ReadCsv(set)), $"bundled-{set.Id}.csv", set.TemplateId, set.Provenance());
    }

    /// <summary>
    /// Saves every bundled set that is not in the store yet. Safe to call again: a set already in is skipped, and so is a set whose kind no visible
    /// calculator declares. A refused set saves nothing and is reported; the others still go in.
    /// </summary>
    /// <remarks>
    /// It runs at every start-up, so it is kept cheap (seeding speed fix, 2026-10-29): the store's records are read once and the record kinds built
    /// once for the whole pass, each set is checked through the ordinary import against those plus the sets planned before it, and every set that
    /// passes is written in ONE transaction. If that write fails, each set is written on its own, so a set that cannot be written refuses only itself.
    /// </remarks>
    public async Task<IReadOnlyList<RecordSeedOutcome>> EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        var sets = RecordSeedCatalogue.All;
        var outcomes = new RecordSeedOutcome?[sets.Count];
        var latest = await _service.Store.ListRecordsAsync(null, cancellationToken).ConfigureAwait(false);
        var present = MarkedSetIds(latest);
        RecordSeedRun? run = null;
        var pending = new List<(int Index, IReadOnlyList<CalcDataRecord> Records)>();

        for (var i = 0; i < sets.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var set = sets[i];

            if (present.Contains(set.Id))
            {
                outcomes[i] = new RecordSeedOutcome(set, RecordSeedResult.AlreadyPresent, 0, null);
                continue;
            }

            // Built only when a set is missing: a start-up on a store that has every set reads the records and nothing else.
            run ??= new RecordSeedRun(_service.RecordImport.Schemas(), latest);

            if (RecordTemplates.Find(set.TemplateId) is not { } template || run.Schemas.GetValueOrDefault(template.Kind) is not { } schema)
            {
                outcomes[i] = new RecordSeedOutcome(set, RecordSeedResult.KindHidden, 0, null);
                continue;
            }

            if (schema.IsMaterial)
            {
                // A material set is checked against the material library in the store, so the sets planned before it are written first.
                await WritePendingAsync(sets, pending, outcomes, cancellationToken).ConfigureAwait(false);
                var outcome = await _service.RecordImport.CommitAsSeedAsync(Request(set), SeedPrincipal, cancellationToken).ConfigureAwait(false);
                run.Add(outcome.Records);
                outcomes[i] = outcome.Committed
                    ? new RecordSeedOutcome(set, RecordSeedResult.Saved, outcome.Records.Count, null)
                    : new RecordSeedOutcome(set, RecordSeedResult.Refused, 0, outcome.Refusal);
                continue;
            }

            var (records, refusal) = await _service.RecordImport.PlanSeedAsync(Request(set), SeedPrincipal, run, cancellationToken).ConfigureAwait(false);

            if (refusal is not null)
                outcomes[i] = new RecordSeedOutcome(set, RecordSeedResult.Refused, 0, refusal);
            else
                pending.Add((i, records));
        }

        await WritePendingAsync(sets, pending, outcomes, cancellationToken).ConfigureAwait(false);
        _seeded = true;
        return [.. outcomes.Select(o => o!)];
    }

    /// <summary>Writes the planned sets in one transaction; if that fails, each set on its own, so a set that cannot be written refuses only itself.</summary>
    private async Task WritePendingAsync(IReadOnlyList<RecordSeedSet> sets, List<(int Index, IReadOnlyList<CalcDataRecord> Records)> pending, RecordSeedOutcome?[] outcomes, CancellationToken cancellationToken)
    {
        if (pending.Count == 0)
            return;

        var failed = await _service.RecordImport.WriteAsync([.. pending.SelectMany(p => p.Records)], cancellationToken).ConfigureAwait(false);

        foreach (var (index, records) in pending)
        {
            var problem = failed is null || pending.Count == 1
                ? failed
                : await _service.RecordImport.WriteAsync(records, cancellationToken).ConfigureAwait(false);

            outcomes[index] = problem is null
                ? new RecordSeedOutcome(sets[index], RecordSeedResult.Saved, records.Count, null)
                : new RecordSeedOutcome(sets[index], RecordSeedResult.Refused, 0, problem);
        }

        pending.Clear();
    }

    private const string MarkerStart = "[seed-set:";

    /// <summary>The ids of the sets whose marker (<see cref="RecordSeedSet.Marker"/>) is in a record's notes: one pass over the notes, not one per set.</summary>
    private static HashSet<string> MarkedSetIds(IReadOnlyList<CalcDataRecord> records)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var notes in records.Select(r => r.Notes))
        {
            if (notes is null)
                continue;

            for (var at = notes.IndexOf(MarkerStart, StringComparison.Ordinal); at >= 0; at = notes.IndexOf(MarkerStart, at + 1, StringComparison.Ordinal))
            {
                var start = at + MarkerStart.Length;
                var end = notes.IndexOf(']', start);

                if (end > start)
                    ids.Add(notes[start..end]);
            }
        }

        return ids;
    }

    /// <summary>Whether a pass has run in this session.</summary>
    public bool HasRun => _seeded;
}
