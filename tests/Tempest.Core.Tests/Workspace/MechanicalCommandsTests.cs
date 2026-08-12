using Tempest.App.Workspace.Mechanical;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Covers all six `WP 9.0A` <c>IWorkspaceCommand</c>/<c>ICommand</c>
/// implementations — the first concrete <c>IWorkspaceCommand</c>s this
/// platform ships (`WP8.1B`'s own disclosed "no concrete IWorkspaceCommand
/// is implemented by this Work Package" is closed here) — directly against
/// a real, in-memory <see cref="EngineeringDomainContext"/>, mirroring
/// <c>StructuralMutationTests</c>'s own lightweight construction.
/// </summary>
public class MechanicalCommandsTests
{
    private static EngineeringDomainContext BuildContext()
    {
        var principalAccessor = new CurrentPrincipalAccessor();
        var store = new InMemoryEngineeringDocumentStore(principalAccessor);
        var repository = new InMemoryEngineeringObjectRepository();
        var relationshipRepository = new InMemoryEngineeringRelationshipRepository();
        var lifecycleTable = new LifecycleTransitionTable();
        var validationRuleSet = new ValidationRuleSet();
        var relationshipDiscovery = new RelationshipDiscoveryService(relationshipRepository, repository);
        var evidenceComposer = new EvidenceComposer(relationshipDiscovery, repository);

        return new EngineeringDomainContext(
            store, repository, relationshipRepository, lifecycleTable, validationRuleSet, evidenceComposer, principalAccessor);
    }

