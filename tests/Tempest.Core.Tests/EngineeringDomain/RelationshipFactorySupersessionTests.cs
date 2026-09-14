using Tempest.Core.EngineeringDomain;
using Tempest.Workspace.Mechanical;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `TD-141` (partly) closure tests: <see cref="EngineeringRelationshipFactory.CreateAsync"/>
/// took raw ids with no supersession guard at all, unlike
/// <see cref="EngineeringObjectBase.LinkAsync"/>'s own private
/// <c>ThrowIfSuperseded</c> check on the specific instance handle a caller
/// used. A raw id carries no handle to go stale, so the factory checks the
/// durable signal a raw id <em>can</em> carry instead — the resolved
/// object's own <see cref="IHasLifecycle.Status"/> — and refuses with the
/// same <see cref="SupersededEngineeringObjectException"/> when either end
/// is already <see cref="LifecycleState.Superseded"/>, before writing
/// anything.
/// </summary>
public sealed class RelationshipFactorySupersessionTests
{
    private const string RelationshipKind = "relatesTo";

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string identifier, string displayName) =>
        (Part)await new EngineeringObjectFactory<Part>(
                MechanicalObjectFactoryRegistry.Part, context,
                (d, r) => new Part(d, r, context, identifier, displayName, EngineeringObjectMetadata.Empty))
            .CreateAsync($"{displayName} — for test purposes.");

    private static async Task<Part> CreateSupersededPartAsync(EngineeringDomainContext context, string identifier, string displayName)
    {
        var part = await CreatePartAsync(context, identifier, displayName);

        await part.TransitionAsync(LifecycleState.InReview);
        await part.TransitionAsync(LifecycleState.Approved);
        await part.TransitionAsync(LifecycleState.Released);
        await part.TransitionAsync(LifecycleState.Superseded);

        return part;
    }

    [Fact]
    public async Task CreateAsync_SourceAlreadySuperseded_ThrowsSupersededEngineeringObjectException_AndWritesNothing()
    {
        var context = TestEngineeringDomain.NewContext();
        var source = await CreateSupersededPartAsync(context, "PRT-141-1", "Bracket");
        var target = await CreatePartAsync(context, "PRT-141-2", "Housing");
        var factory = new EngineeringRelationshipFactory(RelationshipKind, RelationshipCategory.Reference, context);

        var exception = await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => factory.CreateAsync(source.Id, target.Id));

        Assert.Equal(source.Id, exception.ObjectId);

        var references = await context.Store.GetReferencesAsync(source.Id);
        Assert.DoesNotContain(references, r => r.TargetDocumentId == target.Id && r.RelationshipKind == RelationshipKind);
        Assert.Empty(await context.RelationshipRepository.GetOutgoingAsync(source.Id));
    }

    [Fact]
    public async Task CreateAsync_TargetAlreadySuperseded_ThrowsSupersededEngineeringObjectException_AndWritesNothing()
    {
        var context = TestEngineeringDomain.NewContext();
        var source = await CreatePartAsync(context, "PRT-141-3", "Bracket");
        var target = await CreateSupersededPartAsync(context, "PRT-141-4", "Housing");
        var factory = new EngineeringRelationshipFactory(RelationshipKind, RelationshipCategory.Reference, context);

        var exception = await Assert.ThrowsAsync<SupersededEngineeringObjectException>(
            () => factory.CreateAsync(source.Id, target.Id));

        Assert.Equal(target.Id, exception.ObjectId);

        var references = await context.Store.GetReferencesAsync(source.Id);
        Assert.DoesNotContain(references, r => r.TargetDocumentId == target.Id && r.RelationshipKind == RelationshipKind);
        Assert.Empty(await context.RelationshipRepository.GetOutgoingAsync(source.Id));
    }

    /// <summary>The guard is not merely a latch on the id: once neither end is superseded, the identical call succeeds.</summary>
    [Fact]
    public async Task CreateAsync_NeitherEndSuperseded_Succeeds_AndIsVisibleToTheRelationshipRepository()
    {
        var context = TestEngineeringDomain.NewContext();
        var source = await CreatePartAsync(context, "PRT-141-5", "Bracket");
        var target = await CreatePartAsync(context, "PRT-141-6", "Housing");
        var factory = new EngineeringRelationshipFactory(RelationshipKind, RelationshipCategory.Reference, context);

        var relationship = await factory.CreateAsync(source.Id, target.Id);

        Assert.Equal(source.Id, relationship.SourceId);
        Assert.Equal(target.Id, relationship.TargetId);
        Assert.Contains(await context.RelationshipRepository.GetOutgoingAsync(source.Id), r => r.TargetId == target.Id);

        var references = await context.Store.GetReferencesAsync(source.Id);
        Assert.Contains(references, r => r.TargetDocumentId == target.Id && r.RelationshipKind == RelationshipKind);
    }
}
