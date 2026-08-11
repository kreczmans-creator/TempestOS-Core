using Tempest.App.Workspace;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates "Multiple predefined layouts (Engineering, Review,
/// Documentation)" (`WP 10.2B`'s own named scope item) directly against
/// <see cref="PredefinedLayouts"/> — pure logic, no Avalonia control
/// required, since a preset is nothing but a fixed combination of already-
/// existing values.
/// </summary>
public sealed class PredefinedLayoutsTests
{
    [Theory]
    [InlineData(PredefinedLayouts.WorkspaceLayoutPreset.Engineering)]
    [InlineData(PredefinedLayouts.WorkspaceLayoutPreset.Review)]
    [InlineData(PredefinedLayouts.WorkspaceLayoutPreset.Documentation)]
    public void EveryPreset_KeepsExplorerAndInspectorVisible_AtTheirOwnDocumentedDockPositions(PredefinedLayouts.WorkspaceLayoutPreset preset)
    {
        var explorerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();

        var explorer = PredefinedLayouts.ExplorerPlacement(preset, explorerId);
        var inspector = PredefinedLayouts.InspectorPlacement(preset, inspectorId);

        Assert.Equal(explorerId, explorer.PanelId);
        Assert.Equal(WorkspaceDockPosition.Left, explorer.DockPosition);
        Assert.True(explorer.IsVisible);

        Assert.Equal(inspectorId, inspector.PanelId);
        Assert.Equal(WorkspaceDockPosition.Right, inspector.DockPosition);
        Assert.True(inspector.IsVisible);
    }

    [Fact]
    public void Documentation_WidensExplorer_AndAutoHidesInspector()
    {
        var explorer = PredefinedLayouts.ExplorerPlacement(PredefinedLayouts.WorkspaceLayoutPreset.Documentation, Guid.NewGuid());
        var engineeringExplorer = PredefinedLayouts.ExplorerPlacement(PredefinedLayouts.WorkspaceLayoutPreset.Engineering, Guid.NewGuid());

        Assert.True(explorer.Size > engineeringExplorer.Size);
        Assert.False(PredefinedLayouts.InspectorPinned(PredefinedLayouts.WorkspaceLayoutPreset.Documentation));
    }

    [Fact]
    public void Review_ShowsTheOutputPanel_UnlikeEngineeringOrDocumentation()
    {
        var review = PredefinedLayouts.OutputPanelPlacement(PredefinedLayouts.WorkspaceLayoutPreset.Review);
        var engineering = PredefinedLayouts.OutputPanelPlacement(PredefinedLayouts.WorkspaceLayoutPreset.Engineering);
        var documentation = PredefinedLayouts.OutputPanelPlacement(PredefinedLayouts.WorkspaceLayoutPreset.Documentation);

        Assert.True(review.Visible);
        Assert.False(engineering.Visible);
        Assert.False(documentation.Visible);
    }

    [Fact]
    public void EngineeringAndReview_KeepTheInspectorPinned_UnlikeDocumentation()
    {
        Assert.True(PredefinedLayouts.InspectorPinned(PredefinedLayouts.WorkspaceLayoutPreset.Engineering));
        Assert.True(PredefinedLayouts.InspectorPinned(PredefinedLayouts.WorkspaceLayoutPreset.Review));
        Assert.False(PredefinedLayouts.InspectorPinned(PredefinedLayouts.WorkspaceLayoutPreset.Documentation));
    }
}
