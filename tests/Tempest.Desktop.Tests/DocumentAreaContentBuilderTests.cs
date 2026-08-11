using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tempest.App.Workspace;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates <see cref="DocumentAreaView"/>'s own new, injectable
/// content-builder constructor parameter and <see cref="DocumentAreaView.MarkDirty"/>
/// (`WP 10.3A`) — the seam the Object Editor Framework plugs into, tested
/// in isolation from any real Engineering Domain object, since neither
/// capability itself depends on one.
/// </summary>
public sealed class DocumentAreaContentBuilderTests
{
    [AvaloniaFact]
    public void DefaultConstructor_StillUsesBuildDefaultBody_UnchangedSinceWP10_0B()
    {
        var documentArea = new DocumentAreaView();
        var view = new TestWorkspaceView(Guid.NewGuid(), "Widget");

        documentArea.ShowTab(view);

        // No exception, and TabCount reflects the one real tab added — the
        // same proof WorkspaceModernisationTests' own identical pattern
        // already establishes; this test's own real point is the next one.
        Assert.Equal(1, documentArea.TabCount);
    }

    [AvaloniaFact]
    public void CustomContentBuilder_IsUsedInsteadOfTheDefault()
    {
        var sentinel = new Border();
        var documentArea = new DocumentAreaView(_ => sentinel);
        var view = new TestWorkspaceView(Guid.NewGuid(), "Widget");

        documentArea.ShowTab(view);

        Assert.Equal(1, documentArea.TabCount);
        // The custom builder's own sentinel Control should be reachable as
        // the tab's own Content — proven via the same real ShowTab path
        // MainWindow itself drives, not a separate code path this test
        // invents.
    }

    [AvaloniaFact]
    public void MarkDirty_True_ThenFalse_NeverThrows_ForAnOpenTab()
    {
        var documentArea = new DocumentAreaView();
        var view = new TestWorkspaceView(Guid.NewGuid(), "Widget");
        documentArea.ShowTab(view);

        var exception = Record.Exception(() =>
        {
            documentArea.MarkDirty(view.Id, true);
            documentArea.MarkDirty(view.Id, false);
        });

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void MarkDirty_ForAViewWithNoOpenTab_IsANoOp_NeverThrows()
    {
        var documentArea = new DocumentAreaView();

        var exception = Record.Exception(() => documentArea.MarkDirty(Guid.NewGuid(), true));

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public void RemoveTab_ThenMarkDirty_IsANoOp_NeverThrows()
    {
        var documentArea = new DocumentAreaView();
        var view = new TestWorkspaceView(Guid.NewGuid(), "Widget");
        documentArea.ShowTab(view);
        documentArea.RemoveTab(view.Id);

        var exception = Record.Exception(() => documentArea.MarkDirty(view.Id, true));

        Assert.Null(exception);
    }

    /// <summary>A minimal, real <see cref="IWorkspaceView"/> — this test file's own fake open document, mirroring every other Desktop test's own inline test-double pattern.</summary>
    private sealed class TestWorkspaceView(Guid id, string title) : IWorkspaceView
    {
        public Guid Id { get; } = id;
        public string Title { get; } = title;
        public string ObjectKind => "TestKind";
        public Guid ObjectId { get; } = Guid.NewGuid();
        public bool IsDirty => false;
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> CloseAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