    private static async Task<Assembly> CreateAssemblyAsync(EngineeringDomainContext context, string identifier = "ASM-1", string name = "Assembly")
    {
        var factory = new EngineeringObjectFactory<Assembly>(
            "Assembly", context, (doc, rev) => new Assembly(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Assembly)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier = "PART-1", string name = "Part")
    {
        var factory = new EngineeringObjectFactory<Part>(
            "Part", context, (doc, rev) => new Part(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Part)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    private static async Task<Tempest.Core.EngineeringDomain.Configuration> CreateConfigurationAsync(
        EngineeringDomainContext context, string identifier, string name, IReadOnlyList<ConfigurationMember>? members = null)
    {
        var factory = new EngineeringObjectFactory<Tempest.Core.EngineeringDomain.Configuration>(
            "Configuration", context, (doc, rev) => new Tempest.Core.EngineeringDomain.Configuration(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty, members));

        return (Tempest.Core.EngineeringDomain.Configuration)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    private static async Task<Baseline> CreateBaselineAsync(
        EngineeringDomainContext context, string identifier, string name, IReadOnlyList<ConfigurationMember>? members = null)
    {
        var factory = new EngineeringObjectFactory<Baseline>(
            "Baseline", context, (doc, rev) => new Baseline(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty, members));

        return (Baseline)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    // ---- CreateMechanicalObjectCommand ----

    [Theory]
    [InlineData("Project")]
    [InlineData("Assembly")]
    [InlineData("Part")]
    [InlineData("Component")]
    [InlineData("Configuration")]
    [InlineData("Baseline")]
    [InlineData("Release")]
    public async Task Create_SupportedKindWithoutParent_Succeeds(string kind)
    {
        var context = BuildContext();
        var registry = new MechanicalObjectFactoryRegistry(context);
        var handler = new CreateMechanicalObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateMechanicalObjectCommand(kind, "New Object"), default);

        Assert.True(result.Succeeded);
        Assert.Single(await context.Repository.ListByKindAsync(kind));
    }

    [Fact]
    public async Task Create_SubAssemblyWithoutParent_Fails()
    {
        var context = BuildContext();
        var registry = new MechanicalObjectFactoryRegistry(context);
        var handler = new CreateMechanicalObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateMechanicalObjectCommand("SubAssembly", "New Sub-Assembly"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_SubAssemblyWithParent_Succeeds()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var registry = new MechanicalObjectFactoryRegistry(context);
        var handler = new CreateMechanicalObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateMechanicalObjectCommand("SubAssembly", "New Sub-Assembly", parentId: assembly.Id), default);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Create_UnsupportedKind_Fails()
    {
        var context = BuildContext();
        var registry = new MechanicalObjectFactoryRegistry(context);
        var handler = new CreateMechanicalObjectCommandHandler(registry);

        var result = await handler.HandleAsync(new CreateMechanicalObjectCommand("Requirement", "Not Mechanical"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_WithParent_MovesTheNewObjectUnderIt()
    {
        var context = BuildContext();
        var parent = await CreateAssemblyAsync(context);
        var registry = new MechanicalObjectFactoryRegistry(context);
        var handler = new CreateMechanicalObjectCommandHandler(registry);

        await handler.HandleAsync(new CreateMechanicalObjectCommand("Part", "New Part", parentId: parent.Id), default);

        var created = (await context.Repository.ListByKindAsync("Part")).Single();
        Assert.Equal(parent.Id, ((IHasParent)created).ParentId);
    }

    // ---- RenameMechanicalObjectCommand ----

    [Fact]
    public async Task Rename_KnownTarget_Succeeds()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var handler = new RenameMechanicalObjectCommandHandler(context);

        var result = await handler.HandleAsync(new RenameMechanicalObjectCommand(assembly.Id, "Assembly", "New Name"), default);

        Assert.True(result.Succeeded);
        Assert.Equal("New Name", assembly.DisplayName);
    }

    [Fact]
    public async Task Rename_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new RenameMechanicalObjectCommandHandler(context);

        var result = await handler.HandleAsync(new RenameMechanicalObjectCommand(Guid.NewGuid(), "Assembly", "New Name"), default);

        Assert.False(result.Succeeded);
    }

    // ---- DeleteMechanicalObjectCommand ----

    [Fact]
    public async Task Delete_KnownTargetWithNoChildren_Succeeds()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);
        var handler = new DeleteMechanicalObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteMechanicalObjectCommand(part.Id, "Part"), default);

        Assert.True(result.Succeeded);
        Assert.True(part.IsDeleted);
    }

    [Fact]
    public async Task Delete_TargetWithLiveChildren_Fails()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var part = await CreatePartAsync(context);
        await part.MoveAsync(assembly.Id);
        var handler = new DeleteMechanicalObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteMechanicalObjectCommand(assembly.Id, "Assembly"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Delete_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new DeleteMechanicalObjectCommandHandler(context);

        var result = await handler.HandleAsync(new DeleteMechanicalObjectCommand(Guid.NewGuid(), "Part"), default);

        Assert.False(result.Succeeded);
    }

    // ---- MoveMechanicalObjectCommand ----

    [Fact]
    public async Task Move_ToKnownParent_Succeeds()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var part = await CreatePartAsync(context);
        var handler = new MoveMechanicalObjectCommandHandler(context);

        var result = await handler.HandleAsync(new MoveMechanicalObjectCommand(part.Id, "Part", assembly.Id), default);

        Assert.True(result.Succeeded);
        Assert.Equal(assembly.Id, part.ParentId);
    }

    [Fact]
    public async Task Move_UnderOwnDescendant_Fails()
    {
        var context = BuildContext();
        var parent = await CreateAssemblyAsync(context, "ASM-1", "Parent");
        var child = await CreateAssemblyAsync(context, "ASM-2", "Child");
        await child.MoveAsync(parent.Id);
        var handler = new MoveMechanicalObjectCommandHandler(context);

        var result = await handler.HandleAsync(new MoveMechanicalObjectCommand(parent.Id, "Assembly", child.Id), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Move_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new MoveMechanicalObjectCommandHandler(context);

        var result = await handler.HandleAsync(new MoveMechanicalObjectCommand(Guid.NewGuid(), "Part", null), default);

        Assert.False(result.Succeeded);
    }

    // ---- CopyMechanicalObjectCommand ----

    [Fact]
    public async Task Copy_KnownSource_CreatesNewObjectOfSameKindUnderTargetParent()
    {
        var context = BuildContext();
        var source = await CreatePartAsync(context, "PART-1", "Original Part");
        var targetParent = await CreateAssemblyAsync(context);
        var registry = new MechanicalObjectFactoryRegistry(context);
        var handler = new CopyMechanicalObjectCommandHandler(context, registry);

        var result = await handler.HandleAsync(new CopyMechanicalObjectCommand(source.Id, "Part", targetParent.Id), default);

        Assert.True(result.Succeeded);
        var parts = await context.Repository.ListByKindAsync("Part");
        Assert.Equal(2, parts.Count);
        var copy = parts.Single(p => p.Id != source.Id);
        Assert.Equal(targetParent.Id, ((IHasParent)copy).ParentId);
        Assert.Equal("Original Part (Copy)", ((IHasBusinessIdentifier)copy).DisplayName);
    }

    [Fact]
    public async Task Copy_UnknownSource_Fails()
    {
        var context = BuildContext();
        var registry = new MechanicalObjectFactoryRegistry(context);
        var handler = new CopyMechanicalObjectCommandHandler(context, registry);

        var result = await handler.HandleAsync(new CopyMechanicalObjectCommand(Guid.NewGuid(), "Part", null), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Copy_ExplicitNewDisplayNameAndIdentifier_AreHonoured()
    {
        var context = BuildContext();
        var source = await CreatePartAsync(context);
        var registry = new MechanicalObjectFactoryRegistry(context);
        var handler = new CopyMechanicalObjectCommandHandler(context, registry);

        var result = await handler.HandleAsync(
            new CopyMechanicalObjectCommand(source.Id, "Part", null, "PART-2", "Renamed Copy"), default);

        Assert.True(result.Succeeded);
        var copy = (await context.Repository.ListByKindAsync("Part")).Single(p => p.Id != source.Id);
        Assert.Equal("Renamed Copy", ((IHasBusinessIdentifier)copy).DisplayName);
        Assert.Equal("PART-2", ((IHasBusinessIdentifier)copy).Identifier);
    }

    // ---- DuplicateMechanicalObjectCommand ----

    [Fact]
    public async Task Duplicate_KnownSource_CreatesNewObjectUnderSameParent()
    {
        var context = BuildContext();
        var parent = await CreateAssemblyAsync(context);
        var source = await CreatePartAsync(context, "PART-1", "Original Part");
        await source.MoveAsync(parent.Id);

        var registry = new MechanicalObjectFactoryRegistry(context);
        var copyHandler = new CopyMechanicalObjectCommandHandler(context, registry);
        var handler = new DuplicateMechanicalObjectCommandHandler(context, copyHandler);

        var result = await handler.HandleAsync(new DuplicateMechanicalObjectCommand(source.Id, "Part"), default);

        Assert.True(result.Succeeded);
        var parts = await context.Repository.ListByKindAsync("Part");
        Assert.Equal(2, parts.Count);
        var duplicate = parts.Single(p => p.Id != source.Id);
        Assert.Equal(parent.Id, ((IHasParent)duplicate).ParentId);
    }

    [Fact]
    public async Task Duplicate_UnknownSource_Fails()
    {
        var context = BuildContext();
        var registry = new MechanicalObjectFactoryRegistry(context);
        var copyHandler = new CopyMechanicalObjectCommandHandler(context, registry);
        var handler = new DuplicateMechanicalObjectCommandHandler(context, copyHandler);

        var result = await handler.HandleAsync(new DuplicateMechanicalObjectCommand(Guid.NewGuid(), "Part"), default);

        Assert.False(result.Succeeded);
    }

    // ---- SetBomLineCommand (WP 9.0B) ----

    [Fact]
    public async Task SetBomLine_KnownTarget_Succeeds()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);
        var handler = new SetBomLineCommandHandler(context);

        var result = await handler.HandleAsync(new SetBomLineCommand(part.Id, "Part", 4m, "EA", "10", "0010", "J1-J4"), default);

        Assert.True(result.Succeeded);
        Assert.Equal(4m, part.Quantity);
        Assert.Equal("EA", part.UnitOfMeasure);
        Assert.Equal("0010", part.ItemNumber);
    }

    [Fact]
    public async Task SetBomLine_NonPositiveQuantity_Fails()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);
        var handler = new SetBomLineCommandHandler(context);

        var result = await handler.HandleAsync(new SetBomLineCommand(part.Id, "Part", 0m), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task SetBomLine_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var handler = new SetBomLineCommandHandler(context);

        var result = await handler.HandleAsync(new SetBomLineCommand(Guid.NewGuid(), "Part", 1m), default);

        Assert.False(result.Succeeded);
    }

    // ---- CompareBaselinesCommand (WP 9.0B) ----

    [Fact]
    public async Task CompareBaselines_DetectsAddedRemovedAndRevisionChangedMembers()
    {
        var context = BuildContext();
        var stableObject = await CreatePartAsync(context, "PART-1", "Stable Part");
        var removedObject = await CreatePartAsync(context, "PART-2", "Removed Part");
        var addedObject = await CreatePartAsync(context, "PART-3", "Added Part");
        var revisedObject = await CreatePartAsync(context, "PART-4", "Revised Part");

        var first = await CreateBaselineAsync(context, "BASE-1", "First Baseline",
        [
            new ConfigurationMember(stableObject.Id, 1),
            new ConfigurationMember(removedObject.Id, 1),
            new ConfigurationMember(revisedObject.Id, 1),
        ]);
        var second = await CreateBaselineAsync(context, "BASE-2", "Second Baseline",
        [
            new ConfigurationMember(stableObject.Id, 1),
            new ConfigurationMember(addedObject.Id, 1),
            new ConfigurationMember(revisedObject.Id, 2),
        ]);

        var handler = new CompareBaselinesCommandHandler(context);
        var result = await handler.HandleAsync(new CompareBaselinesCommand(first.Id, second.Id), default);

        Assert.True(result.Succeeded);
        Assert.Contains("1 added", result.Message);
        Assert.Contains("1 removed", result.Message);
        Assert.Contains("1 revision-changed", result.Message);
    }

    [Fact]
    public async Task CompareBaselines_IdenticalMembers_ReportsNoDifferences()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);
        var members = new[] { new ConfigurationMember(part.Id, 1) };

        var first = await CreateBaselineAsync(context, "BASE-1", "First Baseline", members);
        var second = await CreateBaselineAsync(context, "BASE-2", "Second Baseline", members);

        var handler = new CompareBaselinesCommandHandler(context);
        var result = await handler.HandleAsync(new CompareBaselinesCommand(first.Id, second.Id), default);

        Assert.True(result.Succeeded);
        Assert.Contains("0 added, 0 removed, 0 revision-changed", result.Message);
    }

