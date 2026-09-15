using System.Globalization;
using SkiaSharp;

namespace Tempest.Desktop.Documents.TechnicalReports;

/// <summary>One revision on a <see cref="TechnicalReportDocumentModel"/>'s own revision-history table.</summary>
/// <param name="RevisionNumber">The revision's own number.</param>
/// <param name="Date">When it was recorded.</param>
/// <param name="Author">Who recorded it, resolved to a display name where one exists.</param>
/// <param name="ChangeSummary">What changed. <see langword="null"/> when none was recorded.</param>
public sealed record TechnicalReportRevisionRow(int RevisionNumber, DateOnly Date, string Author, string? ChangeSummary);

/// <summary>One numbered section of a <see cref="TechnicalReportDocumentModel"/>'s own body.</summary>
/// <param name="Heading">The section's own heading — <see langword="null"/> for the lead section of a document whose content carries no heading markers at all (this model's own remarks).</param>
/// <param name="Body">The section's own running text.</param>
public sealed record TechnicalReportSection(string? Heading, string Body);

/// <summary>
/// Everything the technical report document renders (`WP 21.2A`, scope item
/// 2, from a Document's own revisions and content plus the project's own
/// identity) — a cover block, revision history, and numbered sections.
/// </summary>
/// <param name="Title">The document's own title.</param>
/// <param name="Reference">The document's own business identifier, where it has one.</param>
/// <param name="ProjectCode">The document's own project's business identifier.</param>
/// <param name="ProjectName">The document's own project's display name.</param>
/// <param name="Author">The current revision's own author, resolved to a display name where one exists.</param>
/// <param name="Revisions">Every revision, oldest first — <see cref="Tempest.Core.EngineeringData.IEngineeringDocumentStore.GetRevisionHistoryAsync"/>'s own order.</param>
/// <param name="Sections">
/// The current revision's own content, split into sections — every line
/// starting with one or more <c>#</c> characters (a Markdown-style heading
/// — the plain-text convention this renderer's own caller applies, since
/// nothing in this codebase enforces a richer format on a Document's own
/// content) starts a new section; content before the first such line, when
/// there is any, is one leading section with <see cref="TechnicalReportSection.Heading"/>
/// <see langword="null"/>. A document with no heading markers at all
/// renders as a single "Content" section — never silently empty.
/// </param>
/// <param name="GeneratedAtUtc">When this sheet was generated — supplied, never <see cref="DateTime.Now"/>.</param>
/// <param name="ApplicationVersionText">The running application's own version text.</param>
public sealed record TechnicalReportDocumentModel(
    string Title,
    string? Reference,
    string ProjectCode,
    string ProjectName,
    string Author,
    IReadOnlyList<TechnicalReportRevisionRow> Revisions,
    IReadOnlyList<TechnicalReportSection> Sections,
    DateTimeOffset GeneratedAtUtc,
    string ApplicationVersionText);

/// <summary>Renders a <see cref="TechnicalReportDocumentModel"/> as an A4 PDF (`WP 21.2A`, scope item 2) — cover block, revision history, numbered sections, running header and footer (`DocumentTemplate.AddHeaderBand`/`AppendFooters` already repeat the header rule and footer on every page) — through <see cref="DocumentTemplate"/>.</summary>
public sealed class TechnicalReportDocumentRenderer : IDocumentRenderer<TechnicalReportDocumentModel>
{
    /// <inheritdoc />
    public string DocumentType => "TECHNICAL REPORT";

    /// <inheritdoc />
    public string TemplateName => "technical-report";

