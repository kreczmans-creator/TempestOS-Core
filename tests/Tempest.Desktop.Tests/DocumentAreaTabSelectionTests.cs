using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tempest.App.Workspace;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// <see cref="DocumentAreaView.RemoveTab"/>'s own selection behaviour —
/// "move between objects without losing context" (`WP-Z4` Productisation
/// Phase 2, P0/P1).
/// </summary>
/// <remarks>
/// Confirmed live before this fix, with a real running shell: opening
/// four tabs and closing the third one — the active tab, with a neighbour
/// on each side — silently dumped the user back onto the Cockpit's Home
/// tab rather than the fourth tab sliding into view. The cause is
/// <see cref="TabControl"/>'s own default reaction to its selected
/// container disappearing: it resets <c>SelectedIndex</c> to <c>0</c>
/// rather than to whatever now occupies the closed tab's former position.
/// These tests drive the real <see cref="DocumentAreaView"/> and
/// <see cref="TabControl"/> exactly as <c>WorkspaceViewCoordinator</c>'s
/// own <c>CloseDocumentAsync</c> does, and assert the landing tab by id
/// through <see cref="DocumentAreaView.ActiveClosableViewId"/> — never the
/// method under test's own name.
/// </remarks>
public sealed class DocumentAreaTabSelectionTests
{
    [AvaloniaFact]
    public void ClosingTheActiveMiddleTab_LandsOnTheTabThatSlidIntoItsPlace_NeverOnHome()
    {
        var area = new DocumentAreaView();
        var window = new Window { Content = area, Width = 800, Height = 600 };
        window.Show();
        area.SetHomeTab(new TextBlock { Text = "Cockpit" });

        var one = new TestWorkspaceView(Guid.NewGuid(), "One");
        var two = new TestWorkspaceView(Guid.NewGuid(), "Two");
        var three = new TestWorkspaceView(Guid.NewGuid(), "Three");
        area.ShowTab(one);
        area.ShowTab(two);
        area.ShowTab(three);
        area.ShowTab(two); // Two is the active tab, with a neighbour on each side.

        Assert.Equal(two.Id, area.ActiveClosableViewId);

        area.RemoveTab(two.Id);

        Assert.Equal(three.Id, area.ActiveClosableViewId);
    }

    [AvaloniaFact]
    public void ClosingTheActiveLastTab_LandsOnItsNowLastNeighbour()
    {
        var area = new DocumentAreaView();
        var window = new Window { Content = area, Width = 800, Height = 600 };
        window.Show();
        area.SetHomeTab(new TextBlock { Text = "Cockpit" });

        var one = new TestWorkspaceView(Guid.NewGuid(), "One");
        var two = new TestWorkspaceView(Guid.NewGuid(), "Two");
        area.ShowTab(one);
        area.ShowTab(two); // last-opened, so already active

        Assert.Equal(two.Id, area.ActiveClosableViewId);

        area.RemoveTab(two.Id);

        Assert.Equal(one.Id, area.ActiveClosableViewId);
    }

    [AvaloniaFact]
    public void ClosingTheOnlyOpenObjectTab_FallsBackToTheHomeTab()
    {
        var area = new DocumentAreaView();
        var window = new Window { Content = area, Width = 800, Height = 600 };
        window.Show();
        area.SetHomeTab(new TextBlock { Text = "Cockpit" });

        var only = new TestWorkspaceView(Guid.NewGuid(), "Only");
        area.ShowTab(only);

        area.RemoveTab(only.Id);

        // No object tab is active any more — ActiveClosableViewId reports
        // null for the (now-selected) Home tab, never a stale id.
        Assert.Null(area.ActiveClosableViewId);
        Assert.Equal(1, area.TabCount);
    }

    [AvaloniaFact]
    public void ClosingATabThatIsNotActive_NeverMovesTheUsersActualSelection()
    {
        var area = new DocumentAreaView();
        var window = new Window { Content = area, Width = 800, Height = 600 };
        window.Show();
        area.SetHomeTab(new TextBlock { Text = "Cockpit" });

        var one = new TestWorkspaceView(Guid.NewGuid(), "One");
        var two = new TestWorkspaceView(Guid.NewGuid(), "Two");
        area.ShowTab(one);
        area.ShowTab(two);
        area.ShowTab(one); // the user is deliberately looking at One, not the last-opened tab.

        Assert.Equal(one.Id, area.ActiveClosableViewId);

        area.RemoveTab(two.Id); // closes a different, inactive tab

        Assert.Equal(one.Id, area.ActiveClosableViewId);
    }

    /// <summary>A minimal, real <see cref="IWorkspaceView"/> — mirrors <c>DocumentAreaContentBuilderTests</c>' own inline test-double pattern.</summary>
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
