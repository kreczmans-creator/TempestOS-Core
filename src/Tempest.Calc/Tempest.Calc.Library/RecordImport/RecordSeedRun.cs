namespace Tempest.Calc.Library;

/// <summary>
/// One seeding pass of the bundled record sets (owner decision 2026-10-29), as the record import checks each set against it. The record kinds the
/// visible calculators declare are built once, and the store's records are read once; each set's title check then reads this, with the sets
/// planned earlier in the pass added, instead of reading the whole record collection again. Planning every set reads the store once, not once per set.
/// </summary>
internal sealed class RecordSeedRun
{
    private readonly Dictionary<string, Dictionary<Guid, CalcDataRecord>> _byKind = new(StringComparer.Ordinal);

    /// <param name="schemas">The record kinds the visible calculators declare (<see cref="RecordImportService.Schemas"/>).</param>
    /// <param name="latest">The latest revision of every record in the store (<see cref="CalcInstanceStore.ListRecordsAsync"/> with no kind).</param>
    public RecordSeedRun(IReadOnlyDictionary<string, RecordKindSchema> schemas, IReadOnlyList<CalcDataRecord> latest)
    {
        Schemas = schemas ?? throw new ArgumentNullException(nameof(schemas));
        ArgumentNullException.ThrowIfNull(latest);
        Add(latest);
    }

    /// <summary>The record kinds the visible calculators declare.</summary>
    public IReadOnlyDictionary<string, RecordKindSchema> Schemas { get; }

    /// <summary>The latest revision of every record of <paramref name="kind"/>: the store's, then the ones planned in this pass.</summary>
    public IReadOnlyList<CalcDataRecord> Existing(string kind) =>
        _byKind.TryGetValue(kind, out var records) ? [.. records.Values] : [];

    /// <summary>Adds planned records; a later revision of a record replaces the earlier one.</summary>
    public void Add(IEnumerable<CalcDataRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        foreach (var record in records)
        {
            if (!_byKind.TryGetValue(record.Kind, out var ofKind))
                _byKind[record.Kind] = ofKind = [];

            if (!ofKind.TryGetValue(record.Id, out var known) || known.Revision < record.Revision)
                ofKind[record.Id] = record;
        }
    }
}
