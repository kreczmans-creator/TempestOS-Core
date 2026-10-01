using System.Reflection;
using SkiaSharp;

namespace Tempest.Desktop.Documents;

/// <summary>
/// The horizontal ink Tempest Engineering lockup — dark (ink/indigo)
/// logotype and mark on a transparent ground, the design system's
/// light-ground variant for the paper theme documents use
/// (<c>docs/design/system/assets/logo/derived/logo-horizontal-transparent-ink.svg</c>,
/// rasterised at 4x). It replaced the navy lockup `WP 21.2A` first
/// embedded, whose white-on-navy box the Product Owner rejected on paper
/// documents. Embedded as a <c>Tempest.Desktop</c> resource
/// (<c>Documents/Assets/Logo/tempest-logo-horizontal-ink.png</c>) and
/// decoded once through <see cref="SKBitmap.Decode(System.IO.Stream)"/> —
/// no running Avalonia application needed, mirroring
/// <see cref="DocumentFonts"/>'s own loading discipline.
/// </summary>
public static class DocumentLogo
{
    private static readonly Lazy<SKBitmap?> HorizontalInkBitmap = new(() => Load("tempest-logo-horizontal-ink.png"));

    /// <summary>
    /// The lockup bitmap, or <see langword="null"/> when the embedded
    /// resource could not be loaded — <see cref="DocumentTemplate.AddHeaderBand"/>
    /// falls back to the plain "TEMPEST"/"OS" text pair it always drew
    /// before this Work Package when this is <see langword="null"/>, so a
    /// load failure degrades the header band rather than breaking it.
    /// </summary>
    public static SKBitmap? HorizontalInk => HorizontalInkBitmap.Value;

    private static SKBitmap? Load(string fileName)
    {
        var assembly = typeof(DocumentLogo).Assembly;
        var resourceName = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith(fileName, StringComparison.Ordinal));
        if (resourceName is null)
            return null;

        using var stream = assembly.GetManifestResourceStream(resourceName);
        return stream is null ? null : SKBitmap.Decode(stream);
    }
}
