using Tempest.App.Workspace;
using Tempest.App.Workspace.Samples;

namespace Tempest.Core.Tests.Workspace.Samples;

// Proves SampleWorkspaceViewFactory/SampleWorkspaceView (Tempest.App.
// Workspace.Samples) — the Project Explorer's own living reference content,
// WP 8.1B — against a real, minimal IWorkspaceContext (this project does not
// use a mocking framework).
public class SampleWorkspaceViewFactoryTests
{
    private sealed class TestContext : IWorkspaceContext
    {
        public WorkspaceSelection? CurrentSelection => null;

        public Guid? ActiveViewId => null;
    }

    [Fact]
    public void Kind_ReturnsTheConstructorArgument()
    {
        var factory = new SampleWorkspaceViewFactory(SampleExplorerContent.ComponentKind);

        Assert.Equal(SampleExplorerContent.ComponentKind, factory.Kind);
    }

    [Fact]
    public void Constructor_NullOrWhitespaceKind_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => new SampleWorkspaceViewFactory("  "));

    [Fact]
    public async Task Create_KnownComponentId_ReturnsAViewWithItsOwnTitle()
    {
        var provider = new SampleProjectExplorerNodeProvider("area");
        var category = (await provider.GetRootNodesAsync())[0];
        var primaryStructure = (await provider.GetChildrenAsync(category.Id)).Single(n => n.Title == "Primary Structure");
        var longeron = (await provider.GetChildrenAsync(primaryStructure.Id)).Single(n => n.Title == "Longeron");
        var factory = new SampleWorkspaceViewFactory(SampleExplorerContent.ComponentKind);

        var view = factory.Create(longeron.Id, new TestContext());

        Assert.Equal("Longeron", view.Title);
        Assert.Equal(longeron.Id, view.ObjectId);
        Assert.Equal(SampleExplorerContent.ComponentKind, view.ObjectKind);
        Assert.False(view.IsDirty);
    }

    [Fact]
    public void Create_UnknownObjectId_ThrowsArgumentException()
    {
        var factory = new SampleWorkspaceViewFactory(SampleExplorerContent.ComponentKind);

        Assert.Throws<ArgumentException>(() => factory.Create(Guid.NewGuid(), new TestContext()));
    }

    [Fact]
    public async Task CloseAsync_AlwaysReturnsTrue()
    {
        var view = new SampleWorkspaceView(Guid.NewGuid(), SampleExplorerContent.ComponentKind, "Bracket");

        Assert.True(await view.CloseAsync());
    }

    [Fact]
    public async Task RefreshAsync_CompletesWithoutError()
    {
        var view = new SampleWorkspaceView(Guid.NewGuid(), SampleExplorerContent.ComponentKind, "Bracket");

        var exception = await Record.ExceptionAsync(() => view.RefreshAsync());

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_NullOrWhitespaceObjectKind_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => new SampleWorkspaceView(Guid.NewGuid(), "  ", "Bracket"));

    [Fact]
    public void Constructor_NullOrWhitespaceTitle_ThrowsArgumentException() =>
        Assert.Throws<ArgumentException>(() => new SampleWorkspaceView(Guid.NewGuid(), SampleExplorerContent.ComponentKind, "  "));
}
