using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;
using Tempest.Calc.Library;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;
using Tempest.Desktop.CalcSheets;
using Tempest.Desktop.Documents;
using Tempest.Desktop.Tests.Quotations;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>
/// The layout of the issued calculation sheet, over every worked example of every template (the shots review of
/// v0.26.0 found each of these on paper): Figure 1 on page 1, a header name line that stays right of the logo, every
/// character in a face that has it, Scope and Method printed whole, the Basis line kept together, no heading or empty
/// table header at the foot of a page, and no table cell printed over its neighbour.
/// </summary>
public sealed class CalcSheetPdfLayoutTests
{
    private static readonly string ExamplesRoot = Path.Combine(DesktopTestHelpers.RepositoryRoot, "docs", "worked-examples");

    public static TheoryData<string> Examples => WorkedExamplePdfTests.Examples;

    /// <summary>The worked examples' folders relative to docs/worked-examples (<c>discipline/template/example</c>), packs left out, as <see cref="Examples"/> lists them.</summary>
    private static List<string> ExampleNames() =>
        [.. Directory.EnumerateFiles(ExamplesRoot, "inputs.json", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(ExamplesRoot, Path.GetDirectoryName(file)!).Replace('\\', '/'))
            .Where(relative => !relative.StartsWith("packs/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

    /// <summary>The catalogue every worked example runs against: the gated lifting templates switched on, as <see cref="WorkedExamplePdfTests"/> does.</summary>
    private static readonly Lazy<CalcCatalogue> Catalogue = new(() =>
    {
        var gated = CalcCalculatorComposition.All().Where(c => c.Template.FeatureFlag is { Length: > 0 }).Select(c => c.Template.FeatureFlag!).Distinct(StringComparer.Ordinal);
        return new CalcCatalogue(CalcCalculatorComposition.All(), new CalcFeatureFlags(gated));
    });

    /// <summary>The issued sheet of one worked example, named as the Desktop names it ("title — example").</summary>
    private static (CalcSheet Sheet, CalcTemplate Template, CalcInputSet Inputs) Build(string relative, string? calcId = null)
    {
        var templateId = relative.Split('/')[1];
        var entry = Catalogue.Value.Find(templateId);
        Assert.NotNull(entry);

        var (inputs, _) = CalcInputJson.Read(File.ReadAllText(Path.Combine(ExamplesRoot, relative, "inputs.json")));
        var result = CalcRunner.Run(entry!.Calculator, inputs);
        var report = KnownAnswerRunner.Run(entry.Calculator);
        var at = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        var header = new CalcSheetHeader("Worked examples, every calculator", calcId ?? $"{entry.Template.Title} — {relative.Split('/')[^1]}", entry.Template.Title, "A", CalcSheetStatus.Issued, "alex.author", "chris.checker", at, at, at, "Worked examples, every calculator", "ISS-1");
        var footer = new CalcSheetFooter(entry.Reference, entry.ShortHash, Tempest.Calc.Engine.CalcEngine.Version, report.Passed, report.Total, at, "TempestOS 0.26.0");
        return (CalcSheetBuilder.Build(entry.Template, inputs, result, header, footer), entry.Template, inputs);
    }

    [Fact]
    public void Every_Template_Has_A_Worked_Example_These_Tests_Read()
    {
        var covered = ExampleNames().Select(r => r.Split('/')[1]).ToHashSet(StringComparer.Ordinal);
        var missing = CalcCalculatorComposition.All().Select(c => c.Template.Id).Where(id => !covered.Contains(id)).ToList();
        Assert.True(missing.Count == 0, $"no worked example for: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Figure_1_Is_On_Page_1(string relative)
    {
        // The shots review: Figure 1 was on page 2 for 35 of 54 templates, whenever the method table was long.
        var (sheet, _, _) = Build(relative);
        var plan = CalcSheetPdfRenderer.Plan(sheet);
        Assert.Single(plan[0].Drawings);
        Assert.Contains(plan[0].Texts, t => t.Text.StartsWith("Figure 1 — ", StringComparison.Ordinal));
        Assert.All(plan.Skip(1), page => Assert.Empty(page.Drawings));

        // And in the issued file, read page by page as a reader would.
        var bytes = new CalcSheetPdfRenderer().Render(sheet).ToArray();
        var pages = PdfTextExtractor.ExtractPages(bytes);
        Assert.Equal(PdfTextExtractor.CountPages(bytes), pages.Count);
        Assert.Contains("Figure 1 — ", pages[0], StringComparison.Ordinal);
        Assert.All(pages.Skip(1), page => Assert.DoesNotContain("Figure 1 — ", page, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Scope_And_Method_Are_Printed_Whole(string relative)
    {
        // The shots review: mech.snap-fit and struct.plate-bending lost the end of a method relation behind an ellipsis.
        var (sheet, _, _) = Build(relative);
        var printed = Collapse(string.Join(" ", CalcSheetPdfRenderer.Plan(sheet).SelectMany(p => p.Texts).Select(t => t.Text)));

        Assert.Contains(Collapse(CalcSheetTextRenderer.Clean(sheet.Scope)), printed, StringComparison.Ordinal);

        foreach (var line in sheet.Method.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var at = line.IndexOf(": ", StringComparison.Ordinal);
            var parts = at > 0 ? new[] { line[..at], line[(at + 2)..].TrimEnd('.') } : [line];

            foreach (var part in parts)
                Assert.Contains(Collapse(CalcSheetTextRenderer.Clean(part)), printed, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void No_Page_Ends_With_A_Heading_Or_An_Empty_Table_Header(string relative)
    {
        // The shots review: struct.beam-formulas page 1 ended with "2. Inputs" and an empty column-header row.
        var (sheet, _, _) = Build(relative);
        var plan = CalcSheetPdfRenderer.Plan(sheet);

        for (var n = 0; n < plan.Count - 1; n++)
        {
            var last = plan[n].Texts.MaxBy(t => t.Y);
            Assert.False(IsHeading(last) || IsTableHeader(last), $"{relative}: page {n + 1} ends with '{last.Text}'.");
        }
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void The_Basis_Line_Is_Kept_On_One_Page(string relative)
    {
        // The shots review: mech.spring's Basis line broke across the page, its last clause alone at the top of page 2.
        var (sheet, _, _) = Build(relative);

        if (sheet.ClauseRefs.Count == 0)
            return;

        var plan = CalcSheetPdfRenderer.Plan(sheet);
        var page = plan.Single(p => p.Texts.Any(t => t.Text.StartsWith("Basis: ", StringComparison.Ordinal)));
        var start = page.Texts.FindIndex(t => t.Text.StartsWith("Basis: ", StringComparison.Ordinal));
        var lines = page.Texts.Skip(start).TakeWhile(t => t.Size == DocumentTemplate.CaptionSize && t.FontRole == DocumentFontRole.Body && !t.Bold).ToList();
        Assert.EndsWith(".", lines[^1].Text, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void No_Text_Is_Printed_Over_Other_Text_On_Its_Line(string relative)
    {
        // The shots review: struct.frame-2d's table row label ran over its values, and its symbol 'combinations' into its description.
        var (sheet, _, _) = Build(relative);
        var plan = CalcSheetPdfRenderer.Plan(sheet);
        using var paint = new SKPaint();

        for (var n = 0; n < plan.Count; n++)
        {
            // Diagram labels sit on the figure, placed by the diagram itself; only the text of the sheet is checked here.
            var figureTop = plan[n].Drawings.Count > 0 ? plan[n].Drawings[0].Y : float.MaxValue;
            var figureBottom = figureTop + (float)Tempest.Core.Calculations.Modules.Diagrams.CalculationDiagramSpec.Height;

            foreach (var line in plan[n].Texts.Where(t => t.Text.Length > 0 && (t.Y < figureTop || t.Y > figureBottom + 1f)).GroupBy(t => MathF.Round(t.Y, 1)))
            {
                var spans = line.Select(t =>
                {
                    paint.TextSize = t.Size;
                    var width = DocumentTemplate.MeasureInFaces(paint, t.Text, t.FontRole);
                    var left = t.Align switch { SKTextAlign.Right => t.X - width, SKTextAlign.Center => t.X - width / 2, _ => t.X };
                    return (Left: left, Right: left + width, t.Text);
                }).OrderBy(s => s.Left).ToList();

                for (var i = 1; i < spans.Count; i++)
                    Assert.True(spans[i - 1].Right <= spans[i].Left + 0.5f, $"{relative}, page {n + 1}: '{spans[i - 1].Text}' runs into '{spans[i].Text}'.");

                Assert.All(spans, s => Assert.True(s.Left >= DocumentTemplate.ContentLeft - 0.5f && s.Right <= DocumentTemplate.ContentRight + 0.5f, $"{relative}, page {n + 1}: '{s.Text}' runs past the margin ({s.Left:0.0}–{s.Right:0.0})."));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Every_Character_Printed_Is_In_A_Face_That_Has_It(string relative)
    {
        // The shots review: ⊥ and ∥ (struct.connection-ec3, struct.fillet-weld and its Figure 1 labels) and ∮
        // (struct.section-properties) printed as boxes.
        var (sheet, template, inputs) = Build(relative);
        var problems = new List<string>();

        void Check(string what, string? text)
        {
            if (DocumentFonts.Undrawable(text) is { Count: > 0 } missing)
                problems.Add($"{what}: {string.Join(" ", missing.Select(m => $"{m} (U+{char.ConvertToUtf32(m, 0):X4})"))}");
        }

        Check("sheet.txt", File.ReadAllText(Path.Combine(ExamplesRoot, relative, "sheet.txt")));

        if (CalcDiagramReader.Read(template, inputs) is { } reading)
        {
            foreach (var label in CalcDiagramPainter.Labels(reading))
                Check("diagram label", label.Text);
        }

        // Every run as it is drawn: after typesetting and fallback, each character's own face has it.
        var plan = CalcSheetPdfRenderer.Plan(sheet);
        DocumentTemplate.PrepareText(plan);

        foreach (var run in plan.SelectMany(p => p.Texts))
        {
            var face = DocumentFonts.For(run.FontRole, run.Bold);

            foreach (var rune in run.Text.EnumerateRunes().Where(r => !Rune.IsWhiteSpace(r) && !Rune.IsControl(r)))
            {
                if (!face.ContainsGlyphs(rune.ToString()))
                    problems.Add($"'{rune}' (U+{rune.Value:X4}) drawn in {run.FontRole}, which has no glyph for it, in '{run.Text}'");
            }
        }

        Assert.True(problems.Count == 0, $"{relative}:\n{string.Join("\n", problems.Distinct())}");
    }

    [Fact]
    public void Every_Templates_Own_Text_Is_In_A_Face_That_Has_It()
    {
        // Scope, method, inputs, outputs and assumptions of every template, whether or not a worked example prints them.
        var problems = new List<string>();

        foreach (var template in CalcCalculatorComposition.All().Select(c => c.Template))
        {
            var texts = new List<string> { template.Title, template.Scope, template.Method };
            texts.AddRange(template.Inputs.SelectMany(i => new[] { i.Symbol, i.Description, i.DefaultUnit }.Concat(i.Choices ?? [])));
            texts.AddRange((template.OutputPorts ?? []).SelectMany(p => new[] { p.Symbol, p.Unit, p.Description }));
            texts.AddRange(template.Assumptions.Select(a => a.Statement));
            texts.AddRange(template.ClauseRefs.Select(c => c.Title));

            foreach (var text in texts)
            {
                if (DocumentFonts.Undrawable(text) is { Count: > 0 } missing)
                    problems.Add($"{template.Id}: {string.Join(" ", missing)} in '{text}'");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void The_Symbols_The_Shots_Review_Found_As_Boxes_Are_Drawn_In_The_Fallback_Face()
    {
        Assert.True(DocumentFonts.SymbolLoaded, "TempestSymbols-Regular.ttf did not load from its embedded resource.");

        var texts = new List<DocumentTemplate.TextRun>
        {
            new(10, 20, "√(σ⊥² + 3(τ⊥² + τ∥²))", 9.5f, false, SKTextAlign.Left, SKColors.Black, DocumentFontRole.Body),
            new(10, 30, "A = ½∮(x dy − y dx)", 9.5f, false, SKTextAlign.Right, SKColors.Black, DocumentFontRole.Mono),
        };

        DocumentTemplate.FallBackForMissingGlyphs(texts);

        Assert.Contains(texts, t => t.Text == "⊥" && t.FontRole == DocumentFontRole.Symbol);
        Assert.Contains(texts, t => t.Text == "∥" && t.FontRole == DocumentFontRole.Symbol);
        Assert.Contains(texts, t => t.Text.Contains('∮', StringComparison.Ordinal) && t.FontRole == DocumentFontRole.Symbol);
        Assert.Empty(texts.SelectMany(t => DocumentFonts.Undrawable(t.Text, t.FontRole)));

        // The pieces of the right-aligned run still end at its right edge.
        using var paint = new SKPaint { TextSize = 9.5f };
        var right = texts.Where(t => t.Y == 30).Select(t => t.X + DocumentTemplate.MeasureInFaces(paint, t.Text, t.FontRole)).Max();
        Assert.Equal(10f, right, 0.5f);
    }

    [Fact]
    public void The_Fillet_Weld_Subscripts_Reach_The_File_As_Text()
    {
        // ⊥ and ∥ are real, searchable text in the issued file, not boxes and not blanks (pdftotext showed blanks before).
        var relative = ExampleNames().First(r => r.Split('/')[1] == "struct.fillet-weld");
        var text = PdfTextExtractor.ExtractText(new CalcSheetPdfRenderer().Render(Build(relative).Sheet).ToArray());
        Assert.Contains("⊥", text, StringComparison.Ordinal);
        Assert.Contains("∥", text, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Longest_Name_Fits_Right_Of_The_Logo_In_The_Header()
    {
        // The shots review: struct.ltb-interaction's name ran over the logo and off the left edge of the page; fp.accumulator's over the tagline.
        var longest = ExampleNames()
            .Select(relative => (relative, name: $"{Catalogue.Value.Find(relative.Split('/')[1])!.Template.Title} — {relative.Split('/')[^1]}"))
            .MaxBy(x => x.name.Length);

        var (sheet, _, _) = Build(longest.relative, longest.name);
        AssertHeaderFits(sheet, whole: true);

        // A name past any real one is shrunk and, as a last resort, cut short; it still never reaches the logo.
        AssertHeaderFits(sheet with { Header = sheet.Header with { CalcId = string.Join(" ", Enumerable.Repeat("Lateral-torsional buckling", 8)) } }, whole: false);
        AssertHeaderFits(sheet with { Header = sheet.Header with { CalcId = new string('W', 120) } }, whole: false);
    }

    private static void AssertHeaderFits(CalcSheet sheet, bool whole)
    {
        var page = CalcSheetPdfRenderer.Plan(sheet)[0];
        var logoRight = page.Images.Count > 0 ? page.Images[0].X + page.Images[0].Width : DocumentTemplate.ContentLeft;
        var firstRule = page.Rules.Min(r => r.Y);
        var lines = page.Texts.Where(t => t.FontRole == DocumentFontRole.Mono && t.Y < firstRule).ToList();
        Assert.InRange(lines.Count, 1, 2);

        using var paint = new SKPaint();

        foreach (var line in lines)
        {
            paint.TextSize = line.Size;
            var left = line.X - DocumentTemplate.MeasureInFaces(paint, line.Text, DocumentFontRole.Mono);
            Assert.True(left >= logoRight + DocumentTemplate.HeaderLogoGap - 0.5f, $"'{line.Text}' starts at {left:0.0}, over the logo (which ends at {logoRight:0.0}).");
            Assert.InRange(line.Size, DocumentTemplate.HeaderReferenceMinSize, DocumentTemplate.HeaderReferenceSize);
        }

        var printed = string.Join(" ", lines.Select(l => l.Text));

        if (whole)
            Assert.Equal(Collapse($"{sheet.Header.CalcId}  ·  Revision {sheet.Header.Revision}  ·  2026-10-27"), Collapse(printed));
        else
            Assert.StartsWith(sheet.Header.CalcId[..10], printed, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Kept_Together_Block_Moves_Whole_To_The_Next_Page()
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint();
        state.Y = DocumentTemplate.ContentBottom - DocumentTemplate.CaptionSize * DocumentTemplate.LineLeading * 1.5f;
        var basis = "Basis: " + string.Join("; ", Enumerable.Repeat("End geometry, buckling, critical frequency (Ch. 10, 10th ed.)", 6)) + ".";

        DocumentTemplate.AddWrappedLine(state, measure, basis, DocumentTemplate.CaptionSize, bold: false, keepTogether: true);

        Assert.Equal(2, state.Pages.Count);
        Assert.Empty(state.Pages[0].Texts);
        Assert.True(state.Pages[1].Texts.Count > 1);
    }

    [Fact]
    public void A_Table_Header_Is_Never_Left_Alone_At_The_Foot_Of_A_Page()
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint();
        float[] widths = [DocumentTemplate.ContentWidth * 0.3f, DocumentTemplate.ContentWidth * 0.7f];
        string[][] rows = [["L", string.Join(" ", Enumerable.Repeat("a long description that wraps", 6))]];
        state.Y = DocumentTemplate.ContentBottom - DocumentTemplate.HeaderRowHeight - 4f;

        DocumentTemplate.AddTable(state, measure, ["Symbol", "Description"], widths, rows);

        Assert.Equal(2, state.Pages.Count);
        Assert.Empty(state.Pages[0].Texts);
    }

    [Fact]
    public void A_Row_Taller_Than_A_Page_Runs_On_And_Loses_No_Line()
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint();
        var words = Enumerable.Range(1, 900).Select(i => $"w{i}").ToList();
        float[] widths = [DocumentTemplate.ContentWidth * 0.3f, DocumentTemplate.ContentWidth * 0.7f];

        DocumentTemplate.AddTable(state, measure, ["What", "Relation"], widths, [["Step", string.Join(" ", words)]]);

        Assert.True(state.Pages.Count > 1);
        Assert.All(state.Pages.SelectMany(p => p.Texts), t => Assert.True(t.Y <= DocumentTemplate.ContentBottom + 0.5f, $"'{t.Text}' is below the content area."));
        var printed = string.Join(" ", state.Pages.SelectMany(p => p.Texts).Where(t => !t.Bold && t.Text != "Step").Select(t => t.Text));
        Assert.Equal(string.Join(" ", words), printed);
    }

    private static bool IsHeading(DocumentTemplate.TextRun run) => run.Bold && run.FontRole == DocumentFontRole.Display && run.Size == DocumentTemplate.HeadingSize;

    private static bool IsTableHeader(DocumentTemplate.TextRun run) => run.Bold && run.FontRole == DocumentFontRole.Display && run.Size == DocumentTemplate.CaptionSize;

    private static string Collapse(string text) => Regex.Replace(text, @"\s+", " ").Trim();
}
