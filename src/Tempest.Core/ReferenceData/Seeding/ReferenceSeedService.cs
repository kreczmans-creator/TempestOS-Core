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
                    .RegisterAsync(record.RecordId, record.Definition, record.Provenance, cancellationToken)
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
}
