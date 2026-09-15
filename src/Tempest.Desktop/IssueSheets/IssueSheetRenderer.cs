using System.Globalization;
using System.Text;
using SkiaSharp;
using Tempest.Core.Evidence;
using Tempest.Desktop.Documents;
using Tempest.Workspace.Evidence;

namespace Tempest.Desktop.IssueSheets;

/// <summary>
/// Renders an <see cref="IssueSheetModel"/> as an A4 PDF with SkiaSharp's
/// own PDF document backend (`ADR-0148`, `WP 18.2B`, Product Owner
/// 2026-09-09) — the same <c>SkiaSharp</c> package the desktop build
/// already ships transitively under <c>PDFtoImage</c> (`WP 10.0B`'s own
/// PDF viewer), so no package reference is added here. Page furniture,
/// type and colour render through <see cref="DocumentTemplate"/> since
/// `WP 20.10G` (PO finding D4), closing `TD-182` — this class owned the
/// two-phase layout technique <see cref="DocumentTemplate"/> now shares
/// with <see cref="Tempest.Desktop.Quotations.QuotationSheetRenderer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Determinism.</b> Nothing here reads the clock, a random source, or
/// any file the platform did not already embed. The PDF's own
/// <c>Creation</c>/<c>Modified</c> metadata are set from
/// <see cref="IssueSheetModel.GeneratedAtUtc"/>, so
/// <see cref="Render(IssueSheetModel)"/> called twice on the same model
/// writes the same bytes.
/// </para>
/// <para>
/// <b>Fonts.</b> <see cref="SKTypeface.Default"/> throughout, deliberately
/// — see <see cref="DocumentTemplate"/>'s own remarks for why, and for
/// what this Work Package's report discloses about the design system's
/// own font/logo assets not (yet) being reachable from this worktree.
/// </para>
/// </remarks>
public sealed class IssueSheetRenderer : IIssueSheetRenderer
{
    /// <summary>Supplies the organisation identity for a render whose caller passes none — set by the composer to Settings → Organisation (`WP 20.10G`, threaded at merge).</summary>
    public Func<OrganisationIdentity>? IdentityProvider { get; set; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Render(IssueSheetModel model) => Render(model, identity: null);

    /// <summary>Renders <paramref name="model"/> as a PDF, over <paramref name="identity"/>'s own footer identity — <see cref="OrganisationIdentity.TempestDefaults"/> when the caller supplies none (every call site until Settings → Organisation is threaded through — see this Work Package's own report).</summary>
    public ReadOnlyMemory<byte> Render(IssueSheetModel model, OrganisationIdentity? identity)
    {
        ArgumentNullException.ThrowIfNull(model);

        var orgIdentity = identity ?? IdentityProvider?.Invoke() ?? OrganisationIdentity.TempestDefaults;
        var pages = Layout(model);
        DocumentTemplate.AppendFooters(pages, orgIdentity, FormatFooterDetail(model));

        using var stream = new MemoryStream();
        using (var wstream = new SKManagedWStream(stream))
        {
            var metadata = new SKDocumentPdfMetadata
            {
                Title = $"{model.Title} — Issue Sheet",
                Author = model.AuthorDisplayName,
                Subject = "Issue sheet",
                Creator = model.ApplicationVersionText,
                Producer = model.ApplicationVersionText,
                Creation = model.GeneratedAtUtc.UtcDateTime,
                Modified = model.GeneratedAtUtc.UtcDateTime,
            };

            using var document = SKDocument.CreatePdf(wstream, metadata)
                ?? throw new InvalidOperationException("SkiaSharp could not open a PDF document for the issue sheet.");

            using var textPaint = new SKPaint { IsAntialias = true, Typeface = SKTypeface.Default };
            using var linePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };

            foreach (var page in pages)
            {
                var canvas = document.BeginPage(DocumentTemplate.PageWidth, DocumentTemplate.PageHeight);
                canvas.Clear(DocumentTemplate.PaperPage);

                foreach (var rule in page.Rules)
                {
                    linePaint.StrokeWidth = rule.StrokeWidth;
                    linePaint.Color = rule.Color;
                    canvas.DrawLine(rule.X1, rule.Y, rule.X2, rule.Y, linePaint);
                }

                foreach (var text in page.Texts)
                {
                    textPaint.TextSize = text.Size;
                    textPaint.FakeBoldText = text.Bold;
                    textPaint.TextAlign = text.Align;
                    textPaint.Color = text.Color;
                    canvas.DrawText(text.Text, text.X, text.Y, textPaint);
                }

                document.EndPage();
            }

            document.Close();
        }

        return stream.ToArray();
    }

