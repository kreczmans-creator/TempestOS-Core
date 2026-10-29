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
    public async Task<IReadOnlyList<RecordSeedOutcome>> EnsureSeededAsync(CancellationToken cancellationToken = default)
    {
        var outcomes = new List<RecordSeedOutcome>();
        var present = (await _service.Store.ListRecordsAsync(null, cancellationToken).ConfigureAwait(false))
            .Select(r => r.Notes ?? string.Empty)
            .ToList();

        foreach (var set in RecordSeedCatalogue.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (present.Any(n => n.Contains(set.Marker, StringComparison.Ordinal)))
            {
                outcomes.Add(new RecordSeedOutcome(set, RecordSeedResult.AlreadyPresent, 0, null));
                continue;
            }

            if (RecordTemplates.Find(set.TemplateId) is not { } template || _service.RecordImport.SchemaFor(template.Kind) is null)
            {
                outcomes.Add(new RecordSeedOutcome(set, RecordSeedResult.KindHidden, 0, null));
                continue;
            }

            var outcome = await _service.RecordImport.CommitAsSeedAsync(Request(set), SeedPrincipal, cancellationToken).ConfigureAwait(false);
            outcomes.Add(outcome.Committed
                ? new RecordSeedOutcome(set, RecordSeedResult.Saved, outcome.Records.Count, null)
                : new RecordSeedOutcome(set, RecordSeedResult.Refused, 0, outcome.Refusal));
        }

        _seeded = true;
        return outcomes;
    }

    /// <summary>Whether a pass has run in this session.</summary>
    public bool HasRun => _seeded;
}
