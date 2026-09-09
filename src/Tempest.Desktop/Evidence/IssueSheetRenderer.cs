using System.Globalization;
using System.Text;
using SkiaSharp;
using Tempest.Core.Evidence;
using Tempest.Workspace.Evidence;

namespace Tempest.Desktop.Evidence;

/// <summary>
/// Renders an <see cref="IssueSheetModel"/> as an A4 PDF with SkiaSharp's
/// own PDF document backend (`ADR-0148`, `WP 18.2B`, Product Owner
/// 2026-09-09) — the same <c>SkiaSharp</c> package the desktop build
/// already ships transitively under <c>PDFtoImage</c> (`WP 10.0B`'s own
/// PDF viewer), so no package reference is added here.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two-phase, not draw-as-you-go.</b> <see cref="Layout"/> computes a
/// complete, page-by-page plan — every text run and rule line, at its own
/// absolute page coordinates — using nothing but <see cref="SKPaint.MeasureText(string)"/>
/// (which needs no live canvas or document). Only once the whole plan
/// exists, and the true page count is therefore known, does
/// <see cref="AppendFooters"/> stamp "Page n of N" onto every page, and
/// only then does <see cref="Render"/> open a real <see cref="SKDocument"/>
/// and replay the plan onto it. This is what lets the footer report the
/// final page count truthfully, without a second, separate measuring pass
/// that could disagree with the one that actually draws.
/// </para>
/// <para>
/// <b>Determinism.</b> Nothing here reads the clock, a random source, or
/// any file the platform did not already embed. The PDF's own
/// <c>Creation</c>/<c>Modified</c> metadata are set from
/// <see cref="IssueSheetModel.GeneratedAtUtc"/>, so
/// <see cref="Render"/> called twice on the same model writes the same
/// bytes.
/// </para>
/// <para>
/// <b>Fonts.</b> <see cref="SKTypeface.Default"/> throughout, deliberately
/// — not the Tempest Engineering Design System's own embedded Chakra
/// Petch/Space Mono/Inter faces, which are packaged as Avalonia
/// <c>avares://</c> resources and would need a running Avalonia
/// application (an <see cref="Avalonia.Platform.IAssetLoader"/>) to open,
/// a dependency this renderer would otherwise have no reason to carry.
/// Legibility of a formal, regenerated document does not depend on brand
/// type; the platform default face renders identically wherever this
/// process runs.
/// </para>
/// </remarks>
public sealed class IssueSheetRenderer : IIssueSheetRenderer
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

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Render(IssueSheetModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var pages = Layout(model);
        AppendFooters(pages, model);

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

    private static List<PagePlan> Layout(IssueSheetModel model)
    {
        var firstPage = new PagePlan();
        var state = new LayoutState { Pages = [firstPage], Page = firstPage, Y = Margin };

        using var measure = new SKPaint { Typeface = SKTypeface.Default };

        AddWrappedLine(state, measure, "ISSUE SHEET", TitleSize, bold: true, SKTextAlign.Center);
        AddGap(state, 4f);
        AddRule(state);

        AddWrappedLine(state, measure, $"Project: {model.ProjectCode} — {model.ProjectName}", BodySize, bold: false);
        AddWrappedLine(state, measure, $"Client: {model.Client}", BodySize, bold: false);
        AddWrappedLine(state, measure, $"Evidence: {model.EvidenceReference ?? "—"} — {model.Title}", BodySize, bold: false);
        AddWrappedLine(state, measure, $"Classification: {Humanize(model.Classification.ToString())}", BodySize, bold: false);
        AddWrappedLine(state, measure, $"Revision {model.Revision} · Issue {model.IssueReference} — {FormatDate(model.IssueDateUtc)}", BodySize, bold: false);
        AddGap(state, 4f);
        AddRule(state);

        AddHeading(state, measure, "Review");
        AddWrappedLine(state, measure, $"Author: {model.AuthorDisplayName} — {FormatDate(model.AuthorDateUtc)}", BodySize, bold: false);
        var checkerLine = model.CheckerPrincipalDisplayName is { Length: > 0 } principal
            ? $"Checker: {model.CheckerName}, {model.CheckerOrganisation} ({principal}) — {FormatDate(model.CheckDateUtc)}"
            : $"Checker: {model.CheckerName}, {model.CheckerOrganisation} — {FormatDate(model.CheckDateUtc)}";
        AddWrappedLine(state, measure, checkerLine, BodySize, bold: false);
        AddWrappedLine(state, measure, $"Outcome: {Humanize(model.Outcome.ToString())}", BodySize, bold: false);
        AddGap(state, 4f);
        AddRule(state);

        AddHeading(state, measure, "Citations");
        if (model.Citations.Count == 0)
        {
            AddWrappedLine(state, measure, "No citations recorded.", BodySize, bold: false);
        }
        else
        {
            AddTable(
                state, measure,
                headers: ["Library", "Record", "Rev", "Source"],
                widths: [ContentWidth * 0.16f, ContentWidth * 0.22f, ContentWidth * 0.08f, ContentWidth * 0.54f],
                rows: [.. model.Citations.Select(c => new[]
                {
                    c.Library,
                    c.RecordId,
                    c.Revision.ToString(CultureInfo.InvariantCulture),
                    c.SourceCitationSnapshot ?? "—",
                })]);
        }
        AddGap(state, 6f);

        AddHeading(state, measure, "Declared figures");
        if (model.DeclaredFigures.Count == 0)
        {
            AddWrappedLine(state, measure, "No figures declared.", BodySize, bold: false);
        }
        else
        {
            AddTable(
                state, measure,
                headers: ["Name", "Role", "Value"],
                widths: [ContentWidth * 0.35f, ContentWidth * 0.20f, ContentWidth * 0.45f],
                rows: [.. model.DeclaredFigures.Select(f => new[]
                {
                    f.Name,
                    Humanize(f.Role.ToString()),
                    f.Quantity,
                })]);
        }
        AddGap(state, 10f);

        AddHeading(state, measure, "Signatures");
        AddSignatureRow(state, "Prepared", model.AuthorDisplayName, model.AuthorDateUtc);
        AddSignatureRow(state, "Checked", $"{model.CheckerName}, {model.CheckerOrganisation}", model.CheckDateUtc);
        AddSignatureRow(state, "Issued", "—", model.IssueDateUtc);

        return state.Pages;
    }

    private static void AppendFooters(List<PagePlan> pages, IssueSheetModel model)
    {
        var left = string.Format(
            CultureInfo.InvariantCulture,
            "{0} · Sequence {1} · Generated {2:yyyy-MM-dd HH:mm} UTC",
            model.ApplicationVersionText,
            model.StoreSequence,
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

    private static void AddSignatureRow(LayoutState state, string role, string name, DateTimeOffset date)
    {
        AddGap(state, 14f);
        EnsureSpace(state, 30f);
        state.Page.Lines.Add(new LineItem(ContentLeft, ContentLeft + 260f, state.Y, 0.75f));
        state.Y += 4f;

        var baseline = state.Y + BodySize;
        state.Page.Texts.Add(new TextItem(ContentLeft, baseline, $"{role} — {name}", BodySize, false, SKTextAlign.Left));
        state.Page.Texts.Add(new TextItem(
            ContentRight, baseline, $"Date: {FormatDate(date)}", BodySize, false, SKTextAlign.Right));
        state.Y += BodySize * LineLeading;
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
