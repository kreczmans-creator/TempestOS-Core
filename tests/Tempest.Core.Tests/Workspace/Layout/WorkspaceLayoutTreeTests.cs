using Tempest.Workspace.Layout;

namespace Tempest.Core.Tests.Workspace.Layout;

/// <summary>
/// The data-driven workspace layout model (`TD-72`) — the abstraction that
/// replaced the compile-time five-column docking grid.
/// </summary>
/// <remarks>
/// Every test here runs with no UI in the process. That is the point of
/// the model: docking, tabbing, splitting, floating and resizing are pure
/// functions over data, so they can be proven exhaustively and cheaply,
/// and the renderer has nothing left to decide.
/// </remarks>
public class WorkspaceLayoutTreeTests
{
    private static readonly Guid Explorer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Document = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Inspector = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Output = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static WorkspaceLayoutTree ThreePane()
    {
        var tree = WorkspaceLayoutTree.Single(Document);
        var documentGroup = tree.FindGroupContaining(Document)!;
        tree = tree.Dock(Explorer, documentGroup.Id, DockRelation.Left);
        return tree.Dock(Inspector, tree.FindGroupContaining(Document)!.Id, DockRelation.Right);
    }

    // ----------------------------------------------------------------
    // Structure
    // ----------------------------------------------------------------

    [Fact]
    public void ASinglePanel_IsATabGroupOfOne()
    {
        var tree = WorkspaceLayoutTree.Single(Document);

        var group = Assert.IsType<LayoutTabGroupNode>(tree.Root);
        Assert.Equal([Document], group.PanelIds);
        Assert.Equal(Document, group.SelectedPanelId);
    }

    [Fact]
    public void DockingLeftAndRight_ProducesOneHorizontalSplitOfThree_NotNestedWrappers()
    {
        var tree = ThreePane();

        var split = Assert.IsType<LayoutSplitNode>(tree.Root);
        Assert.Equal(LayoutOrientation.Horizontal, split.Orientation);
        Assert.Equal(3, split.Children.Count);

        // Normalisation flattens same-axis nesting, so repeated docking
        // cannot grow an ever-deeper tree of one-child wrappers.
        Assert.All(split.Children, c => Assert.IsType<LayoutTabGroupNode>(c));
        Assert.Equal([Explorer, Document, Inspector], split.Children.SelectMany(c => c.Panels));
    }

    [Fact]
    public void DockingBelow_ProducesAVerticalSplit_NestedInsideTheHorizontalOne()
    {
        var tree = ThreePane();
        var documentGroup = tree.FindGroupContaining(Document)!;

        tree = tree.Dock(Output, documentGroup.Id, DockRelation.Below);

        var outer = Assert.IsType<LayoutSplitNode>(tree.Root);
        Assert.Equal(LayoutOrientation.Horizontal, outer.Orientation);

        var inner = Assert.IsType<LayoutSplitNode>(outer.Children[1]);
        Assert.Equal(LayoutOrientation.Vertical, inner.Orientation);
        Assert.Equal([Document, Output], inner.Panels);
    }

    [Fact]
    public void DockingInto_TabsThePanelsTogether_AndSelectsTheNewOne()
    {
        var tree = ThreePane();
        var inspectorGroup = tree.FindGroupContaining(Inspector)!;

        tree = tree.Dock(Output, inspectorGroup.Id, DockRelation.Into);

        var group = tree.FindGroupContaining(Output)!;
        Assert.Equal([Inspector, Output], group.PanelIds);
        Assert.Equal(Output, group.SelectedPanelId);
    }

    [Fact]
    public void Weights_AlwaysSumToOne_HoweverTheTreeWasBuilt()
    {
        var tree = ThreePane().Dock(Output, ThreePane().FindGroupContaining(Document)!.Id, DockRelation.Below);

        foreach (var split in tree.Root!.DescendantsAndSelf.OfType<LayoutSplitNode>())
        {
            Assert.Equal(split.Children.Count, split.Weights.Count);
            Assert.Equal(1.0, split.Weights.Sum(), precision: 9);
        }
    }

