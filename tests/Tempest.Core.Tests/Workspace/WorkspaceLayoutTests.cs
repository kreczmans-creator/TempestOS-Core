using Tempest.App.Workspace;

namespace Tempest.Core.Tests.Workspace;

// Pure, Host-free unit tests: WorkspaceLayout is a plain, in-memory
// placement table with no external dependency of its own.
public class WorkspaceLayoutTests
{
    private static WorkspacePanelPlacement Placement(Guid id, WorkspaceDockPosition position = WorkspaceDockPosition.Left) =>
        new(id, position, 30, true);

    [Fact]
    public void PanelPlacements_ReturnsEveryDefault()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var layout = new WorkspaceLayout([Placement(idA), Placement(idB, WorkspaceDockPosition.Right)]);

        Assert.Equal(2, layout.PanelPlacements.Count);
    }

    [Fact]
    public void GetPlacement_KnownPanel_ReturnsIt()
    {
        var id = Guid.NewGuid();
        var layout = new WorkspaceLayout([Placement(id)]);

        var placement = layout.GetPlacement(id);

        Assert.Equal(id, placement.PanelId);
        Assert.Equal(WorkspaceDockPosition.Left, placement.DockPosition);
    }

    [Fact]
    public void GetPlacement_UnknownPanel_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => new WorkspaceLayout([]).GetPlacement(Guid.NewGuid()));

    [Fact]
    public void SetPlacement_KnownPanel_UpdatesIt()
    {
        var id = Guid.NewGuid();
        var layout = new WorkspaceLayout([Placement(id)]);

        layout.SetPlacement(id, new WorkspacePanelPlacement(id, WorkspaceDockPosition.Bottom, 50, false));

        var placement = layout.GetPlacement(id);
        Assert.Equal(WorkspaceDockPosition.Bottom, placement.DockPosition);
        Assert.Equal(50, placement.Size);
        Assert.False(placement.IsVisible);
    }

    [Fact]
    public void SetPlacement_MismatchedPanelId_ThrowsArgumentException()
    {
        var id = Guid.NewGuid();
        var layout = new WorkspaceLayout([Placement(id)]);

        Assert.Throws<ArgumentException>(() =>
            layout.SetPlacement(id, new WorkspacePanelPlacement(Guid.NewGuid(), WorkspaceDockPosition.Bottom, 50, false)));
    }

    [Fact]
    public void SetPlacement_UnknownPanel_ThrowsArgumentException()
    {
        var layout = new WorkspaceLayout([]);
        var id = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            layout.SetPlacement(id, new WorkspacePanelPlacement(id, WorkspaceDockPosition.Bottom, 50, false)));
    }

    [Fact]
    public void ResetToDefault_DiscardsChanges_RestoresOriginalDefaults()
    {
        var id = Guid.NewGuid();
        var layout = new WorkspaceLayout([Placement(id)]);
        layout.SetPlacement(id, new WorkspacePanelPlacement(id, WorkspaceDockPosition.Bottom, 999, false));

        var reset = layout.ResetToDefault();

        var placement = reset.GetPlacement(id);
        Assert.Equal(WorkspaceDockPosition.Left, placement.DockPosition);
        Assert.Equal(30, placement.Size);
        Assert.True(placement.IsVisible);
    }

    [Fact]
    public void ResetToDefault_ReturnsANewInstance_DoesNotMutateOriginal()
    {
        var id = Guid.NewGuid();
        var layout = new WorkspaceLayout([Placement(id)]);

        var reset = layout.ResetToDefault();

        Assert.NotSame(layout, reset);
    }
}
