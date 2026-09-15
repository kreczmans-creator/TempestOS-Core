using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Manufacturing;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Verification;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Tests.Persistence;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `TD-38`: a business identifier is unique among live objects of the
/// same Kind within the same project, enforced generically by
/// <see cref="EngineeringObjectFactory{T}"/> and
/// <see cref="EngineeringObjectBase.RenameAsync"/> for every Kind
/// <see cref="BusinessIdentifierScope.EnforcedKinds"/> names.
/// </summary>
/// <remarks>
/// Covers, per the brief, the generic factory's own five Kind groups
/// (Part, Calculation, Document, Manufacturing, Verification Activity) as
/// one <see cref="Theory"/> over a small creation table — the mechanism
/// under test is Kind-agnostic, so one parameterised body proves it for
/// every group without five near-identical copies. Evidence — which does
/// not go through a factory registry but through <c>EvidenceService</c> —
/// has its own dedicated tests in <c>Evidence/EvidenceUniquenessTests.cs</c>.
/// </remarks>
public sealed class BusinessIdentifierUniquenessTests
{
    private sealed record Lifetime(
        EngineeringDomainContext Domain,
        EngineeringObjectRehydratorRegistry Rehydrators,
        EngineeringObjectRehydrationService Service);

    private static Lifetime NewLifetime(InMemoryQueryablePersistenceStore persistence)
    {
        var principal = new CurrentPrincipalAccessor();
        var documentStore = new EngineeringDocumentStore(persistence, principal);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationships = new InMemoryEngineeringRelationshipRepository();
        var discovery = new RelationshipDiscoveryService(relationships, repository);
        var stateStore = new EngineeringObjectStateStore(persistence);

        var domain = new EngineeringDomainContext(
            persistence, documentStore, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
            new EvidenceComposer(discovery, repository), principal, stateStore);

        var rehydrators = new EngineeringObjectRehydratorRegistry();
        MechanicalObjectFactoryRegistry.RegisterRehydrators(rehydrators, domain);
        CalculationObjectFactoryRegistry.RegisterRehydrators(rehydrators, domain);
        DocumentObjectFactoryRegistry.RegisterRehydrators(rehydrators, domain);
        ManufacturingObjectFactoryRegistry.RegisterRehydrators(rehydrators, domain);
        VerificationActivityFactoryRegistry.RegisterRehydrators(rehydrators, domain);

        return new Lifetime(domain, rehydrators, new EngineeringObjectRehydrationService(domain, rehydrators));
    }

    private static Task<IEngineeringObject> CreateProjectAsync(EngineeringDomainContext domain, string name) =>
        new MechanicalObjectFactoryRegistry(domain).CreateAsync(MechanicalObjectFactoryRegistry.Project, null, name, "Project content.", null);

    private static Task<IEngineeringObject> CreatePartAsync(EngineeringDomainContext domain, string name, Guid? parentId, CancellationToken cancellationToken = default) =>
        new MechanicalObjectFactoryRegistry(domain).CreateAsync(MechanicalObjectFactoryRegistry.Part, null, name, "Part content.", parentId, cancellationToken: cancellationToken);

    private static Task<IEngineeringObject> CreateCalculationAsync(EngineeringDomainContext domain, string name, Guid? parentId, CancellationToken cancellationToken = default) =>
        new CalculationObjectFactoryRegistry(domain).CreateAsync(CalculationObjectFactoryRegistry.CalculationKind, null, name, "Calculation content.", parentId, cancellationToken: cancellationToken);

    private static Task<IEngineeringObject> CreateDocumentAsync(EngineeringDomainContext domain, string name, Guid? parentId, CancellationToken cancellationToken = default) =>
        new DocumentObjectFactoryRegistry(domain).CreateAsync(DocumentObjectFactoryRegistry.Document, null, name, "Document content.", parentId, cancellationToken: cancellationToken);

