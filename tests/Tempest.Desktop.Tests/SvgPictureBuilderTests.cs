using System.Text;
using System.Xml;
using Avalonia.Headless.XUnit;
using SkiaSharp;
using Tempest.Desktop.Viewing;
using Tempest.Workspace.Viewing;

namespace Tempest.Desktop.Tests;

/// <summary>
/// DB-08: the app's own SVG reader. What the comparison fixtures cannot show: the caps, the refusals, what a reference it must
/// never follow does, the parsers behind the attributes, and the size of a document that states none.
/// </summary>
public sealed class SvgPictureBuilderTests
{
    private const string Ns = "xmlns=\"http://www.w3.org/2000/svg\"";

    /// <summary>The colour at (<paramref name="x"/>, <paramref name="y"/>) of <paramref name="markup"/> rendered at scale 1 through the viewer's page source, as BGRA.</summary>
    private static (byte B, byte G, byte R, byte A) PixelAt(string markup, int x, int y)
    {
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, Encoding.UTF8.GetBytes(markup))!;
        using var page = source.RenderPage(0, 1.0);
        var pixels = SvgRenderComparisonTests.Pixels(page);
        var i = ((y * page.PixelSize.Width) + x) * 4;
        return (pixels[i], pixels[i + 1], pixels[i + 2], pixels[i + 3]);
    }

    [AvaloniaFact]
    public void A_Document_Whose_Root_Is_Not_Svg_Is_Refused_As_Unreadable()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SvgPictureBuilder.Build("<html><body>hello</body></html>"));
        Assert.Contains("no readable SVG content", ex.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Broken_Markup_Throws_The_Xml_Readers_Own_Error() =>
        Assert.Throws<XmlException>(() => SvgPictureBuilder.Build("<svg><rect this is not xml"));

    [AvaloniaFact]
    public void A_Doctype_Is_Refused_By_The_Reader_Itself_Should_One_Get_Past_The_Sanitiser()
    {
        // The sanitiser strips every DOCTYPE first; the reader is the second barrier and has no resolver to follow an entity with.
        const string xxe = "<?xml version=\"1.0\"?><!DOCTYPE svg [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><svg " + Ns + " width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\"/></svg>";
        Assert.Throws<XmlException>(() => SvgPictureBuilder.Build(xxe));
    }

    [AvaloniaFact]
    public void An_Image_That_Is_Not_Embedded_Is_Never_Followed_And_Draws_Nothing()
    {
        // Straight into the builder, past the sanitiser: an http, a file and a relative reference draw nothing, and the rect under them shows.
        var markup = "<svg " + Ns + " xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"40\" height=\"40\"><rect width=\"40\" height=\"40\" fill=\"#00FF00\"/>" +
            "<image width=\"40\" height=\"40\" href=\"http://127.0.0.1:9/x.png\"/><image width=\"40\" height=\"40\" xlink:href=\"file:///etc/hostname\"/>" +
            "<image width=\"40\" height=\"40\" href=\"probe.png\"/><image width=\"40\" height=\"40\" href=\"data:,\"/><image width=\"40\" height=\"40\" href=\"data:image/png;base64,!!!\"/></svg>";

        using var picture = SvgPictureBuilder.Build(markup);
        Assert.Equal(new SKSize(40, 40), picture.Size);
        Assert.Equal((0, 255, 0, 255), PixelAt(markup, 20, 20));
    }

    [AvaloniaFact]
    public void Elements_Past_The_Cap_Are_Not_Drawn()
    {
        var sb = new StringBuilder("<svg " + Ns + " width=\"20\" height=\"20\">");

        for (var i = 0; i < SvgPictureBuilder.MaxElements; i++)
            sb.Append("<g/>");

        sb.Append("<rect width=\"20\" height=\"20\" fill=\"#FF0000\"/></svg>");

        // The rect is element MaxElements + 1: the picture stays blank rather than growing without limit.
        Assert.Equal(0, PixelAt(sb.ToString(), 10, 10).A);
    }

    [AvaloniaFact]
    public void A_Use_That_Refers_To_Itself_Or_Its_Ancestor_Ends_Instead_Of_Recursing()
    {
        var markup = "<svg " + Ns + " width=\"20\" height=\"20\"><g id=\"loop\"><rect width=\"20\" height=\"20\" fill=\"#0000FF\"/><use href=\"#loop\"/></g>" +
            "<use id=\"self\" href=\"#self\"/><use href=\"#a\"/><g id=\"a\"><use href=\"#b\"/></g><g id=\"b\"><use href=\"#a\"/></g></svg>";

        Assert.Equal((255, 0, 0, 255), PixelAt(markup, 10, 10));
    }

    [AvaloniaFact]
    public void A_Document_With_No_Stated_Size_Is_The_Size_Of_What_It_Draws()
    {
        using var picture = SvgPictureBuilder.Build("<svg " + Ns + "><rect x=\"10\" y=\"20\" width=\"50\" height=\"30\" fill=\"#000\"/></svg>");
        Assert.Equal(50, picture.Size.Width, 0.5);
        Assert.Equal(30, picture.Size.Height, 0.5);

        using var empty = SvgPictureBuilder.Build("<svg " + Ns + "></svg>");
        Assert.Equal(SKSize.Empty, empty.Size);
    }

    [AvaloniaFact]
    public void A_Percentage_Size_Falls_Back_To_The_ViewBox_And_One_Stated_Edge_Keeps_Its_Aspect()
    {
        using var percent = SvgPictureBuilder.Build("<svg " + Ns + " width=\"100%\" height=\"100%\" viewBox=\"0 0 300 150\"><rect width=\"1\" height=\"1\"/></svg>");
        Assert.Equal(new SKSize(300, 150), percent.Size);

        using var oneEdge = SvgPictureBuilder.Build("<svg " + Ns + " width=\"600\" viewBox=\"0 0 300 150\"><rect width=\"1\" height=\"1\"/></svg>");
        Assert.Equal(new SKSize(600, 300), oneEdge.Size);
    }

    [AvaloniaTheory]
    [InlineData("#f00", 255, 0, 0, 255)]
    [InlineData("#FF000080", 255, 0, 0, 128)]
    [InlineData("rgb(10, 20, 30)", 10, 20, 30, 255)]
    [InlineData("rgb(100%, 0%, 50%)", 255, 0, 128, 255)]
    [InlineData("rgba(1, 2, 3, 0.5)", 1, 2, 3, 128)]
    [InlineData("SteelBlue", 70, 130, 180, 255)]
    [InlineData("transparent", 0, 0, 0, 0)]
    public void Colours_Are_Read_In_Every_Css_Form(string text, int r, int g, int b, int a)
    {
        var colour = SvgPictureBuilder.ParseColour(text, SKColors.Black);
        Assert.Equal(new SKColor((byte)r, (byte)g, (byte)b, (byte)a), colour);
    }

    [AvaloniaTheory]
    [InlineData("#12")]
    [InlineData("#gggggg")]
    [InlineData("nosuchcolour")]
    [InlineData("rgb(1,2)")]
    public void A_Value_That_Is_Not_A_Colour_Reads_As_None(string text) => Assert.Null(SvgPictureBuilder.ParseColour(text, SKColors.Black));

    [AvaloniaFact]
    public void CurrentColor_Is_The_Inherited_Color_Property() => Assert.Equal(SKColors.Teal, SvgPictureBuilder.ParseColour("currentColor", SKColors.Teal));

    [AvaloniaFact]
    public void A_Transform_List_Applies_Right_To_Left_As_The_Specification_Says()
    {
        // translate(10 20) scale(2): the point is scaled first, then moved.
        var matrix = SvgPictureBuilder.ParseTransform("translate(10 20) scale(2)")!.Value;
        var mapped = matrix.MapPoint(new SKPoint(1, 1));
        Assert.Equal(12, mapped.X, 0.001);
        Assert.Equal(22, mapped.Y, 0.001);

        var rotated = SvgPictureBuilder.ParseTransform("rotate(90 10 10)")!.Value.MapPoint(new SKPoint(20, 10));
        Assert.Equal(10, rotated.X, 0.001);
        Assert.Equal(20, rotated.Y, 0.001);

        Assert.Null(SvgPictureBuilder.ParseTransform("  "));
        Assert.Null(SvgPictureBuilder.ParseTransform("wobble(1 2)"));
    }

    [AvaloniaFact]
    public void A_Missing_Gradient_Paints_Its_Fallback_Colour_Or_Nothing()
    {
        Assert.Equal((0, 0, 255, 255), PixelAt("<svg " + Ns + " width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"url(#missing) #ff0000\"/></svg>", 5, 5));
        Assert.Equal(0, PixelAt("<svg " + Ns + " width=\"10\" height=\"10\"><rect width=\"10\" height=\"10\" fill=\"url(#missing)\"/></svg>", 5, 5).A);
    }

    [AvaloniaFact]
    public void Unsupported_Effects_Leave_The_Shape_Drawn_Plainly_And_Their_Definitions_Draw_Nothing()
    {
        var markup = "<svg " + Ns + " width=\"10\" height=\"10\"><defs><filter id=\"f\"><feGaussianBlur stdDeviation=\"3\"/></filter>" +
            "<clipPath id=\"c\"><rect width=\"1\" height=\"1\"/></clipPath><mask id=\"m\"><rect width=\"10\" height=\"10\" fill=\"#fff\"/></mask></defs>" +
            "<marker id=\"k\"><rect width=\"10\" height=\"10\" fill=\"#0f0\"/></marker>" +
            "<rect width=\"10\" height=\"10\" fill=\"#ff0000\" filter=\"url(#f)\" clip-path=\"url(#c)\" mask=\"url(#m)\"/></svg>";

        Assert.Equal((0, 0, 255, 255), PixelAt(markup, 8, 8));

        // I2: what was left off is named, by element, in alphabetical order; an unused definition is not.
        using var picture = SvgPictureBuilder.Build(markup);
        Assert.Equal(["clipPath", "filter", "mask"], picture.NotShown);
    }

    [AvaloniaFact]
    public void Every_Feature_Left_Off_Is_Reported_And_A_Drawing_Drawn_Whole_Reports_Nothing()
    {
        var markup = "<svg " + Ns + " width=\"10\" height=\"10\"><defs><pattern id=\"p\" width=\"1\" height=\"1\"><rect width=\"1\" height=\"1\"/></pattern>" +
            "<marker id=\"m\"><rect width=\"1\" height=\"1\"/></marker><path id=\"curve\" d=\"M0 5 L10 5\"/></defs>" +
            "<rect width=\"10\" height=\"10\" fill=\"url(#p) #ff0000\" style=\"marker-end:url(#m)\"/>" +
            "<text><textPath href=\"#curve\">along</textPath></text><foreignObject width=\"1\" height=\"1\"><div/></foreignObject></svg>";

        using var picture = SvgPictureBuilder.Build(markup);
        Assert.Equal(["foreignObject", "marker", "pattern", "textPath"], picture.NotShown);

        // The pattern's fallback colour is painted, and the text on the path is not drawn in a straight line somewhere.
        Assert.Equal((0, 0, 255, 255), PixelAt(markup, 5, 5));

        using var whole = SvgPictureBuilder.Build("<svg " + Ns + " width=\"10\" height=\"10\"><defs><filter id=\"f\"/><clipPath id=\"c\"/></defs><rect width=\"10\" height=\"10\" clip-path=\"none\"/></svg>");
        Assert.Empty(whole.NotShown);
    }

    [AvaloniaFact]
    public void The_Viewer_Says_In_The_Page_Status_Which_Parts_Of_A_Drawing_Are_Not_Shown()
    {
        var markup = "<svg " + Ns + " width=\"10\" height=\"10\"><defs><clipPath id=\"c\"><rect width=\"1\" height=\"1\"/></clipPath><filter id=\"f\"/></defs>" +
            "<rect width=\"10\" height=\"10\" clip-path=\"url(#c)\" filter=\"url(#f)\"/></svg>";
        var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, Encoding.UTF8.GetBytes(markup))!;
        Assert.Equal("Some parts of this drawing are not shown: clipPath, filter.", source.RenderingNote);

        var viewer = new DocumentViewerView();
        viewer.Open(DocumentViewSession.Ready(Guid.NewGuid(), "clipped.svg", "image/svg+xml", ViewableDocumentFormat.Svg, 1, 10, 10, 400, 300, source.RenderingNote), source);
        Assert.Equal("1 page · Some parts of this drawing are not shown: clipPath, filter.", viewer.PageIndicatorText);

        // A drawing drawn whole keeps the plain page count.
        var plain = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, DocumentPageSourceTests.RedRectangleSvg())!;
        Assert.Null(plain.RenderingNote);
        viewer.Open(DocumentViewSession.Ready(Guid.NewGuid(), "plain.svg", "image/svg+xml", ViewableDocumentFormat.Svg, 1, 120, 80, 400, 300, plain.RenderingNote), plain);
        Assert.Equal("1 page", viewer.PageIndicatorText);
    }

    [AvaloniaFact]
    public void Group_Opacity_Blends_The_Whole_Group_Once()
    {
        var markup = "<svg " + Ns + " width=\"10\" height=\"10\"><g opacity=\"0.5\"><rect width=\"10\" height=\"10\" fill=\"#0000ff\"/><rect width=\"10\" height=\"10\" fill=\"#0000ff\"/></g></svg>";
        var (b, _, _, a) = PixelAt(markup, 5, 5);

        // Two opaque rects in a half-opacity group read as one half-opaque blue, not three quarters.
        Assert.InRange(a, 126, 129);
        Assert.InRange(b, 126, 129);
    }

    [AvaloniaFact]
    public void The_Viewer_Caps_A_Deep_Zoom_Of_A_Huge_Document_As_Before()
    {
        var svg = "<svg " + Ns + " width=\"20000\" height=\"20000\"><rect width=\"20000\" height=\"20000\" fill=\"#0000FF\"/></svg>";
        using var source = DocumentPageSourceFactory.Create(ViewableDocumentFormat.Svg, Encoding.UTF8.GetBytes(svg))!;
        using var page = source.RenderPage(0, 4.0);
        Assert.True(page.PixelSize.Width <= SvgDocumentPageSource.MaxRasterEdge && page.PixelSize.Height <= SvgDocumentPageSource.MaxRasterEdge);
    }
}
