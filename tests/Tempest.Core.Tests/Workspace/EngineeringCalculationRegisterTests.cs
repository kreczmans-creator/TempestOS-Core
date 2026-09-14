using Tempest.Workspace.Engineering;
using Tempest.Workspace;
using Tempest.Workspace.Calculations;
using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Logging;
using Tempest.Core.Tests.Persistence;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// The two governance answers the Engineering Calculations workspace's own
/// naming, renaming and retirement rest on — pinned here so a later change
/// to either has to be a deliberate one.
/// </summary>
/// <remarks>
/// <para>
/// The behaviour itself is proved through the real Desktop shell, in
/// <c>Tempest.Desktop.Tests.EngineeringCalculationLifecycleTests</c>. What
/// is pinned here is narrower and belongs at this layer: that the platform's
/// canonical lifecycle really does forbid the transition the workspace would
/// otherwise have used, and that a retirement is never a soft delete.
/// </para>
/// <para>
/// Composed exactly as <see cref="CalculationsWorkspaceIntegrationTests"/>
/// composes it — a real running host and the real
/// <see cref="CalculationsWorkspaceRegistration.Register"/> call — because a
/// register that dispatches governed commands is only worth testing against
/// the handlers the product actually registers.
/// </para>
/// </remarks>
public class EngineeringCalculationRegisterTests
{
    [Fact]
    public void TheCanonicalLifecycle_ForbidsArchivingSomethingThatIsStillDraft()
    {
        var table = new LifecycleTransitionTable();

        // This is the constraint that decides how a freshly recorded
        // calculation leaves the active list. Archived is the state the
        // workspace would prefer, and it is simply not reachable from where
        // a new object starts — it is reached only through Released, then
        // Superseded or Obsolete.
        Assert.False(table.IsPermitted(LifecycleState.Draft, LifecycleState.Archived));
        Assert.True(table.IsPermitted(LifecycleState.Superseded, LifecycleState.Archived));
        Assert.True(table.IsPermitted(LifecycleState.Obsolete, LifecycleState.Archived));

        // Cancelled is the one retained terminal state a Draft object can
        // reach, which is why retirement uses it there.
        Assert.True(table.IsPermitted(LifecycleState.Draft, LifecycleState.Cancelled));

        // Both are states this workspace counts as "out of the active list",
        // and neither is a deletion.
        Assert.True(EngineeringCalculationRegister.IsRetired(LifecycleState.Archived));
        Assert.True(EngineeringCalculationRegister.IsRetired(LifecycleState.Cancelled));
        Assert.False(EngineeringCalculationRegister.IsRetired(LifecycleState.Draft));
    }

