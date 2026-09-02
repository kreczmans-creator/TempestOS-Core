using Tempest.App.Projects;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Risks, issues and decisions survive a restart with everything the Risks
/// surface shows — status, priority, ownership, scores, the decision record
/// and project membership.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is separate from <see cref="ProjectGovernanceTests"/>.</b>
/// Those tests read the state store to prove a mutation was persisted, which
/// shows the write happened. This one proves the other half: that the write
/// can be <em>read back</em> by the production rehydration path (`TD-104`)
/// into an object with the same values. A field can be captured perfectly
/// and still be dropped by a rehydration constructor that forgets it, and
/// only a second lifetime catches that.
/// </para>
/// <para>
/// A "second lifetime" here is a genuinely new object graph over the same
/// <see cref="IPersistenceStore"/> — new repository, new documents, new
/// context, nothing carried over but the bytes on disk. That is what a
/// restart is.
/// </para>
/// </remarks>
public sealed class GovernanceRestartTests : IDisposable
{
    private static readonly DateTimeOffset Decided = new(2026, 8, 30, 9, 30, 0, TimeSpan.Zero);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "tempest-governance-restart-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task ARiskKeepsItsStatusScoreAndOwner_AcrossARestart()
    {
        Guid riskId;

        // ---- FIRST LIFETIME ------------------------------------------
        {
            var first = BuildLifetime();
            first.Principal.SetCurrent(new PlatformPrincipal(new PlatformIdentity("ada", "ada"), []));

            var project = await CreateProjectAsync(first, "P-1", "Apollo");
            var risk = await first.Workflow.CreateRiskAsync(project.Id, "RSK-001", "Cavitation");
            riskId = risk.Id;

            await first.Workflow.ChangeRiskStatusAsync(riskId, RiskStatus.Mitigating);
            await first.Workflow.ScoreRiskAsync(riskId, "Likely", "Severe");
            await first.Workflow.AssignRiskToCurrentPrincipalAsync(riskId);
        }

        // ---- SECOND LIFETIME -----------------------------------------
        {
            var second = BuildLifetime();
            var result = await RehydrateAsync(second);

            Assert.Empty(result.UnknownKinds);
            Assert.True(result.IsComplete);

            var recovered = Assert.IsType<Risk>(await second.Context.Repository.FindAsync(riskId));

            Assert.Equal(RiskStatus.Mitigating, recovered.RiskStatus);
            Assert.Equal("Likely", recovered.Likelihood);
            Assert.Equal("Severe", recovered.Severity);
            Assert.Equal("ada", recovered.OwnedByPrincipalId);
            Assert.True(recovered.IsLive);
        }
    }

    [Fact]
    public async Task AnIssueKeepsItsStatusPriorityAndAssignee_AcrossARestart()
    {
        Guid issueId;

        {
            var first = BuildLifetime();
            first.Principal.SetCurrent(new PlatformPrincipal(new PlatformIdentity("grace", "grace"), []));

            var project = await CreateProjectAsync(first, "P-1", "Apollo");
            var issue = await first.Workflow.CreateIssueAsync(project.Id, "ISS-001", "Cracked blade");
            issueId = issue.Id;

            await first.Workflow.ChangeIssueStatusAsync(issueId, IssueStatus.Resolved);
            await first.Workflow.SetIssuePriorityAsync(issueId, WorkPriority.Critical);
            await first.Workflow.AssignIssueToCurrentPrincipalAsync(issueId);
        }

        {
            var second = BuildLifetime();
            Assert.True((await RehydrateAsync(second)).IsComplete);

            var recovered = Assert.IsType<Issue>(await second.Context.Repository.FindAsync(issueId));

            Assert.Equal(IssueStatus.Resolved, recovered.IssueStatus);
            Assert.Equal(WorkPriority.Critical, recovered.Priority);
            Assert.Equal("grace", recovered.AssignedToPrincipalId);

            // Resolved still counts as open until it is closed, and that
            // must survive too — it is what every open-issue count reads.
            Assert.True(recovered.IsOpen);
        }
    }

    [Fact]
    public async Task ADecisionKeepsWhoDecidedAndWhen_AcrossARestart()
    {
        Guid decisionId;

        {
            var first = BuildLifetime(() => Decided);
            first.Principal.SetCurrent(new PlatformPrincipal(new PlatformIdentity("grace", "grace"), []));

            var project = await CreateProjectAsync(first, "P-1", "Apollo");
            var decision = await first.Workflow.CreateDecisionAsync(
                project.Id, "DEC-001", "Use titanium", "Lighter for the same stiffness.");
            decisionId = decision.Id;

            await first.Workflow.DecideAsync(decisionId, DecisionStatus.Accepted);
        }

        {
            var second = BuildLifetime();
            Assert.True((await RehydrateAsync(second)).IsComplete);

            var recovered = Assert.IsType<Decision>(await second.Context.Repository.FindAsync(decisionId));

            Assert.Equal(DecisionStatus.Accepted, recovered.DecisionStatus);
            Assert.Equal("grace", recovered.DecidedByPrincipalId);
            Assert.Equal(Decided, recovered.DecidedAt);
            Assert.Equal("Lighter for the same stiffness.", recovered.Rationale);
            Assert.True(recovered.IsInForce);
        }
    }

