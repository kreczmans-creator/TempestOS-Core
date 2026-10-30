using Avalonia.Headless.XUnit;
using Tempest.Desktop.Viewing;
using Tempest.Workspace.Viewing;

namespace Tempest.Desktop.Tests;

/// <summary>
/// DB-08 review, fixture gaps: the features the comparison fixtures did not cover and that no exporter file in the repository
/// shows — a nested <c>svg</c> with a <c>viewBox</c>, every <c>preserveAspectRatio</c> form, a percentage dash array, text on a
/// path, Illustrator's <c>switch</c>, and a <c>symbol</c> with a <c>viewBox</c> — each as a fixture under <c>Fixtures/Svg</c> and
/// checked by the colour of the pixels the feature decides. There is no <c>Svg.Skia</c> baseline for these: the renderer that made
/// the baselines is gone, so they are held to the specification rather than to a picture.
/// </summary>
public sealed class SvgFixtureGapTests
{
    private static readonly (byte B, byte G, byte R) Navy = (0x97, 0x2d, 0x1c);
    private static readonly (byte B, byte G, byte R) Red = (0x2b, 0x39, 0xc0);
    private static readonly (byte B, byte G, byte R) White = (255, 255, 255);
    private static readonly (byte B, byte G, byte R) Black = (0, 0, 0);

    private static string FixturePath(string name) => Path.Combine(DesktopTestHelpers.RepositoryRoot, "tests", "Tempest.Desktop.Tests", "Fixtures", "Svg", name + ".svg");

    private static IDocumentPageSource Open(string name) => DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, File.ReadAllBytes(FixturePath(name)))!;

    /// <summary>The BGR of the fixture's page at scale 1, at each (x, y) asked for.</summary>
    private static (byte B, byte G, byte R)[] PixelsAt(IDocumentPageSource source, params (int X, int Y)[] points)
    {
        using var page = source.RenderPage(0, 1.0);
        var pixels = SvgRenderComparisonTests.Pixels(page);
        var width = page.PixelSize.Width;
        return [.. points.Select(p => { var i = ((p.Y * width) + p.X) * 4; return (pixels[i], pixels[i + 1], pixels[i + 2]); })];
    }

    [AvaloniaFact]
    public void A_Nested_Svg_Fits_Its_ViewBox_And_Clips_To_Its_Viewport()
    {
        using var source = Open("nested-svg");
        Assert.Null(source.RenderingNote);

        // The navy square fills the 50 by 50 viewport at (10, 10); the red square to its right lies outside it and is not shown.
        Assert.Equal([Navy, Navy, White, White], PixelsAt(source, (12, 12), (58, 58), (65, 35), (5, 5)));
    }

    [AvaloniaFact]
    public void PreserveAspectRatio_None_Stretches_XMin_Aligns_Left_And_Slice_Fills_The_Viewport()
    {
        using var source = Open("aspect-ratio");

        // none: the 10 by 10 content is stretched over the whole 90 by 30, so its right edge is navy.
        // xMinYMin meet: scaled to 30 by 30 at the left, the red square beside it; the rest of the viewport stays white.
        // xMidYMid slice: scaled to 90 by 90 and clipped to the 30-high viewport; the red square lies beyond its right edge.
        Assert.Equal([Navy, Navy, Navy, Red, White, Navy, Navy, White], PixelsAt(source, (5, 15), (85, 15), (15, 50), (45, 50), (75, 50), (5, 85), (85, 85), (95, 85)));
    }

    [AvaloniaFact]
    public void A_Percentage_Dash_Refers_To_The_Normalised_Diagonal()
    {
        using var source = Open("dash-percent");

        // 10 % of the 200 by 50 viewport's normalised diagonal (145.8) is 14.6: on to 14.6, off to 29.2, on to 43.7. Of the width it would be 20, and 17 would be on.
        Assert.Equal([Black, White, Black, White], PixelsAt(source, (5, 25), (17, 25), (35, 25), (50, 25)));
    }

    [AvaloniaFact]
    public void Text_On_A_Path_Is_Reported_As_Not_Shown_And_Not_Drawn_Anywhere_Else()
    {
        using var source = Open("textpath");
        Assert.Equal("Some parts of this drawing are not shown: textPath.", source.RenderingNote);

        using var page = source.RenderPage(0, 1.0);
        var pixels = SvgRenderComparisonTests.Pixels(page);
        Assert.All(Enumerable.Range(0, pixels.Length / 4), i => Assert.True(pixels[i * 4] == 255 && pixels[(i * 4) + 1] == 255 && pixels[(i * 4) + 2] == 255, "the page is white: the text is not drawn in a straight line somewhere"));
    }

    [AvaloniaFact]
    public void An_Illustrator_Switch_Draws_The_Group_After_Its_ForeignObject()
    {
        using var source = Open("switch");

        // The foreignObject is Illustrator's own and is passed over, not reported: the drawing that follows is shown whole.
        Assert.Null(source.RenderingNote);
        Assert.Equal([Navy, White], PixelsAt(source, (50, 50), (5, 5)));
    }

    [AvaloniaFact]
    public void A_Symbol_With_A_ViewBox_Is_Fitted_To_The_Use_Size_And_Clipped_To_It()
    {
        using var source = Open("symbol-viewbox");
        Assert.Equal([Navy, Navy, White, White], PixelsAt(source, (12, 12), (58, 58), (65, 35), (5, 5)));
    }
}
