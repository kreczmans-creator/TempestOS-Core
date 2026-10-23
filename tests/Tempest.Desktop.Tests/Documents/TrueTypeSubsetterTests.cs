using SkiaSharp;
using Tempest.Desktop.Documents;

namespace Tempest.Desktop.Tests.Documents;

public sealed class TrueTypeSubsetterTests
{
    [Theory]
    [InlineData(DocumentFontRole.Display, false)]
    [InlineData(DocumentFontRole.Display, true)]
    [InlineData(DocumentFontRole.Mono, false)]
    [InlineData(DocumentFontRole.Mono, true)]
    public void Subset_IsMuchSmaller_AndStillShapesTheKeptGlyphs(DocumentFontRole role, bool bold)
    {
        var raw = DocumentFonts.RawBytes(role, bold);
        Assert.NotNull(raw);

        var subset = TrueTypeSubsetter.Subset(raw!, "Calc 0123456789 MPa σ".EnumerateRunes().Select(r => r.Value));
        Assert.True(subset.Length < raw!.Length * 0.6, $"subset is {subset.Length} of {raw.Length} bytes");

        using var typeface = SKTypeface.FromData(SKData.CreateCopy(subset));
        Assert.NotNull(typeface);
        using var original = SKTypeface.FromData(SKData.CreateCopy(raw));
        Assert.Equal(original!.GlyphCount, typeface!.GlyphCount);

        foreach (var c in "Calc 0123456789 MPa")
        {
            Assert.Equal(original.GetGlyph(c), typeface.GetGlyph(c));
            Assert.NotEqual(0, typeface.GetGlyph(c));
        }

        using var paint = new SKPaint { Typeface = typeface, TextSize = 12 };
        using var originalPaint = new SKPaint { Typeface = original, TextSize = 12 };
        Assert.Equal(originalPaint.MeasureText("Calc 0123456789"), paint.MeasureText("Calc 0123456789"), 3);
    }

    [Fact]
    public void Subset_RefusesGarbage()
    {
        Assert.Throws<InvalidDataException>(() => TrueTypeSubsetter.Subset(new byte[8], [65]));
        Assert.Throws<InvalidDataException>(() => TrueTypeSubsetter.Subset(new byte[] { 0, 1, 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 1, 2, 3 }, [65]));
    }

    [Fact]
    public void SubsetsFor_CoverOnlyBrandFaces()
    {
        var subsets = DocumentFonts.SubsetsFor(
        [
            new DocumentTemplate.TextRun(0, 0, "Heading", 11, true, SKTextAlign.Left, SKColors.Black, DocumentFontRole.Display),
            new DocumentTemplate.TextRun(0, 0, "12.5 kN", 9, false, SKTextAlign.Left, SKColors.Black, DocumentFontRole.Mono),
            new DocumentTemplate.TextRun(0, 0, "body", 9, false, SKTextAlign.Left, SKColors.Black, DocumentFontRole.Body),
        ]);

        try
        {
            // Display and Mono always; Body too when the platform default face exposes its bytes (v0.26.0 Phase 6: Windows's default face is several hundred KB).
            Assert.InRange(subsets.Count, 2, 3);
            Assert.True(subsets.ContainsKey((DocumentFontRole.Display, true)));
            Assert.True(subsets.ContainsKey((DocumentFontRole.Mono, false)));
        }
        finally
        {
            foreach (var t in subsets.Values)
                t.Dispose();
        }
    }
}
