using System.Globalization;
using System.Text;
using SkiaSharp;

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
/// `ADR-0152`, Product Owner comment item 9) — the same SkiaSharp
/// technique <see cref="Tempest.Desktop.IssueSheets.IssueSheetRenderer"/>
/// already established: <see cref="SKDocument.CreatePdf"/>, a two-phase
/// measure-then-draw layout using nothing but <see cref="SKPaint.MeasureText(string)"/>,
/// and <see cref="SKTypeface.Default"/> throughout (that class's own
/// remarks on why: a formal, regenerated document does not depend on
/// brand type, and the platform default face needs no running Avalonia
/// application to open).
/// </summary>
/// <remarks>
/// <b>Duplicated, not shared, layout machinery.</b> <c>IssueSheetRenderer</c>'s
/// own layout helpers (<c>AddWrappedLine</c>, <c>AddTable</c>, <c>WrapText</c>,
/// and so on) are <see langword="private"/> and that class carries its own
/// well-tested pixel-position assertions
/// (<c>tests/Tempest.Desktop.Tests/Evidence/IssueSheetRendererTests.cs</c>);
/// extracting a shared base class would mean editing a file outside this
/// Work Package's own "files you own" list for no reason the brief names.
/// This class instead re-implements the identical approach — same
/// constants (A4 at 20 mm margins, the same type sizes), same two-phase
/// plan-then-render shape — over quote-shaped content, exactly as the
/// brief's own kill switch permits ("the same approach", not the same
/// class) and forbids only a second PDF library, never a second renderer
/// class using the first one's own technique.
/// </remarks>
public sealed class QuotationSheetRenderer
{
    private const float PointsPerMillimetre = 72f / 25.4f;
    private const float PageWidth = 210f * PointsPerMillimetre;
    private const float PageHeight = 297f * PointsPerMillimetre;
    private const float Margin = 20f * PointsPerMillimetre;

    private const float ContentLeft = Margin;
    private const float ContentRight = PageWidth - Margin;
    private const float ContentWidth = ContentRight - ContentLeft;

    /// <summary>The band reserved at the foot of every page for the rule and the footer's own single line of text — inside the 20 mm margin, never below it.</summary>
    private const float FooterReserve = 24f;
    private const float ContentBottom = PageHeight - Margin - FooterReserve;

    private const float TitleSize = 16f;
    private const float HeadingSize = 11f;
    private const float BodySize = 9.5f;
    private const float FooterSize = 7.5f;
    private const float LineLeading = 1.4f;
    private const float CellPaddingX = 4f;
    private const float CellPaddingY = 3f;

    /// <summary>Renders <paramref name="model"/> as a PDF.</summary>
    public ReadOnlyMemory<byte> Render(QuotationSheetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var pages = Layout(model);
        AppendFooters(pages, model);

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

            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColors.Black,
                Typeface = SKTypeface.Default,
            };
            using var linePaint = new SKPaint
            {
                IsAntialias = true,
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
            };

            foreach (var page in pages)
            {
                var canvas = document.BeginPage(PageWidth, PageHeight);
                canvas.Clear(SKColors.White);

                foreach (var line in page.Lines)
                {
                    linePaint.StrokeWidth = line.StrokeWidth;
                    canvas.DrawLine(line.X1, line.Y, line.X2, line.Y, linePaint);
                }

                foreach (var text in page.Texts)
                {
                    textPaint.TextSize = text.Size;
                    textPaint.FakeBoldText = text.Bold;
                    textPaint.TextAlign = text.Align;
                    canvas.DrawText(text.Text, text.X, text.Y, textPaint);
                }

                document.EndPage();
            }

