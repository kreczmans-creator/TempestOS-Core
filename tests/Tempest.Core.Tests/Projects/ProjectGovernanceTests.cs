using Tempest.App.Projects;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// The three governance families — risks, issues and decisions: their own
/// status vocabularies, their project membership, and the register and
/// service the Risks surface is built on.
/// </summary>
/// <remarks>
/// <para>
/// Risk, Hazard, Issue and Decision were real, durable, rehydratable domain
/// types that nothing raised, scored, owned or closed. The Risks area was
/// the last project area still marked <c>Declared</c> that had genuine
/// domain objects sitting behind it.
/// </para>
/// <para>
/// These tests are about the decisions that shape the feature: that each
/// family gets its own state vocabulary mapped onto the canonical lifecycle
/// rather than borrowing the document lifecycle; that an accepted risk is
/// still live while a closed one is not; that a superseded decision is
/// genuinely terminal; and that all three belong to a project through
/// <see cref="ProjectMembership"/> rather than through a field of their own.
/// </para>
/// </remarks>
public sealed class ProjectGovernanceTests : IDisposable
{
    private static readonly DateTimeOffset Today = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly List<string> _fixtureRoots = [];

    /// <summary>
    /// Creates a <see cref="GovernanceFixture"/> and remembers its isolated
    /// persistence root for <see cref="Dispose"/> — the one path every test
    /// below reaches the fixture through, so no individual test needs its
    /// own cleanup. Closes the Core-side leak <c>TD-120</c>
    /// (Technical Debt Register.md) left open — see
    /// <see cref="ProjectFixtureRoot"/>.
    /// </summary>
    private async Task<GovernanceFixture> CreateFixtureAsync(Func<DateTimeOffset>? now = null)
    {
        var fixture = await GovernanceFixture.CreateAsync(now);
        _fixtureRoots.Add(fixture.Root);
        return fixture;
    }

    /// <summary>Deletes every persistence root this instance's own test created — xUnit constructs a fresh instance per test, so this runs once per test, not once per class.</summary>
    public void Dispose()
    {
        foreach (var root in _fixtureRoots)
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort: a locked file must not mask the test's own
                // result as a cleanup failure.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    // ================================================================
    // Each family's state vocabulary is its own, and maps to canonical
    // ================================================================

    [Fact]
    public void EveryRiskStatus_MapsToACanonicalLifecycleState()
    {
        foreach (var status in Enum.GetValues<RiskStatus>())
        {
            var descriptor = RiskStatuses.For(status);

            Assert.Equal(status, descriptor.Status);
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Name));
            Assert.IsAssignableFrom<IFamilySpecificState>(descriptor);
        }

