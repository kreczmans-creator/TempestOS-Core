using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Covers the three additive `WP 9.0A` structural-mutation facets
/// (<see cref="IRenamable"/>, <see cref="IHasParent"/>, <see cref="IDeletable"/>)
/// against the real Product Structure Kinds they are composed into.
/// </summary>
public class StructuralMutationTests
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

    private static async Task<Assembly> CreateAssemblyAsync(EngineeringDomainContext context, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Assembly>(
            "Assembly", context, (doc, rev) => new Assembly(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Assembly)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string name)
    {
        var factory = new EngineeringObjectFactory<Part>(
            "Part", context, (doc, rev) => new Part(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Part)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    // ---- IRenamable ----

    [Fact]
    public async Task RenameAsync_UpdatesDisplayName()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context, "ASM-1", "Original Name");

        await assembly.RenameAsync("New Name");

        Assert.Equal("New Name", assembly.DisplayName);
    }

    [Fact]
    public async Task RenameAsync_DoesNotChangeIdentifier()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context, "ASM-1", "Original Name");

        await assembly.RenameAsync("New Name");

        Assert.Equal("ASM-1", assembly.Identifier);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameAsync_NullOrWhitespace_Throws(string invalidName)
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context, "ASM-1", "Original Name");

        await Assert.ThrowsAsync<ArgumentException>(() => assembly.RenameAsync(invalidName));
    }

    // ---- IHasParent ----

    [Fact]
    public async Task MoveAsync_SetsParentId_AndRecordsGroupedUnderRelationship()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context, "ASM-1", "Parent Assembly");
        var part = await CreatePartAsync(context, "PART-1", "Child Part");

        await part.MoveAsync(assembly.Id);

        Assert.Equal(assembly.Id, part.ParentId);

        var relationships = await part.GetRelationshipsAsync();
        Assert.Contains(relationships, r => r.TargetId == assembly.Id && r.RelationshipKind == "groupedUnder");
    }

    [Fact]
    public async Task MoveAsync_ToNull_ClearsParent()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context, "ASM-1", "Parent Assembly");
        var part = await CreatePartAsync(context, "PART-1", "Child Part");
        await part.MoveAsync(assembly.Id);

        await part.MoveAsync(null);

        Assert.Null(part.ParentId);
    }

    [Fact]
    public async Task MoveAsync_ASecondTime_PreservesTheOriginalRelationshipAsHistory()
    {
        var context = BuildContext();
        var firstParent = await CreateAssemblyAsync(context, "ASM-1", "First Parent");
        var secondParent = await CreateAssemblyAsync(context, "ASM-2", "Second Parent");
        var part = await CreatePartAsync(context, "PART-1", "Child Part");

        await part.MoveAsync(firstParent.Id);
        await part.MoveAsync(secondParent.Id);

        Assert.Equal(secondParent.Id, part.ParentId);

        var relationships = await part.GetRelationshipsAsync();
        Assert.Contains(relationships, r => r.TargetId == firstParent.Id && r.RelationshipKind == "groupedUnder");
        Assert.Contains(relationships, r => r.TargetId == secondParent.Id && r.RelationshipKind == "groupedUnder");
    }

    [Fact]
    public async Task MoveAsync_UnderItself_ThrowsCircularParentAssignmentException()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context, "ASM-1", "Assembly");

        await Assert.ThrowsAsync<CircularParentAssignmentException>(() => assembly.MoveAsync(assembly.Id));
    }

    [Fact]
    public async Task MoveAsync_UnderOwnDescendant_ThrowsCircularParentAssignmentException()
    {
        var context = BuildContext();
        var grandparent = await CreateAssemblyAsync(context, "ASM-1", "Grandparent");
        var parent = await CreateAssemblyAsync(context, "ASM-2", "Parent");
        var child = await CreateAssemblyAsync(context, "ASM-3", "Child");

        await parent.MoveAsync(grandparent.Id);
        await child.MoveAsync(parent.Id);

        await Assert.ThrowsAsync<CircularParentAssignmentException>(() => grandparent.MoveAsync(child.Id));
    }

    // ---- IDeletable ----

    [Fact]
    public async Task DeleteAsync_MarksIsDeleted()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context, "PART-1", "Part");

        await part.DeleteAsync();

        Assert.True(part.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_ObjectWithLiveChildren_ThrowsEngineeringObjectHasChildrenException()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context, "ASM-1", "Parent Assembly");
        var part = await CreatePartAsync(context, "PART-1", "Child Part");
        await part.MoveAsync(assembly.Id);

        await Assert.ThrowsAsync<EngineeringObjectHasChildrenException>(() => assembly.DeleteAsync());
    }

    [Fact]
    public async Task DeleteAsync_ObjectWhoseOnlyChildIsAlreadyDeleted_Succeeds()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context, "ASM-1", "Parent Assembly");
        var part = await CreatePartAsync(context, "PART-1", "Child Part");
        await part.MoveAsync(assembly.Id);
        await part.DeleteAsync();

        await assembly.DeleteAsync();

        Assert.True(assembly.IsDeleted);
    }

    [Fact]
    public async Task DeleteAsync_DoesNotErasePriorRevisionsOrRelationships()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context, "PART-1", "Part");
        var assembly = await CreateAssemblyAsync(context, "ASM-1", "Assembly");
        await part.MoveAsync(assembly.Id);

        await part.DeleteAsync();

        var history = await part.GetRevisionHistoryAsync();
        var relationships = await part.GetRelationshipsAsync();
        Assert.Single(history);
        Assert.Single(relationships);
    }

    // ---- ReviseAsync structural-state preservation (WP 9.0B — found and fixed) ----

    [Fact]
    public async Task ReviseAsync_PreservesRenameAcrossTheNewRevision()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context, "PART-1", "Original Name");
        await part.RenameAsync("Renamed");

        var revised = (Part)await part.ReviseAsync("New content.", null);

        Assert.Equal("Renamed", revised.DisplayName);
    }

    [Fact]
    public async Task ReviseAsync_PreservesParentAcrossTheNewRevision()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context, "ASM-1", "Assembly");
        var part = await CreatePartAsync(context, "PART-1", "Part");
        await part.MoveAsync(assembly.Id);

        var revised = (Part)await part.ReviseAsync("New content.", null);

        Assert.Equal(assembly.Id, revised.ParentId);
    }

    [Fact]
    public async Task ReviseAsync_PreservesDeletedStateAcrossTheNewRevision()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context, "PART-1", "Part");
        await part.DeleteAsync();

        var revised = (Part)await part.ReviseAsync("New content.", null);

        Assert.True(revised.IsDeleted);
    }

    [Fact]
    public async Task ReviseAsync_TheOriginalInstanceIsUnaffectedByTheNewRevisionsOwnFutureMutation()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context, "PART-1", "Part");

        var revised = (Part)await part.ReviseAsync("New content.", null);
        await revised.RenameAsync("Renamed After Revision");

        Assert.Equal("Part", part.DisplayName);
        Assert.Equal("Renamed After Revision", revised.DisplayName);
    }
}
