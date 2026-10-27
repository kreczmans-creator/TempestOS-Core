using System.Text;
using SkiaSharp;

namespace Tempest.Desktop.Documents;

/// <summary>
/// The organisation's own identity — the fields a document's footer shows
/// (`WP 20.10G`, PO finding D4, `TD-182`): a legal name, and the optional
/// details a small business card also carries. Editable in Settings →
/// Organisation (<see cref="Tempest.Desktop.OrganisationIdentitySettings"/>),
/// read by <see cref="DocumentTemplate"/> at render time — never a
/// constant baked into a renderer.
/// </summary>
/// <param name="LegalName">The organisation's own registered name. Never empty — <see cref="TempestDefaults"/> supplies the Product Owner's own templates' own default when nothing has been entered.</param>
/// <param name="CompanyNumber">The companies-register number, or <see langword="null"/> when none is recorded.</param>
/// <param name="Website">The public website, or <see langword="null"/>.</param>
/// <param name="AddressLine1">The first address line, or <see langword="null"/>.</param>
/// <param name="AddressLine2">The second address line, or <see langword="null"/>.</param>
/// <param name="Email">A contact email, or <see langword="null"/>.</param>
/// <param name="Phone">A contact phone number, or <see langword="null"/>.</param>
/// <param name="BankSortCode">The bank account's own sort code, for the invoice's own payment-details section (`WP 21.2A`). <see langword="null"/> when none is recorded.</param>
/// <param name="BankAccountNumber">The bank account number. <see langword="null"/> when none is recorded.</param>
/// <param name="BankAccountName">The account holder's own name, where it differs from <see cref="LegalName"/> enough to state separately. <see langword="null"/> when none is recorded.</param>
/// <param name="BankIban">The IBAN, for an international client. <see langword="null"/> when none is recorded.</param>
/// <param name="VatNumber">The VAT registration number (`v0.24.0` U1, design §8 "PDF identity": Xero's <c>TaxNumber</c> when a reading of Xero exists). <see langword="null"/> when none is recorded — the footer then shows no VAT number, as before.</param>
public sealed record OrganisationIdentity(
    string LegalName,
    string? CompanyNumber,
    string? Website,
    string? AddressLine1,
    string? AddressLine2,
    string? Email,
    string? Phone,
    string? BankSortCode = null,
    string? BankAccountNumber = null,
    string? BankAccountName = null,
    string? BankIban = null,
    string? VatNumber = null)
{
    /// <summary>
    /// The Tempest Design Engineering Ltd defaults — transcribed verbatim
    /// from the Product Owner's own design system templates
    /// (`templates/letterhead/Letterhead.dc.html`,
    /// `templates/invoice/Invoice.dc.html`, `templates/cost-estimate/CostEstimate.dc.html`'s
    /// own footer slot: "Tempest Design Engineering Ltd · Company No.
    /// 17349874" / "www.tempest-engineering.co.uk"). Every Settings →
    /// Organisation field pre-fills from this until a user changes it. No
    /// bank details — the design system's own templates name no real
    /// account to default to, and inventing one would be worse than
    /// leaving the invoice's own payment-details section honestly blank
    /// until a user enters real ones (`WP 21.2A`).
    /// </summary>
    public static OrganisationIdentity TempestDefaults { get; } = new(
        LegalName: "Tempest Design Engineering Ltd",
        CompanyNumber: "17349874",
        Website: "www.tempest-engineering.co.uk",
        AddressLine1: null,
        AddressLine2: null,
        Email: null,
        Phone: null);

    /// <summary>Whether any bank detail at all is recorded — what an invoice renderer checks before drawing a "Payment details" section rather than drawing an empty one.</summary>
    public bool HasBankDetails =>
        !string.IsNullOrWhiteSpace(BankSortCode) || !string.IsNullOrWhiteSpace(BankAccountNumber)
        || !string.IsNullOrWhiteSpace(BankAccountName) || !string.IsNullOrWhiteSpace(BankIban);

    /// <summary>
    /// The footer's own left-hand text — legal name, then "Company No.
    /// {n}", "VAT No. {n}" (`v0.24.0` U1) and any address lines/email/phone that are actually recorded,
    /// each joined with " · " — the same left-hand shape the design
    /// system's own footer slot uses (name, then company number).
    /// </summary>
    public string FooterLeft()
    {
        List<string> parts = [LegalName];

        if (!string.IsNullOrWhiteSpace(CompanyNumber))
            parts.Add($"Company No. {CompanyNumber}");
        if (!string.IsNullOrWhiteSpace(VatNumber))
            parts.Add($"VAT No. {VatNumber}");
        if (!string.IsNullOrWhiteSpace(AddressLine1))
            parts.Add(AddressLine1!);
        if (!string.IsNullOrWhiteSpace(AddressLine2))
            parts.Add(AddressLine2!);
        if (!string.IsNullOrWhiteSpace(Email))
            parts.Add(Email!);
        if (!string.IsNullOrWhiteSpace(Phone))
            parts.Add(Phone!);

        return string.Join(" · ", parts);
    }

    /// <summary>The footer's own right-hand text — the website, or empty when none is recorded (never <see langword="null"/>, so a caller can draw it unconditionally).</summary>
    public string FooterRight() => Website ?? string.Empty;
}

/// <summary>
/// One colour token the Tempest Engineering Design System reference names,
/// verbatim (`docs/design/Tempest Engineering Design System Reference.md`;
/// the pack's own `tokens/colors.css`) — the catalogue a fidelity test
/// checks a supplied design-system HTML template against once one exists
/// in the repository.
/// </summary>
public readonly record struct DesignSystemColorToken(string Name, string Hex);