    [Fact]
    public async Task AHazardComesBackAsAHazard_NotAsAPlainRisk()
    {
        Guid hazardId;

        {
            var first = BuildLifetime();
            var project = await CreateProjectAsync(first, "P-1", "Apollo");
            var hazard = await first.Workflow.CreateRiskAsync(
                project.Id, "HAZ-001", "Stored energy in the accumulator", isHazard: true);
            hazardId = hazard.Id;

            await first.Workflow.ChangeRiskStatusAsync(hazardId, RiskStatus.Accepted);
        }

        {
            var second = BuildLifetime();
            Assert.True((await RehydrateAsync(second)).IsComplete);

            // A safety hazard that came back as an ordinary risk would lose
            // the one thing that distinguishes it.
            var recovered = Assert.IsType<Hazard>(await second.Context.Repository.FindAsync(hazardId));

            Assert.Equal(CanonicalObjectKinds.Hazard, recovered.Kind);
            Assert.Equal(RiskStatus.Accepted, recovered.RiskStatus);
            Assert.True(recovered.IsLive);
        }
    }

    [Fact]
    public async Task ProjectMembershipSurvivesARestart_SoTheRegisterStillFindsAllThree()
    {
        Guid projectId;

        {
            var first = BuildLifetime();
            var project = await CreateProjectAsync(first, "P-1", "Apollo");
            projectId = project.Id;

            // Three levels down, so this proves the transitive chain and not
            // just a direct child.
            var part = await CreatePartAsync(first, "PRT-1", "Impeller", projectId);

            var risk = await first.Workflow.CreateRiskAsync(projectId, "RSK-001", "Cavitation");
            var issue = await first.Workflow.CreateIssueAsync(projectId, "ISS-001", "Cracked blade");
            var decision = await first.Workflow.CreateDecisionAsync(projectId, "DEC-001", "Re-machine", "Cheaper.");

            await ((IHasParent)risk).MoveAsync(part.Id);
            await ((IHasParent)issue).MoveAsync(part.Id);
            await ((IHasParent)decision).MoveAsync(part.Id);
        }

        {
            var second = BuildLifetime();
            Assert.True((await RehydrateAsync(second)).IsComplete);

            // The register is asked the same question it answers in the
            // running product, over a graph that was reconstructed from disk.
            var register = new ProjectGovernanceRegister(second.Context);

            Assert.Single(await register.ListRisksAsync(projectId));
            Assert.Single(await register.ListIssuesAsync(projectId));
            Assert.Single(await register.ListDecisionsAsync(projectId));
        }
    }

    // ================================================================
    // Lifetime
    // ================================================================

    private sealed record Lifetime(
        EngineeringDomainContext Context,
        EngineeringObjectRehydratorRegistry Rehydrators,
        IProjectGovernanceService Workflow,
        CurrentPrincipalAccessor Principal);

    private Lifetime BuildLifetime(Func<DateTimeOffset>? now = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, _root),
            ]))
            .Build();

        var store = new PersistenceStore(configuration);
        var principal = new CurrentPrincipalAccessor();
        var documents = new EngineeringDocumentStore(store, principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);

        var context = new EngineeringDomainContext(
            documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal,
            new EngineeringObjectStateStore(store), new AttachmentContentStore(store));

        // The production registration, not a test one — this is the path
        // `TD-104` established and the one the shipped application uses.
        var rehydrators = new EngineeringObjectRehydratorRegistry();
        CanonicalObjectKinds.RegisterRehydrators(rehydrators, context);
        MechanicalObjectFactoryRegistry.RegisterRehydrators(rehydrators, context);

        return new Lifetime(context, rehydrators, new ProjectGovernanceService(context, now), principal);
    }

    private static Task<EngineeringRehydrationResult> RehydrateAsync(Lifetime lifetime) =>
        new EngineeringObjectRehydrationService(lifetime.Context, lifetime.Rehydrators).RehydrateAsync();

    private static async Task<IProject> CreateProjectAsync(Lifetime lifetime, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Project>(
            ProjectDirectory.ProjectKind, lifetime.Context,
            (d, r) => new Project(d, r, lifetime.Context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Project)await factory.CreateAsync($"Project {identifier}.");
    }

    private static async Task<Part> CreatePartAsync(Lifetime lifetime, string identifier, string name, Guid parentId)
    {
        var factory = new EngineeringObjectFactory<Part>(
            MechanicalObjectFactoryRegistry.Part, lifetime.Context,
            (d, r) => new Part(d, r, lifetime.Context, identifier, name, EngineeringObjectMetadata.Empty));

        var part = (Part)await factory.CreateAsync($"Part {identifier}.");
        await ((IHasParent)part).MoveAsync(parentId);
        return part;
    }
}