    private static async Task<IEngineeringObject> CreateManufacturingOperationAsync(EngineeringDomainContext domain, string name, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var subjectPart = await CreatePartAsync(domain, $"subject-{Guid.NewGuid():N}", parentId: null, cancellationToken).ConfigureAwait(false);
        return await new ManufacturingObjectFactoryRegistry(domain)
            .CreateOperationAsync(null, name, "Operation content.", subjectPart.Id, null, parentId, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IEngineeringObject> CreateVerificationActivityAsync(EngineeringDomainContext domain, string name, Guid? parentId, CancellationToken cancellationToken = default)
    {
        var subjectPart = await CreatePartAsync(domain, $"subject-{Guid.NewGuid():N}", parentId: null, cancellationToken).ConfigureAwait(false);
        return await new VerificationActivityFactoryRegistry(domain)
            .CreateAsync(name, "Activity content.", subjectPart.Id, "Inspection", parentId, cancellationToken)
            .ConfigureAwait(false);
    }

    public static TheoryData<string, Func<EngineeringDomainContext, string, Guid?, CancellationToken, Task<IEngineeringObject>>> EnforcedFactoryKinds()
    {
        var data = new TheoryData<string, Func<EngineeringDomainContext, string, Guid?, CancellationToken, Task<IEngineeringObject>>>
        {
            { MechanicalObjectFactoryRegistry.Part, CreatePartAsync },
            { CalculationObjectFactoryRegistry.CalculationKind, CreateCalculationAsync },
            { DocumentObjectFactoryRegistry.Document, CreateDocumentAsync },
            { ManufacturingObjectFactoryRegistry.ManufacturingOperationKind, CreateManufacturingOperationAsync },
            { VerificationActivityFactoryRegistry.SupportedKind, CreateVerificationActivityAsync },
        };
        return data;
    }

    [Theory]
    [MemberData(nameof(EnforcedFactoryKinds))]
    public async Task Create_DuplicateInSameProject_IsRefused_NamingTheClash(
        string kind, Func<EngineeringDomainContext, string, Guid?, CancellationToken, Task<IEngineeringObject>> create)
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;
        var project = await CreateProjectAsync(domain, "P-001");

        var first = await create(domain, "Bracket", project.Id, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DuplicateBusinessIdentifierException>(
            () => create(domain, "Bracket", project.Id, CancellationToken.None));

        Assert.Equal(kind, ex.Kind);
        Assert.Equal(project.Id, ex.ProjectId);
        Assert.Equal(first.Id, ex.ExistingObjectId);
        Assert.Contains($"A {kind} named 'Bracket' already exists in project", ex.Message, StringComparison.Ordinal);
        Assert.Contains("P-001", ex.Message, StringComparison.Ordinal);
        Assert.Contains(project.Id.ToString(), ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EnforcedFactoryKinds))]
    public async Task Create_SameIdentifier_TrimmedAndCaseInsensitive_IsStillRefused(
        string kind, Func<EngineeringDomainContext, string, Guid?, CancellationToken, Task<IEngineeringObject>> create)
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;
        var project = await CreateProjectAsync(domain, "P-001");

        await create(domain, "Bracket", project.Id, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DuplicateBusinessIdentifierException>(
            () => create(domain, "  bracket  ", project.Id, CancellationToken.None));

        Assert.Equal(kind, ex.Kind);
    }

    [Theory]
    [MemberData(nameof(EnforcedFactoryKinds))]
    public async Task Create_SameIdentifierInAnotherProject_IsAccepted(
        string kind, Func<EngineeringDomainContext, string, Guid?, CancellationToken, Task<IEngineeringObject>> create)
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;
        var projectA = await CreateProjectAsync(domain, "P-001");
        var projectB = await CreateProjectAsync(domain, "P-002");

        var inA = await create(domain, "Bracket", projectA.Id, CancellationToken.None);
        var inB = await create(domain, "Bracket", projectB.Id, CancellationToken.None);

        Assert.NotEqual(inA.Id, inB.Id);
        Assert.Equal(kind, inA.Kind);
        Assert.Equal(kind, inB.Kind);
        Assert.Equal("Bracket", inA.BusinessIdentifier);
        Assert.Equal("Bracket", inB.BusinessIdentifier);
    }

    [Theory]
    [MemberData(nameof(EnforcedFactoryKinds))]
    public async Task Create_SameIdentifierOutsideAnyProject_IsAlsoRefused(
        string kind, Func<EngineeringDomainContext, string, Guid?, CancellationToken, Task<IEngineeringObject>> create)
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;

