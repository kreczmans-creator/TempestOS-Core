using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Covers `WP 9.0A`'s three real Kind-keyed Workspace providers —
/// <see cref="MechanicalProductStructureNodeProvider"/>,
/// <see cref="MechanicalPropertyFacetProvider"/>, and
/// <see cref="MechanicalWorkspaceViewFactory"/>/<see cref="MechanicalWorkspaceView"/>
/// — directly against a real, in-memory <see cref="EngineeringDomainContext"/>,
/// mirroring <c>StructuralMutationTests</c>'s own lightweight construction
/// (no Runtime Host needed — none of these three classes depends on one).
/// </summary>
public class MechanicalNodeProviderAndFacetsTests
{
    private const string AreaKind = "tempest.mechanical.product-structure";

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

    private static async Task<Project> CreateProjectAsync(EngineeringDomainContext context, string identifier = "PROJ-1", string name = "Project")
    {
        var factory = new EngineeringObjectFactory<Project>(
            "Project", context, (doc, rev) => new Project(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Project)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
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

    // ---- MechanicalProductStructureNodeProvider ----

    [Fact]
    public async Task GetRootNodesAsync_ReturnsOnlyLiveProjects()
    {
        var context = BuildContext();
        var project = await CreateProjectAsync(context);
        var deletedProject = await CreateProjectAsync(context, "PROJ-2", "Deleted Project");
        await deletedProject.DeleteAsync();

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var roots = await provider.GetRootNodesAsync();

        var root = Assert.Single(roots);
        Assert.Equal(project.Id, root.Id);
        Assert.Equal("Project", root.Kind);
        Assert.Equal(ProjectExplorerNodeType.Object, root.NodeType);
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsLiveObjectsWhoseParentIdMatches()
    {
        var context = BuildContext();
        var project = await CreateProjectAsync(context);
        var assembly = await CreateAssemblyAsync(context);
        await assembly.MoveAsync(project.Id);
        var part = await CreatePartAsync(context);
        await part.MoveAsync(project.Id);

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var children = await provider.GetChildrenAsync(project.Id);

        Assert.Equal(2, children.Count);
        Assert.Contains(children, n => n.Id == assembly.Id);
        Assert.Contains(children, n => n.Id == part.Id);
    }

    [Fact]
    public async Task GetChildrenAsync_ExcludesDeletedChildren()
    {
        var context = BuildContext();
        var project = await CreateProjectAsync(context);
        var part = await CreatePartAsync(context);
        await part.MoveAsync(project.Id);
        await part.DeleteAsync();

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var children = await provider.GetChildrenAsync(project.Id);

        Assert.Empty(children);
    }

    [Fact]
    public async Task GetChildrenAsync_UnknownNodeId_ThrowsArgumentException()
    {
        var context = BuildContext();
        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetChildrenAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetRootNodesAsync_HasChildrenReflectsLiveChildren()
    {
        var context = BuildContext();
        var project = await CreateProjectAsync(context);
        var assembly = await CreateAssemblyAsync(context);
        await assembly.MoveAsync(project.Id);

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var roots = await provider.GetRootNodesAsync();

        Assert.True(roots[0].HasChildren);
    }

    [Fact]
    public async Task GetAncestryAsync_ReturnsParentChainRootFirst()
    {
        var context = BuildContext();
        var project = await CreateProjectAsync(context);
        var assembly = await CreateAssemblyAsync(context);
        await assembly.MoveAsync(project.Id);
        var part = await CreatePartAsync(context);
        await part.MoveAsync(assembly.Id);

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var ancestry = await provider.GetAncestryAsync(part.Id);

        Assert.Equal(2, ancestry.Count);
        Assert.Equal(project.Id, ancestry[0].Id);
        Assert.Equal(assembly.Id, ancestry[1].Id);
    }

    // ---- MechanicalPropertyFacetProvider ----

    [Fact]
    public async Task GetFacetsAsync_UnknownObjectId_ThrowsArgumentException()
    {
        var context = BuildContext();
        var provider = new MechanicalPropertyFacetProvider("Project", context);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetFacetsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetFacetsAsync_IncludesIdentityAndMetadataFacets()
    {
        var context = BuildContext();
        var project = await CreateProjectAsync(context, "PROJ-1", "Falcon Project");

        var provider = new MechanicalPropertyFacetProvider("Project", context);
        var facets = await provider.GetFacetsAsync(project.Id);

        Assert.Contains(facets, f => f.Name == "Name" && f.Value == "Falcon Project");
        Assert.Contains(facets, f => f.Name == "Engineering Identifier" && f.Value == "PROJ-1");
        Assert.Contains(facets, f => f.Name == "Status" && f.Value == "Draft");
        Assert.Contains(facets, f => f.Name == "Released" && f.Value == "No");
    }

    [Fact]
    public async Task GetFacetsAsync_IncludesParentFacet()
    {
        var context = BuildContext();
        var project = await CreateProjectAsync(context);
        var assembly = await CreateAssemblyAsync(context);
        await assembly.MoveAsync(project.Id);

        var provider = new MechanicalPropertyFacetProvider("Assembly", context);
        var facets = await provider.GetFacetsAsync(assembly.Id);

        Assert.Contains(facets, f => f.Name == "Parent" && f.Value == project.Id.ToString());
    }

    [Fact]
    public async Task GetFacetsAsync_TopLevelObject_ParentFacetIsTopLevel()
    {
        var context = BuildContext();
        var project = await CreateProjectAsync(context);

        var provider = new MechanicalPropertyFacetProvider("Project", context);
        var facets = await provider.GetFacetsAsync(project.Id);

        Assert.Contains(facets, f => f.Name == "Parent" && f.Value == "(top level)");
    }

    [Fact]
    public async Task GetFacetsAsync_DeletedObject_IncludesDeletedFacet()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);
        await part.DeleteAsync();

        var provider = new MechanicalPropertyFacetProvider("Part", context);
        var facets = await provider.GetFacetsAsync(part.Id);

        Assert.Contains(facets, f => f.Name == "Deleted" && f.Value == "Yes");
    }

    [Fact]
    public async Task GetFacetsAsync_ObjectReferencedByAConfiguration_IncludesBaselineFacet()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);

        var configurationFactory = new EngineeringObjectFactory<Tempest.Core.EngineeringDomain.Configuration>(
            "Configuration", context, (doc, rev) => new Tempest.Core.EngineeringDomain.Configuration(
                doc, rev, context, "CFG-1", "Baseline Rev A", EngineeringObjectMetadata.Empty,
                new[] { new ConfigurationMember(assembly.Id, assembly.CurrentRevisionNumber) }));
        await configurationFactory.CreateAsync("Baseline content.");

        var provider = new MechanicalPropertyFacetProvider("Assembly", context);
        var facets = await provider.GetFacetsAsync(assembly.Id);

        Assert.Contains(facets, f => f.Name == "Baseline" && f.Value == "Baseline Rev A");
    }

    [Fact]
    public async Task GetFacetsAsync_ObjectNotInAnyConfiguration_HasNoBaselineFacet()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);

