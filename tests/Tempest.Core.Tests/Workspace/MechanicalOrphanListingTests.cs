using Tempest.Workspace;
using Tempest.Workspace.Mechanical;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Tests.EngineeringDomain;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP 17.9.2`: every live structural object is reachable from a root of
/// the Mechanical Product Structure tree. Before this, a Part that hung
/// from nothing existed in the store and appeared nowhere.
/// </summary>
public class MechanicalOrphanListingTests
{
    private const string AreaKind = "tempest.mechanical.product-structure";

    private static async Task<Project> CreateProjectAsync(EngineeringDomainContext context)
    {
        var factory = new EngineeringObjectFactory<Project>(
            "Project", context, (doc, rev) => new Project(doc, rev, context, "PROJ-1", "Project", EngineeringObjectMetadata.Empty));

        return (Project)await factory.CreateAsync("Project — for test purposes.");
    }

    private static async Task<Part> CreatePartAsync(EngineeringDomainContext context, string name)
    {
        var factory = new EngineeringObjectFactory<Part>(
            "Part", context, (doc, rev) => new Part(doc, rev, context, name, name, EngineeringObjectMetadata.Empty));

        return (Part)await factory.CreateAsync($"{name} — for test purposes.");
    }

    [Fact]
    public async Task ATreeWithNoOrphans_HasNoOrphanCategory()
    {
        var context = TestEngineeringDomain.NewContext();
        var project = await CreateProjectAsync(context);
        var part = await CreatePartAsync(context, "Placed Part");
        await part.MoveAsync(project.Id);

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var roots = await provider.GetRootNodesAsync();

        var root = Assert.Single(roots);
        Assert.Equal(project.Id, root.Id);
    }

    [Fact]
    public async Task AParentlessPart_IsListedUnderNotInAnyProject_AndItsAncestryLeadsThere()
    {
        var context = TestEngineeringDomain.NewContext();
        var project = await CreateProjectAsync(context);
        var orphan = await CreatePartAsync(context, "Orphan Part");

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var roots = await provider.GetRootNodesAsync();

        Assert.Equal(2, roots.Count);
        Assert.Equal(project.Id, roots[0].Id);
        var category = roots[1];
        Assert.Equal(MechanicalProductStructureNodeProvider.NotInAnyProjectNodeId, category.Id);
        Assert.Equal(MechanicalProductStructureNodeProvider.NotInAnyProjectTitle, category.Title);
        Assert.Equal(ProjectExplorerNodeType.Category, category.NodeType);
        Assert.True(category.HasChildren);

        var listed = Assert.Single(await provider.GetChildrenAsync(category.Id));
        Assert.Equal(orphan.Id, listed.Id);
        Assert.Equal("Part", listed.Kind);

        var ancestry = await provider.GetAncestryAsync(orphan.Id);
        Assert.Equal(MechanicalProductStructureNodeProvider.NotInAnyProjectNodeId, Assert.Single(ancestry).Id);
    }

    [Fact]
    public async Task APartUnderAParentlessAssembly_IsReachedThroughTheCategoryThenTheAssembly()
    {
        var context = TestEngineeringDomain.NewContext();
        var assemblyFactory = new EngineeringObjectFactory<Assembly>(
            "Assembly", context, (doc, rev) => new Assembly(doc, rev, context, "ASM-1", "Loose Assembly", EngineeringObjectMetadata.Empty));
        var assembly = (Assembly)await assemblyFactory.CreateAsync("Loose Assembly — for test purposes.");
        var part = await CreatePartAsync(context, "Nested Part");
        await part.MoveAsync(assembly.Id);

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);

        var orphans = await provider.GetChildrenAsync(MechanicalProductStructureNodeProvider.NotInAnyProjectNodeId);
        Assert.Equal(assembly.Id, Assert.Single(orphans).Id);

        var ancestry = await provider.GetAncestryAsync(part.Id);
        Assert.Equal(
            [MechanicalProductStructureNodeProvider.NotInAnyProjectNodeId, assembly.Id],
            ancestry.Select(n => n.Id).ToList());
    }

    [Fact]
    public async Task APlacedPart_AncestryEndsOnTheProject_WithNoCategory()
    {
        var context = TestEngineeringDomain.NewContext();
        var project = await CreateProjectAsync(context);
        var part = await CreatePartAsync(context, "Placed Part");
        await part.MoveAsync(project.Id);

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var ancestry = await provider.GetAncestryAsync(part.Id);

        Assert.Equal(project.Id, Assert.Single(ancestry).Id);
    }
}