            document.Close();
        }

        return stream.ToArray();
    }

    /// <summary>One text run, at its own absolute page coordinates (the baseline).</summary>
    private readonly record struct TextItem(float X, float Y, string Text, float Size, bool Bold, SKTextAlign Align);

    /// <summary>One horizontal rule, spanning <paramref name="X1"/> to <paramref name="X2"/> at <paramref name="Y"/>.</summary>
    private readonly record struct LineItem(float X1, float X2, float Y, float StrokeWidth);

    /// <summary>One page's own plan: every text run and rule it carries, in draw order.</summary>
    private sealed class PagePlan
    {
        public List<TextItem> Texts { get; } = [];
        public List<LineItem> Lines { get; } = [];
    }

    /// <summary>The layout cursor threaded through every <c>Add*</c> helper below — which page is current, and how far down it the next line goes.</summary>
    private sealed class LayoutState
    {
        public required List<PagePlan> Pages { get; init; }
        public required PagePlan Page { get; set; }
        public float Y { get; set; }
    }

    private static List<PagePlan> Layout(QuotationSheetModel model)
    {
        var firstPage = new PagePlan();
        var state = new LayoutState { Pages = [firstPage], Page = firstPage, Y = Margin };

        using var measure = new SKPaint { Typeface = SKTypeface.Default };

        AddWrappedLine(state, measure, "QUOTATION", TitleSize, bold: true, SKTextAlign.Center);
        AddGap(state, 4f);
        AddRule(state);

        AddWrappedLine(state, measure, model.IssuerName, HeadingSize, bold: true);
        AddWrappedLine(state, measure, $"Project: {model.ProjectCode} — {model.ProjectName}", BodySize, bold: false);
        AddWrappedLine(state, measure, $"Client: {model.Client}", BodySize, bold: false);
        AddWrappedLine(
            state, measure,
            $"Reference: {model.Reference}  •  Date: {FormatDate(model.QuoteDate)}  •  Valid {model.ValidityDays} day(s), until {FormatDate(model.QuoteDate.AddDays(model.ValidityDays))}",
            BodySize, bold: false);
        AddWrappedLine(state, measure, $"Status: {model.Status}  •  Currency: {model.Currency}", BodySize, bold: false);
        AddGap(state, 4f);
        AddRule(state);

        AddHeading(state, measure, "Lines");
        if (model.Lines.Count == 0)
        {
            AddWrappedLine(state, measure, "No lines recorded.", BodySize, bold: false);
        }
        else
        {
            AddTable(
                state, measure,
                headers: ["Description", "Hours", "Rate", "Amount"],
                widths: [ContentWidth * 0.52f, ContentWidth * 0.12f, ContentWidth * 0.18f, ContentWidth * 0.18f],
                rows: [.. model.Lines.Select(l => new[] { l.Description, l.Hours ?? "—", l.Rate ?? "—", l.Amount })]);
        }
        AddGap(state, 4f);

        AddWrappedLine(state, measure, $"Total {model.Total}", HeadingSize, bold: true, SKTextAlign.Right);
        AddGap(state, 10f);

        AddHeading(state, measure, "Terms");
        AddWrappedLine(state, measure, string.IsNullOrWhiteSpace(model.Terms) ? "No terms recorded." : model.Terms, BodySize, bold: false);

        return state.Pages;
    }

    private static void AppendFooters(List<PagePlan> pages, QuotationSheetModel model)
    {
        var left = string.Format(
            CultureInfo.InvariantCulture,
            "{0} · {1} · Generated {2:yyyy-MM-dd HH:mm} UTC",
            model.ApplicationVersionText,
            model.Reference,
            model.GeneratedAtUtc.UtcDateTime);

        var total = pages.Count;
        var ruleY = PageHeight - Margin - FooterReserve + 8f;
        var baseline = ruleY + FooterSize + 4f;

        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            page.Lines.Add(new LineItem(ContentLeft, ContentRight, ruleY, 0.75f));
            page.Texts.Add(new TextItem(ContentLeft, baseline, left, FooterSize, false, SKTextAlign.Left));
            page.Texts.Add(new TextItem(
                ContentRight, baseline,
                string.Format(CultureInfo.InvariantCulture, "Page {0} of {1}", i + 1, total),
                FooterSize, false, SKTextAlign.Right));
        }
    }

    private static void NewPage(LayoutState state)
    {
        var page = new PagePlan();
        state.Pages.Add(page);
        state.Page = page;
        state.Y = Margin;
    }

    /// <summary>Starts a new page if <paramref name="height"/> would not fit above <see cref="ContentBottom"/>. Returns whether it did.</summary>
    private static bool EnsureSpace(LayoutState state, float height)
    {
        if (state.Y + height <= ContentBottom)
            return false;

        NewPage(state);
        return true;
    }

    private static void AddGap(LayoutState state, float height) => state.Y += height;

    private static void AddRule(LayoutState state)
    {
        EnsureSpace(state, 4f);
        state.Page.Lines.Add(new LineItem(ContentLeft, ContentRight, state.Y, 0.75f));
        state.Y += 6f;
    }

    private static void AddHeading(LayoutState state, SKPaint measure, string text)
    {
        AddWrappedLine(state, measure, text, HeadingSize, bold: true);
        state.Y += 2f;
    }

    private static void AddWrappedLine(LayoutState state, SKPaint measure, string text, float size, bool bold, SKTextAlign align = SKTextAlign.Left)
    {
        measure.TextSize = size;
        var lineHeight = size * LineLeading;

        foreach (var line in WrapText(text, ContentWidth, measure))
        {
            EnsureSpace(state, lineHeight);
            var x = align switch
            {
                SKTextAlign.Right => ContentRight,
                SKTextAlign.Center => (ContentLeft + ContentRight) / 2f,
                _ => ContentLeft,
            };
            state.Page.Texts.Add(new TextItem(x, state.Y + size, line, size, bold, align));
            state.Y += lineHeight;
        }
    }

    private static void AddTable(
        LayoutState state, SKPaint measure, string[] headers, float[] widths, IReadOnlyList<string[]> rows)
    {
        var colX = new float[headers.Length];
        colX[0] = ContentLeft;
        for (var i = 1; i < headers.Length; i++)
            colX[i] = colX[i - 1] + widths[i - 1];

        void DrawHeaderRow()
        {
            measure.TextSize = BodySize;
            var lineHeight = BodySize * LineLeading;
            var rowHeight = lineHeight + CellPaddingY * 2;
            EnsureSpace(state, rowHeight);

            var baseline = state.Y + CellPaddingY + BodySize;
            for (var i = 0; i < headers.Length; i++)
                state.Page.Texts.Add(new TextItem(colX[i] + CellPaddingX, baseline, headers[i], BodySize, true, SKTextAlign.Left));

            state.Y += rowHeight;
            state.Page.Lines.Add(new LineItem(ContentLeft, ContentRight, state.Y, 0.75f));
            state.Y += 2f;
        }

        DrawHeaderRow();

        foreach (var row in rows)
        {
            measure.TextSize = BodySize;
            var lineHeight = BodySize * LineLeading;

            var wrapped = new List<string>[headers.Length];
            var maxLines = 1;
            for (var i = 0; i < headers.Length; i++)
            {
                wrapped[i] = WrapText(row[i], widths[i] - CellPaddingX * 2, measure);
                maxLines = Math.Max(maxLines, wrapped[i].Count);
            }

            var rowHeight = maxLines * lineHeight + CellPaddingY * 2;

            // A row header repeats on the page it lands on, so a table
            // that overflows never leaves a continuation page's columns
            // unlabelled.
            if (EnsureSpace(state, rowHeight))
                DrawHeaderRow();

            var rowTop = state.Y + CellPaddingY;
            for (var i = 0; i < headers.Length; i++)
            {
                for (var lineIndex = 0; lineIndex < wrapped[i].Count; lineIndex++)
                {
                    var baseline = rowTop + BodySize + lineIndex * lineHeight;
                    state.Page.Texts.Add(new TextItem(colX[i] + CellPaddingX, baseline, wrapped[i][lineIndex], BodySize, false, SKTextAlign.Left));
                }
            }

            state.Y += rowHeight;
            state.Page.Lines.Add(new LineItem(ContentLeft, ContentRight, state.Y, 0.4f));
        }
    }

    /// <summary>Word-wraps <paramref name="text"/> to <paramref name="maxWidth"/> using <paramref name="measure"/>'s own current <see cref="SKPaint.TextSize"/>. A single word wider than <paramref name="maxWidth"/> is left to overflow its line rather than broken mid-word.</summary>
    private static List<string> WrapText(string text, float maxWidth, SKPaint measure)
    {
        if (string.IsNullOrEmpty(text))
            return [string.Empty];

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return [string.Empty];

        List<string> lines = [];
        var current = new StringBuilder();

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (current.Length == 0 || measure.MeasureText(candidate) <= maxWidth)
            {
                current.Clear();
                current.Append(candidate);
            }
            else
            {
                lines.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
        }

        if (current.Length > 0)
            lines.Add(current.ToString());

        return lines;
    }

    private static string FormatDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