    [Fact]
    public async Task CompareBaselines_UnknownFirst_Fails()
    {
        var context = BuildContext();
        var second = await CreateBaselineAsync(context, "BASE-2", "Second Baseline");
        var handler = new CompareBaselinesCommandHandler(context);

        var result = await handler.HandleAsync(new CompareBaselinesCommand(Guid.NewGuid(), second.Id), default);

        Assert.False(result.Succeeded);
    }

    // ---- ValidateConfigurationCommand (WP 9.0B) ----

    [Fact]
    public async Task ValidateConfiguration_ConsistentBaseline_Succeeds()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);
        var baseline = await CreateBaselineAsync(context, "BASE-1", "Baseline", [new ConfigurationMember(part.Id, 1)]);
        var checker = new ReferenceIntegrityChecker(context.Repository);
        var handler = new ValidateConfigurationCommandHandler(context, checker);

        var result = await handler.HandleAsync(new ValidateConfigurationCommand(baseline.Id, "Baseline"), default);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ValidateConfiguration_MissingMember_Fails()
    {
        var context = BuildContext();
        var baseline = await CreateBaselineAsync(context, "BASE-1", "Baseline", [new ConfigurationMember(Guid.NewGuid(), 1)]);
        var checker = new ReferenceIntegrityChecker(context.Repository);
        var handler = new ValidateConfigurationCommandHandler(context, checker);

        var result = await handler.HandleAsync(new ValidateConfigurationCommand(baseline.Id, "Baseline"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ValidateConfiguration_PlainWorkingConfiguration_Fails()
    {
        // A plain Configuration does not itself satisfy IBaseline
        // (Baseline : Configuration, WP8.2C, a frozen shape) — disclosed,
        // deliberate scoping, not a bug.
        var context = BuildContext();
        var configuration = await CreateConfigurationAsync(context, "CFG-1", "Working Set");
        var checker = new ReferenceIntegrityChecker(context.Repository);
        var handler = new ValidateConfigurationCommandHandler(context, checker);

        var result = await handler.HandleAsync(new ValidateConfigurationCommand(configuration.Id, "Configuration"), default);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ValidateConfiguration_UnknownTarget_Fails()
    {
        var context = BuildContext();
        var checker = new ReferenceIntegrityChecker(context.Repository);
        var handler = new ValidateConfigurationCommandHandler(context, checker);

        var result = await handler.HandleAsync(new ValidateConfigurationCommand(Guid.NewGuid(), "Baseline"), default);

        Assert.False(result.Succeeded);
    }
}
