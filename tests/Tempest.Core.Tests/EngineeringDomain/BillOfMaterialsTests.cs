using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// Covers `WP 9.0B`'s additive <see cref="IHasBomLine"/> facet and its
/// five new <see cref="IValidationRule"/> implementations, mirroring
/// `StructuralMutationTests`'s own lightweight construction.
/// </summary>
public class BillOfMaterialsTests
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

    // ---- IHasBomLine ----

    [Fact]
    public async Task Quantity_Default_IsOne()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);

        Assert.Equal(1m, part.Quantity);
    }

    [Fact]
    public async Task SetBomLineAsync_SetsEveryField()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);

        await part.SetBomLineAsync(4m, "EA", "10", "0010", "J1-J4");

        Assert.Equal(4m, part.Quantity);
        Assert.Equal("EA", part.UnitOfMeasure);
        Assert.Equal("10", part.FindNumber);
        Assert.Equal("0010", part.ItemNumber);
        Assert.Equal("J1-J4", part.ReferenceDesignator);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SetBomLineAsync_NonPositiveQuantity_Throws(decimal quantity)
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => part.SetBomLineAsync(quantity));
    }

    [Fact]
    public async Task SetBomLineAsync_DoesNotCreateANewRevision()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);

        await part.SetBomLineAsync(2m, "EA");

        Assert.Equal(1, part.CurrentRevisionNumber);
    }

    [Fact]
    public async Task ReviseAsync_PreservesBomLineAcrossTheNewRevision()
    {
        // Found during WP 9.0B's own representative-data work:
        // EngineeringObjectBase.ReviseAsync originally reconstructed the
        // revised instance from the *original* factory closure, silently
        // discarding any WP 9.0A/9.0B mutation (rename/move/delete/BOM
        // line) made since construction. Fixed by copying structural state
        // forward; this test is that fix's own direct proof.
        var context = BuildContext();
        var part = await CreatePartAsync(context);
        await part.SetBomLineAsync(4m, "EA", "10", "0010", "J1-J4");

        var revised = (Part)await part.ReviseAsync("New content.", null);

        Assert.Equal(4m, revised.Quantity);
        Assert.Equal("EA", revised.UnitOfMeasure);
        Assert.Equal("10", revised.FindNumber);
        Assert.Equal("0010", revised.ItemNumber);
        Assert.Equal("J1-J4", revised.ReferenceDesignator);
    }

    // ---- InvalidQuantityValidationRule ----

    [Fact]
    public async Task InvalidQuantityValidationRule_PositiveQuantity_IsValid()
    {
        var part = await CreatePartAsync(BuildContext());
        var rule = new InvalidQuantityValidationRule();

        var result = await rule.EvaluateAsync(part);

        Assert.True(result.IsValid);
    }

    // ---- MissingParentValidationRule ----

    [Fact]
    public async Task MissingParentValidationRule_NoParentSet_IsValid()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);
        var rule = new MissingParentValidationRule(context.Repository);

        var result = await rule.EvaluateAsync(part);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task MissingParentValidationRule_ParentExists_IsValid()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var part = await CreatePartAsync(context);
        await part.MoveAsync(assembly.Id);
        var rule = new MissingParentValidationRule(context.Repository);

        var result = await rule.EvaluateAsync(part);

        Assert.True(result.IsValid);
    }

    // ---- CircularHierarchyValidationRule ----

    [Fact]
    public async Task CircularHierarchyValidationRule_LinearChain_IsValid()
    {
        var context = BuildContext();
        var grandparent = await CreateAssemblyAsync(context, "ASM-1", "Grandparent");
        var parent = await CreateAssemblyAsync(context, "ASM-2", "Parent");
        await parent.MoveAsync(grandparent.Id);
        var rule = new CircularHierarchyValidationRule(context.Repository);

        var result = await rule.EvaluateAsync(parent);

        Assert.True(result.IsValid);
    }

    // ---- DuplicateItemNumberValidationRule ----

    [Fact]
    public async Task DuplicateItemNumberValidationRule_UniqueAmongSiblings_IsValid()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var partA = await CreatePartAsync(context, "PART-1", "Part A");
        var partB = await CreatePartAsync(context, "PART-2", "Part B");
        await partA.MoveAsync(assembly.Id);
        await partB.MoveAsync(assembly.Id);
        await partA.SetBomLineAsync(1m, itemNumber: "0010");
        await partB.SetBomLineAsync(1m, itemNumber: "0020");

        var rule = new DuplicateItemNumberValidationRule(context.Repository);

        Assert.True((await rule.EvaluateAsync(partA)).IsValid);
        Assert.True((await rule.EvaluateAsync(partB)).IsValid);
    }

    [Fact]
    public async Task DuplicateItemNumberValidationRule_SharedAmongSiblings_IsInvalid()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var partA = await CreatePartAsync(context, "PART-1", "Part A");
        var partB = await CreatePartAsync(context, "PART-2", "Part B");
        await partA.MoveAsync(assembly.Id);
        await partB.MoveAsync(assembly.Id);
        await partA.SetBomLineAsync(1m, itemNumber: "0010");
        await partB.SetBomLineAsync(1m, itemNumber: "0010");

        var rule = new DuplicateItemNumberValidationRule(context.Repository);
        var result = await rule.EvaluateAsync(partA);

        Assert.False(result.IsValid);
        Assert.Equal(StructuralValidationRules.NoDuplicateItemNumber, result.Errors[0].Code);
    }

    [Fact]
    public async Task DuplicateItemNumberValidationRule_SameNumberUnderDifferentParents_IsValid()
    {
        var context = BuildContext();
        var assemblyA = await CreateAssemblyAsync(context, "ASM-1", "Assembly A");
        var assemblyB = await CreateAssemblyAsync(context, "ASM-2", "Assembly B");
        var partA = await CreatePartAsync(context, "PART-1", "Part A");
        var partB = await CreatePartAsync(context, "PART-2", "Part B");
        await partA.MoveAsync(assemblyA.Id);
        await partB.MoveAsync(assemblyB.Id);
        await partA.SetBomLineAsync(1m, itemNumber: "0010");
        await partB.SetBomLineAsync(1m, itemNumber: "0010");

        var rule = new DuplicateItemNumberValidationRule(context.Repository);

        Assert.True((await rule.EvaluateAsync(partA)).IsValid);
        Assert.True((await rule.EvaluateAsync(partB)).IsValid);
    }

    [Fact]
    public async Task DuplicateItemNumberValidationRule_DeletedSiblingIsIgnored()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var partA = await CreatePartAsync(context, "PART-1", "Part A");
        var partB = await CreatePartAsync(context, "PART-2", "Part B");
        await partA.MoveAsync(assembly.Id);
        await partB.MoveAsync(assembly.Id);
        await partA.SetBomLineAsync(1m, itemNumber: "0010");
        await partB.SetBomLineAsync(1m, itemNumber: "0010");
        await partB.DeleteAsync();

        var rule = new DuplicateItemNumberValidationRule(context.Repository);
        var result = await rule.EvaluateAsync(partA);

        Assert.True(result.IsValid);
    }

    // ---- DuplicateFindNumberValidationRule ----

    [Fact]
    public async Task DuplicateFindNumberValidationRule_SharedAmongSiblings_IsInvalid()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var partA = await CreatePartAsync(context, "PART-1", "Part A");
        var partB = await CreatePartAsync(context, "PART-2", "Part B");
        await partA.MoveAsync(assembly.Id);
        await partB.MoveAsync(assembly.Id);
        await partA.SetBomLineAsync(1m, findNumber: "5");
        await partB.SetBomLineAsync(1m, findNumber: "5");

        var rule = new DuplicateFindNumberValidationRule(context.Repository);
        var result = await rule.EvaluateAsync(partA);

        Assert.False(result.IsValid);
        Assert.Equal(StructuralValidationRules.NoDuplicateFindNumber, result.Errors[0].Code);
    }

    // ---- ValidationRuleSet integration (ADR-0075/ValidationRuleSet's own extension point) ----

    [Fact]
    public async Task ValidationRuleSet_WithRegisteredBomRules_ReportsDuplicateItemNumber()
    {
        var context = BuildContext();
        ((ValidationRuleSet)context.ValidationRuleSet).Register(new DuplicateItemNumberValidationRule(context.Repository));

        var assembly = await CreateAssemblyAsync(context);
        var partA = await CreatePartAsync(context, "PART-1", "Part A");
        var partB = await CreatePartAsync(context, "PART-2", "Part B");
        await partA.MoveAsync(assembly.Id);
        await partB.MoveAsync(assembly.Id);
        await partA.SetBomLineAsync(1m, itemNumber: "0010");
        await partB.SetBomLineAsync(1m, itemNumber: "0010");

        var result = await partA.ValidateAsync();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == StructuralValidationRules.NoDuplicateItemNumber);
    }
}
