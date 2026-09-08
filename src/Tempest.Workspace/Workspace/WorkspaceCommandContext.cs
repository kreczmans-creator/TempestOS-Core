using Tempest.Core.Commands;

namespace Tempest.App.Workspace;

/// <summary>
/// Translates the Workspace's own live selection into the Core
/// <see cref="CommandContext"/> a <see cref="CommandBinding"/> reads —
/// TD-77 Stage 5's one adapter, and the only place the two shapes meet.
/// </summary>
/// <remarks>
/// <para>
/// <b>On this side of the boundary, necessarily.</b>
/// <see cref="CommandContext"/> lives in <c>Tempest.Core</c>, which cannot
/// reference <c>Tempest.App</c>, so the translation cannot live with the
/// type it produces. It lives here rather than in <c>Tempest.Desktop</c>
/// because <see cref="ISelectionService"/> is the App-side source and
/// every surface — Ribbon, Palette, and any future one — needs the same
/// translation, not one each.
/// </para>
/// <para>
/// <b>Nothing is added on the way across.</b> A selection goes in and a
/// selection comes out: no project, no active view, no service provider,
/// no property bag. The Core contract carries exactly what the audit
/// found production commands read, and this adapter is not the place to
/// quietly widen it.
/// </para>
/// </remarks>
public static class WorkspaceCommandContext
{
    /// <summary>
    /// Builds the context for <paramref name="selection"/>'s own current
    /// state.
    /// </summary>
    /// <param name="selection">The Workspace's own selection service.</param>
    /// <returns>The context, empty when nothing is selected.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selection"/> is <see langword="null"/>.</exception>
    public static CommandContext From(ISelectionService selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        return From(selection.Current, selection.SelectedItems);
    }

    /// <summary>
    /// Builds the context from an already-read
    /// <paramref name="current"/>/<paramref name="selectedItems"/> pair —
    /// the same translation, for a caller holding a snapshot rather than
    /// the service.
    /// </summary>
    /// <param name="current">The Workspace's own current selection, or <see langword="null"/>.</param>
    /// <param name="selectedItems">Every selected item, in selection order.</param>
    /// <returns>The context, empty when nothing is selected.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selectedItems"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <b><see cref="CommandContext.Primary"/> is <paramref name="current"/>,
    /// deliberately.</b> A single-target binding reads
    /// <see cref="CommandContext.Primary"/>, and every surface means "the
    /// object the user has selected" by that — which is
    /// <see cref="ISelectionService.Current"/>. Those two are not always
    /// the same entry of <see cref="ISelectionService.SelectedItems"/>:
    /// <see cref="ISelectionService.ToggleSelectionAsync"/> makes the
    /// <i>most recently</i> toggled item current, while the list stays in
    /// toggle order (`ADR-0085`). So the current item is placed first and
    /// the rest follow in their own order — otherwise a Rename or a Delete
    /// would act on whichever object the user happened to click first,
    /// which is not the one the surface is showing them.
    /// </remarks>
    public static CommandContext From(WorkspaceSelection? current, IReadOnlyList<WorkspaceSelection> selectedItems)
    {
        ArgumentNullException.ThrowIfNull(selectedItems);

        if (current is null)
        {
            // No current selection means nothing is selected: ClearAsync
            // empties both, and ToggleSelectionAsync only nulls Current
            // once the list is empty. Handled explicitly rather than
            // assumed, so a future selection source cannot silently
            // produce a context with no Primary but several entries.
            return selectedItems.Count == 0 ? CommandContext.Empty : Ordered(selectedItems);
        }

        var ordered = new List<WorkspaceSelection>(selectedItems.Count + 1) { current };
        ordered.AddRange(selectedItems.Where(item => item.ObjectId != current.ObjectId));

        return Ordered(ordered);
    }

    private static CommandContext Ordered(IReadOnlyList<WorkspaceSelection> items) =>
        new([.. items.Select(item => new CommandContextObject(item.ObjectId, item.Kind))]);
}
