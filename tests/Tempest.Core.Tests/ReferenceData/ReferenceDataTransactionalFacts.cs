using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.Tests.ReferenceData;

/// <summary>
/// The `TD-158`/`TD-156` facts every <c>ReferenceDataCatalog&lt;TDefinition&gt;</c>
/// specialisation must hold, expressed once and run generically against
/// each library's own fixture (`WP 19.10K`) — so a regression in one
/// library's own test file cannot hide the shared base class being wrong,
/// and no library is left proving the old, non-transactional path.
/// </summary>
/// <remarks>
/// Orchestration only: what a fault does, and what a freed secondary key
/// permits, is identical for every library, but building a valid
/// definition and a domain's own designation lookup is not — each caller
/// supplies those as small delegates built from its own fixture.
/// </remarks>
internal static class ReferenceDataTransactionalFacts
{
    /// <summary>
    /// (a) A commit fault between the document write and the index write
    /// on <c>RegisterAsync</c> leaves nothing durable — not the document,
    /// not the primary index entry, not the secondary index entry.
    /// </summary>
    public static async Task RegisterAsync_FaultDuringCommit_LeavesNothingDurableAsync(
        Action<bool> setFailNextCommit,
        Func<Task> register,
        Func<Task<bool>> recordExists)
    {
        setFailNextCommit(true);

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => register());

        Assert.False(await recordExists(), "A commit fault during registration left a record reachable on reopen.");
    }

    /// <summary>
    /// (b) A commit fault on <c>SupersedeAsync</c> leaves the record being
    /// superseded exactly as it was — still in its prior (released) state,
    /// naming no replacement.
    /// </summary>
    public static async Task SupersedeAsync_FaultDuringCommit_LeavesTheOldRecordCurrentAsync<TDefinition>(
        Action<bool> setFailNextCommit,
        Func<Task> supersede,
        Func<Task<IReferenceRecord<TDefinition>?>> findOriginal,
        ReferenceValidationState expectedPriorState)
        where TDefinition : class
    {
        setFailNextCommit(true);

        await Assert.ThrowsAsync<PersistenceStoreUnavailableException>(() => supersede());

        var current = await findOriginal();
        Assert.NotNull(current);
        Assert.Equal(expectedPriorState, current!.ValidationState);
        Assert.Null(current.SupersededByRecordId);
    }

    /// <summary>
    /// (c) A superseded record keeps resolving by its own former secondary
    /// key until another record legitimately claims it — `TD-156` closes
    /// the defect that made that claim impossible (a key nominally held by
    /// a superseded record used to block every later record from ever
    /// taking it), not the record's own continued readability by that key,
    /// which this platform keeps deliberately (its history is itself
    /// engineering data).
    /// </summary>
    public static async Task SupersedeAsync_ThenTheReplacementClaimsTheFreedKeyAsync<TDefinition>(
        Func<Task> supersede,
        Func<Task> claimKey,
        Func<Task<IReferenceRecord<TDefinition>?>> findByKey,
        string originalRecordId,
        string replacementRecordId)
        where TDefinition : class
    {
        await supersede();

        var stillTheOriginal = await findByKey();
        Assert.NotNull(stillTheOriginal);
        Assert.Equal(originalRecordId, stillTheOriginal!.Id);

        await claimKey();

        var found = await findByKey();
        Assert.NotNull(found);
        Assert.Equal(replacementRecordId, found!.Id);
        Assert.NotEqual(originalRecordId, found.Id);
    }
}
