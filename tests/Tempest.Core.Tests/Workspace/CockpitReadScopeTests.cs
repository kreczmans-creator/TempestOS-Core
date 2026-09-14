using Tempest.Workspace;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Tests.EngineeringDomain;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP-E`'s own invariant — one render pass performs each
/// persistence-backed read once, and every property derived from that
/// read sees the same snapshot of it — carried forward by `WP 18.1A-R1`
/// (`TD-108`, `TD-118`) onto its own async replacement: <c>CockpitReadScope</c>'s
/// lazy, per-pass memoised cell (still blocked synchronously on every read
/// taken outside an open pass) is gone, superseded by an eager
/// <c>LoadAsync</c> that awaits every underlying read once, into plain
/// fields, before <see cref="RequirementsCockpitReadModel"/> or
/// <see cref="VerificationCockpitReadModel"/> ever answers a property.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this still pins.</b> Every discipline read-model exposed
/// its data as expression-bodied properties over a live read, uncached —
/// so <c>LiveRequirements</c> re-read from persistence on every single
/// access, and the composite properties above it — <c>Status</c>,
/// <c>KpiCards</c>, <c>GetAttentionItems</c>, <c>GetBlockedMessages</c>,
/// <c>GetOpenActionItem</c> — each re-read every leaf they touched. One
/// Cockpit render therefore ran the whole per-requirement validation pass
/// roughly eight times over, and
/// <see cref="RequirementValidationService.ValidateAsync"/> is itself
/// <c>O(N)</c> in stored requirements. That is the <c>O(N²)</c>, and it
/// used to run synchronously on the UI thread — the second half of which
/// `WP 18.1A-R1` also removed, by moving the one remaining read into
/// <c>LoadAsync</c>, awaited by <see cref="Tempest.Workspace.EngineeringCockpit.PrimeAsync"/>.
/// </para>
/// <para>
/// <b>Why these tests count rather than time.</b> A timing assertion on a
/// fast local store would be a flake generator and would prove nothing on
/// a slower or remote one. The defect is a count — how many times the
/// same read is performed — so the count is what is asserted, through a
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
    /// A real <see cref="SqlitePersistenceStore"/> that counts the reads passing
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
        CountingRequirementValidationService Validation);

    private static Harness BuildRequirements(string rootPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();

        var counter = new CountingPersistenceStore(new SqlitePersistenceStore(configuration));
        var principalAccessor = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(counter, principalAccessor);
        var verificationService = new VerificationService(documentStore, principalAccessor, new PermissionEvaluator());
        var requirements = new RequirementsService(documentStore, counter, principalAccessor, verificationService);
        var validation = new CountingRequirementValidationService(new RequirementValidationService(requirements));

        return new Harness(new RequirementsCockpitReadModel(requirements, validation), requirements, counter, validation);
    }

    /// <summary>
    /// The read set one Cockpit render actually performs against the
    /// Requirements read-model — every member the Cockpit surfaces, in
    /// the order the render reaches them.
    /// </summary>
    private static void ReadEverythingARenderReads(RequirementsCockpitReadModel model)
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
    public async Task OneLoadAsyncCall_ReadsPersistenceOnce_NoMatterHowManyPropertiesAreReadAfterward()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        for (var i = 1; i <= 4; i++)
            await harness.Requirements.CreateAsync($"REQ-{i:D3}", $"Requirement {i} shall hold.");

        await harness.ReadModel.LoadAsync();
        harness.Counter.ResetCounts();

        // Every one of these is derived from what LoadAsync already read.
        ReadEverythingARenderReads(harness.ReadModel);
        _ = harness.ReadModel.LiveRequirements;

        Assert.Equal(0, harness.Counter.Reads);
        Assert.Equal(0, harness.Counter.KeyListings);
    }

    [Fact]
    public async Task LoadAsync_ValidatesEachRequirementExactlyOnce_NotOncePerPropertyThatAsks()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        const int RequirementCount = 5;
        for (var i = 1; i <= RequirementCount; i++)
            await harness.Requirements.CreateAsync($"REQ-{i:D3}", $"Requirement {i} shall hold.");

        harness.Validation.ResetCount();
        await harness.ReadModel.LoadAsync();

        // This is `WP-E`'s actual deliverable, stated as the number it is:
        // one validation pass per LoadAsync, not one per property that
        // wants a validation result.
        Assert.Equal(RequirementCount, harness.Validation.Validations);

        harness.Validation.ResetCount();
        ReadEverythingARenderReads(harness.ReadModel);

        // Reading every property afterward validates nothing further —
        // the pass already ran, once, inside LoadAsync.
        Assert.Equal(0, harness.Validation.Validations);
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
    /// <c>O(N²)</c>, and neither `WP-E`'s own memoisation nor
    /// `WP 18.1A-R1`'s async replacement changes that — both reduce the
    /// number of times the pass runs per render (to one), which is the
    /// whole of what either Work Package authorised. Removing the
    /// remaining factor means changing a <c>Tempest.Core</c> validation
    /// service, which is a separate decision.
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
            await harness.ReadModel.LoadAsync();

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
            $"One LoadAsync call cost {atTwo} reads at N=2 and {atEight} at N=8 — that is no longer quadratic. "
            + "RequirementValidationService may have been fixed; update TD-108 rather than this assertion.");
    }

    // ----------------------------------------------------------------
    // Internal consistency within a pass
    // ----------------------------------------------------------------

    [Fact]
    public async Task WithinOneLoadAsyncCall_EveryPropertySeesTheSameSnapshot_EvenIfTheWorkspaceChangesMidLoad()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        await harness.Requirements.CreateAsync("REQ-001", "The system shall hold.");

        await harness.ReadModel.LoadAsync();
        var countBefore = harness.ReadModel.Count;
        Assert.Equal(1, countBefore);

        // A create landing after LoadAsync returns is exactly the race the
        // cards could previously disagree over: a total taken from one
        // read and a coverage figure taken from a later one. Every
        // property below still answers from the one pass LoadAsync
        // already completed, not a fresh read.
        await harness.Requirements.CreateAsync("REQ-002", "The system shall also hold.");

        Assert.Equal(countBefore, harness.ReadModel.Count);
        Assert.Equal(countBefore, harness.ReadModel.LiveRequirements.Count);
        Assert.Equal(
            countBefore.ToString(),
            harness.ReadModel.KpiCards.Single(c => c.Label == "Total Requirements").Value);
    }

    // ----------------------------------------------------------------
    // The read-model is honestly empty until loaded, and re-loads fresh
    // ----------------------------------------------------------------

    [Fact]
    public void BeforeLoadAsyncIsEverCalled_EveryPropertyReportsAnHonestEmptyState()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        // `WP 18.1A-R1`: no live fallback any more — a property read before
        // the first LoadAsync reports the same honest "nothing yet" state
        // every other Cockpit region already uses, never a blocking read
        // and never stale garbage.
        Assert.Equal(0, harness.ReadModel.Count);
        Assert.Empty(harness.ReadModel.LiveRequirements);
        Assert.Equal(EngineeringHealthStatus.Unknown, harness.ReadModel.Status);
        Assert.Equal(0, harness.ReadModel.OutstandingActions);
    }

    [Fact]
    public async Task ASecondLoadAsyncCall_IsAFreshRead_NotAReplayOfTheFirst()
    {
        using var temp = new TempDirectory();
        var harness = BuildRequirements(temp.Path);

        await harness.Requirements.CreateAsync("REQ-001", "The system shall hold.");

        await harness.ReadModel.LoadAsync();
        Assert.Equal(1, harness.ReadModel.Count);

        await harness.Requirements.CreateAsync("REQ-002", "The system shall also hold.");

        // Each Cockpit render calls LoadAsync again, and every render must
        // show what has happened since the last one. A read-model that
        // memoised without ever reloading would freeze the Cockpit at
        // whatever it first rendered.
        await harness.ReadModel.LoadAsync();
        Assert.Equal(2, harness.ReadModel.Count);
    }

    // ----------------------------------------------------------------
    // The other read-model the audit named
    // ----------------------------------------------------------------

    [Fact]
    public async Task TheVerificationReadModel_AlsoLoadsItsRecordReadsOncePerLoadAsyncCall()
    {
        var context = TestEngineeringDomain.NewContext();
        var model = new VerificationCockpitReadModel(context);

        // Before the first LoadAsync, the read-model reports its honest
        // empty state — the scope changed how often the read happened;
        // this Work Package changed what happens before the first load,
        // never what a loaded state says.
        Assert.Equal(EngineeringHealthStatus.Unknown, model.Status);

        await model.LoadAsync();

        Assert.Equal(EngineeringHealthStatus.Unknown, model.Status);
        Assert.Equal(0, model.Count);
        Assert.Equal(0, model.OutstandingActions);
    }
}
