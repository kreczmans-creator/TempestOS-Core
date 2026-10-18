using System.Runtime.Versioning;
using Avalonia.Headless.XUnit;
using PDFtoImage;
using SkiaSharp;
using Tempest.Desktop.Documents;
using Tempest.Desktop.Documents.Timesheets;

namespace Tempest.Desktop.Tests.Documents;

/// <summary>
/// Every document's header lockup is the design system's light-ground (ink)
/// variant — dark logotype on the paper page — never the white-on-navy box
/// the Product Owner rejected on generated documents.
/// </summary>
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
public sealed class DocumentLogoOnPaperTests
{
    [Fact]
    public void HorizontalInk_HasATransparentGround_NotANavyBox()
    {
        var logo = DocumentLogo.HorizontalInk;
        Assert.NotNull(logo);

        // The four corners are empty ground — transparent, so the paper page shows through.
        Assert.Equal(0, logo!.GetPixel(0, 0).Alpha);
        Assert.Equal(0, logo.GetPixel(logo.Width - 1, 0).Alpha);
        Assert.Equal(0, logo.GetPixel(0, logo.Height - 1).Alpha);
        Assert.Equal(0, logo.GetPixel(logo.Width - 1, logo.Height - 1).Alpha);
    }

    [AvaloniaFact]
    public void TimesheetHeader_LogoRegion_IsMostlyPaper_WithDarkLogotype()
    {
        var bytes = new TimesheetDocumentRenderer().Render(TimesheetDocumentModelFixtures.Minimal()).ToArray();
        using var page = Conversion.ToImage(bytes, new Index(0), options: new RenderOptions(Dpi: 72));

        var left = (int)DocumentTemplate.ContentLeft;
        var top = (int)DocumentTemplate.Margin;
        int light = 0, dark = 0, total = 0;
        for (var y = top; y < top + 18; y++)
        {
            for (var x = left; x < left + 70; x++)
            {
                var c = page.GetPixel(x, y);
                var luminance = (0.2126 * c.Red) + (0.7152 * c.Green) + (0.0722 * c.Blue);
                if (luminance > 200)
                    light++;
                else if (luminance < 80)
                    dark++;
                total++;
            }
        }

        Assert.True(light > total / 2, $"The header lockup region must be mostly paper (light); {light}/{total} light pixels.");
        Assert.True(dark > 0, "The header lockup must draw its dark logotype.");
    }
}