    [Fact]
    public async Task RetiringACalculation_LeavesItHeldAndUndeleted_AndRetiringItTwiceIsRefused()
    {
        using var temp = new TempDirectory();
        var (register, domain, host) = await StartAsync(temp.Path);

        // A real executed record, because naming links the governed object
        // to the record's own document and the store refuses a link to a
        // document that does not exist — which is the correct refusal, and
        // is why the product only ever names a calculation that has run.
        var recordId = await ExecuteOneAsync(host);
        var named = await register.NameAsync(recordId, "Bracket check — to be retired");

        Assert.Equal("Bracket check — to be retired", named.DisplayName);
        Assert.Equal(recordId, named.RecordId);
        Assert.False(named.IsRetired);

        var retired = await register.RetireAsync(named.ObjectId);
        Assert.True(retired.Succeeded);
        Assert.Contains("Nothing was deleted", retired.Message, StringComparison.Ordinal);

        // Still held, still not deleted — a retirement is a lifecycle
        // transition, never IDeletable.DeleteAsync, which the platform has
        // no undo for anywhere.
        var subject = await domain.Repository.FindAsync(named.ObjectId);
        Assert.NotNull(subject);
        Assert.False(((IDeletable)subject!).IsDeleted);
        Assert.True(EngineeringCalculationRegister.IsRetired(((IHasLifecycle)subject!).Status));

        // Still listed, and still carrying the record it produced.
        var listed = (await register.ListAsync()).Single(n => n.ObjectId == named.ObjectId);
        Assert.True(listed.IsRetired);
        Assert.Equal(recordId, listed.RecordId);

        // And retiring it again is refused rather than repeated.
        var again = await register.RetireAsync(named.ObjectId);
        Assert.False(again.Succeeded);
        Assert.Contains("already", again.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenamingChangesTheDisplayNameOnly_AndABlankNameIsRefused()
    {
        using var temp = new TempDirectory();
        var (register, domain, host) = await StartAsync(temp.Path);

        var recordId = await ExecuteOneAsync(host);
        var named = await register.NameAsync(recordId, "Bracket check — first name");

        var before = await domain.Repository.FindAsync(named.ObjectId);
        var revisionsBefore = before!.CurrentRevisionNumber;
        var linksBefore = await ((IHasRelationships)before!).GetRelationshipsAsync();

        var blank = await register.RenameAsync(named.ObjectId, "   ");
        Assert.False(blank.Succeeded);
        Assert.Equal("Bracket check — first name", ((IHasBusinessIdentifier)before!).DisplayName);

        var renamed = await register.RenameAsync(named.ObjectId, "Bracket check — second name");
        Assert.True(renamed.Succeeded);

        var after = await domain.Repository.FindAsync(named.ObjectId);
        Assert.Equal(named.ObjectId, after!.Id);
        Assert.Equal("Bracket check — second name", ((IHasBusinessIdentifier)after!).DisplayName);
        Assert.Equal(revisionsBefore, after.CurrentRevisionNumber);
        Assert.Equal(LifecycleState.Draft, ((IHasLifecycle)after!).Status);

        var linksAfter = await ((IHasRelationships)after!).GetRelationshipsAsync();
        Assert.Equal(linksBefore.Count, linksAfter.Count);
        Assert.Contains(linksAfter, l => l.TargetId == recordId);
    }

    [Fact]
    public async Task ANamingThatCreatesTheObjectButCannotLinkIt_SaysExactlyThat_RatherThanSayingItWasNotNamed()
    {
        using var temp = new TempDirectory();
        var (register, domain, _) = await StartAsync(temp.Path);

        // A record Id with no document behind it is the one reliable way to
        // make the link step fail while the create step succeeds — which is
        // precisely the partial state the product must report accurately.
        var absentRecordId = Guid.NewGuid();

        var thrown = await Assert.ThrowsAsync<EngineeringCalculationNamingException>(
            () => register.NameAsync(absentRecordId, "Bracket check — half named"));

        // It names the object that really was created, the record it could
        // not be linked to, and the name that object now carries.
        Assert.Equal(absentRecordId, thrown.RecordId);
        Assert.Equal("Bracket check — half named", thrown.DisplayName);
        Assert.NotEqual(Guid.Empty, thrown.CalculationObjectId);
        Assert.IsType<EngineeringDocumentNotFoundException>(thrown.InnerException);

        // The message must not claim the calculation was not named — it
        // was, durably, which is the whole point of reporting this
        // separately.
        Assert.Contains("was created and named", thrown.Message, StringComparison.Ordinal);

        // The half-created object is withdrawn rather than left behind.
        // Withdrawing residue from a creation that failed is the one place
        // this type uses a soft delete, and it is the opposite of the case
        // it refuses to use one for: this is not anybody's engineering
        // work, and it has existed for the length of one failed call.
        Assert.True(thrown.WasWithdrawn, "The half-created object should have been withdrawn.");
        Assert.Contains("nothing was left behind", thrown.Message, StringComparison.Ordinal);

        var withdrawn = await domain.Repository.FindAsync(thrown.CalculationObjectId);
        Assert.NotNull(withdrawn);
        Assert.True(((IDeletable)withdrawn!).IsDeleted);

        // So it is in no read model: not this register's list, and not
        // findable by the record it was never linked to.
        Assert.DoesNotContain(await register.ListAsync(), n => n.ObjectId == thrown.CalculationObjectId);
        Assert.Null(await register.FindByRecordAsync(absentRecordId));
    }

    /// <summary>
    /// `TD-170`: a failure of the compensating withdrawal itself, on top of
    /// the link failure it was compensating for, is surfaced completely —
    /// both errors reach the caller, and the register logs it — rather than
    /// collapsing into the same "could not be withdrawn either" case a
    /// withdrawal that simply found nothing <see cref="IDeletable"/> would.
    /// </summary>
    /// <remarks>
    /// A hand-built rig, not <see cref="StartAsync"/>'s real SQLite-backed
    /// composition: this needs to fail one specific, later durable write
    /// (the compensating soft delete) while an earlier one (the
    /// calculation's own creation) still lands, which the real composition
    /// offers no seam for. <see cref="NthCommitFailingPersistenceStore"/>
    /// fails the second transaction whose own body completes — the link
    /// attempt's own transaction body never completes at all, because it
    /// throws before writing anything, so it consumes no slot in that
    /// count; the creation's is the first, and the compensating delete's is
    /// the second.
    /// </remarks>
    [Fact]
    public async Task ANamingThatFailsToLinkAndFailsToCompensate_SurfacesBothFailures_AndLogsIt()
    {
        var rawStore = new InMemoryQueryablePersistenceStore();
        var principal = new CurrentPrincipalAccessor();
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);
        var documentStore = new EngineeringDocumentStore(rawStore, principal);

        var domain = new EngineeringDomainContext(
            new NthCommitFailingPersistenceStore(rawStore, failOnBodyNumber: 2),
            documentStore,
            repository,
            relationships,
            new LifecycleTransitionTable(),
            new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository),
            principal,
            new EngineeringObjectStateStore(rawStore),
            new AttachmentContentStore(rawStore));

        var logger = new RecordingLogger();
        var register = new EngineeringCalculationRegister(domain, new NeverDispatchedCommandDispatcher(), projects: null, logger);

        // An absent record Id fails the link on its own, exactly as the
        // test above — no store fault is needed for that half; the fault
        // injector above is reserved entirely for the compensation.
        var absentRecordId = Guid.NewGuid();

        var thrown = await Assert.ThrowsAsync<EngineeringCalculationNamingException>(
            () => register.NameAsync(absentRecordId, "Bracket check — both fail"));

        Assert.False(thrown.WasWithdrawn, "The compensating withdrawal was made to fail and must not be reported as having succeeded.");
        Assert.NotNull(thrown.CompensationFailed);
        Assert.Same(thrown.InnerException, thrown.CompensationFailed!.LinkFailure);
        Assert.IsType<PersistenceStoreUnavailableException>(thrown.CompensationFailed.WithdrawalFailure);
        Assert.Same(thrown.CompensationFailed.WithdrawalFailure, thrown.CompensationFailed.InnerException);
        Assert.Contains("compensating withdrawal", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("also failed", thrown.Message, StringComparison.Ordinal);

        // The register itself records the double failure - the exception
        // reaching the caller is not the only trail (`TD-170`).
        Assert.Contains(
            logger.Messages,
            m => m.Contains("compensating withdrawal", StringComparison.Ordinal) && m.Contains("also failed", StringComparison.Ordinal));

        // The half-created object really is still there, undeleted — a
        // failed compensation must leave it exactly where it was.
        var stillThere = await domain.Repository.FindAsync(thrown.CalculationObjectId);
        Assert.NotNull(stillThere);
        Assert.False(((IDeletable)stillThere!).IsDeleted);
    }

    [Fact]
    public async Task ACalculationThatReachedSupersededRetiresToArchived_NotToCancelled()
    {
        using var temp = new TempDirectory();
        var (register, domain, host) = await StartAsync(temp.Path);

        var named = await register.NameAsync(await ExecuteOneAsync(host), "Bracket check — issued then superseded");
        var subject = (IHasLifecycle)(await domain.Repository.FindAsync(named.ObjectId))!;

        // Walk it along the platform's own permitted route to Superseded,
        // which is the only way Archived becomes reachable at all.
        foreach (var step in new[] { LifecycleState.InReview, LifecycleState.Approved, LifecycleState.Released, LifecycleState.Superseded })
            await subject.TransitionAsync(step);

        Assert.Equal(LifecycleState.Superseded, subject.Status);

        var described = await register.DescribeRetirementAsync(named.ObjectId);
        Assert.True(described.Succeeded);
        Assert.Contains("Archived", described.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Cancelled", described.Message, StringComparison.Ordinal);

        // Describing it changed nothing.
        Assert.Equal(LifecycleState.Superseded, subject.Status);

        var retired = await register.RetireAsync(named.ObjectId);
        Assert.True(retired.Succeeded);
        Assert.Equal(LifecycleState.Archived, subject.Status);

        // Archived is preferred over Cancelled wherever it is reachable —
        // reversing that preference would put a superseded calculation in
        // the wrong state, and this is what would catch it.
        Assert.Contains("Retired from the active list as Archived", retired.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Executes one of the product's own registered calculations and
    /// returns the record it produced — a real record, with a real
    /// document behind it.
    /// </summary>
    /// <remarks>
    /// The bolt shear capacity check is used rather than the bracket
    /// section check because it needs no released reference material: what
    /// this file is testing is naming, renaming and retirement, and which
    /// calculation produced the record is immaterial to all three.
    /// </remarks>
    private static async Task<Guid> ExecuteOneAsync(ITempestHost host)
    {
        var engine = (ICalculationEngine)host.Services!.GetService(typeof(ICalculationEngine));

        var record = await engine.ExecuteAsync<BoltShearCapacityInput, BoltShearCapacityResult>(
            BoltShearCapacityCalculationDefinition.Id,
            new BoltShearCapacityInput(
                new Quantity<Length>(10, LengthUnits.Millimetre),
                new Quantity<Pressure>(330, PressureUnits.Megapascal),
                ShearPlanes: 1,
                SafetyFactor: 1.5));

        return record.Id;
    }

    private static async Task<(EngineeringCalculationRegister Register, EngineeringDomainContext Domain, ITempestHost Host)> StartAsync(string rootPath)
    {
        var host = new TempestHostBuilder([])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        await manager.StartAsync();

        var services = host.Services!;
        var domain = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var dispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));

        CalculationsWorkspaceRegistration.Register(
            manager,
            domain,
            (ICalculationEngine)services.GetService(typeof(ICalculationEngine)),
            dispatcher,
            (ICommandRegistry)services.GetService(typeof(ICommandRegistry)));

        // No project context: a calculation named outside a project is
        // top-level, which is the honest shape for this layer's tests.
        return (new EngineeringCalculationRegister(domain, dispatcher), domain, host);
    }

    /// <summary>
    /// A dispatcher that is never actually reached: <see cref="EngineeringCalculationRegister.NameAsync"/>
    /// creates and links directly (see that type's own remarks on why), and
    /// this file's own `TD-170` rig never calls
    /// <see cref="EngineeringCalculationRegister.RenameAsync"/> or
    /// <see cref="EngineeringCalculationRegister.RetireAsync"/> — the two
    /// operations that would. Throwing rather than silently no-opping means
    /// a test that starts relying on this dispatcher by accident fails
    /// loudly instead of passing on an unintended path.
    /// </summary>
    private sealed class NeverDispatchedCommandDispatcher : ICommandDispatcher
    {
        public void RegisterHandler<TCommand>(ICommandHandler<TCommand> handler) where TCommand : ICommand =>
            throw new NotSupportedException("Not used by this test's own rig.");

        public Task<CommandResult> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : ICommand =>
            throw new NotSupportedException("Not used by this test's own rig.");
    }

    /// <summary>
    /// Fails the transaction whose own body is the <paramref name="failOnBodyNumber"/>th
    /// to complete, counting from 1 — every other transaction commits
    /// normally (`TD-170`). Lets a test put one specific, later write under
    /// fault injection without also failing the writes that must
    /// legitimately succeed before it: a transaction whose own body throws
    /// before finishing — the link attempt against an absent record, here —
    /// never completes at all, so it is not counted.
    /// </summary>
    private sealed class NthCommitFailingPersistenceStore(IQueryablePersistenceStore inner, int failOnBodyNumber) : IQueryablePersistenceStore
    {
        private int _bodiesCompleted;

        public long CurrentSequence => inner.CurrentSequence;

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, string keyPrefix, CancellationToken cancellationToken = default) =>
            inner.ListKeysAsync(collection, keyPrefix, cancellationToken);

        public Task<IReadOnlyList<KeyValuePair<string, string>>> ReadAllAsync(string collection, CancellationToken cancellationToken = default) =>
            inner.ReadAllAsync(collection, cancellationToken);

        public Task<IReadOnlyDictionary<string, string?>> ReadManyAsync(
            string collection, IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
            inner.ReadManyAsync(collection, keys, cancellationToken);

        public Task ExecuteInTransactionAsync(
            Func<IPersistenceTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
            inner.ExecuteInTransactionAsync(
                async (transaction, token) =>
                {
                    await work(transaction, token).ConfigureAwait(false);

                    if (Interlocked.Increment(ref _bodiesCompleted) == failOnBodyNumber)
                        throw new PersistenceStoreUnavailableException(
                            $"Injected commit failure: transaction body #{failOnBodyNumber} completed but the commit did not land (test double).");
                },
                cancellationToken);

        public Task<T> ExecuteInReadTransactionAsync<T>(
            Func<IPersistenceReadTransaction, CancellationToken, Task<T>> read, CancellationToken cancellationToken = default) =>
            inner.ExecuteInReadTransactionAsync(read, cancellationToken);

        public Task<IReadOnlyList<SearchHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
            inner.SearchAsync(query, limit, cancellationToken);

        public Task<bool> IsSearchIndexEmptyAsync(CancellationToken cancellationToken = default) =>
            inner.IsSearchIndexEmptyAsync(cancellationToken);
    }
}
