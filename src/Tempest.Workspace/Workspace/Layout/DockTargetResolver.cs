namespace Tempest.App.Workspace.Layout;

/// <summary>A candidate drop target: a node, and the rectangle it occupies on screen.</summary>
/// <param name="NodeId">The layout node under the pointer.</param>
/// <param name="X">Left edge, in the host's own coordinates.</param>
/// <param name="Y">Top edge, in the host's own coordinates.</param>
/// <param name="Width">Width, in device-independent pixels.</param>
/// <param name="Height">Height, in device-independent pixels.</param>
public readonly record struct DockTargetCandidate(Guid NodeId, double X, double Y, double Width, double Height)
{
    /// <summary>Whether <paramref name="pointX"/>, <paramref name="pointY"/> falls inside this candidate.</summary>
    public bool Contains(double pointX, double pointY) =>
        pointX >= X && pointX < X + Width && pointY >= Y && pointY < Y + Height;
}

/// <summary>Where a drop would put the dragged panel.</summary>
/// <param name="NodeId">The node dropped on.</param>
/// <param name="Relation">Which of the five zones the pointer is in.</param>
public readonly record struct DockTarget(Guid NodeId, DockRelation Relation);

/// <summary>
/// Decides which of the five dock zones a pointer position falls in
/// (`TD-72`).
/// </summary>
/// <remarks>
/// <para>
/// Deliberately pure geometry with no Avalonia types. Drag-to-dock is the
/// gesture most likely to be wrong at the edges — a one-pixel band, a zone
/// that swallows its neighbours, a centre that never triggers — and those
/// are exactly the cases that are painful to test through a UI and trivial
/// to test as a function. The overlay renders what this returns; it does
/// not decide anything itself.
/// </para>
/// <para>
/// The centre zone is generous, because tabbing is the most common intent
/// and the most annoying to miss.
/// </para>
/// </remarks>
public static class DockTargetResolver
{
    /// <summary>The fraction of each axis, measured from the centre, that tabs rather than splits.</summary>
    public const double CentreFraction = 0.4;

    /// <summary>
    /// The drop target for <paramref name="pointX"/>,
    /// <paramref name="pointY"/>, or <see langword="null"/> when the
    /// pointer is over no candidate.
    /// </summary>
    /// <remarks>
    /// Later candidates win an overlap, so a nested pane is preferred over
    /// the container that also contains the point — the reading a user
    /// expects when dropping onto something inside something else.
    /// </remarks>
    public static DockTarget? Resolve(IReadOnlyList<DockTargetCandidate> candidates, double pointX, double pointY)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        DockTargetCandidate? hit = null;
        foreach (var candidate in candidates)
        {
            if (candidate.Width > 0 && candidate.Height > 0 && candidate.Contains(pointX, pointY))
                hit = candidate;
        }

        if (hit is not { } target)
            return null;

        var relativeX = (pointX - target.X) / target.Width;
        var relativeY = (pointY - target.Y) / target.Height;

        var margin = (1 - CentreFraction) / 2;

        var insideHorizontally = relativeX >= margin && relativeX <= 1 - margin;
        var insideVertically = relativeY >= margin && relativeY <= 1 - margin;

        if (insideHorizontally && insideVertically)
            return new DockTarget(target.NodeId, DockRelation.Into);

        // Outside the centre, the nearest edge wins — measured as a
        // fraction of each axis, so a tall narrow pane and a short wide one
        // both behave the way they look.
        var distanceToLeft = relativeX;
        var distanceToRight = 1 - relativeX;
        var distanceToTop = relativeY;
        var distanceToBottom = 1 - relativeY;

        var nearest = Math.Min(Math.Min(distanceToLeft, distanceToRight), Math.Min(distanceToTop, distanceToBottom));

        var relation =
            nearest == distanceToLeft ? DockRelation.Left
            : nearest == distanceToRight ? DockRelation.Right
            : nearest == distanceToTop ? DockRelation.Above
            : DockRelation.Below;

        return new DockTarget(target.NodeId, relation);
    }
}