        var first = await create(domain, "Bracket", null, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DuplicateBusinessIdentifierException>(
            () => create(domain, "Bracket", null, CancellationToken.None));

        Assert.Equal(kind, ex.Kind);
        Assert.Null(ex.ProjectId);
        Assert.Equal(first.Id, ex.ExistingObjectId);
        Assert.DoesNotContain("in project", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EnforcedFactoryKinds))]
    public async Task Rename_ToAnExistingIdentifierInTheSameProject_IsRefused_AndNothingChanges(
        string kind, Func<EngineeringDomainContext, string, Guid?, CancellationToken, Task<IEngineeringObject>> create)
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;
        var project = await CreateProjectAsync(domain, "P-001");

        await create(domain, "Bracket", project.Id, CancellationToken.None);
        var bolt = (IRenamable)await create(domain, "Bolt", project.Id, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<DuplicateBusinessIdentifierException>(() => bolt.RenameAsync("Bracket"));

        Assert.Equal(kind, ex.Kind);
        Assert.Equal("Bolt", bolt.DisplayName);
    }

    // The Ribbon and Palette reach the same refusal through the command
    // handler, which must say it (`CommandResult.Failure`) rather than let
    // the exception escape — the gap `WP 20.1A2` disclosed, closed here.
    [Fact]
    public async Task RenameCommand_ToAnExistingIdentifierInTheSameProject_ReturnsTheRefusal_NotAnException()
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;
        var project = await CreateProjectAsync(domain, "P-001");

        await CreatePartAsync(domain, "Bracket", project.Id);
        var bolt = await CreatePartAsync(domain, "Bolt", project.Id);

        var handler = new RenameMechanicalObjectCommandHandler(domain);
        var result = await handler.HandleAsync(
            new RenameMechanicalObjectCommand(bolt.Id, MechanicalObjectFactoryRegistry.Part, "Bracket"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Bracket", result.Message, StringComparison.Ordinal);
        Assert.Equal("Bolt", ((IRenamable)bolt).DisplayName);
    }

    [Theory]
    [MemberData(nameof(EnforcedFactoryKinds))]
    public async Task Rename_ToAnIdentifierFreeInThisProject_Succeeds(
        string kind, Func<EngineeringDomainContext, string, Guid?, CancellationToken, Task<IEngineeringObject>> create)
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;
        var project = await CreateProjectAsync(domain, "P-001");

        var bolt = (IRenamable)await create(domain, "Bolt", project.Id, CancellationToken.None);

        await bolt.RenameAsync("Washer");

        Assert.Equal("Washer", bolt.DisplayName);

        // The freed name is available to a fresh object, and "Bolt" no
        // longer conflicts with anything.
        var another = await create(domain, "Bolt", project.Id, CancellationToken.None);
        Assert.Equal(kind, another.Kind);
        Assert.Equal("Bolt", another.BusinessIdentifier);
    }

    [Theory]
    [MemberData(nameof(EnforcedFactoryKinds))]
    public async Task SoftDelete_FreesTheIdentifier(
        string kind, Func<EngineeringDomainContext, string, Guid?, CancellationToken, Task<IEngineeringObject>> create)
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;
        var project = await CreateProjectAsync(domain, "P-001");

        var first = (IDeletable)await create(domain, "Bracket", project.Id, CancellationToken.None);
        await first.DeleteAsync();

        var second = await create(domain, "Bracket", project.Id, CancellationToken.None);

        Assert.True(first.IsDeleted);
        Assert.Equal(kind, second.Kind);
        Assert.Equal("Bracket", second.BusinessIdentifier);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Theory]
    [MemberData(nameof(EnforcedFactoryKinds))]
    public async Task Rehydration_RebuildsTheIndex_CreateRestartDuplicateRefused(
        string kind, Func<EngineeringDomainContext, string, Guid?, CancellationToken, Task<IEngineeringObject>> create)
    {
        var persistence = new InMemoryQueryablePersistenceStore();
        Guid projectId;

        {
            var first = NewLifetime(persistence);
            var project = await CreateProjectAsync(first.Domain, "P-001");
            projectId = project.Id;
            await create(first.Domain, "Bracket", projectId, CancellationToken.None);
        }

        // A genuinely new lifetime — a new context, a new (empty) object
        // repository and a new (empty) business-identifier index — over
        // the same durable store, exactly as `EngineeringObjectRehydrationTests`
        // models a real host restart.
        var second = NewLifetime(persistence);
        var result = await second.Service.RehydrateAsync();

        Assert.True(result.IsComplete);

        await Assert.ThrowsAsync<DuplicateBusinessIdentifierException>(
            () => create(second.Domain, "Bracket", projectId, CancellationToken.None));

        // And the identifier is still free in a different project, proving
        // the rebuilt index carries the right scope, not just the right name.
        var anotherProject = await CreateProjectAsync(second.Domain, "P-002");
        var inAnotherProject = await create(second.Domain, "Bracket", anotherProject.Id, CancellationToken.None);
        Assert.Equal(kind, inAnotherProject.Kind);
        Assert.Equal("Bracket", inAnotherProject.BusinessIdentifier);
    }

    // ================================================================
    // Document's own nuance: the business identifier is its number
    // (the raw `Identifier` field) if it has one, else its name.
    // ================================================================

    [Fact]
    public async Task Document_WithANumber_UsesTheNumber_NotTheName_AsItsBusinessIdentifier()
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;
        var project = await CreateProjectAsync(domain, "P-001");

        var document = await new DocumentObjectFactoryRegistry(domain)
            .CreateAsync(DocumentObjectFactoryRegistry.Document, "SPEC-001", "Interface Control Document", "content", project.Id);

        Assert.Equal("SPEC-001", document.BusinessIdentifier);
    }

    [Fact]
    public async Task Document_WithANumber_DuplicateIsRefusedOnTheNumber_EvenWithADifferentName()
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;
        var project = await CreateProjectAsync(domain, "P-001");

        await new DocumentObjectFactoryRegistry(domain)
            .CreateAsync(DocumentObjectFactoryRegistry.Document, "SPEC-001", "Interface Control Document", "content", project.Id);

        await Assert.ThrowsAsync<DuplicateBusinessIdentifierException>(
            () => new DocumentObjectFactoryRegistry(domain)
                .CreateAsync(DocumentObjectFactoryRegistry.Document, "SPEC-001", "A Completely Different Name", "content", project.Id));
    }

    [Fact]
    public async Task Document_WithANumber_TwoDifferentNumbers_AreBothAccepted_EvenWithTheSameName()
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;
        var project = await CreateProjectAsync(domain, "P-001");

        var first = await new DocumentObjectFactoryRegistry(domain)
            .CreateAsync(DocumentObjectFactoryRegistry.Document, "SPEC-001", "Interface Control Document", "content", project.Id);
        var second = await new DocumentObjectFactoryRegistry(domain)
            .CreateAsync(DocumentObjectFactoryRegistry.Document, "SPEC-002", "Interface Control Document", "content", project.Id);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task Document_WithANumber_RenamingItsDisplayName_CannotChangeItsBusinessIdentifier_SoIsNeverRefusedOnThatGround()
    {
        var domain = NewLifetime(new InMemoryQueryablePersistenceStore()).Domain;
        var project = await CreateProjectAsync(domain, "P-001");

        var numbered = (IRenamable)await new DocumentObjectFactoryRegistry(domain)
            .CreateAsync(DocumentObjectFactoryRegistry.Document, "SPEC-001", "Interface Control Document", "content", project.Id);
        await new DocumentObjectFactoryRegistry(domain)
            .CreateAsync(DocumentObjectFactoryRegistry.Document, "SPEC-002", "Some Other Name", "content", project.Id);

        // Renaming to the other Document's own *name* is fine: that other
        // Document's business identifier is its number, "SPEC-002", not
        // its name, so no clash exists on the identifier that actually governs.
        await numbered.RenameAsync("Some Other Name");

        Assert.Equal("Some Other Name", numbered.DisplayName);
        Assert.Equal("SPEC-001", ((IEngineeringObject)numbered).BusinessIdentifier);
    }
}