/// <summary>
/// The shared page grammar every Tempest document template renders
/// through (`WP 20.10G`, PO finding D4, `TD-182`) — page geometry, the
/// header band (wordmark, document type, reference), body type roles,
/// the colour tokens the design system reference names verbatim, the
/// footer (organisation identity, an exporter-supplied detail line, "Page
/// n of m"), and the two-phase measure-then-draw layout plan
/// <c>IssueSheetRenderer</c> (`WP 18.2B`) first established and
/// <c>QuotationSheetRenderer</c> (`WP 19.5B`) had duplicated rather than
/// shared (`TD-182`) — lifted here once, so both renderers, and the next
/// document a caller adds, share one layout engine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mapped to the Product Owner's own templates, not merely the
/// grammar doc.</b> `WP 20.10G` (2/n) read the design system export that
/// landed in the repository at `WP 20.10` (`docs/design/templates/`,
/// `docs/design/system/tokens/*.css`): the header band's wordmark +
/// document-type eyebrow + mono reference line under a 2px
/// <c>indigo-600</c> rule, and the footer's organisation-identity
/// left/right split under a 1px <c>ink-a08</c> hairline, are the exact
/// shapes <c>templates/letterhead/Letterhead.dc.html</c>,
/// <c>templates/invoice/Invoice.dc.html</c> and
/// <c>templates/cost-estimate/CostEstimate.dc.html</c> all three use
/// verbatim; the table header shading (<c>paper-100</c>), row hairlines
/// (<c>ink-a08</c>/<c>ink-a14</c>) and the totals block's <c>indigo-600</c>
/// top rule over a <c>paper-100</c> fill come from the same two files'
/// own line-item tables. See this Work Package's own report for the exact
/// per-renderer mapping.
/// </para>
/// <para>
/// <b>Deliberately still <see cref="SKTypeface.Default"/>, and still no
/// embedded logo bitmap.</b> `IssueSheetRenderer`'s own remarks recorded
/// why at `WP 18.2B`: a formal, regenerated document's legibility does
/// not depend on brand type, and the platform default face needs no
/// running Avalonia application (an <c>IAssetLoader</c>) to open. That
/// reasoning still holds, and this Work Package could not have reversed
/// it tonight regardless — the design system's own font files
/// (`docs/design/system/assets/fonts/*.ttf`) and lockup bitmaps
/// (`docs/design/system/assets/logo/*.png`) are not present in this
/// worktree (the branch that carries them, `release/v0.20.0` at
/// `88311649`, could not be merged into this one — see the report's own
/// "deviations"). What is real: every colour, every hairline weight and
/// alpha, the header/footer/table grammar, and the numeric-column,
/// mono-style right alignment the reference's own type section calls
/// for. The wordmark is a plain "TEMPEST"/"OS" text pair, ink-900 and
/// cyan-500 exactly as the reference's own Logo section states ("OS
/// always cyan"), standing in for the supplied artwork until this
/// renderer can load it.
/// </para>
/// </remarks>
public static class DocumentTemplate
{
    // ------------------------------------------------------------
    // Page geometry — A4, 20mm margins (`ADR-0148`, unchanged).
    // ------------------------------------------------------------

    public const float PointsPerMillimetre = 72f / 25.4f;
    public const float PageWidth = 210f * PointsPerMillimetre;
    public const float PageHeight = 297f * PointsPerMillimetre;
    public const float Margin = 20f * PointsPerMillimetre;

    public const float ContentLeft = Margin;
    public const float ContentRight = PageWidth - Margin;
    public const float ContentWidth = ContentRight - ContentLeft;

    /// <summary>The band reserved at the foot of every page for the footer's rule and its own two lines (organisation identity; exporter detail and "Page n of m") — inside the 20mm margin, never below it.</summary>
    public const float FooterReserve = 34f;
    public const float ContentBottom = PageHeight - Margin - FooterReserve;

    // ------------------------------------------------------------
    // Landscape geometry (`WP 21.2A`) — the drawing register and the
    // progress report render A4 landscape (their own template folders'
    // grammar); every other document stays the portrait geometry above,
    // untouched. A simple axis swap of the same A4/20mm-margin page: every
    // helper below reads geometry off `LayoutState.Landscape` rather than
    // these constants directly, so a portrait `LayoutState` (every caller
    // that does not pass `landscape: true` to `BeginLayout`) computes the
    // identical values the constants above always have — this is additive,
    // never a breaking change to `QuotationSheetRenderer`/`IssueSheetRenderer`
    // or their own tests.
    // ------------------------------------------------------------

    public const float LandscapePageWidth = PageHeight;
    public const float LandscapePageHeight = PageWidth;
    public const float LandscapeContentRight = LandscapePageWidth - Margin;
    public const float LandscapeContentWidth = LandscapeContentRight - ContentLeft;
    public const float LandscapeContentBottom = LandscapePageHeight - Margin - FooterReserve;

    // ------------------------------------------------------------
    // Type roles — sizes only; see the class remarks for why the glyphs
    // themselves stay the platform default face.
    // ------------------------------------------------------------

    public const float WordmarkSize = 14f;
    public const float HeaderEyebrowSize = 9f;
    public const float HeaderReferenceSize = 7.5f;
    public const float TitleSize = 16f;
    public const float HeadingSize = 11f;
    public const float BodySize = 9.5f;
    public const float CaptionSize = 7.5f;
    public const float LineLeading = 1.4f;
    public const float CellPaddingX = 4f;
    public const float CellPaddingY = 3f;

    // ------------------------------------------------------------
    // Colour tokens, verbatim (`docs/design/Tempest Engineering Design
    // System Reference.md`; the pack's own `tokens/colors.css`).
    // ------------------------------------------------------------

    /// <summary>paper-050 — "the paper page" (the reference's own role for this token).</summary>
    public static readonly SKColor PaperPage = SKColor.Parse("#F5F6FA");

    /// <summary>paper-100 — a sunken/panel fill on paper (a table header row, the totals block).</summary>
    public static readonly SKColor PaperPanel = SKColor.Parse("#EFF0F5");

    /// <summary>ink-900 — headings on paper.</summary>
    public static readonly SKColor Ink900 = SKColor.Parse("#16181D");

    /// <summary>slate-700 — body text on paper.</summary>
    public static readonly SKColor Slate700 = SKColor.Parse("#31343F");

    /// <summary>slate-600 — muted text on paper (captions, the footer).</summary>
    public static readonly SKColor Slate600 = SKColor.Parse("#4B5160");

    /// <summary>indigo-600 — the paper theme's own accent; the header band's rule and every eyebrow label.</summary>
    public static readonly SKColor Indigo600 = SKColor.Parse("#1C2D97");

    /// <summary>cyan-500 — "OS always cyan" (the reference's own Logo section rule); the wordmark's second word only.</summary>
    public static readonly SKColor Cyan500 = SKColor.Parse("#40A2CE");

    /// <summary>ink-a08 — ink-900 at 8% alpha, the reference's own "8% ink alpha on paper" hairline.</summary>
    public static readonly SKColor Hairline = Ink900.WithAlpha(20);

    /// <summary>ink-a14 — ink-900 at 14% alpha, the reference's own strong hairline (a table header's own underline).</summary>
    public static readonly SKColor HairlineStrong = Ink900.WithAlpha(36);

