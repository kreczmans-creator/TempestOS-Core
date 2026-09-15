using System.Globalization;
using SkiaSharp;
using Tempest.Desktop.Documents;

namespace Tempest.Desktop.Quotations;

/// <summary>
/// One line of a <see cref="QuotationSheetModel"/>'s own table — every
/// value already resolved and formatted by the caller (`WP 19.5B`), the
/// identical "the renderer reads a flat model, draws it" discipline
/// <c>Tempest.Workspace.Evidence.IssueSheetCitationRow</c> already
/// established for the issue sheet.
/// </summary>
/// <param name="Description">What the line is.</param>
/// <param name="Hours">Billable hours, formatted, or <see langword="null"/> for a fixed-price line.</param>
/// <param name="Rate">The rate one hour bills at, formatted (currency and amount), or <see langword="null"/> for a fixed-price line.</param>
/// <param name="Amount">The line's own amount, formatted (currency and amount).</param>
public sealed record QuotationSheetLineRow(string Description, string? Hours, string? Rate, string Amount);

/// <summary>
/// Everything the quote sheet renders — an immutable snapshot built once
/// (`WP 19.5B`, `ADR-0152`, Product Owner comment item 9: "a quote export
/// is wanted in this release ... through the same SkiaSharp path as the
/// issue sheet"). Carries no behaviour of its own beyond what a caller
/// resolves onto it: the renderer reads it, draws it, and the sheet is
/// never edited — a re-render of the same model is byte-identical to the
/// first (<see cref="GeneratedAtUtc"/> stands in for the clock, exactly as
/// <c>IssueSheetModel</c>'s own does).
/// </summary>
/// <param name="IssuerName">The consultancy's own principal name — the current session principal's own display name at the moment the sheet is generated.</param>
/// <param name="ProjectCode">The quoted project's own business identifier.</param>
/// <param name="ProjectName">The quoted project's own display name.</param>
/// <param name="Client">The client this quotation is raised against, resolved to a display name where one exists — the bare id otherwise (`ADR-0150`'s own rule for a client id: a tag, never validated).</param>
/// <param name="Reference">The quotation's own reference.</param>
/// <param name="QuoteDate">The date this quotation was raised.</param>
/// <param name="ValidityDays">How many days from <paramref name="QuoteDate"/> this quotation stays valid.</param>
/// <param name="Currency">The currency every line and <paramref name="Total"/> are stated in.</param>
/// <param name="Lines">Every line, in the order the quotation carries them.</param>
/// <param name="Total">The quotation's own total, formatted (currency and amount).</param>
/// <param name="Terms">Free-text terms shown on the sheet. <see langword="null"/> when none are recorded.</param>
/// <param name="Status">The quotation's own status at the moment the sheet is generated.</param>
/// <param name="GeneratedAtUtc">When this sheet was generated — supplied, never <see cref="DateTime.Now"/>.</param>
/// <param name="ApplicationVersionText">The running application's own version text — <c>"TempestOS &lt;version&gt; (&lt;commit&gt;)"</c>.</param>
public sealed record QuotationSheetModel(
    string IssuerName,
    string ProjectCode,
    string ProjectName,
    string Client,
    string Reference,
    DateOnly QuoteDate,
    int ValidityDays,
    string Currency,
    IReadOnlyList<QuotationSheetLineRow> Lines,
    string Total,
    string? Terms,
    string Status,
    DateTimeOffset GeneratedAtUtc,
    string ApplicationVersionText);

/// <summary>
/// Renders a <see cref="QuotationSheetModel"/> as an A4 PDF (`WP 19.5B`,
/// `ADR-0152`, Product Owner comment item 9; page furniture, type and
/// colour through <see cref="DocumentTemplate"/> since `WP 20.10G`, PO
/// finding D4, closing `TD-182`) — the same SkiaSharp technique
/// <see cref="Tempest.Desktop.IssueSheets.IssueSheetRenderer"/> uses:
/// <see cref="SKDocument.CreatePdf"/> and a two-phase measure-then-draw
/// layout, both now the template's own shared machinery rather than a
/// second, private copy of it.
/// </summary>
public sealed class QuotationSheetRenderer
{
    /// <summary>Supplies the organisation identity for a render whose caller passes none — set by the composer to Settings → Organisation (`WP 20.10G`, threaded at merge), so the exported sheet carries what the user configured rather than the defaults.</summary>
    public Func<OrganisationIdentity>? IdentityProvider { get; set; }

