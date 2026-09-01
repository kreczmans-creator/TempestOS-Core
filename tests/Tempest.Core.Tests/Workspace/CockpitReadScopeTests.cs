using Tempest.App.Workspace;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP-E` — the Engineering Cockpit's own per-refresh read scope
/// (<see cref="CockpitReadScope"/>): one render pass performs each
/// persistence-backed read once, and every property derived from that read
/// sees the same snapshot of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this pins.</b> Every discipline read-model exposed its
/// data as expression-bodied properties over a live read, uncached. So
/// <c>LiveRequirements</c> re-read from persistence on every single access,
/// and the composite properties above it — <c>Status</c>, <c>KpiCards</c>,
/// <c>GetAttentionItems</c>, <c>GetBlockedMessages</c>,
/// <c>GetOpenActionItem</c> — each re-read every leaf they touched. One
/// <c>CockpitView.Refresh()</c> therefore ran the whole per-requirement
/// validation pass roughly eight times over, and
/// <see cref="RequirementValidationService.ValidateAsync"/> is itself
/// <c>O(N)</c> in stored requirements. That is the <c>O(N²)</c>, performed
/// synchronously on the UI thread.
/// </para>
/// <para>
/// <b>Why these tests count rather than time.</b> A timing assertion on a
/// fast local store would be a flake generator and would prove nothing on
/// a slower or remote one. The defect is a count — how many times the same
/// read is performed — so the count is what is asserted, through a
/// counting decorator over the real <see cref="IPersistenceStore"/> the
/// production types actually use.
/// </para>
/// </remarks>
public class CockpitReadScopeTests
{
    // ----------------------------------------------------------------
    // The seam
    // ----------------------------------------------------------------

    /// <summary>
    /// A real <see cref="PersistenceStore"/> that counts the reads passing
    /// through it. Decorates rather than fakes: the requirements really are
    /// written to and read back from disk, so what is counted is the actual
    /// production read volume, not a mock's idea of it.
    /// </summary>
    private sealed class CountingPersistenceStore(IPersistenceStore inner) : IPersistenceStore
    {
        public int Reads { get; private set; }

        public int KeyListings { get; private set; }

        public void ResetCounts()
        {
            Reads = 0;
            KeyListings = 0;
        }

        public Task<string?> ReadAsync(string collection, string key, CancellationToken cancellationToken = default)
        {
            Reads++;
            return inner.ReadAsync(collection, key, cancellationToken);
        }

        public Task<IReadOnlyList<string>> ListKeysAsync(string collection, CancellationToken cancellationToken = default)
        {
            KeyListings++;
            return inner.ListKeysAsync(collection, cancellationToken);
        }

        public Task WriteAsync(string collection, string key, string value, CancellationToken cancellationToken = default) =>
            inner.WriteAsync(collection, key, value, cancellationToken);

        public Task DeleteAsync(string collection, string key, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(collection, key, cancellationToken);
    }

    /// <summary>
    /// Counts how many times the Cockpit asks for a requirement to be
    /// validated. This is the count the <c>O(N²)</c> was made of: the
    /// service beneath is itself <c>O(N)</c> in stored requirements, so a
    /// refresh that validated every requirement eight times over paid that
    /// eight times over too.
    /// </summary>
    private sealed class CountingRequirementValidationService(IRequirementValidationService inner) : IRequirementValidationService
    {
        public int Validations { get; private set; }

        public void ResetCount() => Validations = 0;

        public Task<IValidationResult> ValidateAsync(Guid requirementId, CancellationToken cancellationToken = default)
        {
            Validations++;
            return inner.ValidateAsync(requirementId, cancellationToken);
        }
    }

    private sealed record Harness(
        RequirementsCockpitReadModel ReadModel,
        IRequirementsService Requirements,
        CountingPersistenceStore Counter,
        CountingRequirementValidationService Validation,
        CockpitReadScope Scope);

    private static Harness BuildRequirements(string rootPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();

        var counter = new CountingPersistenceStore(new PersistenceStore(configuration));
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(counter, principalAccessor);
        var verificationService = new VerificationService(documentStore, principalAccessor, new PermissionEvaluator());
        var requirements = new RequirementsService(documentStore, counter, principalAccessor, verificationService);
        var validation = new CountingRequirementValidationService(new RequirementValidationService(requirements));

        var scope = new CockpitReadScope();
        return new Harness(new RequirementsCockpitReadModel(requirements, validation, scope), requirements, counter, validation, scope);
    }

    /// <summary>
    /// The read set one <c>CockpitView.Refresh()</c> actually performs
    /// against the Requirements read-model — every member the Cockpit
    /// surfaces, in the order the render reaches them.
    /// </summary>
    private static void ReadEverythingARefreshReads(RequirementsCockpitReadModel model)
    {
        _ = model.Status;
        _ = model.KpiCards;
        _ = model.GetAttentionItems();
        _ = model.GetBlockedMessages();
        _ = model.GetOpenActionItem();
        _ = model.Count;
        _ = model.InReviewCount;
        _ = model.OutstandingActions;
    }

    // ----------------------------------------------------------------
    // Read-once
    // ----------------------------------------------------------------

    [Fact]
    public async Task AFullRefreshInsideAScope_ReadsPersistenceDramaticallyFewerTimes_ThanTheSameRefreshWithout()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        for (var i = 1; i <= 4; i++)
            await harness.Requirements.CreateAsync($"REQ-{i:D3}", $"Requirement {i} shall hold.");

        harness.Counter.ResetCounts();
        ReadEverythingARefreshReads(harness.ReadModel);
        var withoutScope = harness.Counter.Reads;

        harness.Counter.ResetCounts();
        using (harness.Scope.Begin())
        {
            ReadEverythingARefreshReads(harness.ReadModel);
        }

        var withScope = harness.Counter.Reads;

        // Both numbers are real reads of a real store. The scope does not
        // make the refresh cheap — it makes it linear: the underlying reads
        // happen once for the pass instead of once per property that needs
        // them.
        Assert.True(
            withScope * 4 < withoutScope,
            $"A scoped refresh performed {withScope} persistence reads against {withoutScope} unscoped — "
            + "expected at least a fourfold reduction. The per-refresh memoisation is not taking effect.");
    }

    [Fact]
    public async Task InsideAScope_TheSameReadIsNeverPerformedTwice()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        await harness.Requirements.CreateAsync("REQ-001", "The system shall hold.");
        await harness.Requirements.CreateAsync("REQ-002", "The system shall also hold.");

        using (harness.Scope.Begin())
        {
            _ = harness.ReadModel.LiveRequirements;
            harness.Counter.ResetCounts();

            // Every one of these is derived from LiveRequirements, which the
            // line above has already read for this pass.
            _ = harness.ReadModel.LiveRequirements;
            _ = harness.ReadModel.Count;
            _ = harness.ReadModel.InReviewCount;

            Assert.Equal(0, harness.Counter.Reads);
            Assert.Equal(0, harness.Counter.KeyListings);
        }
    }

    [Fact]
    public async Task InsideAScope_TheValidationPassRunsOnce_NoMatterHowManyPropertiesDependOnIt()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        for (var i = 1; i <= 3; i++)
            await harness.Requirements.CreateAsync($"REQ-{i:D3}", $"Requirement {i} shall hold.");

        using (harness.Scope.Begin())
        {
            // KpiCards is the widest member: it touches all three leaves —
            // the requirement list, the validation pass, and the per-
            // requirement relationships behind the two coverage figures.
            _ = harness.ReadModel.KpiCards;
            harness.Counter.ResetCounts();

            // Every other member the Cockpit surfaces is derived from those
            // same three leaves. Before `WP-E` each of these re-read them.
            _ = harness.ReadModel.Status;
            _ = harness.ReadModel.OutstandingActions;
            _ = harness.ReadModel.KpiCards;
            _ = harness.ReadModel.GetAttentionItems();
            _ = harness.ReadModel.GetBlockedMessages();
            _ = harness.ReadModel.GetOpenActionItem();

            Assert.Equal(0, harness.Counter.Reads);
        }
    }

    [Fact]
    public async Task AFullRefresh_ValidatesEachRequirementExactlyOnce_NotOncePerPropertyThatAsks()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        const int RequirementCount = 5;
        for (var i = 1; i <= RequirementCount; i++)
            await harness.Requirements.CreateAsync($"REQ-{i:D3}", $"Requirement {i} shall hold.");

        harness.Validation.ResetCount();
        ReadEverythingARefreshReads(harness.ReadModel);
        var withoutScope = harness.Validation.Validations;

        harness.Validation.ResetCount();
        using (harness.Scope.Begin())
        {
            ReadEverythingARefreshReads(harness.ReadModel);
        }

        var withScope = harness.Validation.Validations;

        // This is `WP-E`'s actual deliverable, stated as the number it is:
        // one validation pass per refresh, not one per property that wants
        // a validation result. The unscoped figure is what shipped before.
        Assert.Equal(RequirementCount, withScope);
        Assert.True(
            withoutScope >= RequirementCount * 6,
            $"Expected the unscoped refresh to re-validate repeatedly, but it validated {withoutScope} times "
            + $"for {RequirementCount} requirements — this test no longer describes the defect it was written for.");
    }

    /// <summary>
    /// The residual cost `WP-E` deliberately did not remove, pinned so it
    /// is a recorded fact rather than a surprise later (`TD-108`).
    /// </summary>
    /// <remarks>
    /// <see cref="RequirementValidationService.ValidateAsync"/> calls
    /// <see cref="IRequirementsService.ListAsync"/> for its duplicate-
    /// identifier check, so validating one requirement costs a read of
    /// every requirement. Validating all of them is therefore
    /// <c>O(N²)</c>, and memoising the Cockpit cannot change that — it
    /// reduces the number of times that pass runs per refresh from about
    /// eight to one, which is the whole of what this Work Package
    /// authorised. Removing the remaining factor means changing a
    /// <c>Tempest.Core</c> validation service, which is a separate
    /// decision.
    /// </remarks>
    [Fact]
    public async Task OneValidationPass_IsItselfQuadratic_WhichThisWorkPackageDeliberatelyDidNotChange()
    {
        using var temp = new TempDirectory();

        async Task<int> ReadsForOneValidationPassAsync(int requirementCount, string root)
        {
            var harness = BuildRequirements(root);

            for (var i = 1; i <= requirementCount; i++)
                await harness.Requirements.CreateAsync($"REQ-{i:D3}", $"Requirement {i} shall hold.");

            harness.Counter.ResetCounts();
            using (harness.Scope.Begin())
            {
                _ = harness.ReadModel.OutstandingActions;
            }

            return harness.Counter.Reads;
        }

        var atTwo = await ReadsForOneValidationPassAsync(2, Path.Combine(temp.Path, "two"));
        var atEight = await ReadsForOneValidationPassAsync(8, Path.Combine(temp.Path, "eight"));

        // Fourfold N, more than fourfold cost — the residual quadratic,
        // stated rather than assumed away. If this ever fails because the
        // growth became linear, the Core validation service was fixed and
        // `TD-108` should be re-read, not this test patched.
        Assert.True(
            atEight > atTwo * 4,
            $"One validation pass cost {atTwo} reads at N=2 and {atEight} at N=8 — that is no longer quadratic. "
            + "RequirementValidationService may have been fixed; update TD-108 rather than this assertion.");
    }

    // ----------------------------------------------------------------
    // Internal consistency within a pass
    // ----------------------------------------------------------------

    [Fact]
    public async Task WithinOnePass_EveryPropertySeesTheSameSnapshot_EvenIfTheWorkspaceChangesMidPass()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        await harness.Requirements.CreateAsync("REQ-001", "The system shall hold.");

        using (harness.Scope.Begin())
        {
            var countBefore = harness.ReadModel.Count;
            Assert.Equal(1, countBefore);

            // A create landing mid-render is exactly the race the cards
            // could previously disagree over: a total taken from one read
            // and a coverage figure taken from a later one.
            await harness.Requirements.CreateAsync("REQ-002", "The system shall also hold.");

            Assert.Equal(countBefore, harness.ReadModel.Count);
            Assert.Equal(countBefore, harness.ReadModel.LiveRequirements.Count);
            Assert.Equal(
                countBefore.ToString(),
                harness.ReadModel.KpiCards.Single(c => c.Label == "Total Requirements").Value);
        }
    }

    // ----------------------------------------------------------------
    // The read-model stays live — the memoisation is bounded by the pass
    // ----------------------------------------------------------------

    [Fact]
    public async Task OutsideAScope_EveryReadIsLive_SoTheReadModelIsNotSilentlyStale()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        await harness.Requirements.CreateAsync("REQ-001", "The system shall hold.");
        Assert.Equal(1, harness.ReadModel.Count);

        await harness.Requirements.CreateAsync("REQ-002", "The system shall also hold.");

        // This is the behaviour every existing caller and acceptance test
        // relies on, and the reason the memoisation is scoped rather than
        // cached: read a property after mutating the workspace and you see
        // the mutation.
        Assert.Equal(2, harness.ReadModel.Count);
    }

    [Fact]
    public async Task OnceAPassCloses_TheNextReadSeesWhatChangedDuringIt()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        await harness.Requirements.CreateAsync("REQ-001", "The system shall hold.");

        using (harness.Scope.Begin())
        {
            _ = harness.ReadModel.Count;
            await harness.Requirements.CreateAsync("REQ-002", "The system shall also hold.");
        }

        // Nothing is retained past the pass that read it.
        Assert.Equal(2, harness.ReadModel.Count);
    }

    [Fact]
    public async Task ASecondPass_IsAFreshRead_NotAReplayOfTheFirst()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        await harness.Requirements.CreateAsync("REQ-001", "The system shall hold.");

        using (harness.Scope.Begin())
        {
            Assert.Equal(1, harness.ReadModel.Count);
        }

        await harness.Requirements.CreateAsync("REQ-002", "The system shall also hold.");

        // Each CockpitView.Refresh() opens its own pass, and every refresh
        // must show what has happened since the last one. A scope that
        // memoised without ever clearing would freeze the Cockpit at
        // whatever it first rendered.
        using (harness.Scope.Begin())
        {
            Assert.Equal(2, harness.ReadModel.Count);
        }
    }

    [Fact]
    public async Task ANestedPass_JoinsTheOpenOne_RatherThanDiscardingWhatTheCallerAboveAlreadyRead()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        await harness.Requirements.CreateAsync("REQ-001", "The system shall hold.");

        using (harness.Scope.Begin())
        {
            _ = harness.ReadModel.Count;

            using (harness.Scope.Begin())
            {
                harness.Counter.ResetCounts();
                _ = harness.ReadModel.Count;

                // A nested Begin that invalidated would make the inner read
                // pay again, and would break the outer pass's consistency.
                Assert.Equal(0, harness.Counter.Reads);
            }

            // Leaving the inner pass must not end the outer one.
            harness.Counter.ResetCounts();
            _ = harness.ReadModel.Count;
            Assert.Equal(0, harness.Counter.Reads);
        }
    }

    // ----------------------------------------------------------------
    // The other read-models the audit named
    // ----------------------------------------------------------------

    [Fact]
    public void TheVerificationReadModel_AlsoMemoisesItsRecordReadsPerPass()
    {
        var principalAccessor = new CurrentPrincipalAccessor();
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);

        var context = new EngineeringDomainContext(
            new InMemoryEngineeringDocumentStore(principalAccessor),
            repository,
            relationshipRepository,
            new LifecycleTransitionTable(),
            new ValidationRuleSet(),
            new EvidenceComposer(relationshipDiscovery, repository),
            principalAccessor);

        var scope = new CockpitReadScope();
        var model = new VerificationCockpitReadModel(context, scope);

        // With no Activity registered the read-model reports its honest
        // empty state, and does so identically inside and outside a pass —
        // the scope changes how often the read happens, never what it says.
        Assert.Equal(EngineeringHealthStatus.Unknown, model.Status);

        using (scope.Begin())
        {
            Assert.Equal(EngineeringHealthStatus.Unknown, model.Status);
            Assert.Equal(0, model.Count);
            Assert.Equal(0, model.OutstandingActions);
        }
    }
}
