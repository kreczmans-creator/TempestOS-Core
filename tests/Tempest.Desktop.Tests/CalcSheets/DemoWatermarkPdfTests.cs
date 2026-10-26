using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>Addendum C, C-6: the PDF of a sheet whose header carries a watermark says DEMO in its header band, its notice line, every page's footer and its metadata.</summary>
public sealed class DemoWatermarkPdfTests
{
    private const string Notice = "DEMO — invented data for demonstration only; not for design, purchase or construction";

    private static CalcSheet Sheet(string? watermark, string calcId = "DEMO — Beam")
    {
        var beam = new Tempest.Calc.Structural.BeamFormulasCalculator();
        var inputs = CalcTemplateHasher.DefaultInputs(beam.Template).With(CalcInputValue.Quantity("L", 6, "m", CalcProvenance.Drawing("DRG-1")));
        var at = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);
        var header = new CalcSheetHeader("P-1", calcId, beam.Template.Title, "A", CalcSheetStatus.Draft, "s.kreczman", null, at, null, null, Watermark: watermark);
        var footer = new CalcSheetFooter(beam.Template.Reference, "0123456789abcdef", "calc-engine/1.0.0", 1, 1, at);
        return CalcSheetBuilder.Build(beam.Template, inputs, CalcRunner.Run(beam, inputs), header, footer);
    }

    [Fact]
    public void A_Demo_Sheet_Says_DEMO_In_The_Band_The_Notice_And_The_Footer_Of_Every_Page()
    {
        var text = PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(Sheet(Notice)).ToArray());

        Assert.Contains("CALCULATION SHEET — DRAFT — DEMO", text, StringComparison.Ordinal);
        Assert.Contains($"*** {Notice} ***", text, StringComparison.Ordinal);

        // One "DEMO ·" footer per page: the sheet runs to more than one page, and Page n of m is printed beside each.
        var footers = text.Split("DEMO · ", StringSplitOptions.None).Length - 1;
        var pages = text.Split("Page ", StringSplitOptions.None).Skip(1).Count(p => p.Contains(" of ", StringComparison.Ordinal));
        Assert.True(pages >= 1);
        Assert.Equal(pages, footers);
    }

    [Fact]
    public void A_Sheet_Without_A_Watermark_Has_No_Demo_Mark_Anywhere()
    {
        var text = PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(Sheet(null, "CALC-1")).ToArray());

        Assert.DoesNotContain("DEMO", text, StringComparison.Ordinal);
        Assert.Contains("CALCULATION SHEET — DRAFT", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Pack_Of_Demo_Sheets_Carries_The_Mark_On_Every_Member_And_Page()
    {
        var cover = Sheet(Notice);
        var bytes = new CalcSheetPdfRenderer().RenderPack(cover, [Sheet(Notice), Sheet(Notice)]).ToArray();
        var text = PdfTextExtractor.ExtractText(bytes);

        Assert.Equal(3, text.Split("CALCULATION SHEET — DRAFT — DEMO", StringSplitOptions.None).Length - 1);
        var pages = text.Split("Page ", StringSplitOptions.None).Skip(1).Count(p => p.Contains(" of ", StringComparison.Ordinal));
        Assert.Equal(pages, text.Split("DEMO · ", StringSplitOptions.None).Length - 1);
    }
}