    [Fact]
    public void ASplitWithNonsenseWeights_FallsBackToAnEvenShare()
    {
        var children = new WorkspaceLayoutNode[]
        {
            new LayoutTabGroupNode(Guid.NewGuid(), [Explorer]),
            new LayoutTabGroupNode(Guid.NewGuid(), [Document]),
        };

        var split = new LayoutSplitNode(Guid.NewGuid(), LayoutOrientation.Horizontal, children, [0, double.NaN]);

        Assert.Equal([0.5, 0.5], split.Weights);
    }

    // ----------------------------------------------------------------
    // Docking is a move, never a copy
    // ----------------------------------------------------------------

    [Fact]
    public void DockingAnAlreadyPlacedPanel_MovesIt_RatherThanDuplicatingIt()
    {
        var tree = ThreePane();
        var explorerGroup = tree.FindGroupContaining(Explorer)!;

        tree = tree.Dock(Inspector, explorerGroup.Id, DockRelation.Into);

        Assert.Single(tree.AllPanels, p => p == Inspector);
        Assert.Equal([Explorer, Inspector], tree.FindGroupContaining(Inspector)!.PanelIds);
    }

    [Fact]
    public void DockingTheLastPanelOfAGroupElsewhere_RemovesTheEmptyGroupAndItsSplit()
    {
        var tree = ThreePane();
        var explorerGroup = tree.FindGroupContaining(Explorer)!;

        // Inspector is alone in its group; docking it into the Explorer's
        // group must leave no empty group and no one-child split behind.
        tree = tree.Dock(Inspector, explorerGroup.Id, DockRelation.Into);

        var split = Assert.IsType<LayoutSplitNode>(tree.Root);
        Assert.Equal(2, split.Children.Count);
        Assert.DoesNotContain(split.DescendantsAndSelf.OfType<LayoutTabGroupNode>(), g => g.PanelIds.Count == 0);
    }

    [Fact]
    public void DockingAPanelIntoItsOwnSoleGroup_ChangesNothing()
    {
        var tree = ThreePane();
        var group = tree.FindGroupContaining(Inspector)!;

        var after = tree.Dock(Inspector, group.Id, DockRelation.Into);

        Assert.Equal(tree, after);
    }

    [Fact]
    public void DockingOntoAVanishedTarget_LeavesTheLayoutUntouched()
    {
        var tree = ThreePane();

        var after = tree.Dock(Inspector, Guid.NewGuid(), DockRelation.Left);

        Assert.Equal(tree, after);
    }

    // ----------------------------------------------------------------
    // Removal
    // ----------------------------------------------------------------

    [Fact]
    public void RemovingAPanel_CollapsesTheSplitThatHeldIt()
    {
        var tree = ThreePane().Remove(Inspector);

        var split = Assert.IsType<LayoutSplitNode>(tree.Root);
        Assert.Equal(2, split.Children.Count);
        Assert.DoesNotContain(Inspector, tree.AllPanels);
    }

    [Fact]
    public void ASplitReducedToOneChild_CollapsesAway_RatherThanLeavingAWrapper()
    {
        // Without this, every dock-then-undock would leave a one-child
        // split behind, and a session's worth of rearranging would grow an
        // ever-deeper tree of wrappers around a single pane.
        var tree = WorkspaceLayoutTree.Single(Document);
        tree = tree.Dock(Explorer, tree.Root!.Id, DockRelation.Left);
        Assert.IsType<LayoutSplitNode>(tree.Root);

        tree = tree.Remove(Explorer);

        var group = Assert.IsType<LayoutTabGroupNode>(tree.Root);
        Assert.Equal([Document], group.PanelIds);
    }

    [Fact]
    public void ANestedSplitReducedToOneChild_CollapsesAway_AtEveryDepth()
    {
        var tree = ThreePane();
        tree = tree.Dock(Output, tree.FindGroupContaining(Document)!.Id, DockRelation.Below);

        // The document column is now a vertical split of two. Removing the
        // Output panel must leave the document pane directly in the
        // horizontal split, not a vertical split wrapping one child.
        tree = tree.Remove(Output);

        var root = Assert.IsType<LayoutSplitNode>(tree.Root);
        Assert.All(root.Children, c => Assert.IsType<LayoutTabGroupNode>(c));
        Assert.Equal([Explorer, Document, Inspector], root.Panels);
    }