        var provider = new MechanicalPropertyFacetProvider("Assembly", context);
        var facets = await provider.GetFacetsAsync(assembly.Id);

        Assert.DoesNotContain(facets, f => f.Name == "Baseline");
    }

    // ---- MechanicalWorkspaceViewFactory / MechanicalWorkspaceView ----

    [Fact]
    public async Task Create_UnknownObjectId_ThrowsArgumentException()
    {
        var context = BuildContext();
        var factory = new MechanicalWorkspaceViewFactory("Project", context);

        Assert.Throws<ArgumentException>(() => factory.Create(Guid.NewGuid(), new WorkspaceContext()));
    }

    [Fact]
    public async Task Create_ReturnsViewWithCorrectTitleAndKind()
    {
        var context = BuildContext();
        var project = await CreateProjectAsync(context, "PROJ-1", "Falcon Project");
        var factory = new MechanicalWorkspaceViewFactory("Project", context);

        var view = factory.Create(project.Id, new WorkspaceContext());

        Assert.Equal("Falcon Project", view.Title);
        Assert.Equal(project.Id, view.ObjectId);
        Assert.Equal("Project", view.ObjectKind);
        Assert.False(view.IsDirty);
    }

    [Fact]
    public async Task RefreshAsync_PicksUpARenameMadeAfterTheViewWasCreated()
    {
        var context = BuildContext();
        var project = await CreateProjectAsync(context, "PROJ-1", "Original Name");
        var factory = new MechanicalWorkspaceViewFactory("Project", context);
        var view = factory.Create(project.Id, new WorkspaceContext());

        await project.RenameAsync("Renamed Project");
        await view.RefreshAsync();

        Assert.Equal("Renamed Project", view.Title);
    }

    [Fact]
    public async Task CloseAsync_AlwaysReturnsTrue()
    {
        var context = BuildContext();
        var project = await CreateProjectAsync(context);
        var factory = new MechanicalWorkspaceViewFactory("Project", context);
        var view = factory.Create(project.Id, new WorkspaceContext());

        Assert.True(await view.CloseAsync());
    }

    // ---- WP 9.0B: BOM-aware node titles and sibling ordering ----

    [Fact]
    public async Task GetChildrenAsync_NoBomLineSet_TitleIsPlainDisplayName()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var part = await CreatePartAsync(context, "PART-1", "Plain Part");
        await part.MoveAsync(assembly.Id);

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var children = await provider.GetChildrenAsync(assembly.Id);

        Assert.Equal("Plain Part", children[0].Title);
    }

    [Fact]
    public async Task GetChildrenAsync_BomLineSet_TitleIncludesItemNumberAndQuantity()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var part = await CreatePartAsync(context, "PART-1", "Bolt");
        await part.MoveAsync(assembly.Id);
        await part.SetBomLineAsync(4m, "EA", itemNumber: "0010");

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var children = await provider.GetChildrenAsync(assembly.Id);

        Assert.Equal("0010 ×4 Bolt", children[0].Title);
    }

    [Fact]
    public async Task GetChildrenAsync_EveryChildHasItemNumber_OrdersNumerically()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var partA = await CreatePartAsync(context, "PART-1", "Part A");
        var partB = await CreatePartAsync(context, "PART-2", "Part B");
        var partC = await CreatePartAsync(context, "PART-3", "Part C");
        await partA.MoveAsync(assembly.Id);
        await partB.MoveAsync(assembly.Id);
        await partC.MoveAsync(assembly.Id);
        await partA.SetBomLineAsync(1m, itemNumber: "0030");
        await partB.SetBomLineAsync(1m, itemNumber: "0010");
        await partC.SetBomLineAsync(1m, itemNumber: "0020");

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var children = await provider.GetChildrenAsync(assembly.Id);

        Assert.Equal(new[] { partB.Id, partC.Id, partA.Id }, children.Select(c => c.Id));
    }

    [Fact]
    public async Task GetChildrenAsync_SomeChildrenLackItemNumber_IsNotReorderedByItemNumber()
    {
        // No ordering guarantee is claimed when any sibling lacks an Item
        // Number — only that a partially-numbered BOM isn't reordered
        // around the numbered subset. The repository's own iteration order
        // (InMemoryEngineeringObjectRepository, a ConcurrentDictionary) is
        // itself unspecified, so this asserts membership, not sequence.
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);
        var partA = await CreatePartAsync(context, "PART-1", "Part A");
        var partB = await CreatePartAsync(context, "PART-2", "Part B");
        await partA.MoveAsync(assembly.Id);
        await partB.MoveAsync(assembly.Id);
        await partA.SetBomLineAsync(1m, itemNumber: "0010");
        // partB never gets an Item Number.

        var provider = new MechanicalProductStructureNodeProvider(AreaKind, context);
        var children = await provider.GetChildrenAsync(assembly.Id);

        Assert.Equal(new HashSet<Guid> { partA.Id, partB.Id }, children.Select(c => c.Id).ToHashSet());
    }

    // ---- WP 9.0B: BOM/Configuration facets ----

    [Fact]
    public async Task GetFacetsAsync_BomLineSet_IncludesAllFiveBomFacets()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);
        await part.SetBomLineAsync(4m, "EA", "10", "0010", "J1-J4");

        var provider = new MechanicalPropertyFacetProvider("Part", context);
        var facets = await provider.GetFacetsAsync(part.Id);

        Assert.Contains(facets, f => f.Name == "Quantity" && f.Value == "4");
        Assert.Contains(facets, f => f.Name == "Unit of Measure" && f.Value == "EA");
        Assert.Contains(facets, f => f.Name == "Find Number" && f.Value == "10");
        Assert.Contains(facets, f => f.Name == "Item Number" && f.Value == "0010");
        Assert.Contains(facets, f => f.Name == "Reference Designator" && f.Value == "J1-J4");
    }

    [Fact]
    public async Task GetFacetsAsync_ConfigurationObject_IncludesMemberCount()
    {
        var context = BuildContext();
        var part = await CreatePartAsync(context);
        var configurationFactory = new EngineeringObjectFactory<Tempest.Core.EngineeringDomain.Configuration>(
            "Configuration", context, (doc, rev) => new Tempest.Core.EngineeringDomain.Configuration(
                doc, rev, context, "CFG-1", "Working Set", EngineeringObjectMetadata.Empty,
                new[] { new ConfigurationMember(part.Id, 1) }));
        var configuration = (Tempest.Core.EngineeringDomain.Configuration)await configurationFactory.CreateAsync("content.");

        var provider = new MechanicalPropertyFacetProvider("Configuration", context);
        var facets = await provider.GetFacetsAsync(configuration.Id);

        Assert.Contains(facets, f => f.Name == "Configuration Members" && f.Value == "1");
    }

    [Fact]
    public async Task GetFacetsAsync_ObjectReferencedByABaseline_IncludesBaselineFacet()
    {
        var context = BuildContext();
        var assembly = await CreateAssemblyAsync(context);

        var baselineFactory = new EngineeringObjectFactory<Baseline>(
            "Baseline", context, (doc, rev) => new Baseline(
                doc, rev, context, "BASE-1", "Rev A Baseline", EngineeringObjectMetadata.Empty,
                new[] { new ConfigurationMember(assembly.Id, assembly.CurrentRevisionNumber) }));
        await baselineFactory.CreateAsync("content.");

        var provider = new MechanicalPropertyFacetProvider("Assembly", context);
        var facets = await provider.GetFacetsAsync(assembly.Id);

        Assert.Contains(facets, f => f.Name == "Baseline" && f.Value == "Rev A Baseline");
    }
}
