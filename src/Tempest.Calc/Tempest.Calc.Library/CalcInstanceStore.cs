using System.Text.Json;
using System.Text.Json.Serialization;
using Tempest.Core.Persistence;

namespace Tempest.Calc.Library;

/// <summary>
/// Persistence for instances and packs: one JSON document per record in
/// its own collection, keyed <c>{projectId:N}/{id:N}</c> (or
/// <c>_/{id:N}</c> without a project) so a project's instances list by
/// prefix. Every write goes through the store's own transaction, and the
/// issue write is handed a caller's transaction so it commits with the
/// Evidence. Issued records are immutable: a write that would change one
/// is refused here, below every service.
/// </summary>
public sealed class CalcInstanceStore
{
    public const string InstanceCollection = "Calc.Instances";
    public const string PackCollection = "Calc.Packs";
    public const string PackTemplateCollection = "Calc.PackTemplates";
    public const string RecordCollection = "Calc.DataRecords";

    /// <summary>CAD import mapping profiles (Addendum B, W1), one document per profile version, never overwritten.</summary>
    public const string ImportProfileCollection = "Calc.ImportProfiles";

    /// <summary>Scan import profiles (Addendum B, W2), one document per profile version, never overwritten.</summary>
    public const string ScanProfileCollection = "Calc.ScanProfiles";
    public const string InstanceKind = "CalcInstance";
    public const string PackKind = "CalcPack";
    public const int MaxDocumentBytes = 4_000_000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = false,
    };

    private readonly IQueryablePersistenceStore _store;

    public CalcInstanceStore(IQueryablePersistenceStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    public static string Key(Guid? projectId, Guid id) => $"{(projectId is { } p ? p.ToString("N") : "_")}/{id:N}";

    public static string Serialise<T>(T record) => JsonSerializer.Serialize(record, Json);

    public static T? Deserialise<T>(string? json) => json is null ? default : JsonSerializer.Deserialize<T>(json, Json);

    public async Task<CalcInstance?> FindInstanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = await FindKeyAsync(InstanceCollection, id, cancellationToken).ConfigureAwait(false);

        if (key is null)
            return null;

        var read = await _store.ReadManyAsync(InstanceCollection, [key], cancellationToken).ConfigureAwait(false);
        return Deserialise<CalcInstance>(read.GetValueOrDefault(key));
    }

    /// <summary>
    /// The instances of one project, or — with no project — those saved without one (never every project's: Red R-A-12). Only that
    /// project's keys are listed and read, not the whole collection (Red R-A-07).
    /// </summary>
    public async Task<IReadOnlyList<CalcInstance>> ListInstancesAsync(Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var prefix = (projectId is { } p ? p.ToString("N") : "_") + "/";
        var keys = await _store.ListKeysAsync(InstanceCollection, prefix, cancellationToken).ConfigureAwait(false);

        if (keys.Count == 0)
            return [];

        var read = await _store.ReadManyAsync(InstanceCollection, keys, cancellationToken).ConfigureAwait(false);
        var list = new List<CalcInstance>();

        foreach (var key in keys)
        {
            if (Deserialise<CalcInstance>(read.GetValueOrDefault(key)) is { } instance)
                list.Add(instance);
        }

        return list.OrderByDescending(i => i.UpdatedAt).ToList();
    }

    /// <summary>
    /// The calculation or pack whose Evidence is <paramref name="evidenceId"/>, in any project, or <see langword="null"/> when no worksheet owns it. This is the
    /// route from a managed Evidence record back to the worksheet that governs it (`ADR-0173`); it reads every saved calculation, so it is for a user's
    /// explicit "Open worksheet", not a hot path.
    /// </summary>
    public async Task<(CalcInstance? Instance, CalcPack? Pack)> FindByEvidenceAsync(Guid evidenceId, CancellationToken cancellationToken = default)
    {
        var instances = await _store.ReadAllAsync(InstanceCollection, cancellationToken).ConfigureAwait(false);

        foreach (var (_, value) in instances)
        {
            if (Deserialise<CalcInstance>(value) is { } instance && instance.EvidenceId == evidenceId)
                return (instance, null);
        }

        var packs = await _store.ReadAllAsync(PackCollection, cancellationToken).ConfigureAwait(false);

        foreach (var (_, value) in packs)
        {
            if (Deserialise<CalcPack>(value) is { } pack && pack.EvidenceId == evidenceId)
                return (null, pack);
        }

        return (null, null);
    }

    /// <summary>The Evidence of every saved calculation and pack, in every project (for the one-off sweep that marks it as managed by its worksheet).</summary>
    public async Task<IReadOnlyList<Guid>> ListEvidenceIdsAsync(CancellationToken cancellationToken = default)
    {
        var ids = new HashSet<Guid>();

        foreach (var (_, value) in await _store.ReadAllAsync(InstanceCollection, cancellationToken).ConfigureAwait(false))
        {
            if (Deserialise<CalcInstance>(value) is { EvidenceId: { } id })
                ids.Add(id);
        }

        foreach (var (_, value) in await _store.ReadAllAsync(PackCollection, cancellationToken).ConfigureAwait(false))
        {
            if (Deserialise<CalcPack>(value) is { EvidenceId: { } id })
                ids.Add(id);
        }

        return [.. ids];
    }

    /// <summary>
    /// Writes a draft or checked instance in its own transaction. Refuses to
    /// overwrite an issued one. With <paramref name="expectedUpdatedAt"/>,
    /// the stored copy is re-read under the transaction and the write is
    /// refused (<see cref="CalcConcurrencyException"/>) when someone else
    /// changed it since the caller read it, or deleted it (R-15).
    /// </summary>
    public Task SaveInstanceAsync(CalcInstance instance, CancellationToken cancellationToken = default) => SaveInstanceAsync(instance, expectedUpdatedAt: null, cancellationToken);

    public async Task SaveInstanceAsync(CalcInstance instance, DateTimeOffset? expectedUpdatedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        await _store.ExecuteInTransactionAsync((tx, token) => WriteGuardedAsync(tx, instance, expectedUpdatedAt, token), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes several instances in one transaction with the same guards (Revise: the new draft and the superseded old one).</summary>
    public async Task SaveInstancesAsync(IReadOnlyList<(CalcInstance Instance, DateTimeOffset? ExpectedUpdatedAt)> instances, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instances);
        await _store.ExecuteInTransactionAsync(async (tx, token) =>
        {
            foreach (var (instance, expected) in instances)
                await WriteGuardedAsync(tx, instance, expected, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Re-reads the stored instance under the transaction, enforces immutability and the optimistic version, then writes.</summary>
    public static async Task WriteGuardedAsync(IPersistenceTransaction transaction, CalcInstance instance, DateTimeOffset? expectedUpdatedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(instance);
        var existing = Deserialise<CalcInstance>(await transaction.ReadAsync(InstanceCollection, Key(instance.ProjectId, instance.Id), cancellationToken).ConfigureAwait(false));

        if (existing is { IsImmutable: true } && !(existing.Status == CalcInstanceStatus.Issued && instance.Status == CalcInstanceStatus.Superseded && SameIssuedContent(existing, instance)))
            throw new CalcInstanceImmutableException(instance.Id, existing.Status);

        // A write that expects the stored copy to be as the caller read it cannot create one: a calculation deleted since (the demonstration's removal) is not brought back by a stale update.
        if (expectedUpdatedAt is not null && existing is null)
            throw new CalcConcurrencyException($"Calculation '{instance.Name}' no longer exists (it was removed since you opened it); reload and try again.");

        if (expectedUpdatedAt is { } expected && existing is not null && existing.UpdatedAt != expected)
            throw new CalcConcurrencyException($"Calculation '{instance.Name}' was changed by someone else at {existing.UpdatedAt:O}; reload it and try again.");

        await WriteInstanceAsync(transaction, instance, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes an issued instance inside the Issue transaction after re-reading
    /// the stored copy under that same transaction: if another writer issued
    /// (or superseded) it since the caller looked, the whole transaction —
    /// Evidence status, attachment, issue record — rolls back
    /// (<see cref="CalcConcurrentIssueException"/>). Two Issues raced on one
    /// instance therefore leave exactly one issued revision.
    /// </summary>
    public static async Task WriteIssuedInstanceAsync(IPersistenceTransaction transaction, CalcInstance instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(instance);

        var stored = Deserialise<CalcInstance>(await transaction.ReadAsync(InstanceCollection, Key(instance.ProjectId, instance.Id), cancellationToken).ConfigureAwait(false));

        if (stored is null)
            throw new CalcConcurrentIssueException($"Calculation '{instance.Name}' was deleted while it was being issued.");

        if (stored.Status is CalcInstanceStatus.Issued or CalcInstanceStatus.Superseded)
            throw new CalcConcurrentIssueException($"Calculation '{instance.Name}' is already {stored.Status}: another issue completed first.");

        await WriteInstanceAsync(transaction, instance, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes an instance inside a caller's transaction (the one-transaction Issue).</summary>
    public static async Task WriteInstanceAsync(IPersistenceTransaction transaction, CalcInstance instance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(instance);

        var json = Serialise(instance);

        if (json.Length > MaxDocumentBytes)
            throw new InvalidOperationException($"Calculation instance '{instance.Id}' is larger than {MaxDocumentBytes / 1_000_000} MB and was not written.");

        await transaction.WriteAsync(InstanceCollection, Key(instance.ProjectId, instance.Id), json, cancellationToken).ConfigureAwait(false);
        await transaction.IndexTextAsync(
            instance.Id, InstanceKind, instance.ProjectId,
            Sheets.CalcSheetTextRenderer.Clean(instance.Name), instance.TemplateReference,
            string.Join(" ", instance.Outputs.Select(o => $"{o.Symbol} {o.Text}")),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteDraftInstanceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await FindInstanceAsync(id, cancellationToken).ConfigureAwait(false);

        if (existing is null)
            return;

        if (existing.Status != CalcInstanceStatus.Draft)
            throw new CalcInstanceImmutableException(id, existing.Status);

        await _store.ExecuteInTransactionAsync(async (tx, token) =>
        {
            await tx.DeleteAsync(InstanceCollection, Key(existing.ProjectId, id), token).ConfigureAwait(false);
            await tx.RemoveFromIndexAsync(id, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The one place a calculation, a pack or a scan profile is ever deleted (Addendum C, C-6): in <b>one</b> transaction, every instance and pack row of
    /// <paramref name="projectId"/> and the exact scan-profile versions named (the ones the install wrote), with their search-index rows. It deletes nothing else:
    /// rows are found by the project's own key prefix, a profile id must start with <see cref="CalcDemoProject.ScanProfilePrefix"/>, and a later version of a
    /// profile that a person saved after the install is never touched (a version is immutable and a person's own is theirs). The caller
    /// (<see cref="CalcDemoProjectService"/>) has already proved the project is the demonstration; below that, an issued or superseded row is never
    /// deleted — one found under the project stops the transaction with <see cref="CalcInstanceImmutableException"/> and nothing is removed.
    /// </summary>
    /// <returns>How many calculation rows, pack rows and scan-profile rows were deleted.</returns>
    public async Task<(int Instances, int Packs, int ScanProfiles)> DeleteDemonstrationRowsAsync(Guid projectId, IReadOnlyCollection<(string Id, int Version)> scanProfiles, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scanProfiles);

        if (projectId == Guid.Empty)
            throw new ArgumentException("A project id is needed to delete a demonstration's rows.", nameof(projectId));

        if (scanProfiles.Any(p => !p.Id.StartsWith(CalcDemoProject.ScanProfilePrefix, StringComparison.Ordinal)))
            throw new ArgumentException($"Only scan profiles whose id starts with '{CalcDemoProject.ScanProfilePrefix}' belong to the demonstration.", nameof(scanProfiles));

        var counts = (Instances: 0, Packs: 0, ScanProfiles: 0);

        await _store.ExecuteInTransactionAsync(async (tx, token) =>
        {
            // A transaction may be retried; count from nothing each time.
            counts = (0, 0, 0);
            var prefix = projectId.ToString("N") + "/";

            foreach (var key in await tx.ListKeysAsync(InstanceCollection, prefix, token).ConfigureAwait(false))
            {
                var instance = Deserialise<CalcInstance>(await tx.ReadAsync(InstanceCollection, key, token).ConfigureAwait(false));

                if (instance is { IsImmutable: true })
                    throw new CalcInstanceImmutableException(instance.Id, instance.Status);

                await tx.DeleteAsync(InstanceCollection, key, token).ConfigureAwait(false);

                if (Guid.TryParseExact(key[prefix.Length..], "N", out var id))
                    await tx.RemoveFromIndexAsync(id, token).ConfigureAwait(false);

                counts.Instances++;
            }

            foreach (var key in await tx.ListKeysAsync(PackCollection, prefix, token).ConfigureAwait(false))
            {
                var pack = Deserialise<CalcPack>(await tx.ReadAsync(PackCollection, key, token).ConfigureAwait(false));

                if (pack is { Status: CalcInstanceStatus.Issued or CalcInstanceStatus.Superseded })
                    throw new CalcInstanceImmutableException(pack.Id, pack.Status);

                await tx.DeleteAsync(PackCollection, key, token).ConfigureAwait(false);

                if (Guid.TryParseExact(key[prefix.Length..], "N", out var id))
                    await tx.RemoveFromIndexAsync(id, token).ConfigureAwait(false);

                counts.Packs++;
            }

            foreach (var (profileId, version) in scanProfiles)
            {
                var key = ImportProfileKey(profileId, version);

                if (await tx.ReadAsync(ScanProfileCollection, key, token).ConfigureAwait(false) is null)
                    continue;

                await tx.DeleteAsync(ScanProfileCollection, key, token).ConfigureAwait(false);
                counts.ScanProfiles++;
            }
        }, cancellationToken).ConfigureAwait(false);

        return counts;
    }

    public async Task<CalcPack?> FindPackAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = await FindKeyAsync(PackCollection, id, cancellationToken).ConfigureAwait(false);

        if (key is null)
            return null;

        var read = await _store.ReadManyAsync(PackCollection, [key], cancellationToken).ConfigureAwait(false);
        return Deserialise<CalcPack>(read.GetValueOrDefault(key));
    }

    public async Task<IReadOnlyList<CalcPack>> ListPacksAsync(Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var all = await _store.ReadAllAsync(PackCollection, cancellationToken).ConfigureAwait(false);
        var prefix = projectId is { } p ? p.ToString("N") + "/" : null;
        return all.Where(kv => prefix is null || kv.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(kv => Deserialise<CalcPack>(kv.Value)).OfType<CalcPack>().OrderByDescending(x => x.UpdatedAt).ToList();
    }

    public async Task SavePackAsync(CalcPack pack, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var existing = await FindPackAsync(pack.Id, cancellationToken).ConfigureAwait(false);

        if (existing is { Status: CalcInstanceStatus.Issued or CalcInstanceStatus.Superseded })
            throw new CalcInstanceImmutableException(pack.Id, existing.Status);

        await _store.ExecuteInTransactionAsync((tx, token) => WritePackAsync(tx, pack, token), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Saves a pack that was read at <paramref name="expectedUpdatedAt"/>: refused, not overwritten, if it was changed since.</summary>
    public async Task SavePackAsync(CalcPack pack, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        await _store.ExecuteInTransactionAsync((tx, token) => WriteGuardedPackAsync(tx, pack, expectedUpdatedAt, token), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Re-reads the stored pack under the transaction, enforces that it is not issued and has not changed, then writes (the pack check commits with the Evidence check).</summary>
    public static async Task WriteGuardedPackAsync(IPersistenceTransaction transaction, CalcPack pack, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(pack);
        var existing = Deserialise<CalcPack>(await transaction.ReadAsync(PackCollection, Key(pack.ProjectId, pack.Id), cancellationToken).ConfigureAwait(false));

        if (existing is { Status: CalcInstanceStatus.Issued or CalcInstanceStatus.Superseded })
            throw new CalcInstanceImmutableException(pack.Id, existing.Status);

        if (existing is not null && existing.UpdatedAt != expectedUpdatedAt)
            throw new CalcConcurrencyException($"Pack '{pack.Title}' was changed by someone else at {existing.UpdatedAt:O}; reload it and try again.");

        await WritePackAsync(transaction, pack, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Under a transaction: the stored instance is still as it was when the caller read it (it is not deleted and its update time is unchanged).</summary>
    public static async Task AssertUnchangedAsync(IPersistenceTransaction transaction, Guid? projectId, Guid id, string name, DateTimeOffset expectedUpdatedAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var stored = Deserialise<CalcInstance>(await transaction.ReadAsync(InstanceCollection, Key(projectId, id), cancellationToken).ConfigureAwait(false));

        if (stored is null || stored.UpdatedAt != expectedUpdatedAt)
            throw new CalcConcurrencyException($"Calculation '{name}' changed while the pack was being issued; check the pack again.");
    }

    public static async Task WritePackAsync(IPersistenceTransaction transaction, CalcPack pack, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(pack);

        await transaction.WriteAsync(PackCollection, Key(pack.ProjectId, pack.Id), Serialise(pack), cancellationToken).ConfigureAwait(false);
        await transaction.IndexTextAsync(pack.Id, PackKind, pack.ProjectId, Sheets.CalcSheetTextRenderer.Clean(pack.Title), pack.Revision, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Saves a pack template the user built from a linked chain (Addendum A, L1).</summary>
    public async Task SavePackTemplateAsync(CalcPackTemplate template, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        await _store.ExecuteInTransactionAsync((tx, token) => tx.WriteAsync(PackTemplateCollection, template.Id, Serialise(template), token), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CalcPackTemplate>> ListPackTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var all = await _store.ReadAllAsync(PackTemplateCollection, cancellationToken).ConfigureAwait(false);
        return all.Select(kv => Deserialise<CalcPackTemplate>(kv.Value)).OfType<CalcPackTemplate>().OrderBy(t => t.Title, StringComparer.Ordinal).ToList();
    }

    public static string RecordKey(Guid id, int revision) => $"{id:N}@{revision:D4}";

    /// <summary>Saves a new revision of a reference record (§A3). A revision is never overwritten.</summary>
    public async Task SaveRecordAsync(CalcDataRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var key = RecordKey(record.Id, record.Revision);
        var json = Serialise(record);

        if (json.Length > MaxDocumentBytes)
            throw new InvalidOperationException($"Reference record '{record.Id:N}' is larger than {MaxDocumentBytes / 1_000_000} MB and was not written.");

        await _store.ExecuteInTransactionAsync(async (tx, token) =>
        {
            if (await tx.ReadAsync(RecordCollection, key, token).ConfigureAwait(false) is not null)
                throw new CalcInstanceImmutableException($"Reference record {record.Id:N} revision {record.Revision} exists already; a change is a new revision.");

            await tx.WriteAsync(RecordCollection, key, json, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves several reference records in ONE transaction (Addendum C, C-3: a record import is all or nothing). Every record is a new revision
    /// that must not exist yet; if any one cannot be written, or the commit fails, none of them is. The set may not repeat a record revision.
    /// </summary>
    public async Task SaveRecordsAsync(IReadOnlyList<CalcDataRecord> records, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
            return;

        var batch = new List<(string Key, string Json, CalcDataRecord Record)>(records.Count);

        foreach (var record in records)
        {
            ArgumentNullException.ThrowIfNull(record);
            var json = Serialise(record);

            if (json.Length > MaxDocumentBytes)
                throw new InvalidOperationException($"Reference record '{record.Id:N}' is larger than {MaxDocumentBytes / 1_000_000} MB and was not written.");

            batch.Add((RecordKey(record.Id, record.Revision), json, record));
        }

        if (batch.Select(b => b.Key).Distinct(StringComparer.Ordinal).Count() != batch.Count)
            throw new CalcInstanceImmutableException("The records to save repeat a record revision; a change is a new revision.");

        await _store.ExecuteInTransactionAsync(async (tx, token) =>
        {
            foreach (var (key, json, record) in batch)
            {
                if (await tx.ReadAsync(RecordCollection, key, token).ConfigureAwait(false) is not null)
                    throw new CalcInstanceImmutableException($"Reference record {record.Id:N} revision {record.Revision} exists already; a change is a new revision.");

                await tx.WriteAsync(RecordCollection, key, json, token).ConfigureAwait(false);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A record at a revision, or its latest revision.</summary>
    public async Task<CalcDataRecord?> FindRecordAsync(Guid id, int? revision = null, CancellationToken cancellationToken = default)
    {
        var keys = (await _store.ListKeysAsync(RecordCollection, $"{id:N}@", cancellationToken).ConfigureAwait(false)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var key = revision is { } r ? RecordKey(id, r) : keys.LastOrDefault();

        if (key is null || !keys.Contains(key))
            return null;

        var read = await _store.ReadManyAsync(RecordCollection, [key], cancellationToken).ConfigureAwait(false);
        return Deserialise<CalcDataRecord>(read.GetValueOrDefault(key));
    }

    /// <summary>
    /// Whether any revision of the record is licensed (§A5). The lock belongs to the record, not to a revision: a record flagged licensed in revision 2
    /// to correct a slip must not leave revision 1's values exportable (Addendum B review, SEC-B-02).
    /// </summary>
    public async Task<bool> IsLicensedInAnyRevisionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var keys = await _store.ListKeysAsync(RecordCollection, $"{id:N}@", cancellationToken).ConfigureAwait(false);

        if (keys.Count == 0)
            return false;

        var read = await _store.ReadManyAsync(RecordCollection, keys, cancellationToken).ConfigureAwait(false);
        return read.Values.Any(json => Deserialise<CalcDataRecord>(json)?.IsLicensed() == true);
    }

    /// <summary>The latest revision of every record (of one kind, if given).</summary>
    public async Task<IReadOnlyList<CalcDataRecord>> ListRecordsAsync(string? kind = null, CancellationToken cancellationToken = default)
    {
        var all = await _store.ReadAllAsync(RecordCollection, cancellationToken).ConfigureAwait(false);
        return all.Select(kv => Deserialise<CalcDataRecord>(kv.Value)).OfType<CalcDataRecord>()
            .Where(r => kind is null || string.Equals(r.Kind, kind, StringComparison.Ordinal))
            .GroupBy(r => r.Id).Select(g => g.OrderBy(r => r.Revision).Last())
            .OrderBy(r => r.Kind, StringComparer.Ordinal).ThenBy(r => r.Title, StringComparer.Ordinal).ToList();
    }

    public static string ImportProfileKey(string id, int version) => $"{id}@{version:D6}";

    /// <summary>Saves a CAD import mapping profile version (W1). A version is never overwritten: a change is a new version.</summary>
    public async Task SaveImportProfileAsync(Import.Mapping.CadMappingProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var key = ImportProfileKey(profile.Id, profile.Version);
        var json = Serialise(profile);

        if (json.Length > MaxDocumentBytes)
            throw new InvalidOperationException($"Import profile '{profile.Label}' is larger than {MaxDocumentBytes / 1_000_000} MB and was not written.");

        await _store.ExecuteInTransactionAsync(async (tx, token) =>
        {
            if (await tx.ReadAsync(ImportProfileCollection, key, token).ConfigureAwait(false) is not null)
                throw new CalcInstanceImmutableException($"Import profile {profile.Label} exists already; a change is a new version.");

            await tx.WriteAsync(ImportProfileCollection, key, json, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A profile at a version, or its latest version.</summary>
    public async Task<Import.Mapping.CadMappingProfile?> FindImportProfileAsync(string id, int? version = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var keys = (await _store.ListKeysAsync(ImportProfileCollection, id + "@", cancellationToken).ConfigureAwait(false)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var key = version is { } v ? ImportProfileKey(id, v) : keys.LastOrDefault();

        if (key is null || !keys.Contains(key))
            return null;

        var read = await _store.ReadManyAsync(ImportProfileCollection, [key], cancellationToken).ConfigureAwait(false);
        return Deserialise<Import.Mapping.CadMappingProfile>(read.GetValueOrDefault(key));
    }

    /// <summary>The latest version of every profile.</summary>
    public async Task<IReadOnlyList<Import.Mapping.CadMappingProfile>> ListImportProfilesAsync(CancellationToken cancellationToken = default)
    {
        var all = await _store.ReadAllAsync(ImportProfileCollection, cancellationToken).ConfigureAwait(false);
        return all.Select(kv => Deserialise<Import.Mapping.CadMappingProfile>(kv.Value)).OfType<Import.Mapping.CadMappingProfile>()
            .GroupBy(p => p.Id, StringComparer.Ordinal).Select(g => g.OrderBy(p => p.Version).Last())
            .OrderBy(p => p.Title, StringComparer.Ordinal).ToList();
    }

    /// <summary>Saves a scan import profile version (W2). A version is never overwritten: a change is a new version.</summary>
    public async Task SaveScanProfileAsync(Import.Scan.ScanProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var key = ImportProfileKey(profile.Id, profile.Version);
        var json = Serialise(profile);

        if (json.Length > MaxDocumentBytes)
            throw new InvalidOperationException($"Scan profile '{profile.Label}' is larger than {MaxDocumentBytes / 1_000_000} MB and was not written.");

        await _store.ExecuteInTransactionAsync(async (tx, token) =>
        {
            if (await tx.ReadAsync(ScanProfileCollection, key, token).ConfigureAwait(false) is not null)
                throw new CalcInstanceImmutableException($"Scan profile {profile.Label} exists already; a change is a new version.");

            await tx.WriteAsync(ScanProfileCollection, key, json, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A scan profile at a version, or its latest version.</summary>
    public async Task<Import.Scan.ScanProfile?> FindScanProfileAsync(string id, int? version = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var keys = (await _store.ListKeysAsync(ScanProfileCollection, id + "@", cancellationToken).ConfigureAwait(false)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var key = version is { } v ? ImportProfileKey(id, v) : keys.LastOrDefault();

        if (key is null || !keys.Contains(key))
            return null;

        var read = await _store.ReadManyAsync(ScanProfileCollection, [key], cancellationToken).ConfigureAwait(false);
        return Deserialise<Import.Scan.ScanProfile>(read.GetValueOrDefault(key));
    }

    /// <summary>The latest version of every scan profile.</summary>
    public async Task<IReadOnlyList<Import.Scan.ScanProfile>> ListScanProfilesAsync(CancellationToken cancellationToken = default)
    {
        var all = await _store.ReadAllAsync(ScanProfileCollection, cancellationToken).ConfigureAwait(false);
        return all.Select(kv => Deserialise<Import.Scan.ScanProfile>(kv.Value)).OfType<Import.Scan.ScanProfile>()
            .GroupBy(p => p.Id, StringComparer.Ordinal).Select(g => g.OrderBy(p => p.Version).Last())
            .OrderBy(p => p.Title, StringComparer.Ordinal).ToList();
    }

    private async Task<string?> FindKeyAsync(string collection, Guid id, CancellationToken cancellationToken)
    {
        var keys = await _store.ListKeysAsync(collection, string.Empty, cancellationToken).ConfigureAwait(false);
        var suffix = "/" + id.ToString("N");
        return keys.FirstOrDefault(k => k.EndsWith(suffix, StringComparison.Ordinal));
    }

    /// <summary>The superseded mark may change the status and the update time of an issued record and nothing else: the whole serialised record is compared (R-14).</summary>
    private static bool SameIssuedContent(CalcInstance issued, CalcInstance superseded) =>
        string.Equals(Serialise(issued with { Status = superseded.Status, UpdatedAt = superseded.UpdatedAt }), Serialise(superseded), StringComparison.Ordinal);
}

/// <summary>Thrown when a write would change an issued or superseded instance or pack.</summary>
public sealed class CalcInstanceImmutableException : InvalidOperationException
{
    public CalcInstanceImmutableException(Guid id, CalcInstanceStatus status)
        : base($"Calculation '{id}' is {status} and cannot be changed; revise it to start a new revision.")
    {
        InstanceId = id;
        Status = status;
    }

    public CalcInstanceImmutableException() { }

    public CalcInstanceImmutableException(string message) : base(message) { }

    public CalcInstanceImmutableException(string message, Exception inner) : base(message, inner) { }

    public Guid InstanceId { get; }

    public CalcInstanceStatus Status { get; }
}

/// <summary>Raised inside the Issue transaction when the stored instance is no longer Checked: a concurrent issue won the race. The transaction rolls back and the caller reports a refusal.</summary>
public sealed class CalcConcurrentIssueException : InvalidOperationException
{
    public CalcConcurrentIssueException(string message) : base(message) { }

    public CalcConcurrentIssueException() { }

    public CalcConcurrentIssueException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Raised inside a write transaction when the stored instance changed since the caller read it (optimistic concurrency); the caller reports a refusal.</summary>
public sealed class CalcConcurrencyException : InvalidOperationException
{
    public CalcConcurrencyException(string message) : base(message) { }

    public CalcConcurrencyException() { }

    public CalcConcurrencyException(string message, Exception innerException) : base(message, innerException) { }
}