    [Fact]
    public void RepeatedDockingAndUndocking_NeverGrowsTheTree()
    {
        var tree = ThreePane();
        var depthBefore = Depth(tree.Root!);

        for (var i = 0; i < 10; i++)
        {
            tree = tree.Dock(Inspector, tree.FindGroupContaining(Document)!.Id, DockRelation.Below);
            tree = tree.Dock(Inspector, tree.FindGroupContaining(Document)!.Id, DockRelation.Right);
        }

        Assert.Equal(depthBefore, Depth(tree.Root!));
        Assert.Equal(3, tree.DockedPanels.Count());
    }

    private static int Depth(WorkspaceLayoutNode node) =>
        node is LayoutSplitNode split ? 1 + split.Children.Max(Depth) : 1;

    [Fact]
    public void RemovingEveryPanel_LeavesAnEmptyArrangement_NotAnEmptySplit()
    {
        var tree = ThreePane().Remove(Explorer).Remove(Document).Remove(Inspector);

        Assert.Null(tree.Root);
        Assert.Empty(tree.AllPanels);
    }

    [Fact]
    public void RemovingASelectedTab_SelectsAnotherOne_NeverAnInvalidIndex()
    {
        var tree = WorkspaceLayoutTree.Single(Document);
        tree = tree.Dock(Inspector, tree.Root!.Id, DockRelation.Into);
        tree = tree.Dock(Output, tree.Root!.Id, DockRelation.Into);

        Assert.Equal(Output, tree.FindGroupContaining(Output)!.SelectedPanelId);

        tree = tree.Remove(Output);

        var group = tree.FindGroupContaining(Document)!;
        Assert.Equal(2, group.PanelIds.Count);
        Assert.InRange(group.SelectedIndex, 0, group.PanelIds.Count - 1);
    }

    // ----------------------------------------------------------------
    // Floating
    // ----------------------------------------------------------------

    [Fact]
    public void FloatingAPanel_MovesItOutOfTheDockedTree_IntoItsOwnWindow()
    {
        var tree = ThreePane().Float(Inspector, 100, 200, 400, 300);

        Assert.DoesNotContain(Inspector, tree.DockedPanels);
        Assert.True(tree.IsFloating(Inspector));
        Assert.Contains(Inspector, tree.AllPanels);

        var window = Assert.Single(tree.Floating);
        Assert.Equal(100, window.X);
        Assert.Equal(200, window.Y);
        Assert.Equal(400, window.Width);
        Assert.Equal(300, window.Height);
    }

    [Fact]
    public void AFloatingPanel_CanBeDockedBackIn()
    {
        var tree = ThreePane().Float(Inspector, 100, 200, 400, 300);
        var explorerGroup = tree.FindGroupContaining(Explorer)!;

        tree = tree.Dock(Inspector, explorerGroup.Id, DockRelation.Into);

        Assert.Empty(tree.Floating);
        Assert.False(tree.IsFloating(Inspector));
        Assert.Contains(Inspector, tree.DockedPanels);
    }

    [Fact]
    public void MovingAFloatingWindow_KeepsScreenCoordinates_SoASecondMonitorIsRestorable()
    {
        var tree = ThreePane().Float(Inspector, 100, 200, 400, 300);
        var windowId = tree.Floating.Single().Id;

        // Negative X is a real, ordinary position: a monitor to the left of
        // the primary one.
        tree = tree.MoveFloating(windowId, -1800, 40, 520, 380);

        var window = Assert.Single(tree.Floating);
        Assert.Equal(-1800, window.X);
        Assert.Equal(40, window.Y);
        Assert.Equal(520, window.Width);
    }

    [Fact]
    public void AFloatingWindow_NeverShrinksBelowAUsableSize()
    {
        var tree = ThreePane().Float(Inspector, 0, 0, 1, 1);

        var window = Assert.Single(tree.Floating);
        Assert.True(window.Width >= 120);
        Assert.True(window.Height >= 80);
    }

    [Fact]
    public void RemovingTheLastPanelOfAFloatingWindow_RemovesTheWindow()
    {
        var tree = ThreePane().Float(Inspector, 100, 200, 400, 300).Remove(Inspector);

        Assert.Empty(tree.Floating);
    }

    // ----------------------------------------------------------------
    // Edge docking, selection, sizing, presentation
    // ----------------------------------------------------------------

