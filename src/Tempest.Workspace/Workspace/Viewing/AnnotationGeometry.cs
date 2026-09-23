namespace Tempest.Workspace.Viewing;

/// <summary>
/// Converts one point between a page's native (unrotated) content space and
/// its currently-displayed, rotated content space (`TD-98`, `TD-101`).
/// </summary>
/// <remarks>
/// <para>
/// An <c>AttachmentAnnotation</c>'s own points are stored in the page's
/// native units — the same units its page source reports for the page's
/// natural size — so a stroke drawn at any zoom and any rotation reads back
/// in exactly the same place regardless of how the viewer happens to be
/// showing the page when it is next opened. Rendering that stroke, and
/// turning a pointer position back into a stored point while drawing one,
/// both need the same rotation the rendered bitmap itself already carries
/// (<c>DocumentViewerView.RotateForDisplay</c>) — this type is that
/// rotation, factored out as pure arithmetic so it can be proven correct
/// with no control, no bitmap and no rendering concept in the process, the
/// same discipline <see cref="DocumentViewport"/> already applies to
/// zoom/pan/fit.
/// </para>
/// <para>
/// The rotation matches <c>DocumentViewerView.RotateForDisplay</c>'s own
/// composition exactly: translate the native point so the page's own
/// centre sits at the origin, rotate by <c>rotationDegrees</c> clockwise
/// (positive angle, screen/Y-down convention — the same convention
/// Avalonia's own <c>Matrix.CreateRotation</c> uses, which is what actually
/// turns the bitmap), then translate so the origin sits at the rotated
/// bounding box's own centre. <see cref="ToRotatedContentSpace"/> is that
/// transform; <see cref="FromRotatedContentSpace"/> is its exact inverse.
/// </para>
/// </remarks>
public static class AnnotationGeometry
{
    /// <summary>
    /// Maps a point from a page's native (unrotated) content space into its
    /// currently-displayed content space, after rotating by
    /// <paramref name="rotationDegrees"/> (0, 90, 180 or 270, clockwise).
    /// </summary>
    /// <param name="x">The point's native X.</param>
    /// <param name="y">The point's native Y.</param>
    /// <param name="nativeWidth">The page's own width, unrotated.</param>
    /// <param name="nativeHeight">The page's own height, unrotated.</param>
    /// <param name="rotationDegrees">The current rotation, clockwise — 0, 90, 180 or 270.</param>
    /// <returns>
    /// The same point, in the rotated bounding box's own coordinate space
    /// (width/height swapped from <paramref name="nativeWidth"/>/<paramref name="nativeHeight"/>
    /// for a 90° or 270° rotation).
    /// </returns>
    public static (double X, double Y) ToRotatedContentSpace(
        double x, double y, double nativeWidth, double nativeHeight, int rotationDegrees)
    {
        var (targetWidth, targetHeight) = RotatedSize(nativeWidth, nativeHeight, rotationDegrees);
        var (sin, cos) = SinCos(rotationDegrees);

        var dx = x - nativeWidth / 2.0;
        var dy = y - nativeHeight / 2.0;

        var rx = dx * cos - dy * sin;
        var ry = dx * sin + dy * cos;

        return (rx + targetWidth / 2.0, ry + targetHeight / 2.0);
    }

    /// <summary>
    /// The exact inverse of <see cref="ToRotatedContentSpace"/>: maps a
    /// point from the currently-displayed (rotated) content space back to
    /// the page's own native, unrotated units — what a drawn stroke is
    /// stored as.
    /// </summary>
    /// <param name="x">The point's rotated-space X.</param>
    /// <param name="y">The point's rotated-space Y.</param>
    /// <param name="nativeWidth">The page's own width, unrotated.</param>
    /// <param name="nativeHeight">The page's own height, unrotated.</param>
    /// <param name="rotationDegrees">The current rotation, clockwise — 0, 90, 180 or 270.</param>
    public static (double X, double Y) FromRotatedContentSpace(
        double x, double y, double nativeWidth, double nativeHeight, int rotationDegrees)
    {
        var (targetWidth, targetHeight) = RotatedSize(nativeWidth, nativeHeight, rotationDegrees);
        // The inverse of a rotation by θ is a rotation by −θ.
        var (sin, cos) = SinCos(-rotationDegrees);

        var dx = x - targetWidth / 2.0;
        var dy = y - targetHeight / 2.0;

        var rx = dx * cos - dy * sin;
        var ry = dx * sin + dy * cos;

        return (rx + nativeWidth / 2.0, ry + nativeHeight / 2.0);
    }

    /// <summary>The bounding box of a <paramref name="nativeWidth"/>-by-<paramref name="nativeHeight"/> page after rotating by <paramref name="rotationDegrees"/>.</summary>
    public static (double Width, double Height) RotatedSize(double nativeWidth, double nativeHeight, int rotationDegrees) =>
        IsSwapped(rotationDegrees) ? (nativeHeight, nativeWidth) : (nativeWidth, nativeHeight);

    /// <summary>Whether <paramref name="rotationDegrees"/> swaps width and height — true for 90° and 270°.</summary>
    public static bool IsSwapped(int rotationDegrees) => NormaliseDegrees(rotationDegrees) is 90 or 270;

    private static (double Sin, double Cos) SinCos(int degrees)
    {
        var radians = NormaliseDegrees(degrees) * Math.PI / 180.0;
        return (Math.Sin(radians), Math.Cos(radians));
    }

    private static int NormaliseDegrees(int degrees) => ((degrees % 360) + 360) % 360;
}