    /// <summary>Renders <paramref name="model"/> as a PDF, over <paramref name="identity"/>'s own footer identity — <see cref="OrganisationIdentity.TempestDefaults"/> when the caller supplies none (every call site until Settings → Organisation is threaded through — see this Work Package's own report).</summary>
    public ReadOnlyMemory<byte> Render(QuotationSheetModel model, OrganisationIdentity? identity = null)
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
                Title = $"{model.Reference} — Quotation",
                Author = model.IssuerName,
                Subject = "Quotation",
                Creator = model.ApplicationVersionText,
                Producer = model.ApplicationVersionText,
                Creation = model.GeneratedAtUtc.UtcDateTime,
                Modified = model.GeneratedAtUtc.UtcDateTime,
            };

            using var document = SKDocument.CreatePdf(wstream, metadata)
                ?? throw new InvalidOperationException("SkiaSharp could not open a PDF document for the quotation sheet.");

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

    private static List<DocumentTemplate.PagePlan> Layout(QuotationSheetModel model)
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };

        DocumentTemplate.AddHeaderBand(
            state, measure, "QUOTATION",
            $"{model.Reference}  ·  {FormatDate(model.QuoteDate)}  ·  {model.Status}");
        DocumentTemplate.AddGap(state, 4f);

        DocumentTemplate.AddWrappedLine(state, measure, model.IssuerName, DocumentTemplate.HeadingSize, bold: true, color: DocumentTemplate.Ink900);
        DocumentTemplate.AddWrappedLine(state, measure, $"Project: {model.ProjectCode} — {model.ProjectName}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure, $"Client: {model.Client}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(
            state, measure,
            $"Reference: {model.Reference}  •  Date: {FormatDate(model.QuoteDate)}  •  Valid {model.ValidityDays} day(s), until {FormatDate(model.QuoteDate.AddDays(model.ValidityDays))}",
            DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure, $"Status: {model.Status}  •  Currency: {model.Currency}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddRule(state);

        DocumentTemplate.AddHeading(state, measure, "Lines");
        if (model.Lines.Count == 0)
        {
            DocumentTemplate.AddWrappedLine(state, measure, "No lines recorded.", DocumentTemplate.BodySize, bold: false);
        }
        else
        {
            DocumentTemplate.AddTable(
                state, measure,
                headers: ["Description", "Hours", "Rate", "Amount"],
                widths: [DocumentTemplate.ContentWidth * 0.52f, DocumentTemplate.ContentWidth * 0.12f, DocumentTemplate.ContentWidth * 0.18f, DocumentTemplate.ContentWidth * 0.18f],
                rows: [.. model.Lines.Select(l => new[] { l.Description, l.Hours ?? "—", l.Rate ?? "—", l.Amount })],
                columnAligns: [SKTextAlign.Left, SKTextAlign.Right, SKTextAlign.Right, SKTextAlign.Right]);
        }
        DocumentTemplate.AddGap(state, 4f);

        DocumentTemplate.AddWrappedLine(state, measure, $"Total {model.Total}", DocumentTemplate.HeadingSize, bold: true, SKTextAlign.Right, DocumentTemplate.Ink900);
        DocumentTemplate.AddGap(state, 10f);

        DocumentTemplate.AddHeading(state, measure, "Terms");
        DocumentTemplate.AddWrappedLine(state, measure, string.IsNullOrWhiteSpace(model.Terms) ? "No terms recorded." : model.Terms, DocumentTemplate.BodySize, bold: false);

        return state.Pages;
    }

    private static string FormatFooterDetail(QuotationSheetModel model) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} · {1} · Generated {2:yyyy-MM-dd HH:mm} UTC",
        model.ApplicationVersionText, model.Reference, model.GeneratedAtUtc.UtcDateTime);

    private static string FormatDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
