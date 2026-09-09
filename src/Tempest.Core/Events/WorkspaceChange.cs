namespace Tempest.Core.Events;

/// <summary>
/// What happened to one object as part of a single committed transaction
/// (`WP 18.1A`).
/// </summary>
public enum WorkspaceChangeType
{
    /// <summary>A new object was created.</summary>
    Created,

    /// <summary>An existing object's own fields changed (rename, revise, a Kind-specific field, a relationship added).</summary>
    Updated,

    /// <summary>An object's parent changed.</summary>
    Moved,

    /// <summary>An object was deleted.</summary>
    Deleted,

    /// <summary>An attachment was added to an object.</summary>
    AttachmentAdded,

    /// <summary>An object's lifecycle status changed.</summary>
    StatusChanged,
}

/// <summary>
/// One object touched by a committed transaction, and how (`WP 18.1A`).
/// </summary>
/// <param name="ObjectId">The touched object's id.</param>
/// <param name="Kind">The touched object's canonical Kind.</param>
/// <param name="ChangeType">What happened to it in this transaction.</param>
public readonly record struct WorkspaceChangeEntry(Guid ObjectId, string Kind, WorkspaceChangeType ChangeType);

/// <summary>
/// Everything one committed transaction did, as <see cref="IWorkspaceChanges"/>
/// raises it: the store sequence the commit landed at (`SqlitePersistenceStore.CurrentSequence`,
/// `ADR-0144` extended by `WP 18.1A`) and every object the transaction
/// touched.
/// </summary>
/// <remarks>
/// One event per commit, never one per touched object — a mutation that
/// changes three objects together is one fact, not three, and a subscriber
/// that took one snapshot read per entry could straddle a later commit
/// between them. <see cref="Sequence"/> is what a subscriber's own
/// snapshot read (`Tempest.Workspace.WorkspaceSnapshot`) is taken at or
/// after, so the render it produces is never older than the event that
/// asked for it.
/// </remarks>
public sealed class WorkspaceChange
{
    /// <summary>Initialises a new instance of the <see cref="WorkspaceChange"/> class.</summary>
    /// <param name="sequence">The store sequence this transaction committed at.</param>
    /// <param name="entries">Every object this transaction touched. Must not be empty.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="entries"/> is empty.</exception>
    public WorkspaceChange(long sequence, IReadOnlyList<WorkspaceChangeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
            throw new ArgumentException("A workspace change must name at least one touched object.", nameof(entries));

        Sequence = sequence;
        Entries = entries;
    }

    /// <summary>The store sequence this transaction committed at.</summary>
    public long Sequence { get; }

    /// <summary>Every object this transaction touched, in the order the transaction recorded them.</summary>
    public IReadOnlyList<WorkspaceChangeEntry> Entries { get; }
}