    /// <summary>
    /// Every colour token the design system reference table names,
    /// verbatim — what <c>DesignSystemTemplateFidelityTests</c> checks a
    /// supplied <c>tokens/colors.css</c> against.
    /// </summary>
    public static IReadOnlyList<DesignSystemColorToken> ReferenceColourTokens { get; } =
    [
        new("navy-900", "#070915"), new("navy-800", "#0b0e1e"), new("navy-700", "#111527"), new("navy-600", "#181d33"),
        new("paper-050", "#f5f6fa"), new("paper-100", "#eff0f5"),
        new("slate-400", "#a2a5af"), new("slate-500", "#82848e"), new("slate-600", "#4b5160"), new("slate-700", "#31343f"),
        new("ink-900", "#16181d"), new("indigo-600", "#1c2d97"),
        new("cyan-500", "#40a2ce"), new("cyan-400", "#68bde2"), new("cyan-600", "#2b7fa5"),
        new("violet-500", "#6c29d9"), new("green-500", "#12b981"), new("amber-500", "#f5a524"), new("red-500", "#e5484d"),
    ];

    /// <summary>The reference's own three type families, by role — display (Chakra Petch), body (Inter), mono (Space Mono).</summary>
    public static IReadOnlyList<string> ReferenceTypeNames { get; } = ["Chakra Petch", "Inter", "Space Mono"];

    // ------------------------------------------------------------
    // The shared two-phase layout plan — lifted from IssueSheetRenderer
    // (`WP 18.2B`), unchanged in technique (`TD-182`).
    // ------------------------------------------------------------

    /// <summary>One text run, at its own absolute page coordinates (the baseline), in its own colour and <see cref="DocumentFontRole"/> (`WP 21.2A`; <see cref="DocumentFontRole.Body"/> — the platform default before this Work Package — when a caller does not say otherwise, so every pre-existing positional construction of this record keeps compiling and rendering exactly as before).</summary>
    public readonly record struct TextRun(float X, float Y, string Text, float Size, bool Bold, SKTextAlign Align, SKColor Color, DocumentFontRole FontRole = DocumentFontRole.Body, bool Symbols = false);

    /// <summary>One horizontal rule, spanning <paramref name="X1"/> to <paramref name="X2"/> at <paramref name="Y"/>, in its own colour.</summary>
    public readonly record struct RuleRun(float X1, float X2, float Y, float StrokeWidth, SKColor Color);

    /// <summary>One image, drawn into the rectangle <paramref name="X"/>/<paramref name="Y"/>/<paramref name="Width"/>/<paramref name="Height"/> (`WP 21.2A` — today, only the header band's own logo lockup, drawn by <see cref="AddHeaderBand"/>; every renderer's own draw loop reads this list through <see cref="RenderPdf"/>, the one place a bitmap is actually decoded and drawn).</summary>
    public readonly record struct ImageRun(float X, float Y, float Width, float Height);

    /// <summary>One page's own plan: every text run, rule and image it carries, in draw order (rules and images beneath text — <see cref="RenderPdf"/>'s own fixed draw order).</summary>
    /// <summary>One vector drawing (a calculation's reference diagram): <paramref name="Draw"/> paints in its own units, placed at <paramref name="X"/>/<paramref name="Y"/> and scaled by <paramref name="Scale"/>. Its words are text runs, so they are subset, searchable and selectable like every other.</summary>
    public readonly record struct DrawingRun(float X, float Y, float Scale, Action<SKCanvas> Draw);

    public sealed class PagePlan
    {
        public List<TextRun> Texts { get; } = [];
        public List<RuleRun> Rules { get; } = [];
        public List<ImageRun> Images { get; } = [];
        public List<DrawingRun> Drawings { get; } = [];
    }

    /// <summary>The layout cursor threaded through every <c>Add*</c> helper below — which page is current, how far down it the next line goes, and (`WP 21.2A`) which of the two page geometries (<see cref="Landscape"/>) this document lays out against.</summary>
    public sealed class LayoutState
    {
        public required List<PagePlan> Pages { get; init; }
        public required PagePlan Page { get; set; }
        public float Y { get; set; }

        /// <summary>Whether this layout is A4 landscape (the drawing register, the progress report) rather than the platform's original A4 portrait. Set once, by <see cref="BeginLayout"/>, and read by every geometry-aware helper below.</summary>
        public bool Landscape { get; init; }

        /// <summary>This layout's own page width — <see cref="LandscapePageWidth"/> or <see cref="PageWidth"/>, by <see cref="Landscape"/>.</summary>
        public float PageWidth => Landscape ? DocumentTemplate.LandscapePageWidth : DocumentTemplate.PageWidth;

        /// <summary>This layout's own page height — <see cref="LandscapePageHeight"/> or <see cref="PageHeight"/>, by <see cref="Landscape"/>.</summary>
        public float PageHeight => Landscape ? DocumentTemplate.LandscapePageHeight : DocumentTemplate.PageHeight;

        /// <summary>This layout's own right content edge — <see cref="ContentLeft"/> (the left edge) is identical in both orientations, so no <c>ContentLeft</c> instance property is needed.</summary>
        public float ContentRight => Landscape ? DocumentTemplate.LandscapeContentRight : DocumentTemplate.ContentRight;

        /// <summary>This layout's own content width — what a caller building an <see cref="AddTable"/> <c>widths</c> array should take proportions of.</summary>
        public float ContentWidth => Landscape ? DocumentTemplate.LandscapeContentWidth : DocumentTemplate.ContentWidth;

        /// <summary>This layout's own content bottom — where <see cref="EnsureSpace"/> starts a new page.</summary>
        public float ContentBottom => Landscape ? DocumentTemplate.LandscapeContentBottom : DocumentTemplate.ContentBottom;
    }

    /// <summary>Starts a new layout plan: one page, the cursor at <see cref="Margin"/>. <paramref name="landscape"/> (`WP 21.2A`) — <see langword="false"/>, the default, is the original A4 portrait every renderer used before this Work Package; <see langword="true"/> is A4 landscape (the drawing register, the progress report).</summary>
    public static LayoutState BeginLayout(bool landscape = false)
    {
        var firstPage = new PagePlan();
        return new LayoutState { Pages = [firstPage], Page = firstPage, Y = Margin, Landscape = landscape };
    }

    /// <summary>Starts a new, blank page and moves the cursor onto it — the header band is not repeated; only page 1 carries it, exactly as the pre-`WP 20.10G` renderers' own title line never repeated either.</summary>
    public static void NewPage(LayoutState state)
    {
        var page = new PagePlan();
        state.Pages.Add(page);
        state.Page = page;
        state.Y = Margin;
    }

    /// <summary>Starts a new page if <paramref name="height"/> would not fit above <paramref name="state"/>'s own <see cref="LayoutState.ContentBottom"/>. Returns whether it did.</summary>
    public static bool EnsureSpace(LayoutState state, float height)
    {
        if (state.Y + height <= state.ContentBottom)
            return false;

        NewPage(state);
        return true;
    }

    public static void AddGap(LayoutState state, float height) => state.Y += height;

