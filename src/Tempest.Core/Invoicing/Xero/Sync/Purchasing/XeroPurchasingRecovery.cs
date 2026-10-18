using Tempest.Core.Invoicing.Xero.Api;

namespace Tempest.Core.Invoicing.Xero.Sync.Purchasing;

/// <summary>
/// Recovers one logged purchasing create by identity (`v0.24.0` X5,
/// <see cref="XeroPurchasingOwnership"/>), the same way for a purchase order
/// and a bill: it only finds out what became of the create —
/// <see cref="XeroPurchasingOwnership.Judge{T}"/> alone decides what that
/// means.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A tombstone — <see cref="XeroRecovery.Gone"/>, nothing asked of Xero.</item>
/// <item>Its record's id known — read back by that id only: live is
/// <see cref="XeroRecovery.Live"/>; deleted, voided or not found is
/// <see cref="XeroRecovery.Gone"/>, and a tombstone is recorded so nothing
/// ever re-sends it.</item>
/// <item>No id known, and Xero still holds its key
/// (<see cref="XeroPurchasingOwnership.IdempotencyKeyLifetime"/>) — its body
/// re-sent under its key; the id Xero answers is recorded at once, then read
/// back as above. A refused replay is <see cref="XeroRecovery.Unrecoverable"/>
/// (never struck off the log: the create may still be in Xero).</item>
/// <item>No id known otherwise (key may be forgotten, or no copy of the body) —
/// <see cref="XeroRecovery.Unrecoverable"/>; nothing is sent.</item>
/// </list>
/// </remarks>
internal static class XeroPurchasingRecovery
{
    /// <summary>The Xero calls and wire fields recovery needs for one kind of record.</summary>
    /// <typeparam name="TWrite">The create body.</typeparam>
    /// <typeparam name="T">The record as Xero answers it.</typeparam>
    /// <param name="Replay">Re-sends a create body under its key.</param>
    /// <param name="Read">Reads a record by id.</param>
    /// <param name="IdOf">The record's id.</param>
    /// <param name="NumberOf">The record's number.</param>
    /// <param name="StatusOf">The record's status.</param>
    /// <param name="IsLive">Whether Xero holds the record live.</param>
    public sealed record Calls<TWrite, T>(
        Func<TWrite, string, CancellationToken, Task<XeroApiResult<T>>> Replay,
        Func<string, CancellationToken, Task<XeroApiResult<T>>> Read,
        Func<T, string?> IdOf,
        Func<T, string?> NumberOf,
        Func<T, string?> StatusOf,
        Func<T, bool> IsLive)
        where TWrite : class
        where T : class;

    /// <summary>Recovers <paramref name="create"/>; a failed call (not a refusal) is returned as the push's result.</summary>
    public static async Task<(XeroPushResult? Failed, XeroRecoveredCreate<T>? Recovered)> RecoverAsync<TWrite, T>(
        XeroPurchasingCreateLog log, string tenantId, XeroDocumentRef document, XeroPurchasingSentCreate create, DateTimeOffset now,
        Calls<TWrite, T> calls, CancellationToken cancellationToken)
        where TWrite : class
        where T : class
    {
        string id;
        switch (XeroPurchasingOwnership.RecoveryFor(create, now))
        {
            case XeroPurchasingOwnership.RecoveryStep.Tombstoned:
                return (null, new(create, XeroRecovery.Gone));

            case XeroPurchasingOwnership.RecoveryStep.NoBody:
                return (null, new(create, XeroRecovery.Unrecoverable, Problem: XeroPurchasingOwnership.NoBodyProblem));

            case XeroPurchasingOwnership.RecoveryStep.KeyExpired:
                return (null, new(create, XeroRecovery.Unrecoverable, Problem: XeroPurchasingOwnership.KeyExpiredProblem));

            case XeroPurchasingOwnership.RecoveryStep.Replay:
            {
                if (create.BodyAs<TWrite>() is not { } body)
                    return (null, new(create, XeroRecovery.Unrecoverable, Problem: XeroPurchasingOwnership.NoBodyProblem));

                var replay = await calls.Replay(body, create.IdempotencyKey, cancellationToken).ConfigureAwait(false);
                if (replay.Outcome == ConnectorOutcome.Rejected)
                    return (null, new(create, XeroRecovery.Unrecoverable, Problem: XeroPurchasingMapper.Problem(replay)));
                if (replay.Outcome != ConnectorOutcome.Ok)
                    return (XeroPurchasingMapper.Failed(replay), null);
                if (calls.IdOf(replay.Value!) is not { Length: > 0 } answered)
                    return (null, new(create, XeroRecovery.Unrecoverable, Problem: "Xero's replayed answer carried no id"));

                // Recorded at once: from now on this create is only ever read back by its id.
                await log.RecordXeroIdAsync(tenantId, document, create.IdempotencyKey, answered, cancellationToken).ConfigureAwait(false);
                create = create with { XeroId = answered };
                id = answered;
                break;
            }

            default:
                id = create.XeroId!;
                break;
        }

        var read = await calls.Read(id, cancellationToken).ConfigureAwait(false);
        if (read.Outcome != ConnectorOutcome.Ok)
        {
            if (!read.NotFound)
                return (XeroPurchasingMapper.Failed(read), null);

            return (null, await TombstoneAsync<T>(log, tenantId, document, create, id, XeroPurchasingMapper.StatusDeleted, null, record: null, cancellationToken).ConfigureAwait(false));
        }

        var record = read.Value!;
        if (calls.IsLive(record))
            return (null, new(create, XeroRecovery.Live, record));

        return (null, await TombstoneAsync(
            log, tenantId, document, create, id, XeroPurchasingMapper.Word(calls.StatusOf(record)) ?? XeroPurchasingMapper.StatusDeleted, calls.NumberOf(record), record,
            cancellationToken).ConfigureAwait(false));
    }

    private static async Task<XeroRecoveredCreate<T>> TombstoneAsync<T>(
        XeroPurchasingCreateLog log, string tenantId, XeroDocumentRef document, XeroPurchasingSentCreate create, string id, string status, string? number, T? record,
        CancellationToken cancellationToken)
        where T : class
    {
        await log.RecordGoneAsync(tenantId, document, create.IdempotencyKey, id, status, number, cancellationToken).ConfigureAwait(false);
        var gone = create with { XeroId = id, GoneStatus = status, XeroNumber = string.IsNullOrWhiteSpace(number) ? create.XeroNumber : number.Trim() };
        return new XeroRecoveredCreate<T>(gone, XeroRecovery.Gone, record);
    }
}