    [Theory]
    [InlineData(DockRelation.Left, LayoutOrientation.Horizontal, 0)]
    [InlineData(DockRelation.Right, LayoutOrientation.Horizontal, 1)]
    [InlineData(DockRelation.Above, LayoutOrientation.Vertical, 0)]
    [InlineData(DockRelation.Below, LayoutOrientation.Vertical, 1)]
    public void DockingToAWindowEdge_WrapsTheWholeArrangement(DockRelation edge, LayoutOrientation orientation, int index)
    {
        var tree = WorkspaceLayoutTree.Single(Document).DockToEdge(Output, edge);

        var split = Assert.IsType<LayoutSplitNode>(tree.Root);
        Assert.Equal(orientation, split.Orientation);
        Assert.Equal(Output, split.Children[index].Panels.Single());
    }

    [Fact]
    public void SelectingAPanel_BringsItToTheFrontOfItsOwnGroupOnly()
    {
        var tree = WorkspaceLayoutTree.Single(Document);
        tree = tree.Dock(Inspector, tree.Root!.Id, DockRelation.Into);
        tree = tree.Dock(Explorer, tree.FindGroupContaining(Document)!.Id, DockRelation.Left);

        tree = tree.SelectPanel(Document);

        Assert.Equal(Document, tree.FindGroupContaining(Document)!.SelectedPanelId);
        Assert.Equal(Explorer, tree.FindGroupContaining(Explorer)!.SelectedPanelId);
    }

    [Fact]
    public void SettingWeights_ResizesASplit_AndStaysNormalised()
    {
        var tree = ThreePane();
        var split = (LayoutSplitNode)tree.Root!;

        tree = tree.SetWeights(split.Id, [2, 6, 2]);

        var resized = Assert.IsType<LayoutSplitNode>(tree.Root);
        Assert.Equal([0.2, 0.6, 0.2], resized.Weights.Select(w => Math.Round(w, 6)));
    }

    [Fact]
    public void SettingWeights_WithTheWrongCount_IsIgnored()
    {
        var tree = ThreePane();
        var split = (LayoutSplitNode)tree.Root!;

        var after = tree.SetWeights(split.Id, [1, 1]);

        Assert.Equal(tree, after);
    }

    [Fact]
    public void PinningAndCollapsing_ArePerPanel_AndSurviveOtherEdits()
    {
        var tree = ThreePane()
            .SetPinned(Inspector, false)
            .SetCollapsed(Explorer, true);

        Assert.False(tree.PresentationOf(Inspector).IsPinned);
        Assert.True(tree.PresentationOf(Explorer).IsCollapsed);
        Assert.True(tree.PresentationOf(Document).IsPinned);
        Assert.False(tree.PresentationOf(Document).IsCollapsed);

        // A structural edit elsewhere must not disturb them.
        tree = tree.Dock(Output, tree.FindGroupContaining(Document)!.Id, DockRelation.Below);

        Assert.False(tree.PresentationOf(Inspector).IsPinned);
        Assert.True(tree.PresentationOf(Explorer).IsCollapsed);
    }

    [Fact]
    public void RemovingAPanel_DiscardsItsPresentation_SoAReAddedPanelStartsClean()
    {
        var tree = ThreePane().SetCollapsed(Inspector, true).Remove(Inspector);

        Assert.False(tree.PresentationOf(Inspector).IsCollapsed);
    }

    // ----------------------------------------------------------------
    // Invariants the constructors enforce
    // ----------------------------------------------------------------

    [Fact]
    public void ATabGroup_CannotBeEmpty()
    {
        Assert.Throws<ArgumentException>(() => new LayoutTabGroupNode(Guid.NewGuid(), []));
    }

    [Fact]
    public void ATabGroup_CannotHoldTheSamePanelTwice()
    {
        Assert.Throws<ArgumentException>(() => new LayoutTabGroupNode(Guid.NewGuid(), [Document, Document]));
    }

    [Fact]
    public void ATabGroup_ClampsAnOutOfRangeSelection()
    {
        var group = new LayoutTabGroupNode(Guid.NewGuid(), [Document, Inspector], selectedIndex: 99);

        Assert.Equal(1, group.SelectedIndex);
    }

    [Fact]
    public void ASplit_CannotBeEmpty()
    {
        Assert.Throws<ArgumentException>(() => new LayoutSplitNode(Guid.NewGuid(), LayoutOrientation.Horizontal, []));
    }

