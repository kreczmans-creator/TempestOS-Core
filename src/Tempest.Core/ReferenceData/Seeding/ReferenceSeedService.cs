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
/// neither is something an import may do behind a reviewer's back. The one
/// exception is narrow and stated: with a release policy present, a shipped
/// record that is still exactly as an earlier seed left it — Draft, never
/// revised, never verified, from the same source organisation — is brought
/// up to the current shipped dataset before it is released
/// (<see cref="ReferenceSeedAction.Refreshed"/>). Nobody has touched such a
/// record, so there is no person's work to protect.
/// </para>
/// <para>
/// <b>Release at seed is a policy, not a default of the mechanism.</b>
/// Constructed without a <see cref="ReferenceSeedReleasePolicy"/>, every
/// record lands <see cref="ReferenceValidationState.Draft"/> exactly as
/// before. The running host registers the policy (PO decision 2026-10-01:
/// shipped libraries must be usable from day one), and with it every record
/// this service registers from a dataset marked
/// <see cref="IReferenceSeed{TDefinition}.ReleaseAtSeed"/> is verified and
/// released through
/// <see cref="Review.ReferenceReviewService"/> as the named seed principal —
/// the ordinary governed path, with its permission checks and audit rows,
/// never a direct write of a validation state.
/// </para>
/// </remarks>
public sealed class ReferenceSeedService
{
    private readonly ILogger? _logger;
    private readonly ReferenceSeedReleasePolicy? _releasePolicy;

    /// <summary>Initialises a new instance of the <see cref="ReferenceSeedService"/> class.</summary>
    /// <param name="logger">An optional logger.</param>
    /// <param name="releasePolicy">
    /// When present, every record this service registers (or refreshes) is
    /// released for engineering use through the policy's governed path.
    /// When absent, every record lands Draft.
    /// </param>
    public ReferenceSeedService(ILogger? logger = null, ReferenceSeedReleasePolicy? releasePolicy = null)
    {
        _logger = logger;
        _releasePolicy = releasePolicy;
    }

    /// <summary>Whether this service releases what it seeds.</summary>
    public bool ReleasesAtSeed => _releasePolicy is { IsEnabled: true };

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

        // Colour review board v0.23.0, B3: a pass that releases is the seed
        // process's work from first write to last — the registration, any
        // refresh, and the verify and release rows — so the audit actor and
        // the revision author are the seed identity for its whole duration,
        // restored when the pass ends (or throws).
        using var actingAsSeed = ReleasesAtSeed && seed.ReleaseAtSeed ? _releasePolicy!.ActAsSeed() : null;

        foreach (var record in seed.Records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existing = await catalog.FindAsync(record.RecordId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                entries.Add(await RefreshIfUntouchedAsync(catalog, seed, record, existing, cancellationToken).ConfigureAwait(false));
                continue;
            }

            try
            {
                await catalog
                    .RegisterAsync(record.RecordId, record.Definition, record.Provenance, record.Source, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (DuplicateReferenceRecordException)
            {
                // Another writer registered the same identity between the
                // read above and this write. The catalogue's own lock made
                // that safe; from here it is simply already present.
                entries.Add(new ReferenceSeedEntry(record.RecordId, ReferenceSeedAction.AlreadyPresent));
                continue;
            }
            catch (DuplicateReferenceKeyException)
            {
                // A record under another identity — very possibly a person's
                // own — already holds this designation. It wins; the shipped
                // record is simply not added.
                _logger?.Warning(
                    $"{catalog.LibraryName}: shipped record '{record.RecordId}' not added — another record already holds its key.");
                entries.Add(new ReferenceSeedEntry(record.RecordId, ReferenceSeedAction.KeyConflict));
                continue;
            }

            var released = await ReleaseIfPolicyAsync(catalog, seed, record.RecordId, cancellationToken).ConfigureAwait(false);
            entries.Add(new ReferenceSeedEntry(record.RecordId, ReferenceSeedAction.Registered, released));
        }

        var outcome = new ReferenceSeedOutcome(catalog.LibraryName, seed.DatasetName, seed.DatasetRevision, entries);

        _logger?.Information(
            $"Seeded {catalog.LibraryName} from '{seed.DatasetName}' r{seed.DatasetRevision}: "
            + $"{outcome.RegisteredCount} registered, {outcome.RefreshedCount} refreshed, "
            + $"{outcome.AlreadyPresentCount} already present, {outcome.KeyConflictCount} key conflicts, "
            + $"{outcome.ReleasedCount} released at seed.");

        return outcome;
    }

    /// <summary>
    /// Applies <paramref name="seed"/> to <paramref name="catalog"/> only if
    /// the library holds nothing a person put there — no record at all, or
    /// only records the shipped dataset itself names — the gate a start-up
    /// phase needs (`WP 18.0B-R1`, `TD-163`, extended by the PO decision of
    /// 2026-10-01).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why "nothing a person put there" rather than "record is absent."</b>
    /// <see cref="ApplyAsync{TDefinition}"/> is already per-record additive.
    /// That is the right rule for a deliberate, governed top-up (the
    /// "Populate Material Library" action a person presses). It is the wrong
    /// rule for an unattended start-up phase over a library a person has
    /// started to build themselves: their library would grow with records
    /// they never asked for. A library holding any record under an identity
    /// the shipped dataset does not use is therefore left strictly alone.
    /// </para>
    /// <para>
    /// <b>Why a library of shipped records only is topped up.</b> An
    /// installation seeded by an earlier, smaller dataset holds only shipped
    /// records. Leaving it alone would freeze it at that dataset forever —
    /// the six Draft materials the Product Owner could not calculate with.
    /// Topping it up adds the records the dataset has gained since, and
    /// (with a release policy) refreshes and releases the earlier records
    /// nobody has touched. Records a person has revised, checked or released
    /// are left exactly as they are.
    /// </para>
    /// <para>
    /// Reading the whole library before writing is safe here specifically
    /// because a start-up phase runs once, before anything else can write
    /// to the same catalogue.
    /// </para>
    /// </remarks>
    /// <typeparam name="TDefinition">The library's own definition type.</typeparam>
    /// <param name="catalog">The library to populate.</param>
    /// <param name="seed">The dataset to populate it from.</param>
    /// <param name="cancellationToken">A token observed while seeding.</param>
    /// <returns>What the run did, or <see langword="null"/> if the library held a record of a person's own and was left untouched.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="seed"/> is <see langword="null"/>.</exception>
    public async Task<ReferenceSeedOutcome?> ApplyAtStartupAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog,
        IReferenceSeed<TDefinition> seed,
        CancellationToken cancellationToken = default)
        where TDefinition : class
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(seed);