    /// <summary>A horizontal rule at the current cursor, <paramref name="color"/> defaulting to <see cref="Hairline"/>.</summary>
    public static void AddRule(LayoutState state, SKColor? color = null, float strokeWidth = 0.75f)
    {
        EnsureSpace(state, 4f);
        state.Page.Rules.Add(new RuleRun(ContentLeft, state.ContentRight, state.Y, strokeWidth, color ?? Hairline));
        state.Y += 6f;
    }

    /// <summary>A section heading — bold, <see cref="HeadingSize"/>, <see cref="Ink900"/> (headings on paper), <see cref="DocumentFontRole.Display"/> (`WP 21.2A`; Chakra Petch, the design system's own display face for headings).</summary>
    /// <param name="keepWithNext">The height of what must follow on the same page; by default two body lines. A heading over a table passes <see cref="TableLeadHeight"/>, so it is never left above an empty header row.</param>
    public static void AddHeading(LayoutState state, SKPaint measure, string text, float? keepWithNext = null)
    {
        // Kept with what follows it, so a heading never stands alone at the foot of a page (the showcase journey found one).
        EnsureSpace(state, HeadingSize * LineLeading + 2f + (keepWithNext ?? BodySize * LineLeading * 2));
        AddWrappedLine(state, measure, text, HeadingSize, bold: true, color: Ink900, fontRole: DocumentFontRole.Display);
        state.Y += 2f;
    }

    /// <summary>A word-wrapped line of running text, <paramref name="color"/> defaulting to <see cref="Slate700"/> (body text on paper) and <paramref name="fontRole"/> (`WP 21.2A`) defaulting to <see cref="DocumentFontRole.Body"/> (Inter).</summary>
    public static void AddWrappedLine(
        LayoutState state, SKPaint measure, string text, float size, bool bold, SKTextAlign align = SKTextAlign.Left, SKColor? color = null,
        DocumentFontRole fontRole = DocumentFontRole.Body, bool symbols = false, bool keepTogether = false, float? width = null)
    {
        measure.TextSize = size;
        var lineHeight = size * LineLeading;
        var runColor = color ?? Slate700;
        // Measured in the face the line is drawn in: a mono line measured in the body face ran past the right margin.
        var typeface = measure.Typeface;
        measure.Typeface = DocumentFonts.For(fontRole, bold);
        var lines = WrapText(text, width ?? state.ContentWidth, measure, breakLongWords: true);
        measure.Typeface = typeface;

        // A short block (a Basis line) is not split across a page break: the shots review found its last clause alone on the next page.
        if (keepTogether && lines.Count * lineHeight <= state.ContentBottom - Margin)
            EnsureSpace(state, lines.Count * lineHeight);

        foreach (var line in lines)
        {
            EnsureSpace(state, lineHeight);
            var x = align switch
            {
                SKTextAlign.Right => state.ContentRight,
                SKTextAlign.Center => (ContentLeft + state.ContentRight) / 2f,
                _ => ContentLeft,
            };
            state.Page.Texts.Add(new TextRun(x, state.Y + size, line, size, bold, align, runColor, fontRole, symbols));
            state.Y += lineHeight;
        }
    }

    /// <summary>
    /// A table with a shaded, uppercase, mono-tracked header row (design
    /// system: <see cref="PaperPanel"/> fill, <see cref="Slate600"/> text,
    /// a <see cref="HairlineStrong"/> underline) and body rows separated
    /// by <see cref="Hairline"/>. <paramref name="columnAligns"/> — one
    /// entry per column, defaulting to every column left — lets a caller
    /// right-align a genuinely numeric column, the reference's own
    /// "numeric readouts... in mono" convention (`WP 20.10G`; no
    /// pixel-position test anywhere pins a column's own alignment, so this
    /// is additive, not a breaking change to either renderer's own
    /// existing content). A row header repeats on the page it lands on, so
    /// a table that overflows never leaves a continuation page's columns
    /// unlabelled.
    /// </summary>
    public static void AddTable(
        LayoutState state, SKPaint measure, string[] headers, float[] widths, IReadOnlyList<string[]> rows,
        SKTextAlign[]? columnAligns = null, bool[]? symbolColumns = null)
    {
        var aligns = columnAligns ?? new SKTextAlign[headers.Length];

        var colX = new float[headers.Length];
        colX[0] = ContentLeft;
        for (var i = 1; i < headers.Length; i++)
            colX[i] = colX[i - 1] + widths[i - 1];

        var lineHeight = BodySize * LineLeading;

        void DrawHeaderRow()
        {
            EnsureSpace(state, HeaderRowHeight);

            // The design system's own table header row also carries a
            // paper-100 fill; this renderer draws lines and text only (no
            // filled-rectangle primitive), so the header row is set apart
            // by weight and colour instead — bold, uppercase, indigo-600 —
            // over the same ink-a14 underline the reference's own table
            // header uses.
            var baseline = state.Y + CellPaddingY + CaptionSize;
            for (var i = 0; i < headers.Length; i++)
            {
                var x = CellX(colX[i], widths[i], aligns[i]);
                state.Page.Texts.Add(new TextRun(x, baseline, headers[i].ToUpperInvariant(), CaptionSize, true, aligns[i], Indigo600, DocumentFontRole.Display));
            }

            state.Y += HeaderRowHeight - 2f;
            state.Page.Rules.Add(new RuleRun(ContentLeft, state.ContentRight, state.Y, 0.75f, HairlineStrong));
            state.Y += 2f;
        }

        // The header row never stands alone at the foot of a page: it starts where its first row fits too (or, for a row
        // taller than a page, that row's first lines).
        EnsureSpace(state, TableLeadHeight(measure, widths, rows, aligns, state));
        DrawHeaderRow();

        foreach (var row in rows)
        {
            var wrapped = WrapRow(measure, row, widths, aligns);
            var maxLines = Math.Max(1, wrapped.Max(c => c.Count));

            // A row that fits a page is kept whole, on the next page if need be. A row taller than a page (a long method
            // relation) runs on across pages, so no line of it is lost below the footer.
            if (maxLines * lineHeight <= RowCapacity(state) && EnsureSpace(state, maxLines * lineHeight + CellPaddingY * 2))
                DrawHeaderRow();

            var first = 0;

            while (first < maxLines)
            {
                var fits = (int)Math.Floor((state.ContentBottom - state.Y - CellPaddingY * 2) / lineHeight);
                var pageIsFresh = state.Y <= Margin + HeaderRowHeight + 0.5f;

                if (pageIsFresh)
                    fits = Math.Max(fits, 1);
                else if (fits < Math.Min(KeptRowLines, maxLines - first))
                {
                    NewPage(state);
                    DrawHeaderRow();
                    continue;
                }

                var take = Math.Min(fits, maxLines - first);
                var rowTop = state.Y + CellPaddingY;

                for (var i = 0; i < headers.Length; i++)
                {
                    // `WP 21.2A`: a right- or centre-aligned column is this
                    // table's own numeric/machine-data convention (this
                    // method's own remarks; `AddTable`'s pre-existing
                    // `columnAligns` parameter already exists so a caller can
                    // right-align "a genuinely numeric column") — drawn in
                    // Space Mono; a left-aligned column is running text, in
                    // Inter.
                    var cellFontRole = CellFontRole(aligns[i]);
                    var x = CellX(colX[i], widths[i], aligns[i]);
                    for (var lineIndex = first; lineIndex < Math.Min(first + take, wrapped[i].Count); lineIndex++)
                    {
                        var baseline = rowTop + BodySize + (lineIndex - first) * lineHeight;
                        state.Page.Texts.Add(new TextRun(x, baseline, wrapped[i][lineIndex], BodySize, false, aligns[i], Slate700, cellFontRole, symbolColumns is { } sc && i < sc.Length && sc[i]));
                    }
                }

                state.Y += take * lineHeight + CellPaddingY * 2;
                first += take;
                state.Page.Rules.Add(new RuleRun(ContentLeft, state.ContentRight, state.Y, 0.4f, Hairline));
            }
        }
    }

