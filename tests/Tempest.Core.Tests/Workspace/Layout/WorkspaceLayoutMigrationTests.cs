using Tempest.App.Workspace.Layout;

namespace Tempest.Core.Tests.Workspace.Layout;

/// <summary>
/// Carrying a returning user's own panel preferences into the new layout
/// model (`TD-72`).
/// </summary>
/// <remarks>
/// Replacing the docking abstraction must not cost anyone the arrangement
/// they had. These are the tests for the one question that actually
/// matters to an existing user on upgrade day: does my workspace still
/// look like my workspace?
/// </remarks>
public class WorkspaceLayoutMigrationTests
{
    private static readonly Guid Explorer = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Document = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Inspector = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Output = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static WorkspaceLayoutTree Baseline() =>
        WorkspaceLayoutPresets.Default(Explorer, Document, Inspector, Output);

    [Fact]
    public void NoPreferences_LeaveTheDefaultArrangementAlone()
    {
        var migrated = WorkspaceLayoutMigration.FromLegacyPreferences(Baseline(), []);

        Assert.Equal(Baseline().Root!.Panels, migrated.Root!.Panels);
    }

    [Fact]
    public void APanelTheUserHadHidden_StaysOutOfTheArrangement()
    {
        var migrated = WorkspaceLayoutMigration.FromLegacyPreferences(Baseline(),
            [new LegacyPanelPreference(Inspector, IsVisible: false, IsCollapsed: false, IsPinned: true, Size: 240)]);

        Assert.DoesNotContain(Inspector, migrated.AllPanels);
        Assert.Contains(Explorer, migrated.AllPanels);
        Assert.Contains(Document, migrated.AllPanels);
    }

    [Fact]
    public void ACollapsedPanel_StaysCollapsed_AndAnAutoHiddenOneStaysAutoHidden()
    {
        var migrated = WorkspaceLayoutMigration.FromLegacyPreferences(Baseline(),
        [
            new LegacyPanelPreference(Explorer, IsVisible: true, IsCollapsed: true, IsPinned: true, Size: 240),
            new LegacyPanelPreference(Inspector, IsVisible: true, IsCollapsed: false, IsPinned: false, Size: 240),
        ]);

        Assert.True(migrated.PresentationOf(Explorer).IsCollapsed);
        Assert.False(migrated.PresentationOf(Inspector).IsPinned);
    }

    [Fact]
    public void ARecordedPixelWidth_BecomesTheEquivalentProportion()
    {
        // The old model stored pixels; the new one stores proportions, so
        // a 320 px Explorer in a 1280 px window becomes a quarter.
        var migrated = WorkspaceLayoutMigration.FromLegacyPreferences(Baseline(),
            [new LegacyPanelPreference(Explorer, IsVisible: true, IsCollapsed: false, IsPinned: true, Size: 320)],
            totalSize: 1280);

        var root = (LayoutSplitNode)migrated.Root!;
        var explorerIndex = root.Children.ToList().FindIndex(c => c.Panels.Contains(Explorer));

        Assert.Equal(0.25, root.Weights[explorerIndex], precision: 3);
    }

    [Fact]
    public void AnAbsurdRecordedWidth_IsClamped_RatherThanSwallowingTheWindow()
    {
        var migrated = WorkspaceLayoutMigration.FromLegacyPreferences(Baseline(),
            [new LegacyPanelPreference(Explorer, IsVisible: true, IsCollapsed: false, IsPinned: true, Size: 100000)],
            totalSize: 1280);

        var root = (LayoutSplitNode)migrated.Root!;
        var explorerIndex = root.Children.ToList().FindIndex(c => c.Panels.Contains(Explorer));

        Assert.True(root.Weights[explorerIndex] <= 0.8);
        Assert.True(root.Weights.Sum() > 0.999);
    }

    [Fact]
    public void APreferenceForAPanelThatIsNotInTheBaseline_IsIgnored()
    {
        var stranger = Guid.NewGuid();

        var migrated = WorkspaceLayoutMigration.FromLegacyPreferences(Baseline(),
            [new LegacyPanelPreference(stranger, IsVisible: true, IsCollapsed: true, IsPinned: false, Size: 200)]);

        Assert.DoesNotContain(stranger, migrated.AllPanels);
        Assert.Equal(Baseline().Root!.Panels, migrated.Root!.Panels);
    }

    [Fact]
    public void AZeroWindowExtent_LeavesProportionsAlone_RatherThanDividingByIt()
    {
        var migrated = WorkspaceLayoutMigration.FromLegacyPreferences(Baseline(),
            [new LegacyPanelPreference(Explorer, IsVisible: true, IsCollapsed: false, IsPinned: true, Size: 320)],
            totalSize: 0);

        var root = (LayoutSplitNode)migrated.Root!;
        Assert.All(root.Weights, w => Assert.True(double.IsFinite(w) && w > 0));
    }
}
