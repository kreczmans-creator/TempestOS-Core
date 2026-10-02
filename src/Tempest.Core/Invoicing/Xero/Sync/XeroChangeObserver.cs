using System.Threading.Channels;
using Tempest.Core.Events;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>One TempestOS record a committed change touched, as the sync engine re-plans it.</summary>
/// <param name="CanonicalKind">The record's canonical Kind (<c>"Quotation"</c>, <c>"InvoiceRequest"</c>, <c>"PurchaseOrder"</c>, <c>"ProjectExpense"</c>).</param>
/// <param name="ObjectId">The record's id.</param>
public sealed record XeroObservedChange(string CanonicalKind, Guid ObjectId);

/// <summary>What <see cref="XeroChangeObserver.TakeAll"/> hands the engine.</summary>
/// <param name="Changes">Each changed record once, in the order first seen.</param>
/// <param name="RescanNeeded">Whether changes were dropped because the queue was full: the engine then plans every record (the start-up scan), so nothing is lost.</param>
public sealed record XeroObservedBatch(IReadOnlyList<XeroObservedChange> Changes, bool RescanNeeded);

/// <summary>
/// Listens to TempestOS's saved-change feed (<see cref="IWorkspaceChanges"/>,
/// `WP 18.1A`) for the records the Xero planners watch (`v0.24.0` X6, design
/// §6.2), and hands them to the sync engine off the commit thread.
/// </summary>
/// <remarks>
/// <para>
/// <b>Off the commit thread.</b> <see cref="IWorkspaceChanges.Changed"/> is
/// raised synchronously while the domain write lock is held, so the handler
/// here only writes the touched record into a bounded in-memory channel and
/// raises <see cref="ChangesQueued"/> (the engine merely wakes its loop): no
/// planning, no store read, no network on the commit thread. Every change
/// type counts — an <c>AttachmentAdded</c> (an exported PDF, a receipt) as
/// much as a status change.
/// </para>
/// <para>
/// <b>Nothing lost.</b> When the channel is full the change is dropped and a
/// full re-scan is requested instead (<see cref="XeroObservedBatch.RescanNeeded"/>);
/// and because the queue is in memory, a change saved just before a crash is
/// picked up by the start-up scan, which plans every record from its current
/// state (desired state, not events).
/// </para>
/// </remarks>
public sealed class XeroChangeObserver : IDisposable
{
    /// <summary>How many changed records the queue holds before it asks for a re-scan instead.</summary>
    public const int DefaultCapacity = 1024;

    private readonly IWorkspaceChanges? _changes;
    private readonly Channel<XeroObservedChange> _queue;
    private readonly object _gate = new();
    private HashSet<string> _watched = new(StringComparer.Ordinal);
    private bool _subscribed;
    private int _overflowed;

    /// <summary>Initialises a new instance of the <see cref="XeroChangeObserver"/> class.</summary>
    /// <param name="changes">The platform's change feed; <see langword="null"/> when there is none (the engine then relies on its scans).</param>
    public XeroChangeObserver(IWorkspaceChanges? changes = null)
        : this(changes, DefaultCapacity)
    {
    }

    /// <summary>Test seam: a queue of <paramref name="capacity"/> records.</summary>
    internal XeroChangeObserver(IWorkspaceChanges? changes, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

        _changes = changes;
        _queue = Channel.CreateBounded<XeroObservedChange>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>Raised (on the committing thread — keep the handler trivial) after a watched change was queued, or dropped for a re-scan.</summary>
    public event Action? ChangesQueued;

    /// <summary>Whether the observer is subscribed to the change feed.</summary>
    public bool IsListening
    {
        get
        {
            lock (_gate)
                return _subscribed;
        }
    }

    /// <summary>Starts listening for changes to records of <paramref name="canonicalKinds"/>. Idempotent; a later call replaces the watched kinds.</summary>
    /// <param name="canonicalKinds">The canonical Kinds the planners watch.</param>
    public void Start(IEnumerable<string> canonicalKinds)
    {
        ArgumentNullException.ThrowIfNull(canonicalKinds);

        lock (_gate)
        {
            _watched = new HashSet<string>(canonicalKinds, StringComparer.Ordinal);
            if (_subscribed || _changes is null)
                return;

            _changes.Changed += OnChanged;
            _subscribed = true;
        }
    }

    /// <summary>Stops listening. Changes already queued stay queued.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (!_subscribed || _changes is null)
                return;

            _changes.Changed -= OnChanged;
            _subscribed = false;
        }
    }

    /// <summary>Takes every queued change, each record once, and whether a re-scan is needed because changes were dropped.</summary>
    public XeroObservedBatch TakeAll()
    {
        var seen = new HashSet<XeroObservedChange>();
        var changes = new List<XeroObservedChange>();
        while (_queue.Reader.TryRead(out var change))
        {
            if (seen.Add(change))
                changes.Add(change);
        }

        var rescan = Interlocked.Exchange(ref _overflowed, 0) == 1;
        return new XeroObservedBatch(changes, rescan);
    }

    /// <summary>Queues <paramref name="change"/>'s watched records. Internal so a test can feed the observer without a workspace.</summary>
    /// <param name="change">One committed transaction.</param>
    internal void OnChanged(WorkspaceChange change)
    {
        if (change is null)
            return;

        HashSet<string> watched;
        lock (_gate)
            watched = _watched;

        var queued = false;
        foreach (var entry in change.Entries)
        {
            if (!watched.Contains(entry.Kind))
                continue;

            if (!_queue.Writer.TryWrite(new XeroObservedChange(entry.Kind, entry.ObjectId)))
                Interlocked.Exchange(ref _overflowed, 1);

            queued = true;
        }

        if (!queued)
            return;

        try
        {
            ChangesQueued?.Invoke();
        }
        catch (Exception)
        {
            // Never fail the commit that raised the change: the queued
            // records are still taken on the engine's next wake.
        }
    }

    /// <inheritdoc />
    public void Dispose() => Stop();
}
