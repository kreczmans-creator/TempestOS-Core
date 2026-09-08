namespace Tempest.App.Workspace.Layout;

/// <summary>
/// One panel's own arrangement as the pre-`TD-72` edge-based workspace
/// recorded it — the shape a returning user's saved preferences are in.
/// </summary>
/// <param name="PanelId">The panel.</param>
/// <param name="IsVisible">Whether it was shown at all.</param>
/// <param name="IsCollapsed">Whether it was collapsed in place.</param>
/// <param name="IsPinned">Whether it was pinned, as opposed to auto-hidden.</param>
/// <param name="Size">Its size along its own dock's axis, in device-independent pixels.</param>
public sealed record LegacyPanelPreference(
    Guid PanelId,
    bool IsVisible,
    bool IsCollapsed,
    bool IsPinned,
    double Size);

/// <summary>
/// Carries a user's existing panel preferences into the new layout model
/// (`TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// Replacing the docking abstraction must not cost anyone the arrangement
/// they had. A returning user has no saved layout tree — they have the
/// old edge-based preferences: widths, visibility, collapsed and pinned
/// flags. This turns those into the equivalent tree, once, so their first
/// launch after the upgrade looks like their last launch before it.
/// </para>
/// <para>
/// Deliberately a pure function of the old preferences rather than
/// something the composer does inline, because "did the upgrade preserve
/// what I had" is exactly the question that deserves its own tests.
/// </para>
/// </remarks>
public static class WorkspaceLayoutMigration
{
    /// <summary>
    /// Builds the arrangement equivalent to <paramref name="preferences"/>,
    /// starting from <paramref name="baseline"/>.
    /// </summary>
    /// <param name="baseline">The default arrangement the preferences are applied to.</param>
    /// <param name="preferences">Each panel's own pre-`TD-72` preference.</param>
    /// <param name="totalSize">The window extent the recorded pixel sizes were measured against, used to turn them into proportions.</param>
    public static WorkspaceLayoutTree FromLegacyPreferences(
        WorkspaceLayoutTree baseline, IReadOnlyList<LegacyPanelPreference> preferences, double totalSize = 1280)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(preferences);

        var tree = baseline;

        foreach (var preference in preferences)
        {
            // A panel the user had hidden stays hidden — as "not in the
            // layout", which is what hidden now means.
            if (!preference.IsVisible)
            {
                tree = tree.Remove(preference.PanelId);
                continue;
            }

            if (!tree.Contains(preference.PanelId))
                continue;

            tree = tree
                .SetCollapsed(preference.PanelId, preference.IsCollapsed)
                .SetPinned(preference.PanelId, preference.IsPinned);
        }

        return ApplySizes(tree, preferences, totalSize);
    }

    /// <summary>
    /// Converts recorded pixel sizes into the proportional weights the new
    /// model uses, for the one split that holds them.
    /// </summary>
    private static WorkspaceLayoutTree ApplySizes(
        WorkspaceLayoutTree tree, IReadOnlyList<LegacyPanelPreference> preferences, double totalSize)
    {
        if (tree.Root is not LayoutSplitNode root || totalSize <= 0)
            return tree;

        var recorded = new double?[root.Children.Count];
        var anyRecorded = false;

        for (var i = 0; i < root.Children.Count; i++)
        {
            var panelId = root.Children[i].Panels.FirstOrDefault();
            var preference = preferences.FirstOrDefault(p => p.PanelId == panelId);

            if (preference is not { IsVisible: true, Size: > 0 })
                continue;

            recorded[i] = Math.Clamp(preference.Size / totalSize, 0.05, 0.8);
            anyRecorded = true;
        }

        if (!anyRecorded)
            return tree;

        // A recorded width must survive migration as the fraction the user
        // actually had. Simply substituting it and letting normalisation
        // rescale everything would dilute it — a 320 px Explorer in a
        // 1280 px window would come back as 23.8% rather than 25%. So the
        // recorded fractions are kept exactly, and whatever remains is
        // shared out among the panels that had no recorded size, in their
        // existing proportions.
        var claimed = recorded.Where(r => r is not null).Sum(r => r!.Value);
        var remaining = Math.Max(1 - claimed, 0);
        var unrecordedWeight = root.Weights.Where((_, i) => recorded[i] is null).Sum();

        var sized = new List<double>(root.Children.Count);
        for (var i = 0; i < root.Children.Count; i++)
        {
            sized.Add(recorded[i] is { } fraction
                ? fraction
                : unrecordedWeight > 0 ? remaining * (root.Weights[i] / unrecordedWeight) : 0);
        }

        return tree.SetWeights(root.Id, sized);
    }
}
