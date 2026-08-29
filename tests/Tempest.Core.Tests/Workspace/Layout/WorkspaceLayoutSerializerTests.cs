using Tempest.App.Workspace.Layout;

namespace Tempest.Core.Tests.Workspace.Layout;

/// <summary>Layout persistence (`TD-72`) — a round trip, and every way a stored layout can be wrong.</summary>
public class WorkspaceLayoutSerializerTests
{
    private static readonly Guid Explorer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Document = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Inspector = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static WorkspaceLayoutTree Sample()
    {
        var tree = WorkspaceLayoutTree.Single(Document);
        tree = tree.Dock(Explorer, tree.Root!.Id, DockRelation.Left);

        // Float only ever moves a panel that is already in the arrangement,
        // so the Inspector has to be docked before it can be undocked.
        tree = tree.Dock(Inspector, tree.FindGroupContaining(Document)!.Id, DockRelation.Right);
        tree = tree.Float(Inspector, -1200, 80, 400, 300);

        return tree.SetCollapsed(Explorer, true).SetPinned(Document, false);
    }

    [Fact]
    public void ALayout_SurvivesARoundTrip_Exactly()
    {
        var original = Sample();

        var restored = WorkspaceLayoutSerializer.Deserialise(WorkspaceLayoutSerializer.Serialise(original));

        Assert.NotNull(restored);
        Assert.Equal(original.DockedPanels, restored!.DockedPanels);
        Assert.True(restored.IsFloating(Inspector));
        Assert.True(restored.PresentationOf(Explorer).IsCollapsed);
        Assert.False(restored.PresentationOf(Document).IsPinned);

        var window = Assert.Single(restored.Floating);
        Assert.Equal(-1200, window.X);
        Assert.Equal(400, window.Width);
    }

    [Fact]
    public void SplitProportions_SurviveARoundTrip()
    {
        var tree = Sample();
        var split = (LayoutSplitNode)tree.Root!;
        tree = tree.SetWeights(split.Id, [1, 3]);

        var restored = WorkspaceLayoutSerializer.Deserialise(WorkspaceLayoutSerializer.Serialise(tree));

        var restoredSplit = Assert.IsType<LayoutSplitNode>(restored!.Root);
        Assert.Equal([0.25, 0.75], restoredSplit.Weights.Select(w => Math.Round(w, 6)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("{\"Version\":99,\"Root\":null}")]
    public void AnUnreadableLayout_ReadsBackAsNothing_NeverAsAnException(string? json)
    {
        Assert.Null(WorkspaceLayoutSerializer.Deserialise(json));
    }

    [Fact]
    public void AStructurallyImpossibleLayout_ReadsBackAsNothing()
    {
        // An empty tab group cannot exist in the model; a stored one is a
        // corrupt layout, and must cost the user their panel positions
        // rather than their session.
        var json = "{\"Version\":1,\"Root\":{\"Kind\":\"tabs\",\"Id\":\"11111111-1111-1111-1111-111111111111\",\"PanelIds\":[],\"SelectedIndex\":0}}";

        Assert.Null(WorkspaceLayoutSerializer.Deserialise(json));
    }

    [Fact]
    public void ASplitWhoseChildrenAllVanished_ReadsBackAsNothing()
    {
        var json = "{\"Version\":1,\"Root\":{\"Kind\":\"split\",\"Id\":\"11111111-1111-1111-1111-111111111111\",\"Orientation\":\"Horizontal\",\"Children\":[],\"Weights\":[]}}";

        Assert.Null(WorkspaceLayoutSerializer.Deserialise(json));
    }

    [Fact]
    public void AnEmptyArrangement_RoundTrips()
    {
        var restored = WorkspaceLayoutSerializer.Deserialise(WorkspaceLayoutSerializer.Serialise(WorkspaceLayoutTree.Empty));

        Assert.NotNull(restored);
        Assert.Null(restored!.Root);
        Assert.Empty(restored.AllPanels);
    }

    [Fact]
    public void TheFormatIsVersioned_SoAFutureChangeIsAMigrationRatherThanDataLoss()
    {
        var json = WorkspaceLayoutSerializer.Serialise(Sample());

        Assert.Contains($"\"Version\":{WorkspaceLayoutSerializer.CurrentVersion}", json, StringComparison.Ordinal);
    }
}
