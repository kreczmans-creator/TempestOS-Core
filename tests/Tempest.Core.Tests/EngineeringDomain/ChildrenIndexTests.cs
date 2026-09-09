using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Tests.EngineeringDomain;

/// <summary>
/// `WP 17.9.3`, closing hazard H4 of the design-freeze review: "children of
/// X" is an indexed lookup on the in-memory repository, maintained by
/// registration and by the one mutator of a parent, and it must always agree
/// with a scan of every object.
/// </summary>
public class ChildrenIndexTests
{
    private static async Task<Project> CreateProjectAsync(EngineeringDomainContext context, string name)
    {
        var factory = new EngineeringObjectFactory<Project>(
            "Project", context, (doc, rev) => new Project(doc, rev, context, name, name, EngineeringObjectMetadata.Empty));

        return (Project)await factory.CreateAsync($"{name} — for test purposes.");
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string name)
    {
        var factory = new EngineeringObjectFactory<Part>(
            "Part", context, (doc, rev) => new Part(doc, rev, context, name, name, EngineeringObjectMetadata.Empty));

        return (Part)await factory.CreateAsync($"{name} — for test purposes.");
    }

    private static async Task<IReadOnlyList<Guid>> ScanChildrenAsync(EngineeringDomainContext context, Guid parentId)
    {
        var all = await context.Repository.ListAllAsync();
        return all.Where(o => o is IHasParent { ParentId: { } pid } && pid == parentId).Select(o => o.Id).OrderBy(id => id).ToList();
    }

    private static async Task<IReadOnlyList<Guid>> IndexedChildrenAsync(EngineeringDomainContext context, Guid parentId) =>
        (await context.Repository.ListChildrenAsync(parentId)).Select(o => o.Id).OrderBy(id => id).ToList();

    [Fact]
    public async Task AfterMoves_TheIndexAgreesWithAScan_ForEveryParent()
    {
        var context = TestEngineeringDomain.NewContext();
        var project = await CreateProjectAsync(context, "P-1");
        var otherProject = await CreateProjectAsync(context, "P-2");
        var first = await CreatePartAsync(context, "First");
        var second = await CreatePartAsync(context, "Second");
        var third = await CreatePartAsync(context, "Third");

        await first.MoveAsync(project.Id);
        await second.MoveAsync(project.Id);
        await third.MoveAsync(otherProject.Id);

        Assert.Equal(await ScanChildrenAsync(context, project.Id), await IndexedChildrenAsync(context, project.Id));
        Assert.Equal(await ScanChildrenAsync(context, otherProject.Id), await IndexedChildrenAsync(context, otherProject.Id));
        Assert.Equal(2, (await IndexedChildrenAsync(context, project.Id)).Count);
    }

    [Fact]
    public async Task MovingAChildAway_RemovesItFromTheOldParent_AndAddsItToTheNew()
    {
        var context = TestEngineeringDomain.NewContext();
        var project = await CreateProjectAsync(context, "P-1");
        var otherProject = await CreateProjectAsync(context, "P-2");
        var part = await CreatePartAsync(context, "Wandering Part");

        await part.MoveAsync(project.Id);
        Assert.Contains(part.Id, await IndexedChildrenAsync(context, project.Id));

        await part.MoveAsync(otherProject.Id);
        Assert.DoesNotContain(part.Id, await IndexedChildrenAsync(context, project.Id));
        Assert.Contains(part.Id, await IndexedChildrenAsync(context, otherProject.Id));

        await part.MoveAsync(null);
        Assert.DoesNotContain(part.Id, await IndexedChildrenAsync(context, otherProject.Id));
        Assert.Empty(await ScanChildrenAsync(context, otherProject.Id));
    }

    [Fact]
    public async Task AnUnknownParent_HasNoChildren_RatherThanAnError()
    {
        var context = TestEngineeringDomain.NewContext();

        Assert.Empty(await context.Repository.ListChildrenAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ADeletedChild_IsStillListed_SoCallersFilterLivenessAsTheyAlwaysDid()
    {
        var context = TestEngineeringDomain.NewContext();
        var project = await CreateProjectAsync(context, "P-1");
        var part = await CreatePartAsync(context, "Doomed Part");
        await part.MoveAsync(project.Id);

        await part.DeleteAsync();

        var listed = Assert.Single(await context.Repository.ListChildrenAsync(project.Id));
        Assert.True(((IDeletable)listed).IsDeleted);
    }

    [Fact]
    public async Task ADeleteIsRefusedWhileALiveChildExists_UsingTheIndex()
    {
        var context = TestEngineeringDomain.NewContext();
        var project = await CreateProjectAsync(context, "P-1");
        var part = await CreatePartAsync(context, "Held Part");
        await part.MoveAsync(project.Id);

        await Assert.ThrowsAnyAsync<Exception>(() => project.DeleteAsync());
        Assert.False(project.IsDeleted);

        await part.DeleteAsync();
        await project.DeleteAsync();
        Assert.True(project.IsDeleted);
    }
}
