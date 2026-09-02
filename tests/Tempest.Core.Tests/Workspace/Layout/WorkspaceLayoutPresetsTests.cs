using Tempest.App.Workspace.Layout;

namespace Tempest.Core.Tests.Workspace.Layout;

/// <summary>
/// The named arrangements (`TD-72`) — the tree-based replacement for the
/// three fixed placement sets a preset used to be.
/// </summary>
public class WorkspaceLayoutPresetsTests
{
    private static readonly Guid Explorer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Document = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Inspector = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Output = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static WorkspaceLayoutTree Build(WorkspaceLayoutPreset preset) =>
        WorkspaceLayoutPresets.Build(preset, Explorer, Document, Inspector, Output);

    [Theory]
    [InlineData(WorkspaceLayoutPreset.Engineering)]
    [InlineData(WorkspaceLayoutPreset.Review)]
    [InlineData(WorkspaceLayoutPreset.Documentation)]
    public void EveryPreset_KeepsExplorerDocumentAndInspectorInTheArrangement(WorkspaceLayoutPreset preset)
    {
        var tree = Build(preset);

        Assert.Contains(Explorer, tree.AllPanels);
        Assert.Contains(Document, tree.AllPanels);
        Assert.Contains(Inspector, tree.AllPanels);
    }

    [Theory]
    [InlineData(WorkspaceLayoutPreset.Engineering)]
    [InlineData(WorkspaceLayoutPreset.Review)]
    [InlineData(WorkspaceLayoutPreset.Documentation)]
    public void EveryPreset_PutsExplorerLeftOfTheDocument_AndInspectorRightOfIt(WorkspaceLayoutPreset preset)
    {
        var tree = Build(preset);
        var order = tree.Root!.Panels.ToList();

        Assert.True(order.IndexOf(Explorer) < order.IndexOf(Document));
        Assert.True(order.IndexOf(Document) < order.IndexOf(Inspector));
    }

    [Theory]
    [InlineData(WorkspaceLayoutPreset.Engineering)]
    [InlineData(WorkspaceLayoutPreset.Review)]
    [InlineData(WorkspaceLayoutPreset.Documentation)]
    public void EveryPreset_GivesTheDocumentTheLargestShare(WorkspaceLayoutPreset preset)
    {
        // The pane the engineer actually works in is always the widest, in
        // every preset — which is also what makes the responsive rule
        // identify it without any special-casing.
        var tree = Build(preset);
        var root = Assert.IsType<LayoutSplitNode>(tree.Root);

        var documentIndex = root.Children.ToList().FindIndex(c => c.Panels.Contains(Document));

        Assert.Equal(root.Weights.Max(), root.Weights[documentIndex]);
    }

    [Fact]
    public void Review_ShowsTheOutputPanel_UnlikeEngineeringOrDocumentation()
    {
        Assert.Contains(Output, Build(WorkspaceLayoutPreset.Review).AllPanels);
        Assert.DoesNotContain(Output, Build(WorkspaceLayoutPreset.Engineering).AllPanels);
        Assert.DoesNotContain(Output, Build(WorkspaceLayoutPreset.Documentation).AllPanels);
    }

    [Fact]
    public void Review_PutsTheOutputPanelBelowTheDocument_NotBesideIt()
    {
        var tree = Build(WorkspaceLayoutPreset.Review);

        var vertical = tree.Root!.DescendantsAndSelf
            .OfType<LayoutSplitNode>()
            .Single(s => s.Orientation == LayoutOrientation.Vertical);

        Assert.Equal([Document, Output], vertical.Panels);
    }

    [Fact]
    public void Documentation_WidensExplorer_AndAutoHidesTheInspector()
    {
        var documentation = Build(WorkspaceLayoutPreset.Documentation);
        var engineering = Build(WorkspaceLayoutPreset.Engineering);

        var documentationRoot = (LayoutSplitNode)documentation.Root!;
        var engineeringRoot = (LayoutSplitNode)engineering.Root!;

        Assert.True(documentationRoot.Weights[0] > engineeringRoot.Weights[0]);
        Assert.False(documentation.PresentationOf(Inspector).IsPinned);
    }

    [Fact]
    public void EngineeringAndReview_KeepTheInspectorPinned_UnlikeDocumentation()
    {
        Assert.True(Build(WorkspaceLayoutPreset.Engineering).PresentationOf(Inspector).IsPinned);
        Assert.True(Build(WorkspaceLayoutPreset.Review).PresentationOf(Inspector).IsPinned);
        Assert.False(Build(WorkspaceLayoutPreset.Documentation).PresentationOf(Inspector).IsPinned);
    }

    [Fact]
    public void TheDefault_IsTheEngineeringArrangement()
    {
        var expected = Build(WorkspaceLayoutPreset.Engineering);
        var actual = WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output);

        Assert.Equal(expected.Root!.Panels, actual.Root!.Panels);
    }

    [Fact]
    public void AnUnknownPreset_Throws_RatherThanSilentlyProducingSomeOtherLayout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WorkspaceLayoutPresets.Build((WorkspaceLayoutPreset)99, Explorer, Document, Inspector, Output));
    }
}
