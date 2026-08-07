using System.Text.Json;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.Core.Calculations;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Identity;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// Covers `WP 9.2A`'s three real Kind-keyed Workspace providers —
/// <see cref="CalculationsNodeProvider"/>, <see cref="CalculationsPropertyFacetProvider"/>,
/// <see cref="CalculationsWorkspaceViewFactory"/>/<see cref="CalculationsWorkspaceView"/>
/// — plus <see cref="CalculationTemplateRegistry"/> and
/// <see cref="CalculationRecordReader"/>, directly against a real,
/// in-memory <see cref="EngineeringDomainContext"/> and a real
/// <see cref="CalculationEngine"/>, mirroring
/// <c>MechanicalNodeProviderAndFacetsTests</c>'s own lightweight
/// construction.
/// </summary>
public class CalculationsNodeProviderAndFacetsTests
{
    private const string AreaKind = "tempest.calculations.management";
    private const string SampleCalculationId = "test.double-length";

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

    private static async Task<Calculation> CreateCalculationAsync(EngineeringDomainContext context, string identifier = "CALC-1", string name = "Calculation")
    {
        var factory = new EngineeringObjectFactory<Calculation>(
            "Calculation", context, (doc, rev) => new Calculation(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty));

        return (Calculation)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    private static async Task<CalculationSet> CreateCalculationSetAsync(
        EngineeringDomainContext context, string identifier, string name, IReadOnlyList<Guid>? members = null)
    {
        var factory = new EngineeringObjectFactory<CalculationSet>(
            "CalculationSet", context, (doc, rev) => new CalculationSet(doc, rev, context, identifier, name, EngineeringObjectMetadata.Empty, members));

        return (CalculationSet)await factory.CreateAsync($"{name} — for test purposes.").ConfigureAwait(false);
    }

    private static CalculationTemplateRegistry BuildTemplateRegistry(EngineeringDomainContext context, out ICalculationEngine engine)
    {
        engine = new CalculationEngine(context.Store, context.CurrentPrincipalAccessor);
        engine.RegisterDefinition(new TestDoubleLengthDefinition());

        var registry = new CalculationTemplateRegistry(engine, context);
        registry.Register<Quantity<Length>, Quantity<Length>>(SampleCalculationId, new TestDoubleLengthDefinition().Metadata);

        return registry;
    }

    // ---- CalculationsNodeProvider ----

    [Fact]
    public async Task GetRootNodesAsync_IncludesTemplatesCategoryNode()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var provider = new CalculationsNodeProvider(AreaKind, context, templateRegistry);

        var roots = await provider.GetRootNodesAsync();

        Assert.Contains(roots, n => n.Id == CalculationsNodeProvider.TemplatesNodeId && n.NodeType == ProjectExplorerNodeType.Category);
    }

    [Fact]
    public async Task GetChildrenAsync_TemplatesNode_ReturnsEveryRegisteredTemplate()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var provider = new CalculationsNodeProvider(AreaKind, context, templateRegistry);

        var children = await provider.GetChildrenAsync(CalculationsNodeProvider.TemplatesNodeId);

