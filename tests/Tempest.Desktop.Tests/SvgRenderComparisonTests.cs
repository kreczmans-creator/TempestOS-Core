using System.Text;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Tempest.Desktop.Viewing;
using Tempest.Workspace.Viewing;
using Xunit.Abstractions;

namespace Tempest.Desktop.Tests;

/// <summary>
/// DB-08: the SVG attachment renderer is the app's own code (SkiaSharp, MIT) in place of <c>Svg.Skia</c>, whose transitive
/// <c>Svg.Custom</c> is MS-PL. The baselines under <c>Fixtures/Svg/*.baseline.png</c> were captured with <c>Svg.Skia</c> 2.0.0.8
/// before the switch, at the scale <see cref="Fixtures"/> names; every fixture must still render to the same picture within the
/// tolerance below. The fixtures are the design system's own logo files (lines, paths, polygons, groups, transforms) and
/// synthetic files that cover the rest of the subset the renderer supports: basic shapes and path commands, presentation
/// attributes, inline <c>style</c>, a <c>&lt;style&gt;</c> sheet, opacity, dashes, caps and joins, transforms, <c>viewBox</c>
/// fitting, <c>defs</c>/<c>use</c>, gradients, CSS units and an embedded <c>data:</c> image.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tolerance.</b> Two rasterisers never agree pixel for pixel: anti-aliased edge coverage is computed differently, so the
/// pixels along every edge differ by a fraction of their colour. A pixel counts as different only when some channel differs by
/// more than <see cref="ChannelTolerance"/> (an edge pixel half-covered by one renderer and 40 % by the other differs by about
/// 25 of 255). A fixture passes when at most <see cref="MaxDifferentFraction"/> of its pixels differ that much and the mean
/// absolute channel difference over the whole picture is under <see cref="MaxMeanDifference"/>. The limits were set from the
/// measured differences after the switch (the largest fixture is well under half the limit) and sit far below what a missing,
/// misplaced, mis-sized or wrongly coloured shape produces: the smallest shape in any fixture covers more than 2 % of its
/// picture, and a wrong colour on a filled area moves the mean by tens of units.
/// </para>
/// <para>
/// <b>Recapturing.</b> With <c>TEMPEST_SVG_CAPTURE=1</c> the capture test rewrites every baseline from the renderer now
/// composed; do that only for a deliberate rendering change, and say so in the commit.
/// </para>
/// </remarks>
public sealed class SvgRenderComparisonTests(ITestOutputHelper output)
{
    /// <summary>A channel difference at or under this is anti-aliasing, not a different picture.</summary>
    public const int ChannelTolerance = 40;

    /// <summary>The largest share of a picture's pixels that may differ by more than <see cref="ChannelTolerance"/>.</summary>
    public const double MaxDifferentFraction = 0.02;

    /// <summary>The largest mean absolute channel difference over the whole picture.</summary>
    public const double MaxMeanDifference = 1.5;

