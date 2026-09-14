using Tempest.Core.Logging;

namespace Tempest.Core.ReferenceData.Seeding;

/// <summary>
/// Applies a seed dataset to a reference library through that library's
/// own catalogue.
/// </summary>
/// <remarks>
/// <para>
/// <b>The whole of the import mechanism.</b> There is no pipeline, no
/// staging area, no second store and no schema: this reads a dataset the
/// process already holds and calls
/// <see cref="IReferenceDataCatalog{TDefinition}.RegisterAsync"/> once per
/// record. Everything that makes a registration durable, indexed,
/// revisable and supersedable is the catalogue's own existing behaviour,
/// unchanged.
/// </para>
/// <para>
/// <b>Additive, never destructive.</b> A record whose identity is already
/// present is skipped, not overwritten — so seeding cannot silently
/// discard a value a person has since corrected, checked or released.
/// Correcting seeded data is
/// <see cref="IReferenceDataCatalog{TDefinition}.ReviseAsync"/>'s job, and
/// replacing it wholesale is
/// <see cref="IReferenceDataCatalog{TDefinition}.SupersedeAsync"/>'s;
/// neither is something an import may do behind a reviewer's back.
/// </para>
/// <para>
/// <b>Seeding is not verifying.</b> Every record lands in
/// <see cref="ReferenceValidationState.Draft"/>, because that is where
/// <see cref="IReferenceDataCatalog{TDefinition}.RegisterAsync"/> puts it
/// and this service does not ask for anything else. Promotion needs
/// provenance this service has no standing to write — a named reviewer and
/// a date — which is exactly the guarantee that a populated library cannot
/// masquerade as a verified one.
/// </para>
/// </remarks>
public sealed class ReferenceSeedService
{
    private readonly ILogger? _logger;

    /// <summary>Initialises a new instance of the <see cref="ReferenceSeedService"/> class.</summary>
    /// <param name="logger">An optional logger.</param>
    public ReferenceSeedService(ILogger? logger = null) => _logger = logger;

    /// <summary>
    /// Registers every record in <paramref name="seed"/> that
    /// <paramref name="catalog"/> does not already hold.
    /// </summary>
    /// <typeparam name="TDefinition">The library's own definition type.</typeparam>
    /// <param name="catalog">The library to populate.</param>
    /// <param name="seed">The dataset to populate it from.</param>
    /// <param name="cancellationToken">A token observed while seeding.</param>
    /// <returns>What the run did, record by record.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="seed"/> is <see langword="null"/>.</exception>
    public async Task<ReferenceSeedOutcome> ApplyAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog,
        IReferenceSeed<TDefinition> seed,
        CancellationToken cancellationToken = default)
        where TDefinition : class
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(seed);

        var entries = new List<ReferenceSeedEntry>(seed.Records.Count);

        foreach (var record in seed.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await catalog.FindAsync(record.RecordId, cancellationToken).ConfigureAwait(false) is not null)
            {
                entries.Add(new ReferenceSeedEntry(record.RecordId, ReferenceSeedAction.AlreadyPresent));
                continue;
            }

            try
            {
                await catalog
                    .RegisterAsync(record.RecordId, record.Definition, record.Provenance, record.Source, cancellationToken)
                    .ConfigureAwait(false);

                entries.Add(new ReferenceSeedEntry(record.RecordId, ReferenceSeedAction.Registered));
            }
            catch (DuplicateReferenceRecordException)
            {
                // Another writer registered the same identity between the
                // read above and this write. The catalogue's own lock made
                // that safe; from here it is simply already present.
                entries.Add(new ReferenceSeedEntry(record.RecordId, ReferenceSeedAction.AlreadyPresent));
            }
        }

        var outcome = new ReferenceSeedOutcome(catalog.LibraryName, seed.DatasetName, seed.DatasetRevision, entries);

        _logger?.Information(
            $"Seeded {catalog.LibraryName} from '{seed.DatasetName}' r{seed.DatasetRevision}: "
            + $"{outcome.RegisteredCount} registered, {outcome.AlreadyPresentCount} already present.");

        return outcome;
    }

    /// <summary>
    /// Applies <paramref name="seed"/> to <paramref name="catalog"/> only if
    /// the library currently holds no record at all — the gate a start-up
    /// phase needs and <see cref="ApplyAsync{TDefinition}"/> does not
    /// provide on its own (`WP 18.0B-R1`, `TD-163`).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why "library is empty" rather than "record is absent."</b>
    /// <see cref="ApplyAsync{TDefinition}"/> is already per-record additive
    /// — it never overwrites a record whose identity already exists. That
    /// is the right rule for a deliberate, governed top-up (the "Populate
    /// Material Library" action a person presses). It is the wrong rule for
    /// an unattended start-up phase: if a library already holds even one
    /// record of a user's own under an identity the shipped dataset does
    /// not use, per-record seeding would still pour every shipped record in
    /// beside it — a library nobody asked to be populated growing anyway.
    /// Gating on the library as a whole, once, is what makes a re-launch
    /// over data the user has already touched a strict no-op for that
    /// library, while a library nobody has touched still gets populated.
    /// </para>
    /// <para>
    /// Reading the whole library before writing is safe here specifically
    /// because a start-up phase runs once, before anything else can write
    /// to the same catalogue — unlike <see cref="ApplyAsync{TDefinition}"/>,
    /// this is not meant to be safe against a concurrent writer racing the
    /// same call.
    /// </para>
    /// </remarks>
    /// <typeparam name="TDefinition">The library's own definition type.</typeparam>
    /// <param name="catalog">The library to populate.</param>
    /// <param name="seed">The dataset to populate it from.</param>
    /// <param name="cancellationToken">A token observed while seeding.</param>
    /// <returns>What the run did, or <see langword="null"/> if the library already held at least one record and was left untouched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="seed"/> is <see langword="null"/>.</exception>
    public async Task<ReferenceSeedOutcome?> ApplyIfEmptyAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog,
        IReferenceSeed<TDefinition> seed,
        CancellationToken cancellationToken = default)
        where TDefinition : class
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(seed);

        var existing = await catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            _logger?.Information(
                $"{catalog.LibraryName} already holds {existing.Count} record(s); left untouched by '{seed.DatasetName}'.");

            return null;
        }

        return await ApplyAsync(catalog, seed, cancellationToken).ConfigureAwait(false);
    }
}
