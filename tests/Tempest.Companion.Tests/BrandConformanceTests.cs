using Tempest.Companion.Branding;
using Tempest.Companion.Theming;

namespace Tempest.Companion.Tests;

// Guards the Companion's visual identity against drift from the Tempest
// Engineering Design System (WP 14.1A): the brand triad and machine-state
// hues must remain the pack's own token values, and the mark must remain
// the supplied artwork - eighteen 24-unit strokes in three six-stroke
// layers around the paper hexagonal core, coordinates carried verbatim.
public class BrandConformanceTests
{
    [Theory]
    [InlineData("#0b0e1e", nameof(BrandPalette.Navy800))]
    [InlineData("#111527", nameof(BrandPalette.Navy700))]
    [InlineData("#070915", nameof(BrandPalette.Navy900))]
    [InlineData("#1c2d97", nameof(BrandPalette.Indigo600))]
    [InlineData("#40a2ce", nameof(BrandPalette.Cyan500))]
    [InlineData("#68bde2", nameof(BrandPalette.Cyan400))]
    [InlineData("#2b7fa5", nameof(BrandPalette.Cyan600))]
    [InlineData("#6c29d9", nameof(BrandPalette.Violet500))]
    [InlineData("#12b981", nameof(BrandPalette.Green500))]
    [InlineData("#f5a524", nameof(BrandPalette.Amber500))]
    [InlineData("#e5484d", nameof(BrandPalette.Red500))]
    [InlineData("#f5f6fa", nameof(BrandPalette.Paper050))]
    [InlineData("#16181d", nameof(BrandPalette.Ink900))]
    public void BrandColour_MatchesTheDesignSystemToken(string expectedHex, string fieldName)
    {
        var colour = (Avalonia.Media.Color)typeof(BrandPalette).GetField(fieldName)!.GetValue(null)!;

        Assert.Equal(Avalonia.Media.Color.Parse(expectedHex), colour);
    }

    [Fact]
    public void Mark_IsTheSuppliedArtwork_EighteenStrokesInThreeLayers()
    {
        Assert.Equal(18, TempestMarkGeometry.Strokes.Count);
        Assert.Equal(6, TempestMarkGeometry.Strokes.Count(s => s.Layer == MarkLayer.Indigo));
        Assert.Equal(6, TempestMarkGeometry.Strokes.Count(s => s.Layer == MarkLayer.Cyan));
        Assert.Equal(6, TempestMarkGeometry.Strokes.Count(s => s.Layer == MarkLayer.Violet));
        Assert.Equal(24, TempestMarkGeometry.MarkStrokeWidth);
        Assert.Equal(6, TempestMarkGeometry.CorePoints.Length);
    }

    [Fact]
    public void Mark_FirstStroke_CarriesTheArtworksExactCoordinates()
    {
        // Spot-checks transcription fidelity against the SVG source
        // (logo-os-horizontal-transparent-*.svg, first indigo stroke).
        var first = TempestMarkGeometry.Strokes[0];

        Assert.Equal(new Avalonia.Point(558.5, 569.5), first.Start);
        Assert.Equal(new Avalonia.Point(885.5, 569.5), first.End);
        Assert.Equal(MarkLayer.Indigo, first.Layer);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void WordmarkPaths_ParseAsGeometry()
    {
        Assert.NotNull(Avalonia.Media.Geometry.Parse(TempestMarkGeometry.TempestPathData));
        Assert.NotNull(Avalonia.Media.Geometry.Parse(TempestMarkGeometry.OsPathData));
    }

    [Fact]
    public void SquaredCornerSystem_MatchesThePack()
    {
        Assert.Equal(2, CompanionTokens.BadgeCornerRadius);
        Assert.Equal(3, CompanionTokens.ControlCornerRadius);
        Assert.Equal(5, CompanionTokens.CornerRadius);
    }
}