    /// <summary>A table's header row, from its top to the top of its first body row.</summary>
    public const float HeaderRowHeight = CaptionSize * LineLeading + CellPaddingY * 2 + 2f;

    /// <summary>The lines of a row kept together at a page break, and kept with the header row above them: a row never starts with fewer at the foot of a page.</summary>
    private const int KeptRowLines = 2;

    /// <summary>
    /// The height a table's start needs on one page: its header row and its first row, or the first lines of a first row
    /// taller than a page (<see cref="KeptRowLines"/>). A caller adds it to a heading's own height, so a heading is never
    /// left above an empty header row at the foot of a page (the shots review found one). <paramref name="state"/> gives
    /// the page geometry; portrait when it is <see langword="null"/>.
    /// </summary>
    public static float TableLeadHeight(SKPaint measure, float[] widths, IReadOnlyList<string[]> rows, SKTextAlign[]? columnAligns = null, LayoutState? state = null)
    {
        var aligns = columnAligns ?? new SKTextAlign[widths.Length];
        var lines = Math.Max(1, rows.Count > 0 ? WrapRow(measure, rows[0], widths, aligns).Max(c => c.Count) : 1);
        var lineHeight = BodySize * LineLeading;
        var kept = lines * lineHeight <= RowCapacity(state) ? lines : KeptRowLines;
        return HeaderRowHeight + kept * lineHeight + CellPaddingY * 2;
    }

    /// <summary>The tallest row body a page holds under its table header row: a row up to this height is kept whole; a taller one runs on across pages.</summary>
    private static float RowCapacity(LayoutState? state) => (state?.ContentBottom ?? ContentBottom) - Margin - HeaderRowHeight - CellPaddingY * 2;

    /// <summary>A body cell's face: a right- or centre-aligned column is machine data, in mono; a left-aligned one is running text.</summary>
    private static DocumentFontRole CellFontRole(SKTextAlign align) => align == SKTextAlign.Left ? DocumentFontRole.Body : DocumentFontRole.Mono;

    /// <summary>Each cell of <paramref name="row"/> wrapped to its column, measured in the face the cell is drawn in.</summary>
    private static List<string>[] WrapRow(SKPaint measure, string[] row, float[] widths, SKTextAlign[] aligns)
    {
        var typeface = measure.Typeface;
        var wrapped = new List<string>[widths.Length];

        try
        {
            measure.TextSize = BodySize;

            for (var i = 0; i < widths.Length; i++)
            {
                measure.Typeface = DocumentFonts.For(CellFontRole(aligns[i]), false);
                wrapped[i] = WrapText(i < row.Length ? row[i] : string.Empty, widths[i] - CellPaddingX * 2, measure, breakLongWords: true);
            }
        }
        finally
        {
            measure.Typeface = typeface;
        }

        return wrapped;
    }

    /// <summary>A cell's own draw-x for <paramref name="align"/> — left-padded from the column's own left edge, right-padded from its own right edge, centred otherwise.</summary>
    private static float CellX(float columnLeft, float columnWidth, SKTextAlign align) => align switch
    {
        SKTextAlign.Right => columnLeft + columnWidth - CellPaddingX,
        SKTextAlign.Center => columnLeft + columnWidth / 2f,
        _ => columnLeft + CellPaddingX,
    };

