using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Persistence;

/// <summary>
/// Wraps an <see cref="IQueryablePersistenceStore"/> and fails the
/// transaction <em>after</em> its body has written everything, so the
/// commit is what does not happen (`WP 17.1B` acceptance).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why after the body rather than inside it.</b> Failing a particular
/// write tests that one write's error path. Failing the commit tests the
/// property `ADR-0145` actually claims: that a change which was fully
/// prepared — object state, document record, revision, references,
/// attachment bytes and the audit row all staged — still leaves
/// <em>nothing</em> behind if the transaction does not land. That is the
/// process-kill scenario the Work Package names, expressed as something a
/// test can run deterministically: the body throws on the way out, the
/// inner store drops its working copy, and the durable state is exactly
/// what it was before.
/// </para>
/// <para>
/// Every non-transactional member delegates unchanged, so a test can read
/// the underlying store afterwards and see what really committed.
/// </para>
/// </remarks>
public sealed class CommitFailingPersistenceStore(IQueryablePersistenceStore inner) : IQueryablePersistenceStore
{
    /// <summary>The store being wrapped, for reading what actually landed.</summary>
    public IQueryablePersistenceStore Inner { get; } = inner;

    /// <summary>When set, the next transaction's commit fails.</summary>
    public bool FailNextCommit { get; set; }

    /// <summary>When set, every transaction's commit fails.</summary>
    public bool FailEveryCommit { get; set; }

    /// <summary>How many transaction bodies ran to completion before the injected failure.</summary>
    public int BodiesCompleted { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) =>
        Inner.ListKeysAsync(collection, keyPrefix, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default) =>
        Inner.ReadAllAsync(collection, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(
        string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        Inner.ReadManyAsync(collection, keys, cancellationToken);

    /// <inheritdoc />
    public Task ExecuteInTransactionAsync(
        Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        return Inner.ExecuteInTransactionAsync(
            async (transaction, token) =>
            {
                await work(transaction, token).ConfigureAwait(false);

                BodiesCompleted++;

                if (!FailEveryCommit && !FailNextCommit)
                    return;

                FailNextCommit = false;
                throw new PersistenceStoreUnavailableException(
                    "Injected commit failure: the transaction body completed but the commit did not land.");
            },
            cancellationToken);
    }
}