    [Fact]
    public void ASplit_RejectsAWeightCountThatDisagreesWithItsChildren()
    {
        var children = new WorkspaceLayoutNode[] { new LayoutTabGroupNode(Guid.NewGuid(), [Document]) };

        Assert.Throws<ArgumentException>(() => new LayoutSplitNode(Guid.NewGuid(), LayoutOrientation.Horizontal, children, [1, 1]));
    }

    // ----------------------------------------------------------------
    // The window forest (`ADR-0153` decision 1) — every operation proven
    // across more than one window, with no UI at all.
    // ----------------------------------------------------------------

    [Fact]
    public void ANewArrangement_HasExactlyOnePrimaryWindow()
    {
        var tree = ThreePane();

        var window = Assert.Single(tree.Windows, w => w.IsPrimary);
        Assert.Same(window.Root, tree.Root);
        Assert.DoesNotContain(tree.Windows, w => !w.IsPrimary);
    }

    [Fact]
    public void FloatingAPanel_AddsASecondWindow_ToTheForest()
    {
        var tree = ThreePane().Float(Inspector, 100, 200, 400, 300);

        Assert.Equal(2, tree.Windows.Count);
        var secondary = Assert.Single(tree.Windows, w => !w.IsPrimary);
        Assert.Contains(Inspector, secondary.Panels);
        Assert.False(secondary.IsPrimary);
    }

    [Fact]
    public void DockingIntoATargetInADifferentWindow_MovesThePanelAcrossWindows_InOneEdit()
    {
        // ADR-0153 decision 4: a cross-window dock removes the panel from
        // its source window's subtree and inserts it into the target's, as
        // one atomic tree edit — provable at the model level with no
        // screen coordinates or UI at all.
        var tree = ThreePane().Float(Inspector, 100, 200, 400, 300);
        var secondaryWindow = tree.Windows.Single(w => !w.IsPrimary);
        var secondaryGroup = (LayoutTabGroupNode)secondaryWindow.Root!;

        tree = tree.Dock(Explorer, secondaryGroup.Id, DockRelation.Into);

        Assert.DoesNotContain(Explorer, tree.DockedPanels);
        var afterWindow = tree.Windows.Single(w => !w.IsPrimary);
        Assert.Equal([Inspector, Explorer], afterWindow.Root!.Panels);
        Assert.Single(tree.Windows, w => !w.IsPrimary);
    }

    [Fact]
    public void DockingTheLastPanelOutOfASecondaryWindow_ClosesIt()
    {
        var tree = ThreePane().Float(Inspector, 100, 200, 400, 300);
        var secondaryGroup = (LayoutTabGroupNode)tree.Windows.Single(w => !w.IsPrimary).Root!;

        // Dock Inspector back into the primary window's Explorer group.
        var explorerGroup = tree.FindGroupContaining(Explorer)!;
        tree = tree.Dock(Inspector, explorerGroup.Id, DockRelation.Into);

        Assert.DoesNotContain(tree.Windows, w => !w.IsPrimary);
        Assert.Null(tree.FindNode(secondaryGroup.Id));
    }

    [Fact]
    public void TwoSecondaryWindows_CanExchangeAPanelDirectly_WithNoPrimaryInvolvement()
    {
        var tree = ThreePane();
        tree = tree.Dock(Output, tree.FindGroupContaining(Document)!.Id, DockRelation.Below);
        tree = tree
            .Float(Inspector, 100, 200, 400, 300)
            .Float(Output, 700, 200, 400, 300);

        var targetWindow = tree.Windows.Single(w => !w.IsPrimary && w.Panels.Contains(Output));
        var targetGroup = (LayoutTabGroupNode)targetWindow.Root!;

        tree = tree.Dock(Inspector, targetGroup.Id, DockRelation.Into);

        Assert.Equal(2, tree.Windows.Count);
        var survivor = tree.Windows.Single(w => !w.IsPrimary);
        Assert.Equal([Output, Inspector], survivor.Root!.Panels);
        Assert.Equal([Explorer, Document], tree.DockedPanels);
    }

