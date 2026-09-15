using Tempest.Workspace.Layout;

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

    // ----------------------------------------------------------------
    // ADR-0153: the version-2 window forest, and reading version 1
    // ----------------------------------------------------------------

    [Fact]
    public void TheCurrentVersion_IsTheWindowForest()
    {
        Assert.Equal(2, WorkspaceLayoutSerializer.CurrentVersion);
    }

    [Fact]
    public void AVersion1Document_LoadsIntoAForest_WithOnePrimaryWindow()
    {
        var json = "{\"Version\":1,\"Root\":{\"Kind\":\"tabs\",\"Id\":\"11111111-1111-1111-1111-111111111111\",\"Orientation\":\"Horizontal\",\"PanelIds\":[\"22222222-2222-2222-2222-222222222222\"],\"SelectedIndex\":0},\"Floating\":[{\"Id\":\"55555555-5555-5555-5555-555555555555\",\"Content\":{\"Kind\":\"tabs\",\"Id\":\"66666666-6666-6666-6666-666666666666\",\"Orientation\":\"Horizontal\",\"PanelIds\":[\"33333333-3333-3333-3333-333333333333\"],\"SelectedIndex\":0},\"X\":-1200,\"Y\":80,\"Width\":400,\"Height\":300}],\"Panels\":[]}";

        var restored = WorkspaceLayoutSerializer.Deserialise(json);

        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Windows.Count);
        var primary = Assert.Single(restored.Windows, w => w.IsPrimary);
        Assert.Equal([Document], primary.Root!.Panels);
        var secondary = Assert.Single(restored.Windows, w => !w.IsPrimary);
        Assert.Equal([Inspector], secondary.Root!.Panels);
        Assert.Equal(-1200, secondary.X);
        Assert.Null(secondary.MonitorKey);
    }

    [Fact]
    public void ALayoutWithASecondWindow_SurvivesAVersion2RoundTrip_Exactly()
    {
        var sample = Sample();
        var original = sample.SetWeights(((LayoutSplitNode)sample.Root!).Id, [1, 3]);
        original = original with { Windows = original.Windows.Select(w => w.IsPrimary ? w : w with { MonitorKey = "DISPLAY1@0,0,1920x1080" }).ToList() };

        var json = WorkspaceLayoutSerializer.Serialise(original);
        var restored = WorkspaceLayoutSerializer.Deserialise(json);

        Assert.NotNull(restored);
        Assert.Equal(original.Windows.Count, restored!.Windows.Count);
        var secondary = restored.Windows.Single(w => !w.IsPrimary);
        Assert.Equal("DISPLAY1@0,0,1920x1080", secondary.MonitorKey);
        Assert.Equal(original.Windows.Single(w => !w.IsPrimary).X, secondary.X);
    }

    [Fact]
    public void AVersion2DocumentNamingNoPrimaryWindow_ReadsBackAsNothing()
    {
        var json = "{\"Version\":2,\"Windows\":[{\"Id\":\"11111111-1111-1111-1111-111111111111\",\"Root\":null,\"IsPrimary\":false,\"MonitorKey\":null,\"X\":0,\"Y\":0,\"Width\":0,\"Height\":0}],\"Panels\":[]}";

        Assert.Null(WorkspaceLayoutSerializer.Deserialise(json));
    }

    [Fact]
    public void AVersion2DocumentWithNoWindowsAtAll_ReadsBackAsNothing()
    {
        var json = "{\"Version\":2,\"Windows\":[],\"Panels\":[]}";

        Assert.Null(WorkspaceLayoutSerializer.Deserialise(json));
    }
}
