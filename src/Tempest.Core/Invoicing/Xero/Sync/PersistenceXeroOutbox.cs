using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tempest.Core.Identity;
using Tempest.Core.Invoicing.Xero.Contacts;
using Tempest.Core.Persistence;
using Tempest.Core.Secrets;

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
    /// `v0.24.0` B2 follow-up (additive, for X6): as <see cref="ClaimNextDueAsync(CancellationToken)"/>,
    /// but only an entry in one of <paramref name="states"/> (each
    /// <see cref="XeroOutboxState.Pending"/> or <see cref="XeroOutboxState.Unknown"/>)
    /// is claimed — so the sync engine recovers every write whose answer may
    /// have been lost (<see cref="XeroOutboxState.Unknown"/>) before any other
    /// work, while Xero still holds its <c>Idempotency-Key</c>. Per-document
    /// order is unchanged: only the head of a document's queue is ever claimed.
    /// The default implementation ignores the preference and claims as
    /// <see cref="ClaimNextDueAsync(CancellationToken)"/> does.
    /// </summary>
    /// <param name="states">The states to claim from; empty means Pending or Unknown.</param>
    /// <param name="cancellationToken">Cancels the claim.</param>
    /// <returns>The claimed entry, or <see langword="null"/> when no entry in <paramref name="states"/> is due.</returns>
    Task<XeroOutboxEntry?> ClaimNextDueAsync(IReadOnlyCollection<XeroOutboxState> states, CancellationToken cancellationToken = default) =>
        ClaimNextDueAsync(cancellationToken);

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

    /// <summary>
    /// `v0.24.0` F1 (additive, M1): as
    /// <see cref="ClaimNextDueAsync(IReadOnlyCollection{XeroOutboxState}, CancellationToken)"/>,
    /// and the claimed entry records <paramref name="tenantId"/> — the
    /// organisation it is being sent to — as its
    /// <see cref="XeroOutboxEntry.TenantId"/>, so once it succeeds it names
    /// the organisation it succeeded in. The default implementation claims
    /// without recording it.
    /// </summary>
    /// <param name="states">The states to claim from; empty means Pending or Unknown.</param>
    /// <param name="tenantId">The connected organisation the entry is sent to.</param>
    /// <param name="cancellationToken">Cancels the claim.</param>
    /// <returns>The claimed entry, or <see langword="null"/> when no entry in <paramref name="states"/> is due.</returns>
    Task<XeroOutboxEntry?> ClaimNextDueForTenantAsync(IReadOnlyCollection<XeroOutboxState> states, string tenantId, CancellationToken cancellationToken = default) =>
        ClaimNextDueAsync(states, cancellationToken);

    /// <summary>
    /// `v0.24.0` F1 (additive, M1): TempestOS is now connected to
    /// <paramref name="tenantId"/>, a different organisation from before.
    /// Every open entry — <see cref="XeroOutboxState.Pending"/>,
    /// <see cref="XeroOutboxState.Failed"/>, <see cref="XeroOutboxState.Unknown"/>
    /// or <see cref="XeroOutboxState.WaitingForAuthorisation"/> — that was not
    /// last sent to <paramref name="tenantId"/> was queued under the previous
    /// organisation's rules (its automatic-sync start, its opt-ins) and is
    /// <see cref="XeroOutboxState.Superseded"/> with <paramref name="reason"/>,
    /// never sent here; the engine then plans every record again against this
    /// organisation. An entry written by a newer TempestOS, or one that cannot
    /// be read, is left as it is. The default implementation changes nothing.
    /// </summary>
    /// <param name="tenantId">The organisation connected now.</param>
    /// <param name="reason">The <see cref="XeroOutboxEntry.LastError"/> each superseded entry keeps.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The ids of the entries superseded.</returns>
    Task<IReadOnlyList<Guid>> SupersedeOpenForOtherOrganisationsAsync(string tenantId, string reason, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>([]);

    /// <summary>
    /// `v0.24.0` F1 (additive, M9): removes finished entries
    /// (<see cref="XeroOutboxState.Succeeded"/>, <see cref="XeroOutboxState.Superseded"/>)
    /// last touched more than <paramref name="retention"/> ago, keeping what
    /// de-duplication and recovery need: per document, operation and argument,
    /// the newest finished entry and the newest entry that succeeded in each
    /// organisation; how many entries of each content a removed entry stood
    /// for is carried on the kept entry, so a later write's idempotency key is
    /// never one an earlier write used. Open entries are never removed. The
    /// default implementation removes nothing.
    /// </summary>
    /// <param name="retention">How long a finished entry is kept at least.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many entries were removed.</returns>
    Task<int> PruneAsync(TimeSpan retention, CancellationToken cancellationToken = default) => Task.FromResult(0);
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
/// the clock gives two the same instant. Ties are broken by
/// <see cref="XeroOutboxEntry.EnqueuedAtUtc"/>, then by id. An entry stored
/// without a <c>Sequence</c> (or with one that is not a whole number) is
/// placed by its time: it takes the largest stored sequence among entries
/// queued at or before it (<c>0</c> when none), so it never jumps ahead of
/// earlier work, and it is rewritten without one. The next sequence never
/// wraps (it stays at <see cref="long.MaxValue"/> and the tie-breaks order
/// the rest).
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
/// the same content as the newest entry of its group that is open, or that
/// succeeded in the organisation connected now, returns that entry (not
/// queued twice) — an entry that succeeded in another organisation (the
/// Demo Company, say) does not count (F1, M1). Built without a secret store
/// (no way to tell the connected organisation), every succeeded entry counts,
/// as before F1. Otherwise the new entry
/// supersedes every entry of the group not yet sent —
/// <see cref="XeroOutboxState.Pending"/>,
/// <see cref="XeroOutboxState.Failed"/> and
/// <see cref="XeroOutboxState.WaitingForAuthorisation"/> — but never one
/// that may have reached Xero (<see cref="XeroOutboxState.InFlight"/>,
/// <see cref="XeroOutboxState.Unknown"/>), which must be reconciled first.
/// The replacement takes the queue position of the earliest entry it
/// replaces, so it stays ahead of every later entry for its document: a
/// changed create queued after a status change is still sent before it.
/// </para>
/// <para>
/// <b>Tenant.</b> An entry names a TempestOS record and an operation, never
/// a Xero id: it is sent to whichever organisation is connected when it is
/// drained, and its handler resolves the link in that tenant. Claimed through
/// <see cref="ClaimNextDueForTenantAsync"/> it records that organisation
/// (<see cref="XeroOutboxEntry.TenantId"/>), so a succeeded entry names where
/// it succeeded. When a different organisation is connected, the engine
/// supersedes every open entry not last sent there
/// (<see cref="SupersedeOpenForOtherOrganisationsAsync"/>) and plans again.
/// </para>
/// <para>
/// <b>Index (F1, M9).</b> Every entry is read from the store once per
/// process, on first use, into an in-memory index shared by every instance
/// over the same <see cref="IPersistenceStore"/> (as the write gate is):
/// entries by id, by document (in queue order) and the open set. Enqueue,
/// claim, a document's list (the badge) and a list of open states then touch
/// only the entries concerned, never the whole collection. Every write goes
/// through the store first, then the index, under the one gate. The outbox is
/// the only writer of <see cref="Collection"/>; a change written to the store
/// by anything else is seen after a restart.
/// </para>
/// <para>
/// <b>Pruning (F1, M9).</b> <see cref="PruneAsync"/> removes finished entries
/// older than the retention, keeping per group the newest finished entry and
/// the newest succeeded entry per organisation (de-duplication, the badge's
/// "sent to another organisation" note, and X5's "was a create queued"), and
/// carrying the removed entries' per-content counts on the kept newest entry
/// (an additive <c>PrunedOccurrences</c> JSON property), so occurrence
/// numbers — and with them idempotency keys — are never reused.
/// </para>
/// <para>
/// <b>Schema versioning.</b> Entries are written at
/// <see cref="XeroOutboxEntry.CurrentSchemaVersion"/>. Unknown properties
/// are ignored. An entry written by a newer TempestOS is listed (for the
/// badge) but never claimed, retried, superseded or rewritten, and it holds
/// its document's queue — also when this build cannot deserialize it (an
/// operation or state it does not know): it is then listed as a
/// placeholder built from the fields it can read, at its own
/// <see cref="XeroOutboxEntry.SchemaVersion"/>. An operation it does not
/// know is listed as <see cref="UnknownOperation"/>; a document kind (or a
/// document) it does not know as <see cref="UnknownDocumentKind"/>, which
/// is listed but holds no real document's queue.
/// </para>
/// <para>
/// <b>Corrupt entries.</b> An entry of this build's version (or with no
/// usable version) that cannot be read, but whose document can, is listed
/// as <see cref="XeroOutboxState.Failed"/> with a
/// <see cref="XeroOutboxEntry.LastError"/> saying so, is never claimed,
/// retried, superseded or rewritten, and holds its document's queue — later
/// work for that document never runs ahead of it. An entry whose document
/// cannot be read either (not JSON, no document) cannot hold any queue; it
/// is left in place and out of every listing.
/// </para>
/// </remarks>
public sealed class PersistenceXeroOutbox : IXeroOutbox, IXeroOutboxDrain
{
    /// <summary>The <see cref="IPersistenceStore"/> collection entries live in.</summary>
    public const string Collection = "Xero.Outbox";

    /// <summary>The <see cref="XeroOutboxEntry.EnqueuedBy"/> of an entry queued with no signed-in principal.</summary>
    public const string SystemPrincipal = "system";

    /// <summary>The default for <see cref="PruneAsync"/>'s retention: finished entries are kept at least this long (F1, M9).</summary>
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(30);

    private const string SequenceProperty = "Sequence";

    /// <summary>The additive JSON property, on a kept finished entry, counting per content hash the entries of its group <see cref="PruneAsync"/> removed.</summary>
    internal const string PrunedOccurrencesProperty = "PrunedOccurrences";

    /// <summary>The <see cref="XeroOutboxEntry.LastError"/> of a corrupt entry's placeholder.</summary>
    internal const string UnreadableError =
        "This outbox entry cannot be read by this TempestOS; it is left untouched and holds its document's queue until it is repaired or removed.";

    /// <summary>The <see cref="XeroOutboxEntry.EnqueuedBy"/> of a placeholder whose own value cannot be read.</summary>
    internal const string UnknownPrincipal = "unknown";

    /// <summary>
    /// The <see cref="XeroOutboxEntry.Operation"/> of a placeholder whose own
    /// operation this build does not know: not a defined
    /// <see cref="XeroOperation"/> (<see cref="Enum.IsDefined{TEnum}(TEnum)"/>
    /// is <see langword="false"/>), so a listing shows it as an unknown
    /// operation, never as a real one.
    /// </summary>
    public const XeroOperation UnknownOperation = (XeroOperation)(-1);

    /// <summary>
    /// The <see cref="XeroDocumentRef.Kind"/> of a newer TempestOS's entry
    /// whose document kind this build does not know: not a defined
    /// <see cref="XeroDocumentKind"/>, and refused by
    /// <see cref="EnqueueAsync"/>, so the entry is listed (for the badge) but
    /// holds no real document's queue.
    /// </summary>
    public const XeroDocumentKind UnknownDocumentKind = (XeroDocumentKind)(-1);

    private static readonly XeroOutboxState[] OutcomeStates =
    [
        XeroOutboxState.Pending, XeroOutboxState.Succeeded, XeroOutboxState.Failed,
        XeroOutboxState.Unknown, XeroOutboxState.WaitingForAuthorisation,
    ];

    private static readonly ConditionalWeakTable<IPersistenceStore, StrongBox<OutboxIndex?>> Indexes = new();

    private readonly IPersistenceStore _store;
    private readonly ICurrentPrincipalAccessor? _principals;
    private readonly ISecretStore? _secrets;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate;
    private readonly StrongBox<OutboxIndex?> _index;

    /// <summary>Initialises a new instance of the <see cref="PersistenceXeroOutbox"/> class.</summary>
    /// <param name="store">The platform's persistence store.</param>
    /// <param name="principals">Who is signed in, for <see cref="XeroOutboxEntry.EnqueuedBy"/>; <see langword="null"/> records <see cref="SystemPrincipal"/>.</param>
    /// <param name="secrets">`v0.24.0` F1 (additive): where the connected Xero organisation's tenant id is kept (read only), so a write is de-duplicated only against entries that succeeded in that organisation; <see langword="null"/> counts every succeeded entry, as before F1.</param>
    public PersistenceXeroOutbox(IPersistenceStore store, ICurrentPrincipalAccessor? principals = null, ISecretStore? secrets = null)
        : this(store, principals, TimeProvider.System, secrets)
    {
    }

    /// <summary>Test seam (`ADR-0121`): a pinned clock.</summary>
    internal PersistenceXeroOutbox(IPersistenceStore store, ICurrentPrincipalAccessor? principals, TimeProvider timeProvider, ISecretStore? secrets = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _store = store;
        _principals = principals;
        _secrets = secrets;
        _time = timeProvider;
        _gate = XeroStoreSupport.GateFor(store, Collection);
        _index = Indexes.GetValue(store, _ => new StrongBox<OutboxIndex?>());
    }

    /// <summary>
    /// Test seam: forgets the in-memory index over <paramref name="store"/>,
    /// so the next call reads every entry from the store again — for a test
    /// that writes an entry's JSON straight into the store, as a newer
    /// TempestOS or a corruption would between runs.
    /// </summary>
    /// <param name="store">The backing store.</param>
    internal static void ForgetIndex(IPersistenceStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        var gate = XeroStoreSupport.GateFor(store, Collection);
        gate.Wait();
        try
        {
            Indexes.GetValue(store, _ => new StrongBox<OutboxIndex?>()).Value = null;
        }
        finally
        {
            gate.Release();
        }
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

        if (!Enum.IsDefined(document.Kind))
            throw new ArgumentOutOfRangeException(nameof(document), document.Kind, "Not a kind of TempestOS record.");

        var tenantId = await ReadTenantIdAsync(cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await IndexAsync(cancellationToken).ConfigureAwait(false);
            var group = index.EntriesFor(document)
                .Where(s => !s.Locked && s.Entry.Operation == operation
                            && string.Equals(s.Entry.Argument, argument, StringComparison.Ordinal))
                .ToList();

            // F1 (M1): an open entry, or one that succeeded in the
            // organisation connected now — never one that succeeded in
            // another organisation (the Demo Company's copy is not this one's).
            if (group.LastOrDefault(s => IsOpen(s.Entry.State) || (s.Entry.State == XeroOutboxState.Succeeded && SucceededHere(s.Entry, tenantId))) is { } latest
                && string.Equals(latest.Entry.ContentHash, contentHash, StringComparison.Ordinal))
            {
                return latest.Entry;
            }

            var replaced = group
                .Where(s => s.Entry.State is XeroOutboxState.Pending or XeroOutboxState.Failed or XeroOutboxState.WaitingForAuthorisation)
                .ToList();

            // A replacement takes the earliest replaced entry's sequence, so
            // it keeps that entry's place ahead of later work for the
            // document. Every new entry is stamped strictly after every
            // stored entry of this build (a clock that repeats an instant or steps back moves
            // it on by a tick), so time order is queue order: the tie-break
            // puts a replacement behind the entries it replaces (a crash
            // before they are marked superseded sends the old content first,
            // then this), and an entry whose Sequence is lost is still placed
            // unambiguously by its time.
            var now = _time.GetUtcNow();
            // Only this build's own readable entries count: a locked entry's
            // time (a newer TempestOS's, or a corrupt one's) is not trusted.
            var latestStored = index.LatestUnlockedEnqueuedAtUtc;
            if (now <= latestStored && latestStored < DateTimeOffset.MaxValue)
                now = latestStored.AddTicks(1);

            var sequence = replaced.Count > 0 ? replaced.Min(s => s.Sequence) : index.NextSequence();

            // Removed (pruned) entries still count, through the kept entry that carries their number.
            var occurrence = group.Count(s => string.Equals(s.Entry.ContentHash, contentHash, StringComparison.Ordinal))
                             + group.Sum(s => PrunedCount(s, contentHash));
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

            // The new entry is written before the ones it supersedes: a crash
            // between the two leaves both pending (one redundant push), never
            // neither (a lost write).
            await WriteAsync(index, new StoredEntry(entry, sequence, Newer: false, Original: null), cancellationToken).ConfigureAwait(false);

            foreach (var old in replaced)
            {
                await WriteAsync(
                    index, old with { Entry = old.Entry with { State = XeroOutboxState.Superseded, NotBeforeUtc = null } },
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

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await IndexAsync(cancellationToken).ConfigureAwait(false);
            return [.. index.EntriesFor(document).Select(s => s.Entry)];
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<XeroOutboxEntry>> ListAsync(IReadOnlyCollection<XeroOutboxState> states, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(states);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await IndexAsync(cancellationToken).ConfigureAwait(false);

            // Open states only (the badge, the drain, Retry all): the open set, never every entry.
            var source = states.Count > 0 && states.All(IsOpen) ? index.OpenEntries() : index.All();
            var list = source.Where(s => states.Count == 0 || states.Contains(s.Entry.State)).ToList();
            list.Sort(QueueOrder);
            return [.. list.Select(s => s.Entry)];
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RetryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await IndexAsync(cancellationToken).ConfigureAwait(false);
            if (index.Find(entryId) is not { Locked: false } stored
                || stored.Entry.State != XeroOutboxState.Failed)
            {
                return false;
            }

            await WriteAsync(index, stored with { Entry = stored.Entry with { State = XeroOutboxState.Pending, NotBeforeUtc = null } }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public Task<XeroOutboxEntry?> ClaimNextDueAsync(CancellationToken cancellationToken = default) =>
        ClaimNextDueAsync([], cancellationToken);

    /// <inheritdoc />
    public Task<XeroOutboxEntry?> ClaimNextDueAsync(IReadOnlyCollection<XeroOutboxState> states, CancellationToken cancellationToken = default) =>
        ClaimCoreAsync(states, tenantId: null, cancellationToken);

    /// <inheritdoc />
    public Task<XeroOutboxEntry?> ClaimNextDueForTenantAsync(IReadOnlyCollection<XeroOutboxState> states, string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        return ClaimCoreAsync(states, tenantId, cancellationToken);
    }

    private async Task<XeroOutboxEntry?> ClaimCoreAsync(IReadOnlyCollection<XeroOutboxState> states, string? tenantId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(states);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _time.GetUtcNow();
            var index = await IndexAsync(cancellationToken).ConfigureAwait(false);

            // The head of each document's queue: its oldest entry that is
            // neither done nor replaced. Anything behind a head waits.
            // An entry written by a newer TempestOS, or one that cannot be
            // read, is never claimed, so it holds its document's queue.
            StoredEntry? next = null;
            foreach (var head in index.Heads())
            {
                if (!head.Locked
                    && head.Entry.State is XeroOutboxState.Pending or XeroOutboxState.Unknown
                    && (states.Count == 0 || states.Contains(head.Entry.State))
                    && (head.Entry.NotBeforeUtc is not { } notBefore || notBefore <= now)
                    && (next is null || QueueOrder(head, next) < 0))
                {
                    next = head;
                }
            }

            if (next is null)
                return null;

            var claimed = next.Entry with
            {
                State = XeroOutboxState.InFlight,
                Attempts = next.Entry.Attempts + 1,
                LastAttemptAtUtc = now,
                TenantId = tenantId ?? next.Entry.TenantId,
            };

            await WriteAsync(index, next with { Entry = claimed }, cancellationToken).ConfigureAwait(false);
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
            var index = await IndexAsync(cancellationToken).ConfigureAwait(false);
            if (index.Find(entryId) is not { } stored)
                return null;

            if (stored.Newer)
                throw new InvalidOperationException($"Outbox entry {entryId} was written by a newer TempestOS; it is not changed.");

            if (stored.Unreadable)
                throw new InvalidOperationException($"Outbox entry {entryId} cannot be read; it is not changed.");

            if (stored.Entry.State != XeroOutboxState.InFlight)
                throw new InvalidOperationException($"Outbox entry {entryId} is {stored.Entry.State}, not InFlight; only a claimed entry has an outcome.");

            var updated = stored.Entry with
            {
                State = state,
                LastError = state == XeroOutboxState.Succeeded ? null : lastError,
                NotBeforeUtc = state is XeroOutboxState.Pending or XeroOutboxState.Unknown ? notBeforeUtc : null,
            };

            await WriteAsync(index, stored with { Entry = updated }, cancellationToken).ConfigureAwait(false);
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
    public async Task<XeroOutboxEntry?> FindAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return (await IndexAsync(cancellationToken).ConfigureAwait(false)).Find(entryId)?.Entry;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> SupersedeOpenForOtherOrganisationsAsync(string tenantId, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await IndexAsync(cancellationToken).ConfigureAwait(false);
            var superseded = new List<Guid>();
            var open = index.OpenEntries().ToList();
            open.Sort(QueueOrder);

            foreach (var stored in open)
            {
                if (stored.Locked
                    || stored.Entry.State is not (XeroOutboxState.Pending or XeroOutboxState.Failed or XeroOutboxState.Unknown or XeroOutboxState.WaitingForAuthorisation)
                    || string.Equals(stored.Entry.TenantId, tenantId, StringComparison.Ordinal))
                {
                    continue;
                }

                await WriteAsync(
                    index, stored with { Entry = stored.Entry with { State = XeroOutboxState.Superseded, NotBeforeUtc = null, LastError = reason } },
                    cancellationToken).ConfigureAwait(false);
                superseded.Add(stored.Entry.Id);
            }

            return superseded;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> PruneAsync(TimeSpan retention, CancellationToken cancellationToken = default)
    {
        if (retention < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(retention), retention, "The retention cannot be negative.");

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await IndexAsync(cancellationToken).ConfigureAwait(false);
            var cutoff = _time.GetUtcNow() - retention;
            var removed = 0;

            var groups = index.All()
                .Where(s => !s.Locked)
                .GroupBy(s => (s.Entry.Document, s.Entry.Operation, s.Entry.Argument))
                .ToList();

            foreach (var group in groups)
            {
                var finished = group.Where(s => !IsOpen(s.Entry.State)).ToList();
                if (finished.Count <= 1)
                    continue;

                finished.Sort(QueueOrder);
                var newest = finished[^1];
                var keep = new HashSet<Guid> { newest.Entry.Id };
                foreach (var perTenant in finished.Where(s => s.Entry.State == XeroOutboxState.Succeeded).GroupBy(s => s.Entry.TenantId ?? string.Empty))
                    keep.Add(perTenant.Last().Entry.Id);

                var prunable = finished
                    .Where(s => !keep.Contains(s.Entry.Id) && (s.Entry.LastAttemptAtUtc ?? s.Entry.EnqueuedAtUtc) < cutoff)
                    .ToList();
                if (prunable.Count == 0)
                    continue;

                // The kept newest entry carries how many entries of each
                // content the group held, so occurrence numbers (and so
                // idempotency keys) are never reused. Written before the
                // removals: a crash between leaves a count too high (a fresh
                // key), never too low (a reused one).
                var counts = PrunedCounts(newest);
                foreach (var gone in prunable)
                {
                    counts[gone.Entry.ContentHash] = counts.GetValueOrDefault(gone.Entry.ContentHash) + 1;
                    foreach (var (hash, count) in PrunedCounts(gone))
                        counts[hash] = counts.GetValueOrDefault(hash) + count;
                }

                var countsNode = new JsonObject();
                foreach (var (hash, count) in counts.OrderBy(c => c.Key, StringComparer.Ordinal))
                    countsNode[hash] = count;

                await WriteAsync(index, newest, cancellationToken, node => node[PrunedOccurrencesProperty] = countsNode).ConfigureAwait(false);

                foreach (var gone in prunable)
                {
                    await _store.DeleteAsync(Collection, gone.Entry.Id.ToString("D"), cancellationToken).ConfigureAwait(false);
                    index.Remove(gone.Entry.Id);
                    removed++;
                }
            }

            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<int> MoveAllAsync(XeroOutboxState from, XeroOutboxState to, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await IndexAsync(cancellationToken).ConfigureAwait(false);
            var moving = index.OpenEntries().Where(s => !s.Locked && s.Entry.State == from).ToList();
            moving.Sort(QueueOrder);

            foreach (var stored in moving)
                await WriteAsync(index, stored with { Entry = stored.Entry with { State = to, NotBeforeUtc = null } }, cancellationToken).ConfigureAwait(false);

            return moving.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>The connected organisation's tenant id; <see langword="null"/> when none is connected or no secret store was given.</summary>
    private async Task<string?> ReadTenantIdAsync(CancellationToken cancellationToken)
    {
        if (_secrets is null)
            return null;

        var tenantId = await _secrets.GetAsync(XeroContactLinker.TenantIdSecretKey, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId.Trim();
    }

    /// <summary>Whether a succeeded <paramref name="entry"/> counts as sent to the organisation connected now (<paramref name="tenantId"/>).</summary>
    private bool SucceededHere(XeroOutboxEntry entry, string? tenantId) =>
        _secrets is null
        || (tenantId is not null && string.Equals(entry.TenantId, tenantId, StringComparison.Ordinal));

    private static bool IsOpen(XeroOutboxState state) => state is not (XeroOutboxState.Succeeded or XeroOutboxState.Superseded);

    /// <summary>Queue order: the stored sequence, then the time queued, then the id.</summary>
    private static int QueueOrder(StoredEntry a, StoredEntry b)
    {
        var bySequence = a.Sequence.CompareTo(b.Sequence);
        if (bySequence != 0)
            return bySequence;

        var byTime = a.Entry.EnqueuedAtUtc.CompareTo(b.Entry.EnqueuedAtUtc);
        return byTime != 0 ? byTime : a.Entry.Id.CompareTo(b.Entry.Id);
    }

    /// <summary>The index over this store, read from the store on first use (the caller holds <see cref="_gate"/>).</summary>
    private async Task<OutboxIndex> IndexAsync(CancellationToken cancellationToken)
    {
        if (_index.Value is { } loaded)
            return loaded;

        var index = new OutboxIndex();
        foreach (var stored in await LoadAllAsync(cancellationToken).ConfigureAwait(false))
            index.Put(stored);

        _index.Value = index;
        return index;
    }

    private async Task<List<StoredEntry>> LoadAllAsync(CancellationToken cancellationToken)
    {
        var keys = await _store.ListKeysAsync(Collection, cancellationToken).ConfigureAwait(false);
        var entries = new List<StoredEntry>(keys.Count);

        foreach (var key in keys)
        {
            if (!Guid.TryParseExact(key, "D", out var id))
                continue;

            var json = await _store.ReadAsync(Collection, key, cancellationToken).ConfigureAwait(false);
            if (json is not null && Parse(json, id) is { } stored)
                entries.Add(stored);
        }

        // An entry stored without a usable Sequence is given one the way it
        // was first assigned: walking the entries in the order they were
        // queued (time, then id), it takes one more than the largest sequence
        // seen so far, stored or already filled in, superseded entries
        // included. It therefore sorts after everything queued before it,
        // including the low sequence a replacement inherits from the entry it
        // superseded: it never jumps ahead of work its document queued earlier.
        var byQueueOrder = Enumerable.Range(0, entries.Count)
            .OrderBy(i => entries[i].Entry.EnqueuedAtUtc)
            .ThenBy(i => entries[i].Entry.Id)
            .ToList();
        var largestSoFar = 0L;
        foreach (var i in byQueueOrder)
        {
            if (!entries[i].HasSequence)
                entries[i] = entries[i] with { Sequence = largestSoFar == long.MaxValue ? long.MaxValue : largestSoFar + 1 };

            largestSoFar = Math.Max(largestSoFar, entries[i].Sequence);
        }

        entries.Sort(QueueOrder);
        return entries;
    }

    /// <summary>
    /// Reads one stored entry. One that cannot be fully read (a newer
    /// TempestOS's, or a corrupt one) but whose document can is returned as a
    /// locked placeholder; <see langword="null"/> when not even its document
    /// can be read.
    /// </summary>
    internal static StoredEntry? Parse(string json, Guid id)
    {
        JsonObject? node;
        try
        {
            node = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }

        if (node is null)
            return null;

        var version = XeroStoreSupport.ReadSchemaVersion(json);
        var newer = version > XeroOutboxEntry.CurrentSchemaVersion;
        var stored = ReadSequence(node);
        var sequence = stored ?? 0;
        var hasSequence = stored is not null;

        if (version >= 1)
        {
            try
            {
                var entry = JsonSerializer.Deserialize<XeroOutboxEntry>(json, XeroStoreSupport.JsonOptions);

                if (entry is not null && entry.Id == id && IsComplete(entry))
                    return new StoredEntry(entry with { SchemaVersion = version.Value }, sequence, newer, node, HasSequence: hasSequence);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or NotSupportedException)
            {
                // Falls through to the placeholder.
            }
        }

        return Placeholder(node, id, version ?? 0, newer, sequence, hasSequence);
    }

    /// <summary>
    /// What can be read of an entry that cannot be deserialized: its
    /// document (required for a corrupt entry — without it the entry holds no
    /// queue; a newer TempestOS's entry is listed under
    /// <see cref="UnknownDocumentKind"/> instead), and every other field it
    /// still carries in a form this build reads (an unknown operation reads as
    /// <see cref="UnknownOperation"/>). A newer
    /// TempestOS's entry keeps its own state (an unknown one reads as
    /// <see cref="XeroOutboxState.Pending"/>, so it holds its queue); a
    /// corrupt one reads as <see cref="XeroOutboxState.Failed"/> with
    /// <see cref="UnreadableError"/> unless it says it is finished.
    /// </summary>
    private static StoredEntry? Placeholder(JsonObject node, Guid id, int version, bool newer, long sequence, bool hasSequence)
    {
        var document = node["Document"] as JsonObject;
        var kind = document is null ? null : ReadEnum<XeroDocumentKind>(document, "Kind");
        var tempestKey = document is null ? null : ReadString(document, "TempestKey");
        XeroDocumentRef reference;

        if (kind is not null && tempestKey is { Length: > 0 })
            reference = new XeroDocumentRef(kind.Value, tempestKey);
        else if (newer)
            reference = new XeroDocumentRef(UnknownDocumentKind, tempestKey ?? string.Empty);
        else
            return null;

        var storedState = ReadEnum<XeroOutboxState>(node, "State");
        var finished = storedState is XeroOutboxState.Succeeded or XeroOutboxState.Superseded;
        var state = newer
            ? storedState ?? XeroOutboxState.Pending
            : finished ? storedState!.Value : XeroOutboxState.Failed;

        var entry = new XeroOutboxEntry(
            SchemaVersion: version,
            Id: id,
            Operation: ReadEnum<XeroOperation>(node, "Operation") ?? UnknownOperation,
            Document: reference,
            Argument: ReadString(node, "Argument"),
            IdempotencyKey: ReadString(node, "IdempotencyKey") ?? string.Empty,
            ContentHash: ReadString(node, "ContentHash") ?? string.Empty,
            State: state,
            Attempts: node["Attempts"] is JsonValue attempts && attempts.TryGetValue<int>(out var a) ? a : 0,
            EnqueuedAtUtc: ReadTime(node, "EnqueuedAtUtc") ?? DateTimeOffset.MinValue,
            NotBeforeUtc: ReadTime(node, "NotBeforeUtc"),
            LastAttemptAtUtc: ReadTime(node, "LastAttemptAtUtc"),
            LastError: newer || finished ? ReadString(node, "LastError") : UnreadableError,
            EnqueuedBy: ReadString(node, "EnqueuedBy") ?? UnknownPrincipal)
        {
            TenantId = ReadString(node, "TenantId"),
        };

        return new StoredEntry(entry, sequence, newer, node, Unreadable: !newer, HasSequence: hasSequence);
    }

    /// <summary>The stored <c>Sequence</c>; <see langword="null"/> when it is missing or not a whole number (<see cref="LoadAllAsync"/> places such an entry by its time).</summary>
    private static long? ReadSequence(JsonObject node) =>
        node.TryGetPropertyValue(SequenceProperty, out var value) && value is JsonValue v && v.TryGetValue<long>(out var s) ? s : null;

    /// <summary>How many removed entries with <paramref name="contentHash"/> <paramref name="stored"/> stands for.</summary>
    private static int PrunedCount(StoredEntry stored, string contentHash) =>
        stored.Original?[PrunedOccurrencesProperty] is JsonObject counts
        && counts[contentHash] is JsonValue value && value.TryGetValue<int>(out var count) && count > 0
            ? count
            : 0;

    /// <summary>Every per-content count of removed entries <paramref name="stored"/> carries.</summary>
    private static Dictionary<string, int> PrunedCounts(StoredEntry stored)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (stored.Original?[PrunedOccurrencesProperty] is JsonObject node)
        {
            foreach (var (hash, value) in node)
            {
                if (value is JsonValue v && v.TryGetValue<int>(out var count) && count > 0)
                    counts[hash] = count;
            }
        }

        return counts;
    }

    private static string? ReadString(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static TEnum? ReadEnum<TEnum>(JsonObject node, string name)
        where TEnum : struct, Enum =>
        ReadString(node, name) is { } text && Enum.TryParse<TEnum>(text, ignoreCase: false, out var parsed)
        && Enum.IsDefined(parsed) && !int.TryParse(text, out _)
            ? parsed
            : null;

    private static DateTimeOffset? ReadTime(JsonObject node, string name) =>
        node[name] is JsonValue value && value.TryGetValue<DateTimeOffset>(out var time) ? time : null;

    private static bool IsComplete(XeroOutboxEntry entry) =>
        entry.Document is { TempestKey: { Length: > 0 } }
        && !string.IsNullOrWhiteSpace(entry.IdempotencyKey)
        && !string.IsNullOrWhiteSpace(entry.ContentHash)
        && entry.EnqueuedBy is not null;

    /// <summary>
    /// Writes <paramref name="stored"/> at the current schema version, keeping
    /// any property this build does not know from what was read, then records
    /// it in <paramref name="index"/> (store first: the index never holds what
    /// the store does not). <paramref name="change"/>, when given, adjusts the
    /// JSON before it is written.
    /// </summary>
    private async Task WriteAsync(OutboxIndex index, StoredEntry stored, CancellationToken cancellationToken, Action<JsonObject>? change = null)
    {
        var node = XeroStoreSupport.ToJson(stored.Entry with { SchemaVersion = XeroOutboxEntry.CurrentSchemaVersion }, stored.Original);
        // An entry stored without a Sequence keeps none: its place is worked
        // out from its time on every read, never frozen at a value that
        // could move it ahead of earlier work.
        if (stored.HasSequence)
            node[SequenceProperty] = stored.Sequence;
        else
            node.Remove(SequenceProperty);

        change?.Invoke(node);

        await _store.WriteAsync(Collection, stored.Entry.Id.ToString("D"), node.ToJsonString(), cancellationToken).ConfigureAwait(false);
        index.Put(stored with { Entry = stored.Entry with { SchemaVersion = XeroOutboxEntry.CurrentSchemaVersion }, Original = node });
    }

    /// <summary>One entry as stored: the entry, its queue position, whether a newer TempestOS wrote it, the JSON it was read from (<see langword="null"/> for a new entry), whether it is a corrupt entry's placeholder, and whether its queue position was stored (rather than worked out from its time).</summary>
    internal sealed record StoredEntry(XeroOutboxEntry Entry, long Sequence, bool Newer, JsonObject? Original, bool Unreadable = false, bool HasSequence = true)
    {
        /// <summary>Never claimed, retried, superseded or rewritten by this build: a newer TempestOS's entry, or one that cannot be read.</summary>
        public bool Locked => Newer || Unreadable;
    }

    /// <summary>
    /// The in-memory index over one store's entries (F1, M9): by id, by
    /// document in queue order, and the open set — so no operation reads or
    /// scans every entry. Changed only under the store's write gate.
    /// </summary>
    private sealed class OutboxIndex
    {
        private readonly Dictionary<Guid, StoredEntry> _byId = [];
        private readonly Dictionary<XeroDocumentRef, List<StoredEntry>> _byDocument = [];
        private readonly HashSet<Guid> _open = [];
        private long _largestSequence;

        /// <summary>The latest <see cref="XeroOutboxEntry.EnqueuedAtUtc"/> of any readable entry of this build's version or earlier (never moves back, also when entries are removed).</summary>
        public DateTimeOffset LatestUnlockedEnqueuedAtUtc { get; private set; } = DateTimeOffset.MinValue;

        /// <summary>The sequence after every stored one; never wraps (at <see cref="long.MaxValue"/> it stays there and the tie-breaks order the rest).</summary>
        public long NextSequence() => _largestSequence == long.MaxValue ? long.MaxValue : _largestSequence + 1;

        public StoredEntry? Find(Guid id) => _byId.GetValueOrDefault(id);

        /// <summary>The document's entries, oldest first.</summary>
        public IReadOnlyList<StoredEntry> EntriesFor(XeroDocumentRef document) =>
            _byDocument.TryGetValue(document, out var list) ? list : [];

        public IEnumerable<StoredEntry> All() => _byId.Values;

        public IEnumerable<StoredEntry> OpenEntries() => _open.Select(id => _byId[id]);

        /// <summary>The head of each document's queue that has open work: its oldest entry that is neither done nor replaced.</summary>
        public IEnumerable<StoredEntry> Heads()
        {
            var documents = new HashSet<XeroDocumentRef>();
            foreach (var id in _open)
            {
                var document = _byId[id].Entry.Document;
                if (!documents.Add(document))
                    continue;

                foreach (var entry in _byDocument[document])
                {
                    if (IsOpen(entry.Entry.State))
                    {
                        yield return entry;
                        break;
                    }
                }
            }
        }

        public void Put(StoredEntry stored)
        {
            var id = stored.Entry.Id;
            if (_byId.TryGetValue(id, out var previous))
                RemoveFromDocument(previous);

            _byId[id] = stored;

            if (!_byDocument.TryGetValue(stored.Entry.Document, out var list))
            {
                list = [];
                _byDocument[stored.Entry.Document] = list;
            }

            var at = list.BinarySearch(stored, Comparer<StoredEntry>.Create(QueueOrder));
            list.Insert(at < 0 ? ~at : at, stored);

            if (IsOpen(stored.Entry.State))
                _open.Add(id);
            else
                _open.Remove(id);

            _largestSequence = Math.Max(_largestSequence, Math.Max(0, stored.Sequence));
            if (!stored.Locked && stored.Entry.EnqueuedAtUtc > LatestUnlockedEnqueuedAtUtc)
                LatestUnlockedEnqueuedAtUtc = stored.Entry.EnqueuedAtUtc;
        }

        public void Remove(Guid id)
        {
            if (!_byId.Remove(id, out var previous))
                return;

            RemoveFromDocument(previous);
            _open.Remove(id);
        }

        private void RemoveFromDocument(StoredEntry previous)
        {
            if (!_byDocument.TryGetValue(previous.Entry.Document, out var list))
                return;

            list.RemoveAll(s => s.Entry.Id == previous.Entry.Id);
            if (list.Count == 0)
                _byDocument.Remove(previous.Entry.Document);
        }
    }
}