    [Fact]
    public void FindWindowContaining_LocatesTheWindowOwningANode_AcrossTheWholeForest()
    {
        var tree = ThreePane().Float(Inspector, 100, 200, 400, 300);
        var secondary = tree.Windows.Single(w => !w.IsPrimary);

        var found = tree.FindWindowContaining(secondary.Root!.Id);

        Assert.Equal(secondary.Id, found!.Id);
    }

    [Fact]
    public void WithPrimaryRoot_ReplacesOnlyThePrimaryWindowsOwnRoot()
    {
        var tree = ThreePane().Float(Inspector, 100, 200, 400, 300);
        var secondaryBefore = tree.Windows.Single(w => !w.IsPrimary);

        var replaced = tree.WithPrimaryRoot(new LayoutTabGroupNode(Guid.NewGuid(), [Document]));

        Assert.Equal([Document], replaced.Root!.Panels);
        var secondaryAfter = replaced.Windows.Single(w => !w.IsPrimary);
        Assert.Equal(secondaryBefore.Id, secondaryAfter.Id);
    }

    // ----------------------------------------------------------------
    // Reordering a tab in place (`ADR-0153` decision 8, `TD-133`)
    // ----------------------------------------------------------------

    [Fact]
    public void ReorderTab_MovesAPanelOnePositionLater()
    {
        var tree = WorkspaceLayoutTree.Single(Document);
        tree = tree.Dock(Inspector, tree.Root!.Id, DockRelation.Into);
        tree = tree.Dock(Output, tree.FindGroupContaining(Document)!.Id, DockRelation.Into);
        var group = tree.FindGroupContaining(Document)!;
        Assert.Equal([Document, Inspector, Output], group.PanelIds);

        tree = tree.ReorderTab(group.Id, Document, direction: 1);

        Assert.Equal([Inspector, Document, Output], tree.FindGroupContaining(Document)!.PanelIds);
    }

    [Fact]
    public void ReorderTab_MovesAPanelOnePositionEarlier()
    {
        var tree = WorkspaceLayoutTree.Single(Document);
        tree = tree.Dock(Inspector, tree.Root!.Id, DockRelation.Into);
        tree = tree.Dock(Output, tree.FindGroupContaining(Document)!.Id, DockRelation.Into);
        var group = tree.FindGroupContaining(Document)!;

        tree = tree.ReorderTab(group.Id, Output, direction: -1);

        Assert.Equal([Document, Output, Inspector], tree.FindGroupContaining(Document)!.PanelIds);
    }

    [Fact]
    public void ReorderTab_KeepsTheSamePanelSelected_EvenThoughItsIndexMoved()
    {
        var tree = WorkspaceLayoutTree.Single(Document);
        tree = tree.Dock(Inspector, tree.Root!.Id, DockRelation.Into);
        var group = tree.FindGroupContaining(Document)!;
        tree = tree.SelectPanel(Inspector);
        group = tree.FindGroupContaining(Inspector)!;

        tree = tree.ReorderTab(group.Id, Inspector, direction: -1);

        Assert.Equal(Inspector, tree.FindGroupContaining(Inspector)!.SelectedPanelId);
        Assert.Equal([Inspector, Document], tree.FindGroupContaining(Inspector)!.PanelIds);
    }

    [Fact]
    public void ReorderTab_PastEitherEnd_IsANoOp()
    {
        var tree = WorkspaceLayoutTree.Single(Document);
        tree = tree.Dock(Inspector, tree.Root!.Id, DockRelation.Into);
        var group = tree.FindGroupContaining(Document)!;

        var pastTheStart = tree.ReorderTab(group.Id, Document, direction: -1);
        var pastTheEnd = tree.ReorderTab(group.Id, Inspector, direction: 1);

        Assert.Equal(tree, pastTheStart);
        Assert.Equal(tree, pastTheEnd);
    }

    [Fact]
    public void ReorderTab_OnAPanelNotInThatGroup_IsANoOp()
    {
        var tree = ThreePane();
        var explorerGroup = tree.FindGroupContaining(Explorer)!;

        var after = tree.ReorderTab(explorerGroup.Id, Inspector, direction: 1);

        Assert.Equal(tree, after);
    }

    [Fact]
    public void ReorderTab_OnAVanishedGroup_IsANoOp()
    {
        var tree = ThreePane();

        var after = tree.ReorderTab(Guid.NewGuid(), Explorer, direction: 1);

        Assert.Equal(tree, after);
    }
}