    /// <summary>Supplies the organisation identity for a render whose caller passes none.</summary>
    public Func<OrganisationIdentity>? IdentityProvider { get; set; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Render(TechnicalReportDocumentModel model, OrganisationIdentity? identity = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var orgIdentity = identity ?? IdentityProvider?.Invoke() ?? OrganisationIdentity.TempestDefaults;
        var pages = Layout(model);
        DocumentTemplate.AppendFooters(pages, orgIdentity, FormatFooterDetail(model));

        var metadata = new SKDocumentPdfMetadata
        {
            Title = $"{model.Title} — Technical Report",
            Author = model.Author,
            Subject = "Technical report",
            Creator = model.ApplicationVersionText,
            Producer = model.ApplicationVersionText,
            Creation = model.GeneratedAtUtc.UtcDateTime,
            Modified = model.GeneratedAtUtc.UtcDateTime,
        };

        return DocumentTemplate.RenderPdf(pages, metadata);
    }

    /// <summary>Splits raw content into <see cref="TechnicalReportSection"/>s by leading <c>#</c> markers — this Work Package's own report names the exact convention. Public so a caller building <see cref="TechnicalReportDocumentModel.Sections"/> shares it rather than re-implementing it.</summary>
    public static IReadOnlyList<TechnicalReportSection> SplitIntoSections(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var sections = new List<TechnicalReportSection>();
        string? currentHeading = null;
        var body = new System.Text.StringBuilder();

        void Flush()
        {
            var text = body.ToString().Trim();
            if (currentHeading is not null || text.Length > 0)
                sections.Add(new TechnicalReportSection(currentHeading, text));
            body.Clear();
        }

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#'))
            {
                Flush();
                currentHeading = trimmed.TrimStart('#').Trim();
                if (currentHeading.Length == 0)
                    currentHeading = "Section";
            }
            else
            {
                body.AppendLine(line);
            }
        }

        Flush();

        // No `#` line was ever seen: either one section with its own
        // heading still null (`Flush`'s own first call), or — genuinely
        // empty content — no section was flushed at all (`Flush`'s own
        // "nothing to add" guard). Either way, named "Content" rather than
        // left null or silently empty, so this document always gets at
        // least one real heading on the page.
        if (sections is [{ Heading: null } only])
            sections[0] = only with { Heading = "Content" };
        else if (sections.Count == 0)
            sections.Add(new TechnicalReportSection("Content", content.Trim()));

        return sections;
    }

    private static List<DocumentTemplate.PagePlan> Layout(TechnicalReportDocumentModel model)
    {
        var state = DocumentTemplate.BeginLayout();
        using var measure = new SKPaint { Typeface = SKTypeface.Default };

        DocumentTemplate.AddHeaderBand(
            state, measure, "TECHNICAL REPORT",
            $"{model.Reference ?? model.Title}  ·  Rev {model.Revisions.LastOrDefault()?.RevisionNumber.ToString(CultureInfo.InvariantCulture) ?? "—"}");
        DocumentTemplate.AddGap(state, 4f);

        DocumentTemplate.AddWrappedLine(state, measure, model.Title, DocumentTemplate.TitleSize, bold: true, color: DocumentTemplate.Ink900, fontRole: DocumentFontRole.Display);
        DocumentTemplate.AddWrappedLine(state, measure, $"Project: {model.ProjectCode} — {model.ProjectName}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddWrappedLine(state, measure, $"Reference: {model.Reference ?? "—"}  •  Author: {model.Author}", DocumentTemplate.BodySize, bold: false);
        DocumentTemplate.AddGap(state, 4f);
        DocumentTemplate.AddRule(state);

        DocumentTemplate.AddHeading(state, measure, "Revision history");
        if (model.Revisions.Count == 0)
        {
            DocumentTemplate.AddWrappedLine(state, measure, "No revisions recorded.", DocumentTemplate.BodySize, bold: false);
        }
        else
        {
            DocumentTemplate.AddTable(
                state, measure,
                headers: ["Rev", "Date", "Author", "Summary"],
                widths: [state.ContentWidth * 0.08f, state.ContentWidth * 0.14f, state.ContentWidth * 0.28f, state.ContentWidth * 0.50f],
                rows: [.. model.Revisions.Select(r => new[]
                {
                    r.RevisionNumber.ToString(CultureInfo.InvariantCulture),
                    r.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    r.Author,
                    r.ChangeSummary ?? "—",
                })],
                columnAligns: [SKTextAlign.Center, SKTextAlign.Left, SKTextAlign.Left, SKTextAlign.Left]);
        }
        DocumentTemplate.AddGap(state, 10f);

        var sectionNumber = 1;
        foreach (var section in model.Sections)
        {
            DocumentTemplate.AddHeading(state, measure, section.Heading is { Length: > 0 } heading ? $"{sectionNumber}. {heading}" : $"{sectionNumber}.");
            DocumentTemplate.AddWrappedLine(state, measure, section.Body.Length == 0 ? "(no content)" : section.Body, DocumentTemplate.BodySize, bold: false);
            DocumentTemplate.AddGap(state, 8f);
            sectionNumber++;
        }

        return state.Pages;
    }

    private static string FormatFooterDetail(TechnicalReportDocumentModel model) => string.Format(
        CultureInfo.InvariantCulture,
        "{0} · {1} · Generated {2:yyyy-MM-dd HH:mm} UTC",
        model.ApplicationVersionText, model.Reference ?? model.Title, model.GeneratedAtUtc.UtcDateTime);
}