    /// <summary>Every pixel-compared fixture and the scale it is rendered at (chosen so each baseline is a few hundred pixels across).</summary>
    public static readonly IReadOnlyDictionary<string, double> Fixtures = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["logo-horizontal-transparent-ink"] = 1.0,
        ["logo-horizontal-transparent-light"] = 1.0,
        ["logo-os-horizontal-transparent-ink"] = 1.0,
        ["mark-hub"] = 0.25,
        ["mark-navy"] = 0.25,
        ["mark-paper"] = 0.25,
        ["shapes"] = 2.0,
        ["styles"] = 2.0,
        ["transforms"] = 2.0,
        ["viewbox"] = 2.0,
        ["viewbox-only"] = 2.0,
        ["defs-use"] = 2.0,
        ["units"] = 2.0,
        ["image"] = 2.0,
    };

    public static TheoryData<string> FixtureNames => [.. Fixtures.Keys.Order(StringComparer.Ordinal)];

    private static string FixtureFolder => Path.Combine(DesktopTestHelpers.RepositoryRoot, "tests", "Tempest.Desktop.Tests", "Fixtures", "Svg");

    private static string FixturePath(string name) => Path.Combine(FixtureFolder, name + ".svg");

    private static string BaselinePath(string name) => Path.Combine(FixtureFolder, name + ".baseline.png");

    /// <summary>Renders <paramref name="name"/> through the viewer's page source at its fixture scale (2.0 for a fixture that is not pixel-compared).</summary>
    private static Bitmap Render(string name)
    {
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, File.ReadAllBytes(FixturePath(name)))!;
        return source.RenderPage(0, Fixtures.TryGetValue(name, out var scale) ? scale : 2.0);
    }

    [AvaloniaFact]
    public void Capture_Baselines_When_Asked()
    {
        if (Environment.GetEnvironmentVariable("TEMPEST_SVG_CAPTURE") != "1")
            return;

        foreach (var name in Fixtures.Keys)
        {
            using var page = Render(name);
            page.Save(BaselinePath(name));
            output.WriteLine($"{name}: {page.PixelSize.Width} x {page.PixelSize.Height} written.");
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(FixtureNames))]
    public void Fixture_Renders_As_Its_Baseline(string name)
    {
        var baselinePath = BaselinePath(name);
        Assert.True(File.Exists(baselinePath), $"{baselinePath} is missing; capture it with TEMPEST_SVG_CAPTURE=1.");

        using var baseline = new Bitmap(baselinePath);
        using var page = Render(name);

        Assert.Equal(baseline.PixelSize, page.PixelSize);
        var expected = Pixels(baseline);
        var actual = Pixels(page);
        var (different, mean) = Compare(expected, actual);
        var pixels = baseline.PixelSize.Width * baseline.PixelSize.Height;
        var fraction = (double)different / pixels;
        output.WriteLine($"{name}: {different} of {pixels} pixels differ by more than {ChannelTolerance} ({fraction:P2}); mean channel difference {mean:F3}.");

        Assert.True(fraction <= MaxDifferentFraction, $"{name}: {fraction:P2} of the pixels differ from the Svg.Skia baseline (limit {MaxDifferentFraction:P0}).");
        Assert.True(mean <= MaxMeanDifference, $"{name}: the mean channel difference from the Svg.Skia baseline is {mean:F2} (limit {MaxMeanDifference}).");
    }

    [AvaloniaFact]
    public void Text_Renders_Where_The_Fixture_Places_It()
    {
        // Text is not pixel-compared: the font the two renderers fall back to differs between machines. The words must still
        // appear as ink at the places the fixture puts them, and nowhere else.
        using var page = Render("text");
        var pixels = Pixels(page);
        var width = page.PixelSize.Width;

        Assert.True(InkIn(pixels, width, 20, 40, 180, 90) > 400, "the first line of text is not drawn");
        Assert.True(InkIn(pixels, width, 140, 150, 260, 190) > 200, "the centred text is not drawn");
        Assert.True(InkIn(pixels, width, 250, 230, 390, 270) > 200, "the end-anchored text is not drawn");
        Assert.Equal(0, InkIn(pixels, width, 0, 0, 400, 30));
        Assert.Equal(0, InkIn(pixels, width, 0, 100, 400, 140));
    }

    private static int InkIn(byte[] bgra, int width, int left, int top, int right, int bottom)
    {
        var count = 0;

        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var i = ((y * width) + x) * 4;

                if (bgra[i] < 200 || bgra[i + 1] < 200 || bgra[i + 2] < 200)
                    count++;
            }
        }

        return count;
    }

    private static (int Different, double Mean) Compare(byte[] expected, byte[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        var different = 0;
        long total = 0;

        for (var i = 0; i < expected.Length; i += 4)
        {
            var worst = 0;

            for (var c = 0; c < 4; c++)
            {
                var d = Math.Abs(expected[i + c] - actual[i + c]);
                total += d;
                worst = Math.Max(worst, d);
            }

            if (worst > ChannelTolerance)
                different++;
        }

        return (different, (double)total / expected.Length);
    }

    /// <summary>The BGRA bytes of <paramref name="bitmap"/>, row by row.</summary>
    internal static byte[] Pixels(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);

        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), pixels.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        return pixels;
    }
}