    /// <summary>
    /// Word-wraps <paramref name="text"/> to <paramref name="maxWidth"/> using <paramref name="measure"/>'s own current <see cref="SKPaint.TextSize"/>.
    /// A single word wider than <paramref name="maxWidth"/> is left to overflow its line, unless <paramref name="breakLongWords"/> is set: then it is
    /// broken between characters, so it never runs into the next column (a table cell; the shots review found a symbol printed over its description).
    /// </summary>
    public static List<string> WrapText(string text, float maxWidth, SKPaint measure, bool breakLongWords = false)
    {
        if (string.IsNullOrEmpty(text))
            return [string.Empty];

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return [string.Empty];

        if (breakLongWords && maxWidth > 0)
            words = [.. words.SelectMany(word => BreakWord(word, maxWidth, measure))];

        List<string> lines = [];
        var current = new System.Text.StringBuilder();

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

    /// <summary>A word wider than <paramref name="maxWidth"/>, cut into pieces that each fit (at least one character a piece); a word that fits comes back whole.</summary>
    private static IEnumerable<string> BreakWord(string word, float maxWidth, SKPaint measure)
    {
        if (measure.MeasureText(word) <= maxWidth)
        {
            yield return word;
            yield break;
        }

        // Character widths are summed rather than each candidate measured whole, so a long word costs one measure per character.
        var piece = new StringBuilder();
        var width = 0f;

        foreach (var rune in word.EnumerateRunes())
        {
            var glyph = rune.ToString();
            var glyphWidth = measure.MeasureText(glyph);

            if (piece.Length > 0 && width + glyphWidth > maxWidth)
            {
                yield return piece.ToString();
                piece.Clear();
                width = 0f;
            }

            piece.Append(glyph);
            width += glyphWidth;
        }

        if (piece.Length > 0)
            yield return piece.ToString();
    }

    // ------------------------------------------------------------
    // Header band and footer — page 1 only for the header band (exactly
    // as the pre-`WP 20.10G` renderers' own title line never repeated on
    // a continuation page); every page for the footer.
    // ------------------------------------------------------------

    /// <summary>
    /// The header band every one of the Product Owner's own document
    /// templates carries: a wordmark (left), the document type as an
    /// uppercase, indigo-600 eyebrow and a mono-styled reference/date/
    /// status line (right, two lines), under a 2px indigo-600 rule — the
    /// exact shape `Letterhead.dc.html`/`Invoice.dc.html`/`CostEstimate.dc.html`
    /// all three use for their own header slot.
    /// </summary>
    /// <param name="documentType">The document's own type, upper case (e.g. "QUOTATION", "ISSUE SHEET").</param>
    /// <param name="referenceLine">The reference/date/status row — already formatted by the caller (each document's own fields differ).</param>
    /// <remarks>
    /// <b>The lockup, when it loaded (`WP 21.2A`).</b> <see cref="DocumentLogo.HorizontalInk"/>
    /// draws in place of the plain "TEMPEST"/"OS" text pair this method drew
    /// before this Work Package — the identical fallback
    /// <see cref="DocumentTemplate"/>'s own class remarks already disclosed
    /// for a worktree that could not reach the design system's own asset
    /// files, kept honest rather than removed now those files are reachable
    /// (a future worktree that regresses to missing/corrupt resources still
    /// renders a header band, just the text one). The eyebrow and reference
    /// line now draw in <see cref="DocumentFontRole.Display"/>/<see cref="DocumentFontRole.Mono"/>
    /// respectively, rather than the platform default face every document
    /// used before this Work Package.
    /// </remarks>
    public static void AddHeaderBand(LayoutState state, SKPaint measure, string documentType, string referenceLine)
    {
        var top = state.Y;
        float leftBlockBottom;
        float leftBlockRight;

        if (DocumentLogo.HorizontalInk is { } logo && logo.Height > 0)
        {
            const float logoHeight = 18f;
            var logoWidth = logoHeight * logo.Width / logo.Height;
            state.Page.Images.Add(new ImageRun(ContentLeft, top, logoWidth, logoHeight));
            leftBlockBottom = top + logoHeight;
            leftBlockRight = ContentLeft + logoWidth;
        }
        else
        {
            measure.TextSize = WordmarkSize;
            var wordmarkBaseline = top + WordmarkSize;
            var tempestWidth = measure.MeasureText("TEMPEST ");
            state.Page.Texts.Add(new TextRun(ContentLeft, wordmarkBaseline, "TEMPEST ", WordmarkSize, true, SKTextAlign.Left, Ink900, DocumentFontRole.Display));
            state.Page.Texts.Add(new TextRun(ContentLeft + tempestWidth, wordmarkBaseline, "OS", WordmarkSize, true, SKTextAlign.Left, Cyan500, DocumentFontRole.Display));
            leftBlockBottom = wordmarkBaseline;
            leftBlockRight = ContentLeft + tempestWidth + measure.MeasureText("OS");
        }

        var eyebrowBaseline = top + HeaderEyebrowSize;
        state.Page.Texts.Add(new TextRun(state.ContentRight, eyebrowBaseline, documentType, HeaderEyebrowSize, true, SKTextAlign.Right, Indigo600, DocumentFontRole.Display));

        // The reference line sits right of the logo, never over it (the shots review found a long calculation name over the
        // logo and its tagline, and off the left edge of the page): it wraps to two lines, then shrinks, then is cut short.
        var (lines, size) = FitReferenceLine(referenceLine, state.ContentRight - leftBlockRight - HeaderLogoGap);
        var referenceBaseline = eyebrowBaseline + HeaderEyebrowSize * 0.4f + size;

        for (var n = 0; n < lines.Count; n++)
        {
            if (n > 0)
                referenceBaseline += size * 1.25f;

            state.Page.Texts.Add(new TextRun(state.ContentRight, referenceBaseline, lines[n], size, false, SKTextAlign.Right, Slate700, DocumentFontRole.Mono));
        }

        state.Y = Math.Max(leftBlockBottom, referenceBaseline) + 6f;
        AddRule(state, Indigo600, strokeWidth: 1.6f);
    }

    /// <summary>The clear space between the logo and the header band's reference line.</summary>
    public const float HeaderLogoGap = 12f;

    /// <summary>The smallest size the header band's reference line shrinks to before it is cut short.</summary>
    public const float HeaderReferenceMinSize = 6f;

    /// <summary>
    /// The header band's reference line fitted to <paramref name="width"/>, in the mono face it is drawn in: whole on one
    /// line at <see cref="HeaderReferenceSize"/>; else wrapped to two lines; else two lines at a smaller size, down to
    /// <see cref="HeaderReferenceMinSize"/>; else two lines with the second cut short with an ellipsis.
    /// </summary>
    public static (IReadOnlyList<string> Lines, float Size) FitReferenceLine(string text, float width)
    {
        using var paint = new SKPaint { Typeface = DocumentFonts.For(DocumentFontRole.Mono, false) };
        float Width(string s, float size)
        {
            paint.TextSize = size;
            return MeasureInFaces(paint, s, DocumentFontRole.Mono);
        }

        if (Width(text, HeaderReferenceSize) <= width)
            return ([text], HeaderReferenceSize);

        for (var size = HeaderReferenceSize; size >= HeaderReferenceMinSize - 0.01f; size -= 0.5f)
        {
            if (TwoLines(text, size) is { } lines)
                return (lines, size);
        }

        // Last resort: the first line as wrapped at the smallest size, the rest cut to fit with an ellipsis.
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = string.Empty;
        var used = 0;

        while (used < words.Length && Width(first.Length == 0 ? words[used] : $"{first} {words[used]}", HeaderReferenceMinSize) <= width)
        {
            first = first.Length == 0 ? words[used] : $"{first} {words[used]}";
            used++;
        }

        if (used == 0)
            return ([Ellipsise(text, HeaderReferenceMinSize)], HeaderReferenceMinSize);

        return ([first, Ellipsise(string.Join(' ', words[used..]), HeaderReferenceMinSize)], HeaderReferenceMinSize);

        // Two lines that each fit at size, broken at the word that balances them best, or null.
        string[]? TwoLines(string s, float size)
        {
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string[]? best = null;
            var bestLongest = float.MaxValue;

            for (var cut = 1; cut < parts.Length; cut++)
            {
                var a = string.Join(' ', parts[..cut]);
                var b = string.Join(' ', parts[cut..]);
                var longest = Math.Max(Width(a, size), Width(b, size));

                if (longest <= width && longest < bestLongest)
                {
                    best = [a, b];
                    bestLongest = longest;
                }
            }

            return best;
        }

        string Ellipsise(string s, float size)
        {
            if (Width(s, size) <= width)
                return s;

            var runes = s.EnumerateRunes().Select(r => r.ToString()).ToList();
            var keep = runes.Count;

            while (keep > 0 && Width(string.Concat(runes.Take(keep)).TrimEnd() + "…", size) > width)
                keep--;

            return string.Concat(runes.Take(keep)).TrimEnd() + "…";
        }
    }

    /// <summary>The width of <paramref name="text"/> as drawn: each character in the face of <paramref name="role"/>'s chain that has it (<see cref="FallBackForMissingGlyphs"/>), at <paramref name="paint"/>'s size.</summary>
    internal static float MeasureInFaces(SKPaint paint, string text, DocumentFontRole role)
    {
        var typeface = paint.Typeface;
        var total = 0f;

        try
        {
            var span = new StringBuilder();
            DocumentFontRole? current = null;

            void Flush()
            {
                if (span.Length == 0)
                    return;

                paint.Typeface = DocumentFonts.For(current ?? role, false);
                total += paint.MeasureText(span.ToString());
                span.Clear();
            }

            foreach (var rune in text.EnumerateRunes())
            {
                var face = DocumentFonts.FaceFor(rune, role, false) ?? role;

                if (face != current)
                {
                    Flush();
                    current = face;
                }

                span.Append(rune.ToString());
            }

            Flush();
        }
        finally
        {
            paint.Typeface = typeface;
        }

        return total;
    }

    /// <summary>
    /// The footer every page carries: a 1px ink-a08 hairline, then two
    /// lines — the organisation's own identity (left: legal name and
    /// company number; right: website — the design system's own footer
    /// slot, verbatim) and, below it, this export's own detail line
    /// (application version, the document's own reference or sequence,
    /// generation time) with "Page n of m" at the right — the field set
    /// `PHYSICAL_REVIEW.md` §7c D4 and §7a E9 already name.
    /// </summary>
    /// <param name="landscape">`WP 21.2A` — <see langword="false"/>, the default, positions the footer against the original A4 portrait geometry every caller used before this Work Package; <see langword="true"/> positions it against A4 landscape (the drawing register, the progress report). Must match the <see cref="LayoutState.Landscape"/> <paramref name="pages"/> was laid out with.</param>
    public static void AppendFooters(List<PagePlan> pages, OrganisationIdentity identity, string exportDetailLine, bool landscape = false)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var footerLeft = identity.FooterLeft();
        var footerRight = identity.FooterRight();
        var total = pages.Count;
        var contentRight = landscape ? LandscapeContentRight : ContentRight;
        var contentBottom = landscape ? LandscapeContentBottom : ContentBottom;

        var ruleY = contentBottom + 6f;
        var line1Baseline = ruleY + CaptionSize + 3f;
        var line2Baseline = line1Baseline + CaptionSize * LineLeading;

        for (var i = 0; i < pages.Count; i++)
        {
            var page = pages[i];
            page.Rules.Add(new RuleRun(ContentLeft, contentRight, ruleY, 0.75f, Hairline));

            page.Texts.Add(new TextRun(ContentLeft, line1Baseline, footerLeft, CaptionSize, false, SKTextAlign.Left, Slate600));
            if (footerRight.Length > 0)
                page.Texts.Add(new TextRun(contentRight, line1Baseline, footerRight, CaptionSize, false, SKTextAlign.Right, Slate600));

            page.Texts.Add(new TextRun(ContentLeft, line2Baseline, exportDetailLine, CaptionSize, false, SKTextAlign.Left, Slate600));
            page.Texts.Add(new TextRun(
                contentRight, line2Baseline, string.Format(System.Globalization.CultureInfo.InvariantCulture, "Page {0} of {1}", i + 1, total),
                CaptionSize, false, SKTextAlign.Right, Slate600));
        }
    }

