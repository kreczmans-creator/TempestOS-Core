namespace Tempest.App.Workspace.Layout;

/// <summary>One of the named, ready-made arrangements the View menu offers.</summary>
public enum WorkspaceLayoutPreset
{
    /// <summary>The day-to-day working arrangement — Explorer and Inspector both docked, Output closed.</summary>
    Engineering,

    /// <summary>For reviewing existing work — a narrower Explorer, a wider Inspector, and the Output panel open beneath the document.</summary>
    Review,

    /// <summary>For browsing and authoring documentation — a wide Explorer, the Inspector auto-hidden, Output closed.</summary>
    Documentation,
}

/// <summary>
/// Builds the named arrangements as layout trees (`TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// A preset used to be three panel placements against three fixed docks,
/// because those were the only arrangements the old grid could express.
/// It is now an ordinary tree, built from the same operations a user's own
/// dragging produces — so a preset is nothing more than a layout someone
/// already arranged, and a future preset needs no new mechanism.
/// </para>
/// <para>
/// The document panel is the widest child rather than a privileged centre
/// slot, which is what makes the responsive rule ("keep the working pane
/// usable") apply to presets without any special-casing.
/// </para>
/// </remarks>
public static class WorkspaceLayoutPresets
{
    /// <summary>The arrangement a first run opens with, and the one "Reset Layout" returns to.</summary>
    public static WorkspaceLayoutTree Default(Guid explorer, Guid document, Guid inspector, Guid output) =>
        Build(WorkspaceLayoutPreset.Engineering, explorer, document, inspector, output);

    /// <summary>Builds <paramref name="preset"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="preset"/> is not a declared preset.</exception>
    public static WorkspaceLayoutTree Build(WorkspaceLayoutPreset preset, Guid explorer, Guid document, Guid inspector, Guid output) => preset switch
    {
        WorkspaceLayoutPreset.Engineering => ThreeColumn(explorer, document, inspector, 0.2, 0.6, 0.2),

        WorkspaceLayoutPreset.Review => WithOutputBelowDocument(
            ThreeColumn(explorer, document, inspector, 0.15, 0.55, 0.30), document, output),

        WorkspaceLayoutPreset.Documentation => ThreeColumn(explorer, document, inspector, 0.28, 0.62, 0.10)
            .SetPinned(inspector, false),

        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown workspace layout preset."),
    };

    private static WorkspaceLayoutTree ThreeColumn(
        Guid explorer, Guid document, Guid inspector, double explorerWeight, double documentWeight, double inspectorWeight)
    {
        var tree = WorkspaceLayoutTree.Single(document);
        tree = tree.Dock(explorer, tree.FindGroupContaining(document)!.Id, DockRelation.Left);
        tree = tree.Dock(inspector, tree.FindGroupContaining(document)!.Id, DockRelation.Right);

        var split = (LayoutSplitNode)tree.Root!;
        return tree.SetWeights(split.Id, [explorerWeight, documentWeight, inspectorWeight]);
    }

    private static WorkspaceLayoutTree WithOutputBelowDocument(WorkspaceLayoutTree tree, Guid document, Guid output)
    {
        var withOutput = tree.Dock(output, tree.FindGroupContaining(document)!.Id, DockRelation.Below);

        // The document keeps roughly three quarters of its column; the
        // Output panel takes the rest.
        var documentGroup = withOutput.FindGroupContaining(document)!;
        var verticalSplit = withOutput.Root!.DescendantsAndSelf
            .OfType<LayoutSplitNode>()
            .First(s => s.Orientation == LayoutOrientation.Vertical && s.Children.Any(c => c.Id == documentGroup.Id));

        return withOutput.SetWeights(verticalSplit.Id, [0.72, 0.28]);
    }
}