        var shipped = new HashSet<string>(seed.Records.Select(r => r.RecordId), StringComparer.Ordinal);
        var existing = await catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        var foreign = existing.Count(r => !shipped.Contains(r.Id));

        if (foreign > 0)
        {
            _logger?.Information(
                $"{catalog.LibraryName} holds {foreign} record(s) the shipped dataset does not name; "
                + $"left untouched by '{seed.DatasetName}'.");

            return null;
        }

        return await ApplyAsync(catalog, seed, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReferenceSeedEntry> RefreshIfUntouchedAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog,
        IReferenceSeed<TDefinition> seed,
        ReferenceSeedRecord<TDefinition> record,
        IReferenceRecord<TDefinition> existing,
        CancellationToken cancellationToken)
        where TDefinition : class
    {
        if (!ReleasesAtSeed || !seed.ReleaseAtSeed || !IsUntouchedShippedRecord(existing, record))
            return new ReferenceSeedEntry(record.RecordId, ReferenceSeedAction.AlreadyPresent);

        try
        {
            await catalog.ReviseAsync(
                record.RecordId,
                record.Definition,
                record.Provenance,
                $"Brought up to the shipped dataset '{seed.DatasetName}' r{seed.DatasetRevision} before release at seed "
                + $"({ReferenceSeedReleasePolicy.DecisionReference}). The record had not been revised, checked or "
                + "verified by anyone since it was first seeded.",
                record.Source,
                cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateReferenceKeyException)
        {
            _logger?.Warning(
                $"{catalog.LibraryName}: shipped record '{record.RecordId}' not refreshed — another record already holds its key.");
            return new ReferenceSeedEntry(record.RecordId, ReferenceSeedAction.KeyConflict);
        }

        var released = await ReleaseIfPolicyAsync(catalog, seed, record.RecordId, cancellationToken).ConfigureAwait(false);
        return new ReferenceSeedEntry(record.RecordId, ReferenceSeedAction.Refreshed, released);
    }

    private static bool IsUntouchedShippedRecord<TDefinition>(IReferenceRecord<TDefinition> existing, ReferenceSeedRecord<TDefinition> shipped)
        where TDefinition : class =>
        existing.ValidationState == ReferenceValidationState.Draft
        && existing.RevisionNumber == 1
        && existing.Provenance.VerificationStatus == ReferenceVerificationStatus.NotVerified
        && string.Equals(existing.Provenance.SourceOrganisation, shipped.Provenance.SourceOrganisation, StringComparison.Ordinal);

    private async Task<bool> ReleaseIfPolicyAsync<TDefinition>(
        IReferenceDataCatalog<TDefinition> catalog,
        IReferenceSeed<TDefinition> seed,
        string recordId,
        CancellationToken cancellationToken)
        where TDefinition : class
    {
        if (!ReleasesAtSeed || !seed.ReleaseAtSeed)
            return false;

        await _releasePolicy!.ReleaseAsync(catalog, recordId, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
