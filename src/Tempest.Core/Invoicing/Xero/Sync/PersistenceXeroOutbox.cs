using System.Text.Json;
using System.Text.Json.Nodes;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>
/// The drain's side of the outbox (`v0.24.0` B2, for X6): claim the next
/// due entry, record what happened to it, and the start-up and
/// re-authorisation transitions. <see cref="IXeroOutbox"/> is the
/// enqueue-and-read side every planner and badge uses; only the sync engine
/// uses this one.
/// </summary>
public interface IXeroOutboxDrain
{
    /// <summary>
    /// Claims the oldest entry that may be sent now: the head of its
    /// document's queue (no earlier entry for the document is still open),
    /// <see cref="XeroOutboxState.Pending"/> or
    /// <see cref="XeroOutboxState.Unknown"/>, and due
    /// (<see cref="XeroOutboxEntry.NotBeforeUtc"/> unset or passed). The
    /// entry is persisted as <see cref="XeroOutboxState.InFlight"/> with
    /// <see cref="XeroOutboxEntry.Attempts"/> incremented and
    /// <see cref="XeroOutboxEntry.LastAttemptAtUtc"/> set <em>before</em> it
    /// is returned, so a crash mid-request is found at start-up
    /// (<see cref="RecoverInFlightAsync"/>). An entry with
    /// <see cref="XeroOutboxEntry.Attempts"/> above 1 has been sent before:
    /// its handler looks the record up before resending (design §6.4).
    /// </summary>
    /// <returns>The claimed entry, or <see langword="null"/> when nothing is due.</returns>
    Task<XeroOutboxEntry?> ClaimNextDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the outcome of the attempt on a claimed
    /// (<see cref="XeroOutboxState.InFlight"/>) entry: <paramref name="state"/>
    /// is <see cref="XeroOutboxState.Succeeded"/>,
    /// <see cref="XeroOutboxState.Failed"/>, <see cref="XeroOutboxState.Unknown"/>,
    /// <see cref="XeroOutboxState.WaitingForAuthorisation"/>, or
    /// <see cref="XeroOutboxState.Pending"/> (retry later, not before
    /// <paramref name="notBeforeUtc"/>). The idempotency key never changes.
    /// </summary>
    /// <returns>The entry as stored, or <see langword="null"/> when no entry has <paramref name="entryId"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="state"/> is not one of the five above.</exception>
    /// <exception cref="InvalidOperationException">The entry is not <see cref="XeroOutboxState.InFlight"/>, or was written by a newer TempestOS.</exception>
    Task<XeroOutboxEntry?> RecordOutcomeAsync(
        Guid entryId, XeroOutboxState state, string? lastError = null, DateTimeOffset? notBeforeUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>At start-up: every <see cref="XeroOutboxState.InFlight"/> entry becomes <see cref="XeroOutboxState.Unknown"/> — its response may have been lost, so it is reconciled by lookup before any resend.</summary>
    /// <returns>How many entries were moved.</returns>
    Task<int> RecoverInFlightAsync(CancellationToken cancellationToken = default);

    /// <summary>After a successful re-authorisation: every <see cref="XeroOutboxState.WaitingForAuthorisation"/> entry becomes <see cref="XeroOutboxState.Pending"/>, due at once.</summary>
    /// <returns>How many entries were moved.</returns>
    Task<int> ResumeAfterAuthorisationAsync(CancellationToken cancellationToken = default);

    /// <summary>The entry with <paramref name="entryId"/>, or <see langword="null"/>.</summary>
    Task<XeroOutboxEntry?> FindAsync(Guid entryId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The durable outbox (`v0.24.0` B2, `ADR-0162` decision 4; design §6.1):
/// one JSON document per <see cref="XeroOutboxEntry"/> in the
/// <see cref="IPersistenceStore"/> collection <see cref="Collection"/>,
/// keyed by the entry's id.
/// </summary>
/// <remarks>
/// <para>
/// <b>Order.</b> Each stored entry also carries a store-assigned
/// <c>Sequence</c> (an additive JSON property the
/// <see cref="XeroOutboxEntry"/> shape does not have, ignored by any other
/// reader), so "oldest first" is the order entries were queued even when
/// the clock gives two the same instant.
/// </para>
/// <para>
/// <b>Per-document FIFO.</b> An entry is claimable only when every earlier
/// entry for its document is <see cref="XeroOutboxState.Succeeded"/> or
/// <see cref="XeroOutboxState.Superseded"/>: a status change is never sent
/// ahead of the create it depends on.
/// </para>
/// <para>
/// <b>Dedupe and supersede.</b> Entries are grouped by document, operation
/// and argument (a quote's <c>SetQuoteStatus</c> to <c>SENT</c> and to
/// <c>ACCEPTED</c> are separate writes, both sent, in order). Enqueueing
/// the same content as the newest open-or-succeeded entry of its group
/// returns that entry (not queued twice). Otherwise the new entry
/// supersedes every entry of the group not yet sent —
/// <see cref="XeroOutboxState.Pending"/>,
/// <see cref="XeroOutboxState.Failed"/> and
/// <see cref="XeroOutboxState.WaitingForAuthorisation"/> — but never one
/// that may have reached Xero (<see cref="XeroOutboxState.InFlight"/>,
/// <see cref="XeroOutboxState.Unknown"/>), which must be reconciled first.
/// </para>
/// <para>
/// <b>Tenant.</b> An entry names a TempestOS record and an operation, never
/// a Xero id: it is sent to whichever organisation is connected when it is
/// drained, and its handler resolves the link in that tenant.
/// </para>
/// <para>
/// <b>Schema versioning.</b> Entries are written at
/// <see cref="XeroOutboxEntry.CurrentSchemaVersion"/>. Unknown properties
/// are ignored. An entry written by a newer TempestOS is listed (for the
/// badge) but never claimed, retried, superseded or rewritten, and it holds
/// its document's queue. An entry that cannot be read at all is left in
/// place and out of every listing.
/// </para>
/// </remarks>
public sealed class PersistenceXeroOutbox : IXeroOutbox, IXeroOutboxDrain
{
    /// <summary>The <see cref="IPersistenceStore"/> collection entries live in.</summary>
    public const string Collection = "Xero.Outbox";

    /// <summary>The <see cref="XeroOutboxEntry.EnqueuedBy"/> of an entry queued with no signed-in principal.</summary>
    public const string SystemPrincipal = "system";

    private const string SequenceProperty = "Sequence";

    private static readonly XeroOutboxState[] OutcomeStates =
    [
        XeroOutboxState.Pending, XeroOutboxState.Succeeded, XeroOutboxState.Failed,
        XeroOutboxState.Unknown, XeroOutboxState.WaitingForAuthorisation,
    ];

    private readonly IPersistenceStore _store;
    private readonly ICurrentPrincipalAccessor? _principals;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate;

    /// <summary>Initialises a new instance of the <see cref="PersistenceXeroOutbox"/> class.</summary>
    /// <param name="store">The platform's persistence store.</param>
    /// <param name="principals">Who is signed in, for <see cref="XeroOutboxEntry.EnqueuedBy"/>; <see langword="null"/> records <see cref="SystemPrincipal"/>.</param>
    public PersistenceXeroOutbox(IPersistenceStore store, ICurrentPrincipalAccessor? principals = null)
        : this(store, principals, TimeProvider.System)
    {
    }

    /// <summary>Test seam (`ADR-0121`): a pinned clock.</summary>
    internal PersistenceXeroOutbox(IPersistenceStore store, ICurrentPrincipalAccessor? principals, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _store = store;
        _principals = principals;
        _time = timeProvider;
        _gate = XeroStoreSupport.GateFor(store, Collection);
    }

    /// <inheritdoc />
    public async Task<XeroOutboxEntry> EnqueueAsync(
        XeroOperation operation, XeroDocumentRef document, string contentHash, string? argument = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.TempestKey, nameof(document));
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        if (!Enum.IsDefined(operation))
            throw new ArgumentOutOfRangeException(nameof(operation), operation, "Not a Xero operation.");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var all = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
            var group = all
                .Where(s => !s.Newer && s.Entry.Document == document && s.Entry.Operation == operation
                            && string.Equals(s.Entry.Argument, argument, StringComparison.Ordinal))
                .ToList();

            if (group.LastOrDefault(s => s.Entry.State != XeroOutboxState.Superseded) is { } latest
                && string.Equals(latest.Entry.ContentHash, contentHash, StringComparison.Ordinal))
            {
                return latest.Entry;
            }

            var now = _time.GetUtcNow();
            var occurrence = group.Count(s => string.Equals(s.Entry.ContentHash, contentHash, StringComparison.Ordinal));
            var entry = new XeroOutboxEntry(
                SchemaVersion: XeroOutboxEntry.CurrentSchemaVersion,
                Id: Guid.CreateVersion7(now),
                Operation: operation,
                Document: document,
                Argument: argument,
                IdempotencyKey: XeroIdempotencyKey.Create(document, operation, contentHash, argument, occurrence),
                ContentHash: contentHash,
                State: XeroOutboxState.Pending,
                Attempts: 0,
                EnqueuedAtUtc: now,
                NotBeforeUtc: null,
                LastAttemptAtUtc: null,
                LastError: null,
                EnqueuedBy: _principals?.Current?.Identity.Id is { Length: > 0 } id ? id : SystemPrincipal);

            var sequence = all.Count == 0 ? 1 : all.Max(s => s.Sequence) + 1;

            // The new entry is written before the ones it supersedes: a crash
            // between the two leaves both pending (one redundant push), never
            // neither (a lost write).
            await WriteAsync(new StoredEntry(entry, sequence, Newer: false, Original: null), cancellationToken).ConfigureAwait(false);

            foreach (var replaced in group.Where(s => s.Entry.State is XeroOutboxState.Pending or XeroOutboxState.Failed or XeroOutboxState.WaitingForAuthorisation))
            {
                await WriteAsync(
                    replaced with { Entry = replaced.Entry with { State = XeroOutboxState.Superseded, NotBeforeUtc = null } },
                    cancellationToken).ConfigureAwait(false);
            }

            return entry;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<XeroOutboxEntry>> ListForDocumentAsync(XeroDocumentRef document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var all = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(s => s.Entry.Document == document).Select(s => s.Entry).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<XeroOutboxEntry>> ListAsync(IReadOnlyCollection<XeroOutboxState> states, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(states);

        var all = await LoadAllAsync(cancellationToken).ConfigureAwait(false);
        return all.Where(s => states.Count == 0 || states.Contains(s.Entry.State)).Select(s => s.Entry).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> RetryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await LoadAsync(entryId, cancellationToken).ConfigureAwait(false) is not { Newer: false } stored
                || stored.Entry.State != XeroOutboxState.Failed)
            {
                return false;
            }

            await WriteAsync(stored with { Entry = stored.Entry with { State = XeroOutboxState.Pending, NotBeforeUtc = null } }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<XeroOutboxEntry?> ClaimNextDueAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _time.GetUtcNow();
            var all = await LoadAllAsync(cancellationToken).ConfigureAwait(false);

            // The head of each document's queue: its oldest entry that is
            // neither done nor replaced. Anything behind a head waits.
            // An entry written by a newer TempestOS is never claimed, so it
            // holds its document's queue until that build drains it.
            var heads = all
                .Where(s => s.Entry.State is not (XeroOutboxState.Succeeded or XeroOutboxState.Superseded))
                .GroupBy(s => s.Entry.Document)
                .Select(g => g.First());

            var next = heads.FirstOrDefault(s =>
                !s.Newer
                && s.Entry.State is XeroOutboxState.Pending or XeroOutboxState.Unknown
                && (s.Entry.NotBeforeUtc is not { } notBefore || notBefore <= now));

            if (next is null)
                return null;

            var claimed = next.Entry with
            {
                State = XeroOutboxState.InFlight,
                Attempts = next.Entry.Attempts + 1,
                LastAttemptAtUtc = now,
            };

            await WriteAsync(next with { Entry = claimed }, cancellationToken).ConfigureAwait(false);
            return claimed;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<XeroOutboxEntry?> RecordOutcomeAsync(
        Guid entryId, XeroOutboxState state, string? lastError = null, DateTimeOffset? notBeforeUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (!OutcomeStates.Contains(state))
            throw new ArgumentException($"'{state}' is not the outcome of an attempt.", nameof(state));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await LoadAsync(entryId, cancellationToken).ConfigureAwait(false) is not { } stored)
                return null;

            if (stored.Newer)
                throw new InvalidOperationException($"Outbox entry {entryId} was written by a newer TempestOS; it is not changed.");

            if (stored.Entry.State != XeroOutboxState.InFlight)
                throw new InvalidOperationException($"Outbox entry {entryId} is {stored.Entry.State}, not InFlight; only a claimed entry has an outcome.");

            var updated = stored.Entry with
            {
                State = state,
                LastError = state == XeroOutboxState.Succeeded ? null : lastError,
                NotBeforeUtc = state is XeroOutboxState.Pending or XeroOutboxState.Unknown ? notBeforeUtc : null,
            };

            await WriteAsync(stored with { Entry = updated }, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public Task<int> RecoverInFlightAsync(CancellationToken cancellationToken = default) =>
        MoveAllAsync(XeroOutboxState.InFlight, XeroOutboxState.Unknown, cancellationToken);

    /// <inheritdoc />
    public Task<int> ResumeAfterAuthorisationAsync(CancellationToken cancellationToken = default) =>
        MoveAllAsync(XeroOutboxState.WaitingForAuthorisation, XeroOutboxState.Pending, cancellationToken);

    /// <inheritdoc />
    public async Task<XeroOutboxEntry?> FindAsync(Guid entryId, CancellationToken cancellationToken = default) =>
        (await LoadAsync(entryId, cancellationToken).ConfigureAwait(false))?.Entry;

    private async Task<int> MoveAllAsync(XeroOutboxState from, XeroOutboxState to, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var moved = 0;

            foreach (var stored in await LoadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                if (stored.Newer || stored.Entry.State != from)
                    continue;

                await WriteAsync(stored with { Entry = stored.Entry with { State = to, NotBeforeUtc = null } }, cancellationToken).ConfigureAwait(false);
                moved++;
            }

            return moved;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<StoredEntry>> LoadAllAsync(CancellationToken cancellationToken)
    {
        var keys = await _store.ListKeysAsync(Collection, cancellationToken).ConfigureAwait(false);
        var entries = new List<StoredEntry>(keys.Count);

        foreach (var key in keys)
        {
            if (!Guid.TryParseExact(key, "D", out var id))
                continue;

            if (await LoadAsync(id, cancellationToken).ConfigureAwait(false) is { } stored)
                entries.Add(stored);
        }

        entries.Sort(static (a, b) =>
        {
            var bySequence = a.Sequence.CompareTo(b.Sequence);
            if (bySequence != 0)
                return bySequence;

            var byTime = a.Entry.EnqueuedAtUtc.CompareTo(b.Entry.EnqueuedAtUtc);
            return byTime != 0 ? byTime : a.Entry.Id.CompareTo(b.Entry.Id);
        });

        return entries;
    }

    private async Task<StoredEntry?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        var json = await _store.ReadAsync(Collection, id.ToString("D"), cancellationToken).ConfigureAwait(false);
        return json is null ? null : Parse(json, id);
    }

    /// <summary>Reads one stored entry; <see langword="null"/> when it cannot be read or does not match its key.</summary>
    internal static StoredEntry? Parse(string json, Guid id)
    {
        if (XeroStoreSupport.ReadSchemaVersion(json) is not { } version || version < 1)
            return null;

        try
        {
            var node = JsonNode.Parse(json)?.AsObject();
            var entry = JsonSerializer.Deserialize<XeroOutboxEntry>(json, XeroStoreSupport.JsonOptions);

            if (node is null || entry is null || entry.Id != id || !IsComplete(entry))
                return null;

            var sequence = node.TryGetPropertyValue(SequenceProperty, out var value) && value is JsonValue v && v.TryGetValue<long>(out var s)
                ? s
                : long.MaxValue;

            return new StoredEntry(entry with { SchemaVersion = version }, sequence, version > XeroOutboxEntry.CurrentSchemaVersion, node);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static bool IsComplete(XeroOutboxEntry entry) =>
        entry.Document is { TempestKey: { Length: > 0 } }
        && !string.IsNullOrWhiteSpace(entry.IdempotencyKey)
        && !string.IsNullOrWhiteSpace(entry.ContentHash)
        && entry.EnqueuedBy is not null;

    /// <summary>Writes <paramref name="stored"/> at the current schema version, keeping any property this build does not know from what was read.</summary>
    private Task WriteAsync(StoredEntry stored, CancellationToken cancellationToken)
    {
        var node = XeroStoreSupport.ToJson(stored.Entry with { SchemaVersion = XeroOutboxEntry.CurrentSchemaVersion }, stored.Original);
        node[SequenceProperty] = stored.Sequence;

        return _store.WriteAsync(Collection, stored.Entry.Id.ToString("D"), node.ToJsonString(), cancellationToken);
    }

    /// <summary>One entry as stored: the entry, its queue position, whether a newer TempestOS wrote it, and the JSON it was read from (<see langword="null"/> for a new entry).</summary>
    internal sealed record StoredEntry(XeroOutboxEntry Entry, long Sequence, bool Newer, JsonObject? Original);
}
