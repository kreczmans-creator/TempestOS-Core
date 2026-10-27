using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Avalonia.Headless.XUnit;
using SkiaSharp;
using Tempest.Desktop.Viewing;
using Tempest.Workspace.Viewing;

namespace Tempest.Desktop.Tests;

/// <summary>
/// DB-08 review: the SVG reader against the hostile-input classes the review measured (`odp-review-final.md`, F1 to F11, I1), each
/// scaled to a document under 1 MB that must build in under two seconds (scaled by <see cref="DesktopTestHelpers.TimeoutFactor"/>)
/// once the fix is in. The full-size probes, which took seconds to minutes or killed the process before the fixes, live in
/// <see cref="SvgBoundedProbeTests"/> and run one per process under memory and time limits when CI asks for them.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SvgHostileInputTests
{
    private const string Ns = "xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"";

    /// <summary>The time a scaled-down hostile document may take: two seconds, scaled for a loaded runner.</summary>
    private static TimeSpan Budget => TimeSpan.FromSeconds(2 * DesktopTestHelpers.TimeoutFactor);

    private static string Svg(string body, string size = "width=\"100\" height=\"100\"") => $"<svg {Ns} {size}>{body}</svg>";

    /// <summary>The colour at (<paramref name="x"/>, <paramref name="y"/>) of <paramref name="picture"/> drawn at scale 1 on a transparent bitmap of its own size.</summary>
    private static SKColor PixelAt(SvgPictureBuilder.SvgPicture picture, int x, int y)
    {
        using var bitmap = new SKBitmap(Math.Max(1, (int)Math.Ceiling(picture.Size.Width)), Math.Max(1, (int)Math.Ceiling(picture.Size.Height)), SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(picture.Picture);
        return bitmap.GetPixel(x, y);
    }

    /// <summary>A black 1-bit greyscale PNG of <paramref name="width"/> by <paramref name="height"/>: a few hundred bytes on disk, <c>width * height * 4</c> bytes once decoded (the review's "PNG bomb").</summary>
    internal static byte[] OneBitPng(int width, int height)
    {
        static byte[] Chunk(string type, byte[] data)
        {
            var typeBytes = Encoding.ASCII.GetBytes(type);
            var chunk = new byte[12 + data.Length];
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(chunk, (uint)data.Length);
            typeBytes.CopyTo(chunk, 4);
            data.CopyTo(chunk, 8);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + data.Length), Crc32(typeBytes, data));
            return chunk;
        }

        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 1; // bit depth

        var row = new byte[1 + ((width + 7) / 8)];
        using var raw = new MemoryStream();

        using (var zlib = new ZLibStream(raw, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
                zlib.Write(row);
        }

        return [.. new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, .. Chunk("IHDR", header), .. Chunk("IDAT", raw.ToArray()), .. Chunk("IEND", [])];
    }

    private static uint Crc32(byte[] first, byte[] second)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in first.Concat(second))
        {
            crc ^= b;

            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static string DataUri(byte[] png) => "data:image/png;base64," + Convert.ToBase64String(png);

    /// <summary>Builds <paramref name="markup"/> and returns the picture with the time it took, failing when that is over <see cref="Budget"/>.</summary>
    private static SvgPictureBuilder.SvgPicture BuildWithin(string markup, string what)
    {
        Assert.True(markup.Length < 1024 * 1024, "a hostile test input stays under 1 MB");
        var watch = Stopwatch.StartNew();
        var picture = SvgPictureBuilder.Build(markup);
        watch.Stop();
        Assert.True(watch.Elapsed < Budget, $"{what}: building took {watch.Elapsed.TotalSeconds:F2} s (limit {Budget.TotalSeconds:F0} s).");
        return picture;
    }

    // ------------------------------------------------------------------
    // F1: regular expressions — linear, and never on the UI thread for long
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void An_Rgb_Colour_With_A_Long_Run_Of_Spaces_Is_Read_In_Linear_Time()
    {
        // Before: `\s*[,\s]\s*` on the backtracking engine tried the run in quadratically many ways (40 000 spaces: 4.3 s; 60 000: ~10 s).
        using var picture = BuildWithin(Svg("<rect width=\"10\" height=\"10\" fill=\"rgb(1" + new string(' ', 60_000) + "x)\"/>"), "rgb() with 60 000 spaces");
        Assert.Equal(new SkiaSharp.SKSize(100, 100), picture.Size);
    }

    [AvaloniaFact]
    public void A_Style_Sheet_Of_Unclosed_Comment_Openers_Is_Read_In_Linear_Time()
    {
        // Before: `/\*.*?\*/` rescanned to the end of the sheet from every opener (100 000 openers: 1.5 s; 200 000: ~6 s).
        using var picture = BuildWithin(Svg("<style>" + string.Concat(Enumerable.Repeat("/*a", 200_000)) + "</style><rect width=\"10\" height=\"10\"/>"), "200 000 comment openers");
        Assert.Equal(new SkiaSharp.SKSize(100, 100), picture.Size);
    }

    [AvaloniaFact]
    public void Fifty_Thousand_Script_Openers_Pass_The_Sanitiser_In_Linear_Time()
    {
        // Before: `<script\b[^>]*>.*?</script\s*>` rescanned to the end from every opener (the review's `script-50k-openers`, "tens of seconds").
        var markup = Svg(string.Concat(Enumerable.Repeat("<script>", 50_000)), "width=\"10\" height=\"10\"");
        Assert.True(markup.Length < 1024 * 1024);

        var watch = Stopwatch.StartNew();
        var sanitised = SvgMarkupSanitiser.Sanitise(markup);
        watch.Stop();

        // Nothing closes the elements, so nothing is removed; the XML reader then refuses the file (50 000 levels deep: see the nesting test).
        Assert.Equal(markup, sanitised);
        Assert.True(watch.Elapsed < Budget, $"the sanitiser took {watch.Elapsed.TotalSeconds:F2} s.");
    }

    [AvaloniaFact]
    public void A_Colour_Longer_Than_64_Characters_Is_Not_A_Colour()
    {
        // Before: the pattern accepted any amount of space inside the brackets, so this was rgb(1, 2, 3) after a long match.
        var padded = "rgb(1, 2, 3" + new string(' ', 70) + ")";
        Assert.True(padded.Length > SvgPictureBuilder.MaxColourLength);
        Assert.Null(SvgPictureBuilder.ParseColour(padded, SkiaSharp.SKColors.Black));

        // The longest honest colour is well within the limit.
        Assert.Equal(new SkiaSharp.SKColor(255, 255, 255, 255), SvgPictureBuilder.ParseColour("rgba(100%, 100%, 100%, 100%)", SkiaSharp.SKColors.Black));
    }

    // ------------------------------------------------------------------
    // F2: gradient stops — parsed once per gradient, at most 64
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Gradient_Is_Parsed_Once_For_The_Document_And_Takes_At_Most_64_Stops()
    {
        // 100 stops, blue up to the 64th and red after: the shader takes the first 64 and is blue to the end.
        var stops = new StringBuilder();

        for (var i = 0; i < 100; i++)
            stops.Append(System.Globalization.CultureInfo.InvariantCulture, $"<stop offset=\"{i / 99.0:F4}\" stop-color=\"{(i < SvgPictureBuilder.MaxGradientStops ? "#0000ff" : "#ff0000")}\"/>");

        var markup = Svg("<defs><linearGradient id=\"g\" gradientUnits=\"userSpaceOnUse\" x1=\"0\" y1=\"0\" x2=\"100\" y2=\"0\">" + stops + "</linearGradient></defs>" +
            "<rect width=\"100\" height=\"50\" fill=\"url(#g)\"/><rect y=\"50\" width=\"100\" height=\"50\" fill=\"url(#g)\" stroke=\"url(#g)\"/>");

        using var picture = BuildWithin(markup, "a gradient of 100 stops on three paints");

        // Three paints, one parse (before: three, and one per shape of the 2 000 that aborted the process).
        Assert.Equal(1, picture.Statistics.GradientStopParses);
        Assert.Equal(new SKColor(0, 0, 255), PixelAt(picture, 98, 25));
        Assert.Equal(new SKColor(0, 0, 255), PixelAt(picture, 98, 75));
    }

    // ------------------------------------------------------------------
    // F3: a dash array of more than 64 values is no dash
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Dash_Array_Of_More_Than_64_Values_Draws_The_Stroke_Solid()
    {
        static string Line(int values) => Svg("<line x1=\"0\" y1=\"50\" x2=\"100\" y2=\"50\" stroke=\"#000\" stroke-width=\"4\" stroke-dasharray=\"" +
            string.Join(' ', Enumerable.Range(0, values).Select(i => i % 2 == 0 ? "0" : "100")) + "\"/>");

        // 64 values of "0 100": every dash has no length, so the line is invisible.
        using var dashed = SvgPictureBuilder.Build(Line(SvgPictureBuilder.MaxDashes));
        Assert.Equal(0, PixelAt(dashed, 50, 50).Alpha);

        // 66 values: the list is ignored and the stroke is solid, rather than copied into a path effect for every stroke (a million values on 500 lines aborted the process).
        using var solid = SvgPictureBuilder.Build(Line(SvgPictureBuilder.MaxDashes + 2));
        Assert.Equal(255, PixelAt(solid, 50, 50).Alpha);
    }

    // ------------------------------------------------------------------
    // F4: embedded images — one decode per reference, a pixel budget, 4096 px edges
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void An_Embedded_Image_Is_Decoded_Once_However_Often_It_Is_Used()
    {
        var limits = SvgPictureBuilder.Limits.Default with { DecodedPixels = 1_000_000 };
        var png = DataUri(OneBitPng(700, 700));
        var markup = Svg($"<defs><image id=\"i\" width=\"20\" height=\"100\" href=\"{png}\"/></defs>" +
            string.Concat(Enumerable.Range(0, 5).Select(i => $"<use href=\"#i\" x=\"{i * 20}\"/>")));

        using var picture = SvgPictureBuilder.Build(markup, CancellationToken.None, limits);

        // Five uses, 490 000 pixels decoded once (before: five decodes, each copied into the picture by DrawBitmap).
        Assert.Equal(700L * 700, picture.Statistics.DecodedPixels);
        Assert.Equal(0, picture.Statistics.ImagesSkipped);
        Assert.Equal(255, PixelAt(picture, 90, 50).Alpha);
    }

    [AvaloniaFact]
    public void Images_Past_The_Documents_Pixel_Budget_Are_Not_Decoded()
    {
        var limits = SvgPictureBuilder.Limits.Default with { DecodedPixels = 1_000_000 };
        var markup = Svg($"<image width=\"30\" height=\"100\" href=\"{DataUri(OneBitPng(700, 700))}\"/>" +
            $"<image x=\"30\" width=\"30\" height=\"100\" href=\"{DataUri(OneBitPng(700, 699))}\"/>" +
            $"<image x=\"60\" width=\"40\" height=\"100\" href=\"{DataUri(OneBitPng(700, 698))}\"/>");

        using var picture = SvgPictureBuilder.Build(markup, CancellationToken.None, limits);

        // The third image would take the document past its budget: it is skipped, the first two are drawn.
        Assert.Equal((700L * 700) + (700L * 699), picture.Statistics.DecodedPixels);
        Assert.Equal(1, picture.Statistics.ImagesSkipped);
        Assert.Equal(255, PixelAt(picture, 45, 50).Alpha);
        Assert.Equal(0, PixelAt(picture, 80, 50).Alpha);
        Assert.Equal(64_000_000, SvgPictureBuilder.MaxDecodedPixels);
    }

    [AvaloniaFact]
    public void An_Image_Edge_Over_4096_Pixels_Is_Not_Decoded()
    {
        Assert.Equal(4096, SvgPictureBuilder.MaxImageEdge);

        using var tooWide = SvgPictureBuilder.Build(Svg($"<image width=\"100\" height=\"100\" href=\"{DataUri(OneBitPng(4097, 2))}\"/>"));
        Assert.Equal(0, PixelAt(tooWide, 50, 50).Alpha);
        Assert.Equal(0, tooWide.Statistics.DecodedPixels);

        using var atTheEdge = SvgPictureBuilder.Build(Svg($"<image width=\"100\" height=\"100\" preserveAspectRatio=\"none\" href=\"{DataUri(OneBitPng(4096, 2))}\"/>"));
        Assert.Equal(255, PixelAt(atTheEdge, 50, 50).Alpha);
    }

    // ------------------------------------------------------------------
    // F5/F11: nesting and node count refused before the tree is built
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void Nesting_Deeper_Than_256_Levels_Is_Refused_Before_The_Load_That_It_Makes_Quadratic()
    {
        // 300 nested groups around a red rect (the review's `nest-300-g-past-cap`): refused, not silently blank.
        var markup = Svg(string.Concat(Enumerable.Repeat("<g>", 300)) + "<rect width=\"100\" height=\"100\" fill=\"#f00\"/>" + string.Concat(Enumerable.Repeat("</g>", 300)));
        var ex = Assert.Throws<InvalidOperationException>(() => SvgPictureBuilder.Build(markup));
        Assert.Contains("deeper than 256 levels", ex.Message, StringComparison.Ordinal);

        var viewer = Assert.Throws<DocumentRenderException>(() => DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, Encoding.UTF8.GetBytes(markup)));
        Assert.Contains("deeper than 256 levels", viewer.Message, StringComparison.Ordinal);

        // 256 levels are read.
        using var atTheCap = SvgPictureBuilder.Build(Svg(string.Concat(Enumerable.Repeat("<g>", 255)) + "<rect width=\"100\" height=\"100\" fill=\"#f00\"/>" + string.Concat(Enumerable.Repeat("</g>", 255))));
        Assert.Equal(new SKColor(255, 0, 0), PixelAt(atTheCap, 50, 50));
    }

    [AvaloniaFact]
    public void Fifty_Thousand_Unclosed_Elements_Are_Refused_In_Linear_Time()
    {
        // The review's `script-50k-openers` through the page source: before, XDocument.Load spent 13 s nesting them before the nesting cap was looked at.
        var markup = Svg(string.Concat(Enumerable.Repeat("<script>", 50_000)), "width=\"10\" height=\"10\"");
        var watch = Stopwatch.StartNew();
        var ex = Assert.Throws<DocumentRenderException>(() => DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, Encoding.UTF8.GetBytes(markup)));
        watch.Stop();
        Assert.Contains("deeper than 256 levels", ex.Message, StringComparison.Ordinal);
        Assert.True(watch.Elapsed < Budget, $"refusing took {watch.Elapsed.TotalSeconds:F2} s.");
    }

    [AvaloniaFact]
    public void A_Document_Longer_Than_The_Character_Cap_Is_Refused_By_The_Reader()
    {
        // 16M characters: the DOM takes about twenty times the file, so the 64M cap of before (above the 25 MB attachment cap) never bound.
        Assert.Equal(16L * 1024 * 1024, SvgPictureBuilder.MaxDocumentCharacters);
        var limits = SvgPictureBuilder.Limits.Default with { DocumentCharacters = 2_000 };
        var markup = Svg(string.Concat(Enumerable.Repeat("<rect width=\"5\" height=\"5\"/>", 100)));
        Assert.True(markup.Length > 2_000);

        Assert.Throws<XmlException>(() => SvgPictureBuilder.Build(markup, CancellationToken.None, limits));
        using var picture = SvgPictureBuilder.Build(markup, CancellationToken.None, limits with { DocumentCharacters = 10_000 });
        Assert.Equal(new SKColor(0, 0, 0), PixelAt(picture, 2, 2));
    }

    [AvaloniaFact]
    public void More_Elements_And_Attributes_Than_The_Cap_Are_Refused_Before_The_Load()
    {
        var limits = SvgPictureBuilder.Limits.Default with { Nodes = 1_000 };
        var markup = Svg("<rect width=\"5\" height=\"5\"" + string.Concat(Enumerable.Range(0, 1_200).Select(i => $" a{i}=\"\"")) + "/>");

        var ex = Assert.Throws<InvalidOperationException>(() => SvgPictureBuilder.Build(markup, CancellationToken.None, limits));
        Assert.Contains("more than 1,000 elements and attributes", ex.Message, StringComparison.Ordinal);
        Assert.Equal(2_000_000, SvgPictureBuilder.MaxNodes);

        // Under the cap, the same shape is read.
        using var picture = SvgPictureBuilder.Build(markup, CancellationToken.None, limits with { Nodes = 2_000 });
        Assert.Equal(new SKColor(0, 0, 0), PixelAt(picture, 2, 2));
    }

    // ------------------------------------------------------------------
    // F6: text — one typeface lookup per family and style, at most 8 names, a size cap
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Typeface_Is_Looked_Up_Once_Per_Family_List_And_At_Most_8_Names_Are_Tried()
    {
        // 200 runs naming 20 missing families: before, 4 000 font-configuration queries (20 000 runs of 10 names ran past 120 s).
        var families = string.Join(',', Enumerable.Range(1, 20).Select(i => $"NoSuchFace{i}"));
        var markup = Svg(string.Concat(Enumerable.Range(0, 200).Select(i => $"<text font-family=\"{families}\" x=\"0\" y=\"50\">W</text>")));

        using var picture = BuildWithin(markup, "200 texts naming 20 missing families");

        Assert.Equal(1, picture.Statistics.TypefaceLookups);
        Assert.Equal(SvgPictureBuilder.MaxFontFamilies, picture.Statistics.FamilyLookups);
        Assert.Equal(8, SvgPictureBuilder.MaxFontFamilies);

        // Bold and upright are different faces of the same list: two lookups, not two hundred.
        using var two = SvgPictureBuilder.Build(Svg(string.Concat(Enumerable.Range(0, 100).Select(i => $"<text font-family=\"serif\" font-weight=\"{(i % 2 == 0 ? "bold" : "normal")}\" y=\"50\">W</text>"))));
        Assert.Equal(2, two.Statistics.TypefaceLookups);
    }

    [AvaloniaFact]
    public void A_Font_Size_Is_Clamped_At_16384_User_Units()
    {
        Assert.Equal(16_384f, SvgPictureBuilder.MaxFontSize);

        // No stated size: the picture is the size of what it draws, and at a million units the text would be a million units tall.
        using var picture = BuildWithin($"<svg {Ns}><text font-size=\"1e6\" x=\"0\" y=\"20000\">W</text></svg>", "font-size 1e6");
        Assert.True(picture.Size.Height > 1_000, $"the text is drawn ({picture.Size})");
        Assert.True(picture.Size.Height < 25_000, $"the text is clamped ({picture.Size})");
    }

    // ------------------------------------------------------------------
    // F7: opacity layers — at most 8 deep and 1 000 in all, folded into the paint beyond that
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void Opacity_Layers_Are_Capped_In_Depth_And_Number_And_Folded_Into_The_Paint_Beyond_Them()
    {
        Assert.Equal(8, SvgPictureBuilder.MaxOpacityLayerDepth);
        Assert.Equal(1_000, SvgPictureBuilder.MaxOpacityLayers);
        var limits = SvgPictureBuilder.Limits.Default with { OpacityLayerDepth = 2, OpacityLayers = 3 };

        // Four nested half-opacity groups: two layers, two folds, and the rect still reads as one sixteenth (before: a live page-sized bitmap per group at draw — 20 of them at the viewer's 8 000-pixel edge is 5 GB).
        var nested = Svg(string.Concat(Enumerable.Repeat("<g opacity=\"0.5\">", 4)) + "<rect width=\"100\" height=\"100\" fill=\"#000\"/>" + string.Concat(Enumerable.Repeat("</g>", 4)));
        using var deep = SvgPictureBuilder.Build(nested, CancellationToken.None, limits);
        Assert.Equal(2, deep.Statistics.OpacityLayers);
        Assert.Equal(2, deep.Statistics.OpacityFolds);
        Assert.InRange(PixelAt(deep, 50, 50).Alpha, 14, 18);

        // Six sibling groups: three layers, three folds, every rect half-opaque.
        var siblings = Svg(string.Concat(Enumerable.Range(0, 6).Select(i => $"<g opacity=\"0.5\"><rect x=\"{i * 10}\" width=\"10\" height=\"100\" fill=\"#000\"/></g>")));
        using var wide = SvgPictureBuilder.Build(siblings, CancellationToken.None, limits);
        Assert.Equal(3, wide.Statistics.OpacityLayers);
        Assert.Equal(3, wide.Statistics.OpacityFolds);
        Assert.InRange(PixelAt(wide, 15, 50).Alpha, 126, 129);
        Assert.InRange(PixelAt(wide, 55, 50).Alpha, 126, 129);

        // Within the caps every group is a layer, as before.
        using var plain = SvgPictureBuilder.Build(nested);
        Assert.Equal(4, plain.Statistics.OpacityLayers);
        Assert.Equal(0, plain.Statistics.OpacityFolds);
    }

    // ------------------------------------------------------------------
    // F8: path data — a size cap per shape, a verb budget per document, no allocation per number
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Shape_Whose_Data_Is_Over_The_Cap_Is_Left_Out()
    {
        Assert.Equal(2_000_000, SvgPictureBuilder.MaxShapeDataCharacters);
        var limits = SvgPictureBuilder.Limits.Default with { ShapeDataCharacters = 200 };
        var longPath = "M0 0 " + string.Concat(Enumerable.Repeat("l1 0 ", 50)) + "L100 0 L100 100 L0 100 Z";
        var longPoints = string.Join(' ', Enumerable.Range(0, 60).Select(i => $"{i},0")) + " 100,100 0,100";
        Assert.True(longPath.Length > 200 && longPoints.Length > 200);

        using var path = SvgPictureBuilder.Build(Svg($"<path d=\"{longPath}\"/>"), CancellationToken.None, limits);
        Assert.Equal(1, path.Statistics.ShapesSkipped);
        Assert.Equal(0, PixelAt(path, 50, 50).Alpha);

        using var polygon = SvgPictureBuilder.Build(Svg($"<polygon points=\"{longPoints}\"/>"), CancellationToken.None, limits);
        Assert.Equal(1, polygon.Statistics.ShapesSkipped);
        Assert.Equal(0, PixelAt(polygon, 50, 50).Alpha);

        // Under the default cap both draw.
        using var drawn = SvgPictureBuilder.Build(Svg($"<path d=\"{longPath}\"/><polygon points=\"{longPoints}\"/>"));
        Assert.Equal(0, drawn.Statistics.ShapesSkipped);
        Assert.Equal(255, PixelAt(drawn, 50, 50).Alpha);
    }

    [AvaloniaFact]
    public void Shapes_Past_The_Documents_Verb_Budget_Are_Left_Out()
    {
        Assert.Equal(4_000_000, SvgPictureBuilder.MaxPathVerbs);
        var limits = SvgPictureBuilder.Limits.Default with { PathVerbs = 10 };

        // Three squares of five verbs each: the third would take the document past ten and is left out.
        var markup = Svg("<path d=\"M0 0 h30 v100 h-30 z\"/><path d=\"M35 0 h30 v100 h-30 z\"/><path d=\"M70 0 h30 v100 h-30 z\"/>");
        using var picture = SvgPictureBuilder.Build(markup, CancellationToken.None, limits);

        Assert.Equal(10, picture.Statistics.PathVerbs);
        Assert.Equal(1, picture.Statistics.ShapesSkipped);
        Assert.Equal(255, PixelAt(picture, 15, 50).Alpha);
        Assert.Equal(255, PixelAt(picture, 50, 50).Alpha);
        Assert.Equal(0, PixelAt(picture, 85, 50).Alpha);

        // A polygon's points count as one verb per point.
        using var polygon = SvgPictureBuilder.Build(Svg("<polygon points=\"0,0 100,0 100,100 0,100\"/>"), CancellationToken.None, limits);
        Assert.Equal(4, polygon.Statistics.PathVerbs);
    }

    [AvaloniaFact]
    public void The_Number_Scanner_Reads_Every_Form_The_Pattern_Did()
    {
        // The regular expression it replaces: [-+]?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)? — a bare "e" is not an exponent, "+-4" is -4, "1.5.5" is 1.5 and .5.
        Assert.Equal([1.5f, -2000f, 0.5f, 0.5f, -0.05f, 1f, 3f, -4f, 1.5f, 0.5f], SvgPictureBuilder.Numbers("1.5-2e3.5,.5 -.5e-1 1e 3 +-4 1.5.5"));
        Assert.Empty(SvgPictureBuilder.Numbers(" . - e +"));
        Assert.Empty(SvgPictureBuilder.Numbers(null));

        // A value past the range of a float is not a number, as before.
        Assert.Equal([1f], SvgPictureBuilder.Numbers("1e40 1"));
    }

    // ------------------------------------------------------------------
    // F9: style rules — indexed by id, class and tag, at most 10 000
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void Style_Rules_Are_Found_By_Index_Rather_Than_By_Scanning_Every_Rule_For_Every_Element()
    {
        // 8 000 class rules and one tag rule on 8 000 rects: before, 64 million predicate evaluations and 8 000 sorts (20 000 × 20 000 never finished).
        var markup = Svg("<style>" + string.Concat(Enumerable.Range(0, 8_000).Select(i => $".c{i}{{fill:#123456}}")) + "rect{fill:#ff0000}</style>" +
            string.Concat(Enumerable.Repeat("<rect width=\"1\" height=\"1\"/>", 8_000)));

        using var picture = BuildWithin(markup, "8 000 rules on 8 000 elements");

        Assert.Equal(8_001, picture.Statistics.StyleRules);
        Assert.Equal(0, picture.Statistics.StyleRulesIgnored);
        Assert.Equal(new SKColor(255, 0, 0), PixelAt(picture, 0, 0));
    }

    [AvaloniaFact]
    public void Indexed_Rules_Keep_Their_Specificity_And_Order()
    {
        // tag < class < tag.class < id; among equals the later wins; a rule for another class or id does not apply; * applies to everything.
        var markup = Svg("<style>*{fill:#070707} rect{fill:#010101} .a{fill:#020202} rect.a{fill:#030303} #i{fill:#040404} .b{fill:#050505} #j{fill:#060606} .z{fill:#080808} .z{fill:#090909}</style>" +
            "<rect id=\"i\" class=\"a\" width=\"10\" height=\"10\"/><rect class=\"a\" x=\"10\" width=\"10\" height=\"10\"/><rect x=\"20\" width=\"10\" height=\"10\"/>" +
            "<rect class=\"x a\" x=\"30\" width=\"10\" height=\"10\"/><circle cx=\"45\" cy=\"5\" r=\"5\"/><rect class=\"z\" x=\"50\" width=\"10\" height=\"10\"/>");

        using var picture = SvgPictureBuilder.Build(markup);

        Assert.Equal(new SKColor(4, 4, 4), PixelAt(picture, 5, 5));
        Assert.Equal(new SKColor(3, 3, 3), PixelAt(picture, 15, 5));
        Assert.Equal(new SKColor(1, 1, 1), PixelAt(picture, 25, 5));
        Assert.Equal(new SKColor(3, 3, 3), PixelAt(picture, 35, 5));
        Assert.Equal(new SKColor(7, 7, 7), PixelAt(picture, 45, 5));
        Assert.Equal(new SKColor(9, 9, 9), PixelAt(picture, 55, 5));
    }

    [AvaloniaFact]
    public void Style_Rules_Past_The_Cap_Are_Ignored()
    {
        Assert.Equal(10_000, SvgPictureBuilder.MaxStyleRules);
        var limits = SvgPictureBuilder.Limits.Default with { StyleRules = 2 };
        var markup = Svg("<style>rect{fill:#010101} rect{fill:#020202} rect{fill:#030303}</style><rect width=\"10\" height=\"10\"/>");

        using var picture = SvgPictureBuilder.Build(markup, CancellationToken.None, limits);

        Assert.Equal(2, picture.Statistics.StyleRules);
        Assert.Equal(1, picture.Statistics.StyleRulesIgnored);
        Assert.Equal(new SKColor(2, 2, 2), PixelAt(picture, 5, 5));
    }

    // ------------------------------------------------------------------
    // I1: nothing non-finite reaches the bounds
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void A_Length_Past_The_Range_Of_A_Float_Is_No_Length_And_A_Sizeless_Document_Stays_Finite()
    {
        // -1e38 inches is -9.6e39 pixels, past a float: the attribute falls back to 0 rather than starting the line at minus infinity,
        // and the sizeless document is the size of what it draws (a 5 by 5 line with a 1-unit stroke), not infinite or empty.
        using var picture = SvgPictureBuilder.Build($"<svg {Ns}><line x1=\"-1e38in\" y1=\"0\" x2=\"5\" y2=\"5\" stroke=\"#000\"/></svg>");
        Assert.True(float.IsFinite(picture.Size.Width) && float.IsFinite(picture.Size.Height), $"the size is {picture.Size}");
        Assert.InRange(picture.Size.Width, 5, 8);
        Assert.InRange(picture.Size.Height, 5, 8);

        // The same through an em chain, and a percentage of nothing.
        using var chain = SvgPictureBuilder.Build($"<svg {Ns}><g font-size=\"1e30em\"><rect x=\"1e38em\" width=\"10\" height=\"10\"/></g></svg>");
        Assert.Equal(new SKSize(10, 10), chain.Size);
    }

    // ------------------------------------------------------------------
    // F10: a deadline, honoured by the reader and applied by the viewer
    // ------------------------------------------------------------------

    [AvaloniaFact]
    public void The_Reader_Stops_At_A_Cancelled_Token_Before_Drawing_An_Element()
    {
        var markup = Svg(string.Concat(Enumerable.Repeat("<rect width=\"1\" height=\"1\"/>", 1_000)));
        Assert.Throws<OperationCanceledException>(() => SvgPictureBuilder.Build(markup, new CancellationToken(canceled: true)));

        // The page source lets the caller's own cancellation through as what it is, not as "could not be read".
        Assert.ThrowsAny<OperationCanceledException>(() => DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, Encoding.UTF8.GetBytes(markup), new CancellationToken(canceled: true)));

        // And without one, the same document opens.
        using var picture = SvgPictureBuilder.Build(markup, CancellationToken.None);
        Assert.Equal(new SkiaSharp.SKSize(100, 100), picture.Size);
    }

    [AvaloniaFact]
    public async Task A_Document_That_Takes_Too_Long_To_Read_Is_Reported_Rather_Than_Freezing_The_Window()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var host = new WorkspaceHost(root);

        try
        {
            await host.StartAsync();
            var (documentId, attachmentId) = await DocumentViewerAcceptanceTests.CreateDocumentWithAttachmentAsync(
                host, "DWG-SLOW", "slow.svg", "image/svg+xml", DocumentPageSourceTests.RedRectangleSvg());
            var (owner, attachment) = await DocumentViewerAcceptanceTests.ResolveAsync(host, documentId, attachmentId);

            var window = new MainWindow(host);
            Assert.Equal(TimeSpan.FromSeconds(15), AttachmentViewerLauncher.DefaultReadDeadline);

            // An immediate deadline stands in for a document that keeps the reader busy for fifteen seconds.
            window.AttachmentViewers.ReadDeadline = TimeSpan.Zero;
            var viewer = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);

            var session = viewer.Session!;
            Assert.Equal(DocumentViewStatus.Corrupt, session.Status);
            Assert.Equal(ViewableDocumentFormat.Svg, session.Format);
            Assert.Contains("took too long to read", session.UnavailableDetail, StringComparison.Ordinal);
            Assert.True(viewer.IsShowingUnavailableState);
            Assert.Equal("This attachment could not be read", viewer.UnavailableHeadline);

            // With the real deadline the same attachment opens in a second tab-free re-open.
            window.AttachmentViewers.Close(attachment.Id);
            window.AttachmentViewers.ReadDeadline = AttachmentViewerLauncher.DefaultReadDeadline;
            var reopened = await window.AttachmentViewers.OpenAsync(owner, attachment, 800, 600);
            Assert.Equal(DocumentViewStatus.Ready, reopened.Session!.Status);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
