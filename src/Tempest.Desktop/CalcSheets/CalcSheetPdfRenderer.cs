using System.Globalization;
using SkiaSharp;
using Tempest.Calc.Sheets;
using Tempest.Desktop.Documents;

namespace Tempest.Desktop.CalcSheets;

/// <summary>
/// `v0.26.0` (ADR-0165, D-06): renders a <see cref="CalcSheet"/> to PDF
/// through the platform's own <see cref="DocumentTemplate"/> pipeline, in
/// exactly the §3.4 order: header with status watermark, scope and method,
/// inputs with provenance, numbered assumptions, working (formula →
/// substitution → result, clause in the margin), checks governing first,
/// applicability, verification footer. A pack renders a cover (index and
/// revision table) followed by every member sheet. Every string is drawn
/// as text, never interpreted, so a hostile symbol cannot inject anything.
/// </summary>
public sealed class CalcSheetPdfRenderer : ICalcSheetRenderer
{
    public Func<OrganisationIdentity>? IdentityProvider { get; set; }

    public ReadOnlyMemory<byte> Render(CalcSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };
        Layout(state, measure, sheet);
        return Finish(state.Pages, sheet);
    }

    public ReadOnlyMemory<byte> RenderPack(CalcSheet cover, IReadOnlyList<CalcSheet> sheets)
    {
        ArgumentNullException.ThrowIfNull(cover);
        ArgumentNullException.ThrowIfNull(sheets);
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };
        Layout(state, measure, cover);

        foreach (var sheet in sheets)
        {
            DocumentTemplate.NewPage(state);
            Layout(state, measure, sheet);
        }

        return Finish(state.Pages, cover);
    }

    private ReadOnlyMemory<byte> Finish(List<DocumentTemplate.PagePlan> pages, CalcSheet sheet)
    {
        var identity = IdentityProvider?.Invoke() ?? OrganisationIdentity.TempestDefaults;
        DocumentTemplate.AppendFooters(pages, identity, $"{T(sheet.Footer.TemplateReference, 120)} · {T(sheet.Footer.ContentHash, 80)} · {T(sheet.Footer.EngineVersion, 40)}");

        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"{T(sheet.Header.CalcId, 120)} — {T(sheet.Header.Title)}",
            Author = T(sheet.Header.Author),
            Subject = "Calculation sheet",
            Creator = T(sheet.Footer.ApplicationVersion, 80, "TempestOS"),
            Producer = T(sheet.Footer.ApplicationVersion, 80, "TempestOS"),
            Creation = (sheet.Header.IssuedAt ?? sheet.Footer.TestRunAt ?? DateTimeOffset.UnixEpoch).UtcDateTime,
            Modified = (sheet.Header.IssuedAt ?? sheet.Footer.TestRunAt ?? DateTimeOffset.UnixEpoch).UtcDateTime,
        };

        return DocumentTemplate.RenderPdf(pages, metadata);
    }

    private static void Layout(DocumentTemplate.LayoutState state, SKPaint measure, CalcSheet sheet)
    {
        var h = sheet.Header;
        // AMA: each work is numbered in order of first citation and cited by a superscript number with its locator;
        // the numbered list closes the sheet (the owner's ask, 2026-10-24).
        var references = new CalcBibliography.Register(sheet.ClauseRefs);
        var status = h.Status.ToString().ToUpperInvariant();

        DocumentTemplate.AddHeaderBand(state, measure, $"CALCULATION SHEET — {status}", $"{T(h.CalcId, 120)}  ·  Revision {T(h.Revision, 40)}  ·  {Date(h.IssuedAt ?? h.CheckedAt ?? h.AuthoredAt)}");
        DocumentTemplate.AddGap(state, 4f);
        Line(state, measure, $"Project: {T(h.Project)}");
        Line(state, measure, $"Title: {T(h.Title)}");
        // Wrapped text collapses runs of spaces, so the fields are parted by a middle dot (the showcase journey found them run together).
        Line(state, measure, $"Status: {status}  ·  Author: {T(h.Author)} ({Date(h.AuthoredAt)})  ·  Checker: {T(h.Checker, MaxFieldChars, "—")} ({Date(h.CheckedAt)})  ·  Issued: {Date(h.IssuedAt)}");

        if (h.IssueReference is { Length: > 0 } || h.Client is { Length: > 0 })
            Line(state, measure, $"Issue: {T(h.IssueReference, MaxFieldChars, "—")}  ·  Client: {T(h.Client, MaxFieldChars, "—")}");

        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddRule(state);

        if (sheet.PackIndex is { Count: > 0 } index)
        {
            DocumentTemplate.AddHeading(state, measure, "Pack index");
            DocumentTemplate.AddTable(
                state, measure, ["#", "Calc", "Title", "Rev", "Status", "Governing"],
                [W(0.05f), W(0.18f), W(0.35f), W(0.07f), W(0.12f), W(0.23f)],
                [.. index.Select(r => new[] { r.Number.ToString(CultureInfo.InvariantCulture), CalcSheetTextRenderer.Clean(r.CalcId), CalcSheetTextRenderer.Clean(r.Title), CalcSheetTextRenderer.Clean(r.Revision), CalcSheetTextRenderer.Clean(r.Status), CalcSheetTextRenderer.Clean(r.Governing ?? "—") })],
                [SKTextAlign.Right, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left]);
            DocumentTemplate.AddGap(state, 6f);
        }

        if (sheet.Revisions is { Count: > 0 } revisions)
        {
            DocumentTemplate.AddHeading(state, measure, "Revisions");
            DocumentTemplate.AddTable(
                state, measure, ["Rev", "Date", "By", "Description"],
                [W(0.08f), W(0.16f), W(0.22f), W(0.54f)],
                [.. revisions.Select(r => new[] { T(r.Revision, 40), Date(r.Date), T(r.Author), T(r.Description) })],
                [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left]);
            DocumentTemplate.AddGap(state, 6f);
        }

        DocumentTemplate.AddHeading(state, measure, "1. Scope and method");

        if (sheet.Scope.Length > 0)
            Line(state, measure, sheet.Scope);

        if (MethodRows(sheet.Method) is { Count: > 0 } method)
        {
            // One row per step, what it calculates beside the relation, so the method reads as a sequence and not as a
            // wall of formulas (the owner's ask, 2026-10-24).
            DocumentTemplate.AddGap(state, 2f);
            DocumentTemplate.AddTable(
                state, measure, ["What it calculates", "Relation"],
                [W(0.42f), W(0.58f)],
                method,
                [SKTextAlign.Left, SKTextAlign.Left]);
            DocumentTemplate.AddGap(state, 2f);
        }

        if (sheet.ClauseRefs.Count > 0)
            Line(state, measure, $"Basis: {string.Join("; ", sheet.ClauseRefs.Select(c => T(c.Title, 200) + CalcBibliography.Format(references.Cite(c))))}.", DocumentTemplate.CaptionSize);

        DocumentTemplate.AddGap(state, 6f);
        AddDiagram(state, measure, sheet);

        if (sheet.Inputs.Count > 0)
        {
            DocumentTemplate.AddHeading(state, measure, "2. Inputs");
            DocumentTemplate.AddTable(
                state, measure, ["Symbol", "Description", "Value", "Unit", "Source", "Valid range"],
                [W(0.12f), W(0.30f), W(0.12f), W(0.08f), W(0.22f), W(0.16f)],
                [.. sheet.Inputs.Where(i => !IsElementRow(i)).Select(i => new[] { T(i.Symbol), T(i.Description), T(i.Value), T(CalcDiagramReader.Pretty(i.Unit)), T(i.Source), T(CalcDiagramReader.Pretty(i.ValidRange)) })],
                [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Right, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left]);

            // A table input's rows get the full width beneath, one line each, instead of a word per line in the
            // narrow Value column (the showcase journey found it); their source is the table input's own, above.
            if (sheet.Inputs.Where(IsElementRow).ToList() is { Count: > 0 } elements)
            {
                DocumentTemplate.AddGap(state, 4f);
                DocumentTemplate.AddTable(
                    state, measure, ["Table row", "Values"],
                    [W(0.14f), W(0.86f)],
                    [.. elements.Select(i => new[] { T(i.Symbol), T(i.Value) })],
                    [SKTextAlign.Left, SKTextAlign.Left]);
            }

            DocumentTemplate.AddGap(state, 6f);
        }

        if (sheet.Assumptions.Count > 0)
        {
            DocumentTemplate.AddHeading(state, measure, "3. Assumptions");

            for (var n = 0; n < sheet.Assumptions.Count; n++)
            {
                var a = sheet.Assumptions[n];
                Line(state, measure, $"A{n + 1}. {a.Statement}{(a.Basis is { Length: > 0 } ? $" ({a.Basis})" : string.Empty)}");
            }

            DocumentTemplate.AddGap(state, 6f);
        }

        if (sheet.Working.Count > 0)
        {
            DocumentTemplate.AddHeading(state, measure, "4. Working");

            foreach (var s in sheet.Working)
            {
                DocumentTemplate.EnsureSpace(state, DocumentTemplate.BodySize * DocumentTemplate.LineLeading * 3);
                Line(state, measure, $"[{s.Ordinal}] {s.Symbol} = {s.Formula}", fontRole: DocumentFontRole.Mono, color: DocumentTemplate.Ink900);

                if (s.Clause is { Length: > 0 })
                {
                    // The clause reference sits in the right margin of the formula line.
                    var last = state.Page.Texts[^1];
                    state.Page.Texts.Add(new DocumentTemplate.TextRun(state.ContentRight, last.Y, T(CalcBibliography.Format(references.Cite(s.Clause)), 120), DocumentTemplate.CaptionSize, false, SKTextAlign.Right, DocumentTemplate.Indigo600, DocumentFontRole.Body));
                }

                if (!string.Equals(s.Substitution, s.Formula, StringComparison.Ordinal))
                    Line(state, measure, $"      = {s.Substitution}", fontRole: DocumentFontRole.Mono);

                Line(state, measure, $"      = {s.ResultText}", fontRole: DocumentFontRole.Mono, color: DocumentTemplate.Ink900);

                if (s.Description is { Length: > 0 })
                    Line(state, measure, $"        {s.Description}", DocumentTemplate.CaptionSize);
            }

            DocumentTemplate.AddGap(state, 6f);
        }

        if (sheet.Outputs.Count > 0)
        {
            DocumentTemplate.AddHeading(state, measure, "5. Results");
            DocumentTemplate.AddTable(
                state, measure, ["Symbol", "Value", "Description"],
                [W(0.18f), W(0.27f), W(0.55f)],
                [.. sheet.Outputs.Select(o => new[] { T(o.Symbol), T(o.ValueText), T(o.Description) })],
                [SKTextAlign.Left, SKTextAlign.Right, SKTextAlign.Left]);
            DocumentTemplate.AddGap(state, 6f);
        }

        DocumentTemplate.AddHeading(state, measure, "6. Checks (governing first)");

        if (sheet.Checks.Count == 0)
        {
            Line(state, measure, "No pass/fail checks in this calculation.");
        }
        else
        {
            DocumentTemplate.AddTable(
                state, measure, ["Result", "Check", "Demand", "Capacity", "Utilisation", "Ref"],
                [W(0.07f), W(0.19f), W(0.25f), W(0.18f), W(0.10f), W(0.21f)],
                [.. sheet.Checks.Select(c => new[]
                {
                    c.Passed ? "PASS" : "FAIL", T(c.Name), T(c.DemandText), T(c.CapacityText),
                    double.IsInfinity(c.Utilisation) ? "—" : Tempest.Calc.Engine.Values.CalcValue.FormatNumber(c.Utilisation, 3),
                    c.Clause is { Length: > 0 } clause ? T(CalcBibliography.Format(references.Cite(clause))) : string.Empty,
                })],
                [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Right, SKTextAlign.Right, SKTextAlign.Right, SKTextAlign.Left]);
        }

        DocumentTemplate.AddGap(state, 6f);
        DocumentTemplate.AddHeading(state, measure, "7. Applicability");

        if (sheet.Warnings.Count == 0 && sheet.Blocks.Count == 0)
            Line(state, measure, "No warnings.");

        foreach (var b in sheet.Blocks)
            Line(state, measure, $"BLOCK — {b.RuleId}: {b.Message}", color: DocumentTemplate.Ink900);

        foreach (var w in sheet.Warnings)
            Line(state, measure, $"WARNING — {w.RuleId}: {w.Message}");

        DocumentTemplate.AddGap(state, 6f);
        DocumentTemplate.AddHeading(state, measure, "8. Verification");
        var f = sheet.Footer;
        Line(state, measure, $"Template {T(f.TemplateReference, 120)}    Content hash {T(f.ContentHash, 80)}    Engine {T(f.EngineVersion, 40)}", DocumentTemplate.CaptionSize, DocumentFontRole.Mono);
        Line(state, measure, $"Known-answer cases passed {f.KnownAnswerPassed}/{f.KnownAnswerTotal}    Test run {Date(f.TestRunAt, true)}    {T(f.ApplicationVersion, 80)}", DocumentTemplate.CaptionSize, DocumentFontRole.Mono);

        if (references.References.Count > 0)
        {
            DocumentTemplate.AddGap(state, 6f);
            DocumentTemplate.AddHeading(state, measure, "9. References");

            foreach (var r in references.References)
                Line(state, measure, $"{r.Number}. {r.Entry}", DocumentTemplate.CaptionSize + 1f);
        }
    }

    private static float W(float fraction) => DocumentTemplate.ContentWidth * fraction;

    /// <summary>The diagram's labels in the PDF, in points (the screen sets them at 13 px in a panel about 350 px wide).</summary>
    private const float DiagramLabelSize = 9.5f;

    /// <summary>
    /// The reference diagram read against the sheet's own inputs (ADR-0158, D-41), as the form drew it: shapes as vectors,
    /// labels ("P = 25 W") as text, the variant's caption beneath. The owner's ask: an issued sheet is not only text and
    /// maths. Nothing when the sheet carries no inputs (a pack cover, an archive) or the template has no diagram.
    /// </summary>
    private static void AddDiagram(DocumentTemplate.LayoutState state, SKPaint measure, CalcSheet sheet)
    {
        if (sheet.InputValues is not { } inputs || TemplateFor(sheet.Footer.TemplateReference) is not { } template || CalcDiagramReader.Read(template, inputs) is not { } reading)
            return;

        var width = (float)Tempest.Core.Calculations.Modules.Diagrams.CalculationDiagramSpec.Width;
        var height = (float)Tempest.Core.Calculations.Modules.Diagrams.CalculationDiagramSpec.Height;
        DocumentTemplate.EnsureSpace(state, height + DocumentTemplate.CaptionSize * DocumentTemplate.LineLeading * 3 + 8f);
        var x = DocumentTemplate.ContentLeft + (state.ContentWidth - width) / 2;
        var y = state.Y;

        state.Page.Drawings.Add(new DocumentTemplate.DrawingRun(x, y, 1f, canvas =>
        {
            using var panel = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White };
            canvas.DrawRect(0, 0, width, height, panel);
            panel.Style = SKPaintStyle.Stroke;
            panel.StrokeWidth = 0.75f;
            panel.Color = DocumentTemplate.Slate600.WithAlpha(0x60);
            canvas.DrawRect(0, 0, width, height, panel);
            CalcDiagramPainter.Paint(canvas, reading);
        }));

        foreach (var label in CalcDiagramPainter.Labels(reading))
            state.Page.Texts.Add(new DocumentTemplate.TextRun(x + label.X, y + label.Y + DiagramLabelSize * 0.35f, T(label.Text, 80), DiagramLabelSize, false, label.LeftAligned ? SKTextAlign.Left : SKTextAlign.Center, DocumentTemplate.Ink900, DocumentFontRole.Body));

        state.Y += height + 4f;
        Line(state, measure, $"Figure 1 — {reading.Variant.Caption}. Not to scale; the labels are the inputs as entered.", DocumentTemplate.CaptionSize);
        DocumentTemplate.AddGap(state, 6f);
    }

    private static readonly Lazy<IReadOnlyList<CalcTemplate>> Templates = new(() => [.. CalcCalculatorComposition.All().Select(c => c.Template)]);

    /// <summary>The composed template the sheet was run on: by its exact reference, else by its id (an older version's sheet reads against today's drawing).</summary>
    private static CalcTemplate? TemplateFor(string reference)
    {
        var id = reference.Split('@')[0];
        return Templates.Value.FirstOrDefault(t => t.Reference == reference) ?? Templates.Value.FirstOrDefault(t => t.Id == id);
    }

    /// <summary>At most this many method lines are printed; past it the rest are counted (a template states a handful).</summary>
    private const int MaxMethodRows = 60;

    /// <summary>
    /// The template's method, one row per line: the words before the first ": " say what the step calculates, the rest
    /// is its relation. A line with no ": " is narrative across the row.
    /// </summary>
    private static List<string[]> MethodRows(string method)
    {
        var lines = method.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rows = lines.Take(MaxMethodRows).Select(line => line.IndexOf(": ", StringComparison.Ordinal) is var at and > 0
            ? new[] { T(line[..at]), T(line[(at + 2)..].TrimEnd('.')) }
            : [T(line), string.Empty]).ToList();

        if (lines.Length > MaxMethodRows)
            rows.Add([$"… and {lines.Length - MaxMethodRows} more line(s)", string.Empty]);

        return rows;
    }

    /// <summary>A table input's element row (stages[1]): no description or unit of its own, its value the row's cells as text.</summary>
    private static bool IsElementRow(CalcSheetInputRow i) => i.Symbol.Contains('[', StringComparison.Ordinal) && i.Description.Length == 0 && i.Unit.Length == 0;

    private static void Line(DocumentTemplate.LayoutState state, SKPaint measure, string text, float size = DocumentTemplate.BodySize, DocumentFontRole fontRole = DocumentFontRole.Body, SKColor? color = null) =>
        DocumentTemplate.AddWrappedLine(state, measure, T(text, MaxLineChars), size, bold: false, color: color, fontRole: fontRole);

    /// <summary>A field of the sheet holds at most this many characters in the PDF; the sheet builder caps values and provenance, this caps everything else (R-05, R-24).</summary>
    private const int MaxFieldChars = 400;

    /// <summary>One wrapped paragraph holds at most this many characters; past it the file and the page count stop growing with the input.</summary>
    private const int MaxLineChars = 4_000;

    /// <summary>Every string drawn goes through here: control, separator and format characters are stripped and the text is cut at <paramref name="max"/> with an ellipsis.</summary>
    private static string T(string? text, int max = MaxFieldChars, string whenEmpty = "")
    {
        var clean = CalcSheetTextRenderer.Clean(text);

        if (clean.Length == 0)
            return whenEmpty;

        return clean.Length > max ? clean[..max] + "…" : clean;
    }

    private static string Date(DateTimeOffset? d, bool withTime = false) =>
        d is null ? "—" : d.Value.UtcDateTime.ToString(withTime ? "yyyy-MM-dd HH:mm 'UTC'" : "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