    // ------------------------------------------------------------
    // The shared PDF draw loop (`WP 21.2A`) — lifted out of
    // `QuotationSheetRenderer`/`IssueSheetRenderer`, which each duplicated
    // this identical ~40-line `SKDocument`/canvas block, so every one of
    // this Work Package's six new renderers (and the two existing ones,
    // retrofitted) shares it instead of duplicating it a further six times
    // — the same "lift the duplicate once" discipline `TD-182` applied to
    // the layout plan itself.
    // ------------------------------------------------------------

    /// <summary>
    /// Draws <paramref name="pages"/> as a PDF — a rule, then every image,
    /// then every text run, per page, in that order; each <see cref="TextRun.FontRole"/>
    /// resolved to its own embedded typeface via <see cref="DocumentFonts.For"/>
    /// (`WP 21.2A`), each <see cref="ImageRun"/> drawn from
    /// <see cref="DocumentLogo.HorizontalInk"/> (today's only image source —
    /// see that property's own remarks for what an unloadable resource
    /// degrades to instead). <paramref name="landscape"/> must match the
    /// <see cref="LayoutState.Landscape"/> <paramref name="pages"/> was laid
    /// out with.
    /// </summary>
    /// <summary>
    /// Sets the symbols of every run that asks for it (<see cref="TextRun.Symbols"/>) as an engineer writes them: a
    /// run is split into pieces on the line and pieces set low and smaller (a subscript), each placed after the last,
    /// so <c>D_1</c> prints as D₁ (<see cref="Tempest.Desktop.CalcSheets.SymbolText"/>). A centred or right-aligned
    /// run keeps its alignment as a whole.
    /// </summary>
    internal static void TypesetSymbols(List<TextRun> texts)
    {
        using var paint = new SKPaint();

        for (var i = 0; i < texts.Count; i++)
        {
            var run = texts[i];

            if (!run.Symbols || !Tempest.Desktop.CalcSheets.SymbolText.HasSymbols(run.Text))
                continue;

            paint.Typeface = DocumentFonts.For(run.FontRole, run.Bold);
            paint.FakeBoldText = run.Bold && run.FontRole == DocumentFontRole.Body;
            var segments = Tempest.Desktop.CalcSheets.SymbolText.Parse(run.Text);
            var subSize = run.Size * (float)Tempest.Desktop.CalcSheets.SymbolText.SubscriptScale;
            var widths = segments.Select(s => { paint.TextSize = s.Subscript ? subSize : run.Size; return paint.MeasureText(s.Text); }).ToList();
            var total = widths.Sum();
            var x = run.Align switch
            {
                SKTextAlign.Right => run.X - total,
                SKTextAlign.Center => run.X - total / 2,
                _ => run.X,
            };

            var pieces = new List<TextRun>(segments.Count);

            for (var n = 0; n < segments.Count; n++)
            {
                if (segments[n].Text.Length > 0)
                {
                    pieces.Add(segments[n].Subscript
                        ? run with { X = x, Y = run.Y + run.Size * 0.22f, Text = segments[n].Text, Size = subSize, Align = SKTextAlign.Left, Symbols = false }
                        : run with { X = x, Text = segments[n].Text, Align = SKTextAlign.Left, Symbols = false });
                }

                x += widths[n];
            }

            texts.RemoveAt(i);
            texts.InsertRange(i, pieces);
            i += pieces.Count - 1;
        }
    }

