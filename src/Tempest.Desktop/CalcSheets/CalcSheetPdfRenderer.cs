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
        DocumentTemplate.AppendFooters(pages, identity, $"{sheet.Footer.TemplateReference} · {sheet.Footer.ContentHash} · {sheet.Footer.EngineVersion}");

        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"{sheet.Header.CalcId} — {sheet.Header.Title}",
            Author = sheet.Header.Author,
            Subject = "Calculation sheet",
            Creator = sheet.Footer.ApplicationVersion ?? "TempestOS",
            Producer = sheet.Footer.ApplicationVersion ?? "TempestOS",
            Creation = (sheet.Header.IssuedAt ?? sheet.Footer.TestRunAt ?? DateTimeOffset.UnixEpoch).UtcDateTime,
            Modified = (sheet.Header.IssuedAt ?? sheet.Footer.TestRunAt ?? DateTimeOffset.UnixEpoch).UtcDateTime,
        };

        return DocumentTemplate.RenderPdf(pages, metadata);
    }

    private static void Layout(DocumentTemplate.LayoutState state, SKPaint measure, CalcSheet sheet)
    {
        var h = sheet.Header;
        var status = h.Status.ToString().ToUpperInvariant();

        DocumentTemplate.AddHeaderBand(state, measure, $"CALCULATION SHEET — {status}", $"{h.CalcId}  ·  Revision {h.Revision}  ·  {Date(h.IssuedAt ?? h.CheckedAt ?? h.AuthoredAt)}");
        DocumentTemplate.AddGap(state, 4f);
        Line(state, measure, $"Project: {h.Project}");
        Line(state, measure, $"Title: {h.Title}");
        Line(state, measure, $"Status: {status}    Author: {h.Author} ({Date(h.AuthoredAt)})    Checker: {h.Checker ?? "—"} ({Date(h.CheckedAt)})    Issued: {Date(h.IssuedAt)}");

        if (h.IssueReference is { Length: > 0 } || h.Client is { Length: > 0 })
            Line(state, measure, $"Issue: {h.IssueReference ?? "—"}    Client: {h.Client ?? "—"}");

        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddRule(state);

        if (sheet.PackIndex is { Count: > 0 } index)
        {
            DocumentTemplate.AddHeading(state, measure, "Pack index");
            DocumentTemplate.AddTable(
                state, measure, ["#", "Calc", "Title", "Rev", "Status", "Governing"],
                [W(0.05f), W(0.18f), W(0.35f), W(0.07f), W(0.12f), W(0.23f)],
                [.. index.Select(r => new[] { r.Number.ToString(CultureInfo.InvariantCulture), r.CalcId, r.Title, r.Revision, r.Status, r.Governing ?? "—" })],
                [SKTextAlign.Right, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left]);
            DocumentTemplate.AddGap(state, 6f);
        }

        if (sheet.Revisions is { Count: > 0 } revisions)
        {
            DocumentTemplate.AddHeading(state, measure, "Revisions");
            DocumentTemplate.AddTable(
                state, measure, ["Rev", "Date", "By", "Description"],
                [W(0.08f), W(0.16f), W(0.22f), W(0.54f)],
                [.. revisions.Select(r => new[] { r.Revision, Date(r.Date), r.Author, r.Description })],
                [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left]);
            DocumentTemplate.AddGap(state, 6f);
        }

        DocumentTemplate.AddHeading(state, measure, "1. Scope and method");

        if (sheet.Scope.Length > 0)
            Line(state, measure, sheet.Scope);

        if (sheet.Method.Length > 0)
            Line(state, measure, sheet.Method);

        foreach (var c in sheet.ClauseRefs)
            Line(state, measure, $"Ref: {c.Standard}{(c.Clause is { Length: > 0 } and not "—" ? " " + c.Clause : string.Empty)} — {c.Title}", DocumentTemplate.CaptionSize);

        DocumentTemplate.AddGap(state, 6f);

        if (sheet.Inputs.Count > 0)
        {
            DocumentTemplate.AddHeading(state, measure, "2. Inputs");
            DocumentTemplate.AddTable(
                state, measure, ["Symbol", "Description", "Value", "Unit", "Source", "Valid range"],
                [W(0.12f), W(0.30f), W(0.12f), W(0.08f), W(0.22f), W(0.16f)],
                [.. sheet.Inputs.Select(i => new[] { i.Symbol, i.Description, i.Value, i.Unit, i.Source, i.ValidRange })],
                [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Right, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left]);
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
                    state.Page.Texts.Add(new DocumentTemplate.TextRun(state.ContentRight, last.Y, s.Clause, DocumentTemplate.CaptionSize, false, SKTextAlign.Right, DocumentTemplate.Indigo600, DocumentFontRole.Display));
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
                [.. sheet.Outputs.Select(o => new[] { o.Symbol, o.ValueText, o.Description })],
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
                [W(0.09f), W(0.29f), W(0.16f), W(0.16f), W(0.12f), W(0.18f)],
                [.. sheet.Checks.Select(c => new[]
                {
                    c.Passed ? "PASS" : "FAIL", c.Name, c.DemandText, c.CapacityText,
                    double.IsInfinity(c.Utilisation) ? "—" : Tempest.Calc.Engine.Values.CalcValue.FormatNumber(c.Utilisation, 3),
                    c.Clause ?? string.Empty,
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
        Line(state, measure, $"Template {f.TemplateReference}    Content hash {f.ContentHash}    Engine {f.EngineVersion}", DocumentTemplate.CaptionSize, DocumentFontRole.Mono);
        Line(state, measure, $"Known-answer cases passed {f.KnownAnswerPassed}/{f.KnownAnswerTotal}    Test run {Date(f.TestRunAt, true)}    {f.ApplicationVersion ?? string.Empty}", DocumentTemplate.CaptionSize, DocumentFontRole.Mono);
    }

    private static float W(float fraction) => DocumentTemplate.ContentWidth * fraction;

    private static void Line(DocumentTemplate.LayoutState state, SKPaint measure, string text, float size = DocumentTemplate.BodySize, DocumentFontRole fontRole = DocumentFontRole.Body, SKColor? color = null) =>
        DocumentTemplate.AddWrappedLine(state, measure, CalcSheetTextRenderer.Clean(text), size, bold: false, color: color, fontRole: fontRole);

    private static string Date(DateTimeOffset? d, bool withTime = false) =>
        d is null ? "—" : d.Value.UtcDateTime.ToString(withTime ? "yyyy-MM-dd HH:mm 'UTC'" : "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
