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

    public async Task<IReadOnlyList<CalcInstance>> ListInstancesAsync(Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var all = await _store.ReadAllAsync(InstanceCollection, cancellationToken).ConfigureAwait(false);
        var prefix = projectId is { } p ? p.ToString("N") + "/" : null;
        var list = new List<CalcInstance>();

        foreach (var (key, json) in all)
        {
            if (prefix is not null && !key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            if (Deserialise<CalcInstance>(json) is { } instance)
                list.Add(instance);
        }

        return list.OrderByDescending(i => i.UpdatedAt).ToList();
    }

    /// <summary>Writes a draft or checked instance in its own transaction. Refuses to overwrite an issued one.</summary>
    public async Task SaveInstanceAsync(CalcInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var existing = await FindInstanceAsync(instance.Id, cancellationToken).ConfigureAwait(false);

        if (existing is { IsImmutable: true } && !(existing.Status == CalcInstanceStatus.Issued && instance.Status == CalcInstanceStatus.Superseded && SameIssuedContent(existing, instance)))
            throw new CalcInstanceImmutableException(instance.Id, existing.Status);

        await _store.ExecuteInTransactionAsync((tx, token) => WriteInstanceAsync(tx, instance, token), cancellationToken).ConfigureAwait(false);
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

    public static async Task WritePackAsync(IPersistenceTransaction transaction, CalcPack pack, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(pack);

        await transaction.WriteAsync(PackCollection, Key(pack.ProjectId, pack.Id), Serialise(pack), cancellationToken).ConfigureAwait(false);
        await transaction.IndexTextAsync(pack.Id, PackKind, pack.ProjectId, Sheets.CalcSheetTextRenderer.Clean(pack.Title), pack.Revision, null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> FindKeyAsync(string collection, Guid id, CancellationToken cancellationToken)
    {
        var keys = await _store.ListKeysAsync(collection, string.Empty, cancellationToken).ConfigureAwait(false);
        var suffix = "/" + id.ToString("N");
        return keys.FirstOrDefault(k => k.EndsWith(suffix, StringComparison.Ordinal));
    }

    private static bool SameIssuedContent(CalcInstance a, CalcInstance b) =>
        a.Inputs.Count == b.Inputs.Count && a.Outputs.Count == b.Outputs.Count && a.Issue == b.Issue && a.IssuedAt == b.IssuedAt && a.EvidenceId == b.EvidenceId;
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
