using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;
using Tempest.Desktop.CalcSheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// §9: every worked example under docs/worked-examples renders to a PDF
/// through the Desktop renderer, under 200 KB, with its working as text.
/// With TEMPEST_WRITE_WORKED_EXAMPLES=1 the PDF is written beside the
/// example's inputs.json; otherwise the render is checked and the stored
/// PDF must exist.
/// </summary>
public sealed class WorkedExamplePdfTests
{
    public static TheoryData<string> Examples
    {
        get
        {
            var root = Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "worked-examples");
            var data = new TheoryData<string>();

            if (Directory.Exists(root))
            {
                foreach (var file in Directory.EnumerateFiles(root, "inputs.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
                    data.Add(Path.GetRelativePath(root, Path.GetDirectoryName(file)!).Replace('\\', '/'));
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Worked_Example_Renders_To_A_Small_Pdf(string relative)
    {
        var root = Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "worked-examples");
        var folder = Path.Combine(root, relative);
        var templateId = relative.Split('/')[1];
        var catalogue = new CalcCatalogue(CalcCalculatorComposition.All());
        var entry = catalogue.Find(templateId);
        Assert.NotNull(entry);

        var (inputs, notices) = CalcInputJson.Read(File.ReadAllText(Path.Combine(folder, "inputs.json")));
        Assert.Empty(notices);
        var result = CalcRunner.Run(entry!.Calculator, inputs);
        Assert.False(result.IsBlocked, string.Join("; ", result.Blocks.Select(b => b.Message)));

        var report = KnownAnswerRunner.Run(entry.Calculator);
        var at = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var header = new CalcSheetHeader("Worked examples", relative.Split('/')[^1], entry.Template.Title, "A", CalcSheetStatus.Draft, "TempestOS", null, at, null, null);
        var footer = new CalcSheetFooter(entry.Reference, entry.ShortHash, Tempest.Calc.Engine.CalcEngine.Version, report.Passed, report.Total, at, "TempestOS 0.26.0");
        var sheet = CalcSheetBuilder.Build(entry.Template, inputs, result, header, footer);
        var bytes = new CalcSheetPdfRenderer().Render(sheet).ToArray();

        Assert.True(bytes.Length < 200 * 1024, $"{relative}: {bytes.Length / 1024} KB");
        var text = Quotations.PdfTextExtractor.ExtractText(bytes);
        Assert.Contains("Working", text, StringComparison.Ordinal);

        var pdf = Path.Combine(folder, "sheet.pdf");

        if (Environment.GetEnvironmentVariable("TEMPEST_WRITE_WORKED_EXAMPLES") == "1")
            File.WriteAllBytes(pdf, bytes);
        else
            Assert.True(File.Exists(pdf), $"{pdf} is missing; run with TEMPEST_WRITE_WORKED_EXAMPLES=1 to write it.");
    }
}