    /// <summary>
    /// A face lacks some symbols a calculation prints: Space Mono has no Σ ("R_tot = Σ R_i" drew a box, the showcase
    /// journey found it), and the body face has no ⊥, ∥ or ∮ (the fillet weld's subscripts, a section's contour
    /// integral; the shots review found them). Each character is drawn in the first face of its run's chain that has it
    /// (<see cref="DocumentFonts.FallbackChain"/>: own face, body face, Tempest Symbols). A left-aligned run is split
    /// in place. A centred or right-aligned run the body face covers is drawn wholly in it; any other is split and keeps
    /// its alignment as a whole.
    /// </summary>
    internal static void FallBackForMissingGlyphs(List<TextRun> texts)
    {
        using var paint = new SKPaint();

        for (var i = 0; i < texts.Count; i++)
        {
            var run = texts[i];
            var face = DocumentFonts.For(run.FontRole, run.Bold);

            if (run.Text.Length == 0 || face.ContainsGlyphs(run.Text))
                continue;

            // Each character's face, in runs of the same face.
            var spans = new List<(string Text, DocumentFontRole Role)>();

            foreach (var rune in run.Text.EnumerateRunes())
            {
                var role = DocumentFonts.FaceFor(rune, run.FontRole, run.Bold) ?? run.FontRole;

                if (spans.Count > 0 && spans[^1].Role == role)
                    spans[^1] = (spans[^1].Text + rune.ToString(), role);
                else
                    spans.Add((rune.ToString(), role));
            }

            if (spans.Count == 1 && spans[0].Role == run.FontRole)
                continue;

            if (run.Align != SKTextAlign.Left && spans.All(s => s.Role is DocumentFontRole.Body || s.Role == run.FontRole) && run.FontRole != DocumentFontRole.Body)
            {
                texts[i] = run with { FontRole = DocumentFontRole.Body };
                continue;
            }

            var widths = spans.Select(s =>
            {
                paint.Typeface = DocumentFonts.For(s.Role, run.Bold);
                paint.TextSize = run.Size;
                paint.FakeBoldText = run.Bold && SynthesisesBold(s.Role);
                return paint.MeasureText(s.Text);
            }).ToList();

            var x = run.Align switch
            {
                SKTextAlign.Right => run.X - widths.Sum(),
                SKTextAlign.Center => run.X - widths.Sum() / 2,
                _ => run.X,
            };

            var pieces = new List<TextRun>(spans.Count);

            for (var n = 0; n < spans.Count; n++)
            {
                pieces.Add(run with { X = x, Text = spans[n].Text, FontRole = spans[n].Role, Align = SKTextAlign.Left });
                x += widths[n];
            }

            texts.RemoveAt(i);
            texts.InsertRange(i, pieces);
            i += pieces.Count - 1;
        }
    }

    /// <summary>Whether a bold run in <paramref name="role"/> is set in synthetic bold: the body face and the fallback face have one weight embedded; Display and Mono have a real bold file.</summary>
    internal static bool SynthesisesBold(DocumentFontRole role) => role is DocumentFontRole.Body or DocumentFontRole.Symbol;

    /// <summary>The last step before drawing: every run's symbols typeset (<see cref="TypesetSymbols"/>) and each character given a face that has it (<see cref="FallBackForMissingGlyphs"/>). It runs once per plan; a run already prepared is left as it is.</summary>
    internal static void PrepareText(List<PagePlan> pages)
    {
        foreach (var page in pages)
        {
            TypesetSymbols(page.Texts);
            FallBackForMissingGlyphs(page.Texts);
        }
    }

    public static ReadOnlyMemory<byte> RenderPdf(List<PagePlan> pages, SKDocumentPdfMetadata metadata, bool landscape = false)
    {
        var pageWidth = landscape ? LandscapePageWidth : PageWidth;
        var pageHeight = landscape ? LandscapePageHeight : PageHeight;

        using var stream = new MemoryStream();
        using (var wstream = new SKManagedWStream(stream))
        {
            using var document = SKDocument.CreatePdf(wstream, metadata)
                ?? throw new InvalidOperationException("SkiaSharp could not open a PDF document.");

            using var textPaint = new SKPaint { IsAntialias = true };
            using var linePaint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };
            using var imagePaint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High };
            var logo = DocumentLogo.HorizontalInk;

            PrepareText(pages);

            // `v0.26.0` (§3.6): each brand face is embedded as a subset of
            // the glyphs this document draws, not whole (`TrueTypeSubsetter`).
            var subsets = DocumentFonts.SubsetsFor(pages.SelectMany(p => p.Texts));

            try
            {

            foreach (var page in pages)
            {
                var canvas = document.BeginPage(pageWidth, pageHeight);
                canvas.Clear(PaperPage);

                foreach (var rule in page.Rules)
                {
                    linePaint.StrokeWidth = rule.StrokeWidth;
                    linePaint.Color = rule.Color;
                    canvas.DrawLine(rule.X1, rule.Y, rule.X2, rule.Y, linePaint);
                }

                foreach (var drawing in page.Drawings)
                {
                    canvas.Save();
                    canvas.Translate(drawing.X, drawing.Y);
                    canvas.Scale(drawing.Scale);
                    drawing.Draw(canvas);
                    canvas.Restore();
                }

                if (logo is not null)
                {
                    foreach (var image in page.Images)
                        canvas.DrawBitmap(logo, new SKRect(image.X, image.Y, image.X + image.Width, image.Y + image.Height), imagePaint);
                }

                foreach (var text in page.Texts)
                {
                    textPaint.TextSize = text.Size;
                    // A real bold file backs Display/Mono (`DocumentFonts.For`
                    // already selected it) — `FakeBoldText` on top would
                    // double-bold. Body (Inter) has only the one variable-font
                    // weight embedded, so a bold Body run still synthesises
                    // bold exactly as every renderer did before this Work
                    // Package.
                    textPaint.FakeBoldText = text.Bold && SynthesisesBold(text.FontRole);
                    textPaint.TextAlign = text.Align;
                    textPaint.Color = text.Color;
                    textPaint.Typeface = subsets.TryGetValue((text.FontRole, text.Bold), out var subset) ? subset : DocumentFonts.For(text.FontRole, text.Bold);
                    canvas.DrawText(text.Text, text.X, text.Y, textPaint);
                }

                document.EndPage();
            }

            document.Close();
            }
            finally
            {
                foreach (var typeface in subsets.Values)
                    typeface.Dispose();
            }
        }

        return stream.ToArray();
    }
}
