using System.Text.RegularExpressions;
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
/// PDF must exist. Yellow Y-B-12: the stored PDF must also be the PDF of the
/// sheet.txt beside it: its template line (<c>id@version · hash</c>) must name
/// the version sheet.txt names and the template's current content hash, and it
/// must print the governing check sheet.txt lists first. A fix that changes a
/// sheet and leaves its PDF behind fails here (it passed on existence and size).
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
                // Pack worked examples (packs/) hold every step's inputs and the issued pack sheets; they are not one sheet.
                foreach (var file in Directory.EnumerateFiles(root, "inputs.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
                {
                    var relative = Path.GetRelativePath(root, Path.GetDirectoryName(file)!).Replace('\\', '/');

                    if (!relative.StartsWith("packs/", StringComparison.Ordinal))
                        data.Add(relative);
                }
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
        // The gated calculators (S8 lifting, ADR-0173) are withheld from the shipped catalogue; their worked examples are rendered with the features
        // they are gated behind on, in this test only. A feature is on only because a composed template names it, and the two lifting templates (S8 and the lifting-lug port) are the gated ones.
        var gated = CalcCalculatorComposition.All().Where(c => c.Template.FeatureFlag is { Length: > 0 }).ToList();
        Assert.Equal(["mech.lifting-lug", "struct.lifting"], gated.Select(c => c.Template.Id).Order(StringComparer.Ordinal));
        var catalogue = new CalcCatalogue(CalcCalculatorComposition.All(), new CalcFeatureFlags(gated.Select(c => c.Template.FeatureFlag!).Distinct(StringComparer.Ordinal)));
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

        AssertStoredPdfIsThePdfOfTheSheet(relative, pdf, Path.Combine(folder, "sheet.txt"), entry.ShortHash);
    }

    /// <summary>
    /// The version and hash of the PDF's template line (<c>fp.accumulator@1.3.1 · 13736352fb1985ad · calc-engine/1.0.0</c>). The extractor
    /// joins the footer's draw calls without a separator, so the id is not matched here: it is compared as part of the whole line.
    /// </summary>
    private static readonly Regex PrintedTemplateLine = new(@"@(?<version>\d+\.\d+\.\d+) · (?<hash>[0-9a-f]{16}) · ", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>The template line of sheet.txt: <c>  Template: fp.accumulator@1.3.1</c> (its hash is the golden zero, so only id and version are compared).</summary>
    private static readonly Regex SheetTemplateLine = new(@"^\s*Template: (?<ref>\S+@\S+)\s*$", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static void AssertStoredPdfIsThePdfOfTheSheet(string relative, string pdfPath, string sheetPath, string currentHash)
    {
        Assert.True(File.Exists(sheetPath), $"{relative}: sheet.txt is missing beside the PDF.");
        var sheet = File.ReadAllText(sheetPath);
        var expected = SheetTemplateLine.Match(sheet);
        Assert.True(expected.Success, $"{relative}: sheet.txt has no 'Template: id@version' line.");
        var reference = expected.Groups["ref"].Value;

        var text = Quotations.PdfTextExtractor.ExtractText(File.ReadAllBytes(pdfPath));
        var printed = PrintedTemplateLine.Match(text);
        Assert.True(printed.Success, $"{relative}: the stored PDF prints no 'id@version · hash' template line.");

        // The template line of the PDF is the one sheet.txt names (id and version) with the template's current content hash.
        var stale = $"{relative}: sheet.pdf is stale; regenerate it with TEMPEST_WRITE_WORKED_EXAMPLES=1 (dotnet test tests/Tempest.Desktop.Tests --filter FullyQualifiedName~WorkedExamplePdf).";
        Assert.True(text.Contains($"{reference} · {currentHash} · ", StringComparison.Ordinal), $"{stale} sheet.txt says {reference} and the template's hash is {currentHash}; the PDF prints version {printed.Groups["version"].Value} with hash {printed.Groups["hash"].Value}.");

        // The governing check sheet.txt lists first ("PASS  Name: u = …") is printed in the PDF.
        var governing = Regex.Match(sheet, @"^6\. Checks[^\n]*\n\s*(?:PASS|FAIL)\s+(?<name>[^:\n]+):", RegexOptions.Multiline | RegexOptions.CultureInvariant);

        if (governing.Success)
        {
            // The extractor joins wrapped lines without a separator, so the name is compared with every white space dropped.
            // The PDF typesets symbols (D-47: T_max prints as T with a subscript, which the extractor reads as "Tmax"), so the name is compared as printed.
            var typeset = string.Concat(SymbolText.Parse(governing.Groups["name"].Value).Select(segment => segment.Text));
            var name = Regex.Replace(typeset, @"\s+", string.Empty);
            Assert.True(Regex.Replace(text, @"\s+", string.Empty).Contains(name, StringComparison.Ordinal), $"{stale} It does not print the governing check '{governing.Groups["name"].Value.Trim()}'.");
        }
    }
}