        Assert.Equal(LifecycleState.Approved, RiskStatuses.For(RiskStatus.Accepted).CanonicalEquivalent);
        Assert.Equal(LifecycleState.Archived, RiskStatuses.For(RiskStatus.Closed).CanonicalEquivalent);
    }

    [Fact]
    public void EveryIssueStatus_MapsToACanonicalLifecycleState()
    {
        foreach (var status in Enum.GetValues<IssueStatus>())
            Assert.IsAssignableFrom<IFamilySpecificState>(IssueStatuses.For(status));

        Assert.Equal(LifecycleState.Released, IssueStatuses.For(IssueStatus.Resolved).CanonicalEquivalent);
        Assert.Equal(LifecycleState.Archived, IssueStatuses.For(IssueStatus.Closed).CanonicalEquivalent);
    }

    [Fact]
    public void EveryDecisionStatus_MapsToACanonicalLifecycleState()
    {
        foreach (var status in Enum.GetValues<DecisionStatus>())
            Assert.IsAssignableFrom<IFamilySpecificState>(DecisionStatuses.For(status));

        // The decision family maps almost exactly onto the canonical
        // lifecycle, and the mapping is used rather than worked around.
        Assert.Equal(LifecycleState.InReview, DecisionStatuses.For(DecisionStatus.Proposed).CanonicalEquivalent);
        Assert.Equal(LifecycleState.Approved, DecisionStatuses.For(DecisionStatus.Accepted).CanonicalEquivalent);
        Assert.Equal(LifecycleState.Superseded, DecisionStatuses.For(DecisionStatus.Superseded).CanonicalEquivalent);
    }

    [Fact]
    public void AnAcceptedRisk_IsStillLive_ButAClosedOneIsNot()
    {
        // The distinction the whole risk family turns on. An accepted risk
        // is one the team decided to carry, not one that went away —
        // collapsing the two would delete exactly the risks a reviewer most
        // needs to see.
        Assert.True(RiskStatuses.IsLive(RiskStatus.Accepted));
        Assert.True(RiskStatuses.IsLive(RiskStatus.Open));
        Assert.True(RiskStatuses.IsLive(RiskStatus.Mitigating));
        Assert.False(RiskStatuses.IsLive(RiskStatus.Closed));
    }

    [Fact]
    public void AResolvedIssue_StillCountsAsOpen_UntilItIsClosed()
    {
        // A fix nobody has confirmed is still somebody's problem.
        Assert.True(IssueStatuses.IsOpen(IssueStatus.Resolved));
        Assert.False(IssueStatuses.IsOpen(IssueStatus.Closed));
    }

    [Fact]
    public void ASupersededDecision_IsTerminal_UnlikeEveryOtherGovernanceState()
    {
        // The one genuinely terminal state across the three families. A
        // decision that was replaced is a matter of record; bringing it back
        // would rewrite what the project decided and when.
        Assert.Empty(DecisionStatusTransitions.GetPermittedTargets(DecisionStatus.Superseded));

        foreach (var target in Enum.GetValues<DecisionStatus>())
            Assert.False(DecisionStatusTransitions.IsPermitted(DecisionStatus.Superseded, target));

        // Everything else can still move somewhere.
        Assert.NotEmpty(RiskStatusTransitions.GetPermittedTargets(RiskStatus.Closed));
        Assert.NotEmpty(IssueStatusTransitions.GetPermittedTargets(IssueStatus.Closed));
    }

    [Fact]
    public void AClosedRiskAndAClosedIssue_CanBeReopened_BecauseBothRecur()
    {
        Assert.True(RiskStatusTransitions.IsPermitted(RiskStatus.Closed, RiskStatus.Open));
        Assert.True(IssueStatusTransitions.IsPermitted(IssueStatus.Closed, IssueStatus.Open));

        // Same-to-same is never permitted, mirroring the canonical table.
        Assert.False(RiskStatusTransitions.IsPermitted(RiskStatus.Open, RiskStatus.Open));
        Assert.False(IssueStatusTransitions.IsPermitted(IssueStatus.Open, IssueStatus.Open));
        Assert.False(DecisionStatusTransitions.IsPermitted(DecisionStatus.Proposed, DecisionStatus.Proposed));
    }

    [Fact]
    public void ADecisionCannotJumpStraightFromProposedToSuperseded()
    {
        // Superseding something nobody ever accepted is not a thing that
        // happens; it has to be decided before it can be replaced.
        Assert.False(DecisionStatusTransitions.IsPermitted(DecisionStatus.Proposed, DecisionStatus.Superseded));
        Assert.True(DecisionStatusTransitions.IsPermitted(DecisionStatus.Proposed, DecisionStatus.Accepted));
        Assert.True(DecisionStatusTransitions.IsPermitted(DecisionStatus.Accepted, DecisionStatus.Superseded));
    }

    [Fact]
    public async Task ARefusedTransition_ThrowsAndChangesNothing()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var decision = await fixture.Workflow.CreateDecisionAsync(project.Id, "DEC-001", "Use titanium", "Lighter for the same stiffness.");

        await fixture.Workflow.DecideAsync(decision.Id, DecisionStatus.Accepted);

        var refused = await Assert.ThrowsAsync<InvalidDecisionStatusTransitionException>(
            () => fixture.Workflow.DecideAsync(decision.Id, DecisionStatus.Rejected));

        Assert.Equal(DecisionStatus.Accepted, refused.From);
        Assert.Equal(DecisionStatus.Rejected, refused.To);

        // The refusal left the decision exactly where it was, and said what
        // it would have accepted.
        Assert.Equal(DecisionStatus.Accepted, decision.DecisionStatus);
        Assert.Contains("Superseded", refused.Message, StringComparison.Ordinal);
    }

    // ================================================================
    // Project membership is ProjectMembership's answer, transitively
    // ================================================================

    [Fact]
    public async Task AllThreeFamilies_JoinAProjectThroughTheParentChain_NotAField()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var assembly = await fixture.CreatePartAsync("ASM-1", "Turbopump", project.Id);
        var part = await fixture.CreatePartAsync("PRT-1", "Impeller", assembly.Id);

        // Raised three levels down the structure, exactly where a real one
        // would be raised.
        var risk = await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Cavitation");
        await ((IHasParent)risk).MoveAsync(part.Id);

        var issue = await fixture.Workflow.CreateIssueAsync(project.Id, "ISS-001", "Blade cracked in test");
        await ((IHasParent)issue).MoveAsync(part.Id);

        var decision = await fixture.Workflow.CreateDecisionAsync(project.Id, "DEC-001", "Re-machine the blades", "Cheaper than a new casting.");
        await ((IHasParent)decision).MoveAsync(part.Id);

        Assert.Equal(risk.Id, Assert.Single(await fixture.Register.ListRisksAsync(project.Id)).ObjectId);
        Assert.Equal(issue.Id, Assert.Single(await fixture.Register.ListIssuesAsync(project.Id)).ObjectId);
        Assert.Equal(decision.Id, Assert.Single(await fixture.Register.ListDecisionsAsync(project.Id)).ObjectId);
    }

    [Fact]
    public async Task OneProjectsGovernance_NeverLeaksIntoAnother()
    {
        var fixture = await CreateFixtureAsync();
        var apollo = await fixture.CreateProjectAsync("P-1", "Apollo");
        var gemini = await fixture.CreateProjectAsync("P-2", "Gemini");

        await fixture.Workflow.CreateRiskAsync(apollo.Id, "RSK-001", "Cavitation");
        await fixture.Workflow.CreateIssueAsync(gemini.Id, "ISS-001", "Leak on the test stand");

        Assert.Single(await fixture.Register.ListRisksAsync(apollo.Id));
        Assert.Empty(await fixture.Register.ListIssuesAsync(apollo.Id));

        Assert.Empty(await fixture.Register.ListRisksAsync(gemini.Id));
        Assert.Single(await fixture.Register.ListIssuesAsync(gemini.Id));
    }

    [Fact]
    public async Task AHazardAppearsOnTheRiskRegister_BecauseAHazardIsARisk()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Schedule slip");
        await fixture.Workflow.CreateRiskAsync(project.Id, "HAZ-001", "Stored energy in the accumulator", isHazard: true);

        var risks = await fixture.Register.ListRisksAsync(project.Id);

        // A safety risk that did not appear on the risk register would be
        // the most dangerous kind of omission.
        Assert.Equal(2, risks.Count);
        Assert.Contains(risks, r => r.Kind == CanonicalObjectKinds.Hazard);
        Assert.Contains(risks, r => r.Kind == CanonicalObjectKinds.Risk);
    }

    [Fact]
    public async Task NoGovernanceModelCarriesAProjectIdOfItsOwn()
    {
        // The rule this feature had to respect: membership is answered once,
        // by the parent chain. A ProjectId property on any of the three
        // would be a second, competing answer.
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var risk = await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Cavitation");
        var issue = await fixture.Workflow.CreateIssueAsync(project.Id, "ISS-001", "Cracked blade");
        var decision = await fixture.Workflow.CreateDecisionAsync(project.Id, "DEC-001", "Re-machine", "Cheaper.");

        foreach (var governanceObject in new object[] { risk, issue, decision })
        {
            Assert.Null(governanceObject.GetType().GetProperty("ProjectId"));

            var state = ((EngineeringObjectBase)governanceObject).CaptureState();
            Assert.DoesNotContain("ProjectId", state.TypeState.Keys, StringComparer.OrdinalIgnoreCase);
        }
    }

    // ================================================================
    // Create, edit, status, priority, ownership
    // ================================================================

    [Fact]
    public async Task ARaisedRisk_StartsOpenUnownedAndUnscored()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Cavitation");

        var entry = Assert.Single(await fixture.Register.ListRisksAsync(project.Id));

        Assert.Equal(RiskStatus.Open, entry.Status);
        Assert.True(entry.IsLive);
        Assert.True(entry.IsUnowned);
        Assert.False(entry.IsScored);
    }

    [Fact]
    public async Task ARiskScoredOnOneAxisOnly_IsStillUnscored()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var risk = await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Cavitation");

        await fixture.Workflow.ScoreRiskAsync(risk.Id, "Likely", null);

        // A risk scored on one axis cannot be ranked against any other, so
        // for a register's purposes it is unscored.
        Assert.False(Assert.Single(await fixture.Register.ListRisksAsync(project.Id)).IsScored);

        await fixture.Workflow.ScoreRiskAsync(risk.Id, "Likely", "Severe");

        var scored = Assert.Single(await fixture.Register.ListRisksAsync(project.Id));
        Assert.True(scored.IsScored);
        Assert.Equal("Likely", scored.Likelihood);
        Assert.Equal("Severe", scored.Severity);
    }

    [Fact]
    public async Task TakingOwnershipReadsThePrincipalBoundary_NotAnArgument()
    {
        var fixture = await CreateFixtureAsync();
        fixture.SignInAs("ada");

        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var risk = await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Cavitation");
        var issue = await fixture.Workflow.CreateIssueAsync(project.Id, "ISS-001", "Cracked blade");

        await fixture.Workflow.AssignRiskToCurrentPrincipalAsync(risk.Id);
        await fixture.Workflow.AssignIssueToCurrentPrincipalAsync(issue.Id);

        Assert.Equal("ada", Assert.Single(await fixture.Register.ListRisksAsync(project.Id)).OwnedByPrincipalId);
        Assert.Equal("ada", Assert.Single(await fixture.Register.ListIssuesAsync(project.Id)).AssignedToPrincipalId);
    }

    [Fact]
    public async Task ADecisionRecordsWhoDecidedAndWhen_OnlyAtTheMomentOfDeciding()
    {
        var fixture = await CreateFixtureAsync(() => Today);
        fixture.SignInAs("grace");

        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var decision = await fixture.Workflow.CreateDecisionAsync(project.Id, "DEC-001", "Use titanium", "Lighter for the same stiffness.");

        Assert.Null(decision.DecidedAt);
        Assert.Null(decision.DecidedByPrincipalId);

        await fixture.Workflow.DecideAsync(decision.Id, DecisionStatus.Accepted);

        Assert.Equal("grace", decision.DecidedByPrincipalId);
        Assert.Equal(Today, decision.DecidedAt);

        // Superseding later must not rewrite who took the original decision
        // — that record is the point of keeping a decision log at all.
        fixture.SignInAs("alan");
        await fixture.Workflow.DecideAsync(decision.Id, DecisionStatus.Superseded);

        Assert.Equal("grace", decision.DecidedByPrincipalId);
        Assert.Equal(Today, decision.DecidedAt);
    }

    [Fact]
    public async Task EditingRewritesTheTitleAndAddsARevision_RatherThanOverwritingHistory()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var risk = await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Cavitaton");

        var revisionBefore = risk.CurrentRevisionNumber;

        await fixture.Workflow.EditRiskAsync(risk.Id, "Cavitation", "Suction pressure margin is thin at low flow.");

        var entry = Assert.Single(await fixture.Register.ListRisksAsync(project.Id));
        Assert.Equal("Cavitation", entry.DisplayName);
        Assert.Contains("Suction pressure", entry.Description, StringComparison.Ordinal);

        // A rewritten description is a new revision, not an overwrite: what
        // a risk used to say is part of its history. Re-read rather than
        // trusting the local reference — ReviseAsync returns a new instance
        // by design, so the one captured above is deliberately stale.
        var reloaded = (EngineeringObjectBase)(await fixture.Domain.Repository.FindAsync(risk.Id))!;

        Assert.True(
            reloaded.CurrentRevisionNumber > revisionBefore,
            $"Editing did not add a revision (was {revisionBefore}, now {reloaded.CurrentRevisionNumber}).");
    }

    [Fact]
    public async Task AnOperationNamingTheWrongFamily_FailsWithAnActionableMessage()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var issue = await fixture.Workflow.CreateIssueAsync(project.Id, "ISS-001", "Cracked blade");

        // An issue is not a risk, and asking for it as one must say so
        // rather than returning nothing and looking like an empty register.
        var failure = await Assert.ThrowsAsync<GovernanceObjectNotFoundException>(
            () => fixture.Workflow.ChangeRiskStatusAsync(issue.Id, RiskStatus.Closed));

        Assert.Equal(issue.Id, failure.ObjectId);
        Assert.Equal("Risk", failure.ExpectedFamily);
    }

    // ================================================================
    // Ordering: what a reviewer must not miss comes first
    // ================================================================

    [Fact]
    public async Task TheRiskRegister_PutsLiveRisksFirst_AndUnscoredAboveScored()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var closed = await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Closed one");
        await fixture.Workflow.ChangeRiskStatusAsync(closed.Id, RiskStatus.Closed);

        var scored = await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-002", "Scored one");
        await fixture.Workflow.ScoreRiskAsync(scored.Id, "Low", "Low");

        var unscored = await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-003", "Unscored one");

        var risks = await fixture.Register.ListRisksAsync(project.Id);

        Assert.Equal(unscored.Id, risks[0].ObjectId);
        Assert.Equal(scored.Id, risks[1].ObjectId);

        // Closed risks sink rather than disappearing: a register that hides
        // what it closed cannot be audited.
        Assert.Equal(closed.Id, risks[2].ObjectId);
    }

    [Fact]
    public async Task TheDecisionLog_PutsUndecidedFirst()
    {
        var fixture = await CreateFixtureAsync(() => Today);
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var decided = await fixture.Workflow.CreateDecisionAsync(project.Id, "DEC-001", "Decided", "Because.");
        await fixture.Workflow.DecideAsync(decided.Id, DecisionStatus.Accepted);

        var proposed = await fixture.Workflow.CreateDecisionAsync(project.Id, "DEC-002", "Still open", "Pending review.");

        var decisions = await fixture.Register.ListDecisionsAsync(project.Id);

        Assert.Equal(proposed.Id, decisions[0].ObjectId);
        Assert.True(decisions[0].IsAwaitingDecision);
        Assert.True(decisions[1].IsInForce);
    }

    // ================================================================
    // The risk → issue link, read from the one place it is written
    // ================================================================

    [Fact]
    public async Task ARiskThatMaterialised_LinksToItsIssue_AndTheIssueReportsItBackwards()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var risk = await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Cavitation");
        var issue = await fixture.Workflow.CreateIssueAsync(project.Id, "ISS-001", "Impeller cavitated on test");

        await fixture.Workflow.RecordRiskRealisedAsync(risk.Id, issue.Id);

        Assert.Equal(issue.Id, Assert.Single(await fixture.Register.ListRisksAsync(project.Id)).RealisedAsIssueId);

        // The link is written once, by the risk. The issue side is found by
        // reading it backwards rather than storing it twice.
        Assert.Equal(risk.Id, Assert.Single(await fixture.Register.ListIssuesAsync(project.Id)).RaisedByRiskId);
    }

    [Fact]
    public async Task RecordingARiskAsRealisedBySomethingThatIsNotAnIssue_IsRefused()
    {
        var fixture = await CreateFixtureAsync();
        var project = await fixture.CreateProjectAsync("P-1", "Apollo");
        var risk = await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Cavitation");

        await Assert.ThrowsAsync<GovernanceObjectNotFoundException>(
            () => fixture.Workflow.RecordRiskRealisedAsync(risk.Id, project.Id));
    }

    // ================================================================
    // Every mutation reaches the store
    // ================================================================

    [Fact]
    public async Task EveryGovernanceMutation_ReachesTheStore_NotOnlyTheInstance()
    {
        // The lesson the Tasks work package learned the hard way: every
        // assertion that reads the object it just changed cannot tell
        // "saved" from "set on this instance". These read the store back.
        var fixture = await CreateFixtureAsync(() => Today);
        fixture.SignInAs("ada");

        var project = await fixture.CreateProjectAsync("P-1", "Apollo");

        var risk = await fixture.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Cavitation");
        await fixture.Workflow.ChangeRiskStatusAsync(risk.Id, RiskStatus.Mitigating);
        Assert.Equal(nameof(RiskStatus.Mitigating), (await fixture.LoadStoredStateAsync(risk.Id)).Type("RiskStatus"));

        await fixture.Workflow.ScoreRiskAsync(risk.Id, "Likely", "Severe");
        Assert.Equal("Likely", (await fixture.LoadStoredStateAsync(risk.Id)).Type("Likelihood"));
        Assert.Equal("Severe", (await fixture.LoadStoredStateAsync(risk.Id)).Type("Severity"));

        await fixture.Workflow.AssignRiskToCurrentPrincipalAsync(risk.Id);
        Assert.Equal("ada", (await fixture.LoadStoredStateAsync(risk.Id)).Type("OwnedByPrincipalId"));

        // Clearing an owner must reach the store too — otherwise the owner
        // comes back on the next launch after the user removed them.
        await fixture.Workflow.AssignRiskOwnerAsync(risk.Id, null);
        Assert.Null((await fixture.LoadStoredStateAsync(risk.Id)).Type("OwnedByPrincipalId"));

        var issue = await fixture.Workflow.CreateIssueAsync(project.Id, "ISS-001", "Cracked blade");
        await fixture.Workflow.ChangeIssueStatusAsync(issue.Id, IssueStatus.Investigating);
        Assert.Equal(nameof(IssueStatus.Investigating), (await fixture.LoadStoredStateAsync(issue.Id)).Type("IssueStatus"));

        await fixture.Workflow.SetIssuePriorityAsync(issue.Id, WorkPriority.Critical);
        Assert.Equal(nameof(WorkPriority.Critical), (await fixture.LoadStoredStateAsync(issue.Id)).Type("Priority"));

        var decision = await fixture.Workflow.CreateDecisionAsync(project.Id, "DEC-001", "Use titanium", "Lighter.");
        await fixture.Workflow.DecideAsync(decision.Id, DecisionStatus.Accepted);

        var stored = await fixture.LoadStoredStateAsync(decision.Id);
        Assert.Equal(nameof(DecisionStatus.Accepted), stored.Type("DecisionStatus"));
        Assert.Equal("ada", stored.Type("DecidedByPrincipalId"));
        Assert.Equal(Today, stored.TypeDate("DecidedAt"));

        await fixture.Workflow.SetDecisionRationaleAsync(decision.Id, "Lighter for the same stiffness.");
        Assert.Equal("Lighter for the same stiffness.", (await fixture.LoadStoredStateAsync(decision.Id)).Type("Rationale"));
    }

    [Fact]
    public void ARecordWrittenBeforeTheseFieldsExisted_ComesBackAtTheFamilyStartingState()
    {
        // `TD-85`'s established rule: a missing field comes back visibly
        // empty rather than failing the whole rehydration. A risk saved by
        // an older build has no RiskStatus key at all, and "a risk nobody
        // ever closed" is honestly Open.
        var legacy = new EngineeringObjectState(
            1, Guid.NewGuid(), CanonicalObjectKinds.Risk, "RSK-OLD", "Old risk",
            EngineeringObjectMetadata.Empty, LifecycleState.Draft, null, false,
            EngineeringObjectBomLineState.Default, [], [],
            new Dictionary<string, string?> { ["Likelihood"] = "Low" });

        Assert.Equal("Low", legacy.Type("Likelihood"));
        Assert.Null(legacy.Type("RiskStatus"));
        Assert.False(Enum.TryParse<RiskStatus>(legacy.Type("RiskStatus"), out _));
    }

    // ================================================================
    // TD-120 (Core side): the fixture's own persistence root is deleted
    // ================================================================

    [Fact]
    public async Task DisposingThisTestInstance_DeletesTheFixturesOwnPersistenceRoot()
    {
        // A real write, not just construction — `PersistenceStore` creates
        // its collection directories lazily on first write (see
        // `PersistenceStore.cs`), so the root only exists on disk once
        // something has actually been saved to it, exactly like every
        // other test in this file.
        var fixture = await CreateFixtureAsync();
        await fixture.CreateProjectAsync("P-CLEANUP", "Proves the root is removed");

        Assert.True(
            Directory.Exists(fixture.Root),
            $"Expected the fixture to have created its own persistence root at '{fixture.Root}'.");

        // xUnit calls this again itself once this test method returns
        // (a fresh instance per test, `IDisposable` on every test class) —
        // calling it here too is what makes this a real, direct proof
        // rather than an inference, and the loop's own `Directory.Exists`
        // guard makes the second call a no-op.
        Dispose();

        Assert.False(
            Directory.Exists(fixture.Root),
            $"Expected 'Dispose' to have deleted '{fixture.Root}' — the Core-side leak `TD-120` (Technical Debt Register.md) named.");
    }

    // ================================================================
    // Fixture
    // ================================================================

    private sealed class GovernanceFixture
    {
        private GovernanceFixture(
            EngineeringDomainContext domain, CurrentPrincipalAccessor principal,
            EngineeringObjectStateStore states, Func<DateTimeOffset> now, string root)
        {
            Domain = domain;
            Principal = principal;
            States = states;
            Register = new ProjectGovernanceRegister(domain);
            Workflow = new ProjectGovernanceService(domain, now);
            Root = root;
        }

        private EngineeringObjectStateStore States { get; }

        /// <summary>This fixture's own isolated persistence root — the caller's own <c>Dispose</c> deletes it (`TD-120` Core-side closure).</summary>
        public string Root { get; }

        public EngineeringDomainContext Domain { get; }

        public CurrentPrincipalAccessor Principal { get; }

        public IProjectGovernanceRegister Register { get; }

        public IProjectGovernanceService Workflow { get; }

        /// <summary>Reads back what the store actually holds for <paramref name="objectId"/>.</summary>
        public async Task<EngineeringObjectState> LoadStoredStateAsync(Guid objectId) =>
            await States.FindAsync(objectId) ?? throw new InvalidOperationException($"Nothing is persisted for '{objectId}'.");

        public static Task<GovernanceFixture> CreateAsync(Func<DateTimeOffset>? now = null)
        {
            var root = ProjectFixtureRoot.NewIsolatedRoot("governance");
            var configuration = new ConfigurationBuilder()
                .AddSource(new MemoryConfigurationSource(
                [
                    new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, root),
                ]))
                .Build();

            var store = new PersistenceStore(configuration);
            var principal = new CurrentPrincipalAccessor();
            var documents = new EngineeringDocumentStore(store, principal);
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);
            var states = new EngineeringObjectStateStore(store);

            var domain = new EngineeringDomainContext(
                documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal,
                states, new AttachmentContentStore(store));

            return Task.FromResult(new GovernanceFixture(domain, principal, states, now ?? (() => DateTimeOffset.UtcNow), root));
        }

        public void SignInAs(string identityId) =>
            Principal.SetCurrent(new PlatformPrincipal(new PlatformIdentity(identityId, identityId), []));

        public async Task<IProject> CreateProjectAsync(string identifier, string name)
        {
            var factory = new EngineeringObjectFactory<Project>(
                ProjectDirectory.ProjectKind, Domain,
                (d, r) => new Project(d, r, Domain, identifier, name, EngineeringObjectMetadata.Empty));

            return (Project)await factory.CreateAsync($"Project {identifier}.");
        }

        public async Task<Part> CreatePartAsync(string identifier, string name, Guid parentId)
        {
            var factory = new EngineeringObjectFactory<Part>(
                MechanicalObjectFactoryRegistry.Part, Domain,
                (d, r) => new Part(d, r, Domain, identifier, name, EngineeringObjectMetadata.Empty));

            var part = (Part)await factory.CreateAsync($"Part {identifier}.");
            await ((IHasParent)part).MoveAsync(parentId);
            return part;
        }
    }
}
