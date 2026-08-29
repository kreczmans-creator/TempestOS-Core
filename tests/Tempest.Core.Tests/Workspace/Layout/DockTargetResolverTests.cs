using Tempest.App.Workspace.Layout;

namespace Tempest.Core.Tests.Workspace.Layout;

/// <summary>
/// Drop-zone geometry for drag-to-dock (`TD-72`).
/// </summary>
/// <remarks>
/// Drag-to-dock is the gesture most likely to be subtly wrong — a zone
/// that swallows its neighbours, a centre that never triggers, an edge
/// band one pixel wide. Keeping the decision as a pure function is what
/// makes those cases cheap to enumerate instead of a manual click-around.
/// </remarks>
public class DockTargetResolverTests
{
    private static readonly Guid Pane = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Other = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly DockTargetCandidate Square = new(Pane, 0, 0, 100, 100);

    [Fact]
    public void TheCentre_Tabs()
    {
        var target = DockTargetResolver.Resolve([Square], 50, 50);

        Assert.Equal(new DockTarget(Pane, DockRelation.Into), target);
    }

    [Theory]
    [InlineData(5, 50, DockRelation.Left)]
    [InlineData(95, 50, DockRelation.Right)]
    [InlineData(50, 5, DockRelation.Above)]
    [InlineData(50, 95, DockRelation.Below)]
    public void EachEdgeBand_SplitsTowardsThatEdge(double x, double y, DockRelation expected)
    {
        var target = DockTargetResolver.Resolve([Square], x, y);

        Assert.Equal(new DockTarget(Pane, expected), target);
    }

    [Fact]
    public void ThePointerOutsideEveryCandidate_ResolvesToNothing_WhichIsWhatUndocksAPanel()
    {
        Assert.Null(DockTargetResolver.Resolve([Square], 400, 400));
        Assert.Null(DockTargetResolver.Resolve([Square], -1, 50));
    }

    [Fact]
    public void NoCandidates_ResolveToNothing()
    {
        Assert.Null(DockTargetResolver.Resolve([], 10, 10));
    }

    [Fact]
    public void AZeroSizedCandidate_IsNeverATarget()
    {
        Assert.Null(DockTargetResolver.Resolve([new DockTargetCandidate(Pane, 0, 0, 0, 0)], 0, 0));
    }

    [Fact]
    public void ANestedPane_WinsOverTheContainerThatAlsoHoldsThePoint()
    {
        // Candidates are collected in render order, outermost first, so the
        // innermost pane under the pointer is the one the user is aiming at.
        var container = new DockTargetCandidate(Other, 0, 0, 100, 100);
        var nested = new DockTargetCandidate(Pane, 40, 40, 20, 20);

        var target = DockTargetResolver.Resolve([container, nested], 50, 50);

        Assert.Equal(Pane, target!.Value.NodeId);
    }

    [Fact]
    public void TheCentreZone_IsGenerousEnoughToHit_ButNeverSwallowsTheWholePane()
    {
        // Tabbing is the commonest intent, so the centre is deliberately
        // large — but the edges must still be reachable.
        var margin = (1 - DockTargetResolver.CentreFraction) / 2;

        Assert.Equal(DockRelation.Into, DockTargetResolver.Resolve([Square], 100 * (margin + 0.01), 50)!.Value.Relation);
        Assert.NotEqual(DockRelation.Into, DockTargetResolver.Resolve([Square], 100 * (margin - 0.01), 50)!.Value.Relation);
    }

    [Fact]
    public void ATallNarrowPane_StillOffersAllFourEdges()
    {
        var tall = new DockTargetCandidate(Pane, 0, 0, 40, 400);

        Assert.Equal(DockRelation.Left, DockTargetResolver.Resolve([tall], 1, 200)!.Value.Relation);
        Assert.Equal(DockRelation.Right, DockTargetResolver.Resolve([tall], 39, 200)!.Value.Relation);
        Assert.Equal(DockRelation.Above, DockTargetResolver.Resolve([tall], 20, 1)!.Value.Relation);
        Assert.Equal(DockRelation.Below, DockTargetResolver.Resolve([tall], 20, 399)!.Value.Relation);
    }

    [Fact]
    public void ACandidatesOwnOffset_IsRespected_SoNestedPanesResolveCorrectly()
    {
        var offset = new DockTargetCandidate(Pane, 200, 100, 100, 100);

        Assert.Equal(DockRelation.Into, DockTargetResolver.Resolve([offset], 250, 150)!.Value.Relation);
        Assert.Equal(DockRelation.Left, DockTargetResolver.Resolve([offset], 205, 150)!.Value.Relation);
    }
}