        var node = Assert.Single(children);
        Assert.Equal("CalculationTemplate", node.Kind);
        Assert.Equal("Double Length (Test)", node.Title);
    }

    [Fact]
    public async Task GetRootNodesAsync_ReturnsOnlyLiveUnparentedCalculationsAndSets()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var calculation = await CreateCalculationAsync(context);
        var deleted = await CreateCalculationAsync(context, "CALC-2", "Deleted");
        await deleted.DeleteAsync();
        var set = await CreateCalculationSetAsync(context, "SET-1", "Set");

        var provider = new CalculationsNodeProvider(AreaKind, context, templateRegistry);
        var roots = await provider.GetRootNodesAsync();

        Assert.Contains(roots, n => n.Id == calculation.Id);
        Assert.Contains(roots, n => n.Id == set.Id && n.NodeType == ProjectExplorerNodeType.Collection);
        Assert.DoesNotContain(roots, n => n.Id == deleted.Id);
    }

    [Fact]
    public async Task GetChildrenAsync_CalculationSet_ReturnsLiveMembers()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var member = await CreateCalculationAsync(context);
        var set = await CreateCalculationSetAsync(context, "SET-1", "Set", [member.Id]);

        var provider = new CalculationsNodeProvider(AreaKind, context, templateRegistry);
        var children = await provider.GetChildrenAsync(set.Id);

        var node = Assert.Single(children);
        Assert.Equal(member.Id, node.Id);
    }

    [Fact]
    public async Task GetChildrenAsync_ParentedCalculation_ReturnsItsOwnChildren()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var parent = await CreateCalculationAsync(context, "CALC-1", "Parent");
        var child = await CreateCalculationAsync(context, "CALC-2", "Child");
        await child.MoveAsync(parent.Id);

        var provider = new CalculationsNodeProvider(AreaKind, context, templateRegistry);
        var children = await provider.GetChildrenAsync(parent.Id);

        var node = Assert.Single(children);
        Assert.Equal(child.Id, node.Id);
    }

    [Fact]
    public async Task GetChildrenAsync_UnknownNodeId_ThrowsArgumentException()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var provider = new CalculationsNodeProvider(AreaKind, context, templateRegistry);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetChildrenAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAncestryAsync_ReturnsParentChainRootFirst()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var grandparent = await CreateCalculationAsync(context, "CALC-1", "Grandparent");
        var parent = await CreateCalculationAsync(context, "CALC-2", "Parent");
        await parent.MoveAsync(grandparent.Id);
        var child = await CreateCalculationAsync(context, "CALC-3", "Child");
        await child.MoveAsync(parent.Id);

        var provider = new CalculationsNodeProvider(AreaKind, context, templateRegistry);
        var ancestry = await provider.GetAncestryAsync(child.Id);

        Assert.Equal(2, ancestry.Count);
        Assert.Equal(grandparent.Id, ancestry[0].Id);
        Assert.Equal(parent.Id, ancestry[1].Id);
    }

    // ---- CalculationsPropertyFacetProvider ----

    [Fact]
    public async Task GetFacetsAsync_TemplateNode_IncludesNameAndAssumptions()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var template = templateRegistry.FindByCalculationId(SampleCalculationId)!;

        var provider = new CalculationsPropertyFacetProvider("CalculationTemplate", context, templateRegistry);
        var facets = await provider.GetFacetsAsync(template.NodeId);

        Assert.Contains(facets, f => f.Name == "Name" && f.Value == "Double Length (Test)");
        Assert.Contains(facets, f => f.Name == "Calculation Id" && f.Value == SampleCalculationId);
        Assert.Contains(facets, f => f.Name == "Assumptions");
    }

    [Fact]
    public async Task GetFacetsAsync_TemplateNode_UnknownNodeId_ThrowsArgumentException()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var provider = new CalculationsPropertyFacetProvider("CalculationTemplate", context, templateRegistry);

        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetFacetsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetFacetsAsync_Calculation_IncludesIdentityStatusAndApprovalFacets()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var calculation = await CreateCalculationAsync(context, "CALC-1", "Bolt Check");

        var provider = new CalculationsPropertyFacetProvider("Calculation", context, templateRegistry);
        var facets = await provider.GetFacetsAsync(calculation.Id);

        Assert.Contains(facets, f => f.Name == "Name" && f.Value == "Bolt Check");
        Assert.Contains(facets, f => f.Name == "Status" && f.Value == "Draft");
        Assert.Contains(facets, f => f.Name == "Approved" && f.Value == "No");
        Assert.Contains(facets, f => f.Name == "Result History" && f.Value == "Never executed");
    }

    [Fact]
    public async Task GetFacetsAsync_ApprovedCalculation_ApprovalFacetIsYes()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var calculation = await CreateCalculationAsync(context);
        await calculation.TransitionAsync(LifecycleState.InReview);
        await calculation.TransitionAsync(LifecycleState.Approved);

        var provider = new CalculationsPropertyFacetProvider("Calculation", context, templateRegistry);
        var facets = await provider.GetFacetsAsync(calculation.Id);

        Assert.Contains(facets, f => f.Name == "Approved" && f.Value == "Yes");
    }

    [Fact]
    public async Task GetFacetsAsync_ExecutedCalculation_IncludesLatestResultAndSafetyFactor()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out var engine);
        var calculation = await CreateCalculationAsync(context);

        var record = await engine.ExecuteAsync<Quantity<Length>, Quantity<Length>>(
            SampleCalculationId, new Quantity<Length>(5.0, LengthUnits.Metre));
        await calculation.LinkAsync(record.Id, CalculationTemplateRegistry.CalculatedByRelationshipKind);

        var provider = new CalculationsPropertyFacetProvider("Calculation", context, templateRegistry);
        var facets = await provider.GetFacetsAsync(calculation.Id);

        Assert.Contains(facets, f => f.Name == "Result History" && f.Value == "1 execution(s)");
        Assert.Contains(facets, f => f.Name == "Latest Result" && f.Value == "10 m");
        Assert.Contains(facets, f => f.Name == "Latest Result Outcome" && f.Value == "Valid");
        Assert.Contains(facets, f => f.Name == "Safety Factor" && f.Value == "1");
        Assert.Contains(facets, f => f.Name == "Assumptions");
    }

    [Fact]
    public async Task GetFacetsAsync_CalculationSet_IncludesMemberCount()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var member = await CreateCalculationAsync(context);
        var set = await CreateCalculationSetAsync(context, "SET-1", "Set", [member.Id]);

        var provider = new CalculationsPropertyFacetProvider("CalculationSet", context, templateRegistry);
        var facets = await provider.GetFacetsAsync(set.Id);

        Assert.Contains(facets, f => f.Name == "Members" && f.Value == "1");
    }

    [Fact]
    public async Task GetFacetsAsync_DigitalThreadLink_IncludesUsedByFacet()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var subject = await CreateCalculationAsync(context, "CALC-1", "Subject Part Stand-in");
        var calculation = await CreateCalculationAsync(context, "CALC-2", "Calculation");
        await subject.LinkAsync(calculation.Id, CalculationTemplateRegistry.CalculatedByRelationshipKind);

        var provider = new CalculationsPropertyFacetProvider("Calculation", context, templateRegistry);
        var facets = await provider.GetFacetsAsync(calculation.Id);

        Assert.Contains(facets, f => f.Name == "Used By (Digital Thread)" && f.Value == subject.Id.ToString());
    }

    // ---- CalculationsWorkspaceViewFactory / CalculationsWorkspaceView ----

    [Fact]
    public async Task Create_Calculation_ReturnsViewWithCorrectTitleAndKind()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var calculation = await CreateCalculationAsync(context, "CALC-1", "Bolt Check");
        var factory = new CalculationsWorkspaceViewFactory("Calculation", context, templateRegistry);

        var view = factory.Create(calculation.Id, new WorkspaceContext());

        Assert.Equal("Bolt Check", view.Title);
        Assert.Equal("Calculation", view.ObjectKind);
        Assert.False(view.IsDirty);
    }

    [Fact]
    public void Create_Template_ReturnsViewWithTemplateName()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var template = templateRegistry.FindByCalculationId(SampleCalculationId)!;
        var factory = new CalculationsWorkspaceViewFactory("CalculationTemplate", context, templateRegistry);

        var view = factory.Create(template.NodeId, new WorkspaceContext());

        Assert.Equal("Double Length (Test)", view.Title);
    }

    [Fact]
    public void Create_UnknownObjectId_ThrowsArgumentException()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var factory = new CalculationsWorkspaceViewFactory("Calculation", context, templateRegistry);

        Assert.Throws<ArgumentException>(() => factory.Create(Guid.NewGuid(), new WorkspaceContext()));
    }

    [Fact]
    public async Task RefreshAsync_PicksUpARenameMadeAfterTheViewWasCreated()
    {
        var context = BuildContext();
        var templateRegistry = BuildTemplateRegistry(context, out _);
        var calculation = await CreateCalculationAsync(context, "CALC-1", "Original Name");
        var factory = new CalculationsWorkspaceViewFactory("Calculation", context, templateRegistry);
        var view = factory.Create(calculation.Id, new WorkspaceContext());

        await calculation.RenameAsync("Renamed Calculation");
        await view.RefreshAsync();

        Assert.Equal("Renamed Calculation", view.Title);
    }

    // ---- CalculationRecordReader ----

    [Fact]
    public async Task GetResultHistoryAsync_NoExecutions_ReturnsEmpty()
    {
        var context = BuildContext();
        var calculation = await CreateCalculationAsync(context);

        var history = await CalculationRecordReader.GetResultHistoryAsync(context, calculation.Id);

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetLatestAsync_MultipleExecutions_ReturnsTheMostRecentOne()
    {
        var context = BuildContext();
        BuildTemplateRegistry(context, out var engine);
        var calculation = await CreateCalculationAsync(context);

        var first = await engine.ExecuteAsync<Quantity<Length>, Quantity<Length>>(SampleCalculationId, new Quantity<Length>(1.0, LengthUnits.Metre));
        await calculation.LinkAsync(first.Id, CalculationTemplateRegistry.CalculatedByRelationshipKind);
        var second = await engine.ExecuteAsync<Quantity<Length>, Quantity<Length>>(SampleCalculationId, new Quantity<Length>(2.0, LengthUnits.Metre));
        await calculation.LinkAsync(second.Id, CalculationTemplateRegistry.CalculatedByRelationshipKind);

        var latest = await CalculationRecordReader.GetLatestAsync(context, calculation.Id);

        Assert.NotNull(latest);
        Assert.Equal(second.Id, latest.RecordId);
        Assert.Equal("4 m", latest.ResultDisplay);
    }

    /// <summary>A deliberately trivial, test-local calculation — mirrors <c>Tempest.Samples.DoubleLengthCalculationDefinition</c>'s own shape without depending on <c>Tempest.Samples</c>.</summary>
    private sealed class TestDoubleLengthDefinition : ICalculationDefinition<Quantity<Length>, Quantity<Length>>
    {
        public string CalculationId => SampleCalculationId;

        public CalculationMetadata Metadata { get; } = new(
            "Double Length (Test)", "Doubles a length.", "Test",
            [new CalculationAssumption("The input represents a valid physical length.", null)],
            [new CalculationConstraint("Input length must be positive.")]);

        public Quantity<Length> Calculate(Quantity<Length> input, CalculationContext context)
        {
            var isPositive = input.Value > 0;
            context.RecordConstraintCheck("Input length must be positive.", isPositive, $"Input value was {input.Value}.");

            if (!isPositive)
                throw new CalculationInputInvalidException($"Input length must be positive; received {input.Value}.");

            var doubled = input * 2.0;
            context.RecordIntermediate("Safety Factor", 1.0);

            return doubled;
        }
    }
}