    private static List<DocumentTemplate.PagePlan> Layout(IssueSheetModel model)
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };

        DocumentTemplate.AddHeaderBand(
            state, measure, "ISSUE SHEET",
            $"{model.IssueReference}  ·  Revision {model.Revision}  ·  {FormatDate(model.IssueDateUtc)}");
        DocumentTemplate.AddGap(state, 4f);

        DocumentTemplate.AddWrappedLine(state, measure, $"Project: {model.ProjectCode} — {model.ProjectName}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure, $"Client: {model.Client}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure, $"Evidence: {model.EvidenceReference ?? "—"} — {model.Title}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure, $"Classification: {Humanize(model.Classification.ToString())}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure, $"Revision {model.Revision} · Issue {model.IssueReference} — {FormatDate(model.IssueDateUtc)}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddRule(state);

        DocumentTemplate.AddHeading(state, measure, "Review");
        DocumentTemplate.AddWrappedLine(state, measure, $"Author: {model.AuthorDisplayName} — {FormatDate(model.AuthorDateUtc)}", DocumentTemplate.BodySize, bold: false);
        var checkerLine = model.CheckerPrincipalDisplayName is { Length: > 0 } principal
            ? $"Checker: {model.CheckerName}, {model.CheckerOrganisation} ({principal}) — {FormatDate(model.CheckDateUtc)}"
            : $"Checker: {model.CheckerName}, {model.CheckerOrganisation} — {FormatDate(model.CheckDateUtc)}";
        DocumentTemplate.AddWrappedLine(state, measure, checkerLine, DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure, $"Outcome: {Humanize(model.Outcome.ToString())}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddRule(state);

        DocumentTemplate.AddHeading(state, measure, "Citations");
        if (model.Citations.Count == 0)
        {
            DocumentTemplate.AddWrappedLine(state, measure, "No citations recorded.", DocumentTemplate.BodySize, bold: false);
        }
        else
        {
            DocumentTemplate.AddTable(
                state, measure,
                headers: ["Library", "Record", "Rev", "Source"],
                widths: [DocumentTemplate.ContentWidth * 0.16f, DocumentTemplate.ContentWidth * 0.22f, DocumentTemplate.ContentWidth * 0.08f, DocumentTemplate.ContentWidth * 0.54f],
                rows: [.. model.Citations.Select(c => new[]
                {
                    c.Library,
                    c.RecordId,
                    c.Revision.ToString(CultureInfo.InvariantCulture),
                    c.SourceCitationSnapshot ?? "—",
                })],
                columnAligns: [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Right, SKTextAlign.Left]);
        }
        DocumentTemplate.AddGap(state, 6f);

        DocumentTemplate.AddHeading(state, measure, "Declared figures");
        if (model.DeclaredFigures.Count == 0)
        {
            DocumentTemplate.AddWrappedLine(state, measure, "No figures declared.", DocumentTemplate.BodySize, bold: false);
        }
        else
        {
            DocumentTemplate.AddTable(
                state, measure,
                headers: ["Name", "Role", "Value"],
                widths: [DocumentTemplate.ContentWidth * 0.35f, DocumentTemplate.ContentWidth * 0.20f, DocumentTemplate.ContentWidth * 0.45f],
                rows: [.. model.DeclaredFigures.Select(f => new[]
                {
                    f.Name,
                    Humanize(f.Role.ToString()),
                    f.Quantity,
                })],
                columnAligns: [SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Right]);
        }
        DocumentTemplate.AddGap(state, 10f);

        DocumentTemplate.AddHeading(state, measure, "Signatures");
        AddSignatureRow(state, "Prepared", model.AuthorDisplayName, model.AuthorDateUtc);
        AddSignatureRow(state, "Checked", $"{model.CheckerName}, {model.CheckerOrganisation}", model.CheckDateUtc);
        AddSignatureRow(state, "Issued", "—", model.IssueDateUtc);

        return state.Pages;
    }

    private static string FormatFooterDetail(IssueSheetModel model) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} · Sequence {1} · Generated {2:yyyy-MM-dd HH:mm} UTC",
        model.ApplicationVersionText, model.StoreSequence, model.GeneratedAtUtc.UtcDateTime);

    private static void AddSignatureRow(DocumentTemplate.LayoutState state, string role, string name, DateTimeOffset date)
    {
        DocumentTemplate.AddGap(state, 14f);
        DocumentTemplate.EnsureSpace(state, 30f);
        state.Page.Rules.Add(new DocumentTemplate.RuleRun(DocumentTemplate.ContentLeft, DocumentTemplate.ContentLeft + 260f, state.Y, 0.75f, DocumentTemplate.Hairline));
        state.Y += 4f;

        var baseline = state.Y + DocumentTemplate.BodySize;
        state.Page.Texts.Add(new DocumentTemplate.TextRun(DocumentTemplate.ContentLeft, baseline, $"{role} — {name}", DocumentTemplate.BodySize, false, SKTextAlign.Left, DocumentTemplate.Slate700));
        state.Page.Texts.Add(new DocumentTemplate.TextRun(
            DocumentTemplate.ContentRight, baseline, $"Date: {FormatDate(date)}", DocumentTemplate.BodySize, false, SKTextAlign.Right, DocumentTemplate.Slate700));
        state.Y += DocumentTemplate.BodySize * DocumentTemplate.LineLeading;
    }

    /// <summary>"AcceptedWithComments" -&gt; "Accepted With Comments": a closed enum's own name, spaced for reading on a printed sheet.</summary>
    private static string Humanize(string enumName)
    {
        if (string.IsNullOrEmpty(enumName))
            return enumName;

        var builder = new StringBuilder(enumName.Length + 8);
        builder.Append(enumName[0]);
        for (var i = 1; i < enumName.Length; i++)
        {
            if (char.IsUpper(enumName[i]))
                builder.Append(' ');
            builder.Append(enumName[i]);
        }

        return builder.ToString();
    }

    private static string FormatDate(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
